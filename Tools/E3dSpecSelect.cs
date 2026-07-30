using System;
using System.Collections;
using System.Linq;
using System.Text;
using Aveva.Core.Database;
using Aveva.Core.PMLNet;
using Aveva.E3D.Select;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>等级驱动选型</b> —— 用 AVEVA 原生 <c>Aveva.E3D.Select</c>，
    /// 取代「发一条 <c>SELECT</c> 再读 SPREF 看空不空」。
    ///
    /// ═══ 为什么要它（三个真机失败，一个 API 全答）═══════════════════════════
    /// · §26 ③ **16 个管件只选到 4 个**，而脚本只读回 SPREF、**没抓报错** ——
    ///   拿到「4/16」却说不出**为什么**（是命令不对，还是这个等级里根本没这类件）。
    ///   → <c>ComponentSelection.AvailableComponents(type)</c> 直接回答"等级里有没有"，
    ///     <c>GetMessages()</c> 直接回答"为什么选不到"。
    /// · §26 ⑥ **HVAC 建成了但 HBRAN 被拒** → 下一步不是猜类型码，
    ///   是 <c>HVACSelect.GetAvailableTypes()</c>。
    /// · §22 **管嘴 SELECT 回 (61,28)** → 那句话字面就是对的，管嘴不在适用面内。
    ///
    /// ═══ ★★结构型钢是**另一个范式**（这解释了 §26 ② 的失败）══════════════
    /// 配管是**流式过滤**：<c>InSpecification().With().WithPurpose()</c> 逐步收窄；
    /// 结构是**问答式**：<c>StructuralSpec.GetQuestion()</c> → 答 → <c>GetAnswer()/RowSets()</c>，
    /// 在**另一个程序集** <c>Aveva.E3D.Interaction.Structural</c> 里。
    /// → SCTN 发 <c>SELECT</c> 回 <c>(61,28)</c> **不是命令写错，是走错了范式**。
    ///   本文件**只做配管 + 暖通**；型钢那条要另开一个工具，别硬塞进来。
    ///
    /// ═══ ⚠ 第一个要验的：<c>PMLNetAny</c> 拿不拿得到 ═══════════════════════
    /// <c>Select.InSpecification(PMLNetAny)</c> 收的是 PML 对象，不是 <c>DbElement</c>。
    /// 而 <c>PMLNetAny</c>（在 <b>PMLNet.dll</b>）只有两条构造路径：
    ///   <c>getGlobalVariable(string)</c>  —— 取 PML 全局变量（要求那个变量**已经存在**）
    ///   <c>createInstance(string className, object[] args, int nargs)</c> —— 新建 PML 对象
    /// **在插件进程里这两条哪条能拿到有效的 SPECIFICATION 对象，是未知的** ——
    /// 所以本工具的 <c>probe</c> 模式**专门验这一步**，并把两条路径的真实结果都报出来。
    /// 验不过，后面整条选型链都是空谈；**绝不假装它成了**。
    ///
    /// ⚠ 未编译验证、未上真机。
    /// </summary>
    internal static class E3dSpecSelect
    {
        /// <summary>PML 里挂等级对象的全局变量名（本工具自己建、自己用）。</summary>
        private const string SpecVar = "!!PCSELSPEC";

        public static string Run(string mode, string spec, string type, string attr, string value, string purpose)
        {
            mode = (mode ?? "probe").Trim().ToLowerInvariant();
            switch (mode)
            {
                case "probe": return Probe(spec);
                case "available": return Available(spec, type, attr, value, purpose);
                case "hvac": return HvacTypes(spec);
                default:
                    return "specselect: mode 只能是 probe / available / hvac。收到「" + mode + "」。"
                         + "\n★第一次用请先跑 probe —— 它验的是 PMLNetAny 拿不拿得到，"
                         + "那是整条选型链的门槛。";
            }
        }

        // ── ① probe：**唯一要紧的未知** —— PMLNetAny 能不能拿到 ────────────────
        private static string Probe(string spec)
        {
            var sb = new StringBuilder();
            sb.Append("specselect probe —— 验 PMLNetAny 的两条构造路径\n");
            sb.Append("★这一步不过，后面整条选型链都是空谈。两条路径的真实结果都在下面。\n\n");

            if (string.IsNullOrWhiteSpace(spec))
            {
                sb.Append("⚠ 没给 spec —— 只能验 createInstance，验不了 getGlobalVariable。\n");
                sb.Append("  建议带上一个**本机现查的**等级名再跑一次（等级名不写进任何文件）。\n\n");
            }

            // 路径 A：先用 PML 把等级挂到全局变量上，再 getGlobalVariable 取回来
            sb.Append("── A) getGlobalVariable（先用 PML 建全局变量，再取回）──\n");
            if (string.IsNullOrWhiteSpace(spec))
            {
                sb.Append("   跳过（没给 spec）\n");
            }
            else
            {
                string err;
                // ★用 `!!x = object SPECIFICATION(/名字)` 还是别的形态，**我不知道**。
                //   这里发最直白的一条，成不成都把 E3D 原话带回来。
                var pml = SpecVar + " = object SPECIFICATION('" + spec.Trim() + "')";
                bool ran = E3dApiWrapper.TryRunPmlPublic(pml, out err);
                sb.Append("   发: ").Append(pml).Append("\n");
                sb.Append("   → ").Append(ran ? "命令接受" : ("被拒: " + (err ?? "(无错误文本)"))).Append("\n");

                PMLNetAny any = null;
                string getErr = null;
                try { any = PMLNetAny.getGlobalVariable(SpecVar.TrimStart('!')); }
                catch (Exception ex) { getErr = ex.Message; }
                if (any == null)
                {
                    // 变量名带不带 `!!` 前缀是未知的 —— 两种都试，别只试一种就下结论。
                    try { any = PMLNetAny.getGlobalVariable(SpecVar); }
                    catch (Exception ex) { getErr = (getErr ?? "") + " / " + ex.Message; }
                }
                sb.Append("   getGlobalVariable → ").Append(Describe(any, getErr)).Append("\n");
            }

            // 路径 B：直接 createInstance
            sb.Append("\n── B) createInstance（直接造 PML 对象）──\n");
            foreach (var cls in new[] { "SPECIFICATION", "SPECREF", "DBREF" })
            {
                PMLNetAny any = null;
                string e2 = null;
                try
                {
                    var args = string.IsNullOrWhiteSpace(spec) ? new object[0] : new object[] { spec.Trim() };
                    any = PMLNetAny.createInstance(cls, args, args.Length);
                }
                catch (Exception ex) { e2 = ex.Message; }
                sb.Append("   createInstance(\"").Append(cls).Append("\") → ").Append(Describe(any, e2)).Append("\n");
            }

            sb.Append("\n★判据：哪条路径回了 type()/methods() 非空，就用哪条。");
            sb.Append("\n  两条都不成 → 把上面原文贴回来；那说明 PMLNetAny 在插件进程里不可用，");
            sb.Append("\n  选型只能退回命令通道（裸 SELECT，§17 已证对管件有效）。");
            return sb.ToString();
        }

        /// <summary>把一个 PMLNetAny 的真实状态说清楚 —— 拿不到就说拿不到，不含糊。</summary>
        private static string Describe(PMLNetAny any, string err)
        {
            if (any == null) return "null" + (string.IsNullOrWhiteSpace(err) ? "" : "（" + err + "）");
            string t = null, ms = null;
            try { t = any.type(); } catch (Exception ex) { t = "(type() 抛异常: " + ex.Message + ")"; }
            try { var m = any.methods(); ms = m == null ? "null" : (m.Length + " 个方法"); }
            catch (Exception ex) { ms = "(methods() 抛异常: " + ex.Message + ")"; }
            return "拿到对象  type=" + t + "  methods=" + ms;
        }

        // ── ② available：这个等级里到底有哪些件 ────────────────────────────────
        private static string Available(string spec, string type, string attr, string value, string purpose)
        {
            if (string.IsNullOrWhiteSpace(spec))
                return "specselect available: 缺 spec（等级名）。★等级名请现查现用，不要写死。";

            PMLNetAny pmlSpec = ResolveSpec(spec);
            if (pmlSpec == null)
                return "specselect available: **拿不到 PMLNetAny**（等级「" + spec + "」）。"
                     + "\n★先跑 mode=probe —— 它会把两条构造路径的真实结果都列出来。"
                     + "\n（这不是\"这个等级不存在\"，是\"我们还没拿到通往它的 PML 对象\"，两者别混。）";

            Select sel;
            try
            {
                sel = new Select().InSpecification(pmlSpec);
                if (!string.IsNullOrWhiteSpace(attr) && !string.IsNullOrWhiteSpace(value)) sel = sel.With(attr.Trim(), value.Trim());
                if (!string.IsNullOrWhiteSpace(purpose)) sel = sel.WithPurpose(purpose.Trim());
            }
            catch (Exception ex) { return "specselect: 构造 Select 抛异常：" + ex.Message; }

            var cs = sel.Selection;
            if (cs == null) return "specselect: Select.Selection 回 null。";

            var sb = new StringBuilder();
            sb.Append("specselect: 等级「").Append(spec).Append("」");
            if (!string.IsNullOrWhiteSpace(type)) sb.Append("  类型=").Append(type);
            if (!string.IsNullOrWhiteSpace(attr)) sb.Append("  ").Append(attr).Append("=").Append(value);
            if (!string.IsNullOrWhiteSpace(purpose)) sb.Append("  purpose=").Append(purpose);
            sb.Append("\n");

            Hashtable avail = null;
            string aerr = null;
            try { avail = string.IsNullOrWhiteSpace(type) ? cs.AvailableComponents() : cs.AvailableComponents(type.Trim()); }
            catch (Exception ex) { aerr = ex.Message; }

            if (avail == null)
            {
                sb.Append("  AvailableComponents → 取不到").Append(aerr == null ? "" : "（" + aerr + "）").Append("\n");
            }
            else
            {
                sb.Append("  可选构件 ").Append(avail.Count).Append(" 个\n");
                int n = 0;
                foreach (DictionaryEntry e in avail)
                {
                    if (n++ >= 30) break;
                    sb.Append("    ").Append(e.Key).Append(" = ").Append(e.Value).Append("\n");
                }
                // ★截断说出来 —— 悄悄截断会被读成"就这些"，那是假事实。
                if (avail.Count > 30) sb.Append("    ★另有 ").Append(avail.Count - 30).Append(" 个未列出（不是没有）\n");
            }

            // ★这一段才是 §26 ③ 缺的那半：选不到时**它会说为什么**。
            try
            {
                var msgs = cs.GetMessages();
                if (msgs != null && msgs.Count > 0)
                {
                    sb.Append("  ★E3D 的说明（GetMessages —— 这就是\"为什么选不到\"）：\n");
                    foreach (DictionaryEntry e in msgs) sb.Append("    ").Append(e.Key).Append(": ").Append(e.Value).Append("\n");
                }
                else
                {
                    sb.Append("  GetMessages: (空 —— 没有额外说明)\n");
                }
            }
            catch (Exception ex) { sb.Append("  GetMessages 抛异常：").Append(ex.Message).Append("\n"); }

            return sb.ToString();
        }

        // ── ③ hvac：暖通有哪些类型/尺寸（§26 ⑥ 的下一步）────────────────────
        private static string HvacTypes(string spec)
        {
            var sb = new StringBuilder("specselect hvac：\n");
            HVACSelect hs;
            try { hs = new HVACSelect(); }
            catch (Exception ex) { return "specselect hvac: 构造 HVACSelect 抛异常：" + ex.Message; }

            try
            {
                var types = hs.GetAvailableTypes();
                sb.Append("  GetAvailableTypes → ").Append(types == null ? "null" : types.Count + " 个\n");
                if (types != null)
                {
                    int n = 0;
                    foreach (DictionaryEntry e in types) { if (n++ >= 30) break; sb.Append("    ").Append(e.Key).Append(" = ").Append(e.Value).Append("\n"); }
                }
            }
            catch (Exception ex) { sb.Append("  GetAvailableTypes 抛异常：").Append(ex.Message).Append("\n"); }

            if (!string.IsNullOrWhiteSpace(spec))
            {
                var pmlSpec = ResolveSpec(spec);
                if (pmlSpec == null)
                {
                    sb.Append("  （给了 spec 但拿不到 PMLNetAny → ShapesInSpec/GetValidSizes 跳过；先跑 probe）\n");
                }
                else
                {
                    try
                    {
                        var shapes = hs.ShapesInSpec(pmlSpec);
                        sb.Append("  ShapesInSpec → ").Append(shapes == null ? "null" : shapes.Count + " 个\n");
                        if (shapes != null) { int n = 0; foreach (DictionaryEntry e in shapes) { if (n++ >= 20) break; sb.Append("    ").Append(e.Key).Append(" = ").Append(e.Value).Append("\n"); } }
                    }
                    catch (Exception ex) { sb.Append("  ShapesInSpec 抛异常：").Append(ex.Message).Append("\n"); }
                }
            }
            return sb.ToString();
        }

        /// <summary>拿等级的 PMLNetAny。★拿不到就回 null —— **不抛、不假装**。</summary>
        private static PMLNetAny ResolveSpec(string spec)
        {
            string err;
            try { E3dApiWrapper.TryRunPmlPublic(SpecVar + " = object SPECIFICATION('" + spec.Trim() + "')", out err); }
            catch { /* 命令层失败不算数，下面还要试 createInstance */ }

            foreach (var nm in new[] { SpecVar.TrimStart('!'), SpecVar })
            {
                try { var a = PMLNetAny.getGlobalVariable(nm); if (a != null) return a; } catch { }
            }
            try
            {
                var args = new object[] { spec.Trim() };
                return PMLNetAny.createInstance("SPECIFICATION", args, args.Length);
            }
            catch { return null; }
        }
    }
}
