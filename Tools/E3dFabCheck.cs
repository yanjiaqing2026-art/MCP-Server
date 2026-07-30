using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>管道可制造性检查（预制）</b> —— 走 AVEVA 自带的 <c>PipeFabricationAddin</c>。
    ///
    /// ═══ 这是一整块我们从没碰过的专业能力 ═══════════════════════════════════════
    /// 2026-07-31 用「签名里出现 <c>DbElement</c> 才够得着」这个判据筛全量索引，
    /// 筛出 <c>Aveva.Piping.Implementation.PipeFabrication.FabricationCheck</c> ——
    /// **一整套预制检查引擎**，入口干净得出奇：
    /// <code>
    ///   var p = new PFPipe(pipeElement);   // ★构造器就一个参数：PIPE 元素
    ///   p.LoadSpools();                    // 划分预制件（spool）
    ///   p.IsReadyForFabrication();         // 这根管**能不能预制**
    ///   p.PipeSpools → spool.PipePieces → piece.CheckResults → PFCheckMessage
    ///                                      → .GetMessage() / .Code / .Level
    /// </code>
    /// <c>PFCheckResult</c> 列出来的规则是**真的工程规则**，不是通用校验：
    ///   首/末管段太短 · 两弯之间太短 · 弯管与碰撞面干涉 · 预焊法兰/搭接不支持 ·
    ///   法兰组无效 · 外径无效 · 管长无效 · 松散件 · **轨道焊/机械焊/手工焊各自的最小长度** ·
    ///   超 PIP/PIF/PIL 上限 · 支管连接余量 · 虾米弯余量 · 弯管首末余量 ·
    ///   各弯半径不一致 · 手工弯 …
    /// —— 这些我们**一条都答不出来**，而它们决定"这根管画得出来但做不做得出来"。
    ///
    /// ═══ ⚠ 一个**没验证**的关键点，因此按最保守处理 ═══════════════════════════
    /// <c>LoadSpools()</c> 名字像"只算不写"，但同一个类上还有
    /// <c>CreatePipeSpools</c> / <c>DeletePipeSpools</c> / <c>DeleteGabageElements</c>，
    /// 而 <c>PFPipePiece</c> 上有 <c>Create()</c> / <c>Delete()</c> ——
    /// <b>我没有证据说 LoadSpools 不会在库里留下临时元素</b>。
    /// → 所以这个工具**声明成 write**（09 侧会要工程师签），
    ///   description 里也**明写"这条没验证"**。
    /// → 验收脚本会在调用**前后各数一次成员数**：真机跑一次就能定。
    ///   定了是只读，再放宽成 read —— <b>先按最危险的取，拿到证据再放宽</b>，
    ///   反过来（先当只读、出事再收紧）是拿用户的库赌。
    /// ★ 本工具**永不**调 Create*/Delete*/Rename* —— 那些是真写库，属工程决策不由 agent 做。
    ///
    /// ⚠ 走反射不加编译期引用：<c>PipeFabricationAddin</c> 不一定装在每套 E3D 上。
    ///   没装就**明说没装**，那不等于"这根管没问题"。
    /// ⚠ <b>未上真机</b>。
    /// </summary>
    internal static class E3dFabCheck
    {
        private const string Asm = "PipeFabricationAddin";
        private const string Ns = "Aveva.Piping.Implementation.PipeFabrication.FabricationCheck.";

        public static string Run(string names, int max)
        {
            if (max <= 0) max = 40;
            if (string.IsNullOrWhiteSpace(names))
                return "fabcheck: 缺 names（PIPE 元素路径，逗号分隔）。";

            Type tPipe = Type.GetType(Ns + "PFPipe, " + Asm);
            if (tPipe == null)
                return "fabcheck: 找不到 PFPipe —— **这套 E3D 没装 PipeFabricationAddin**。\n"
                     + "★这不等于\"这些管没有可制造性问题\" —— 是**没检查**。两者别混。";

            var ctor = tPipe.GetConstructor(new[] { typeof(DbElement) });
            if (ctor == null) return "fabcheck: PFPipe 上没有 .ctor(DbElement) —— 这个 E3D 版本签名不同。";

            var sb = new StringBuilder();
            int done = 0, ready = 0, notReady = 0, unknown = 0;

            foreach (var raw in names.Split(','))
            {
                var path = (raw ?? "").Trim();
                if (path.Length == 0) continue;

                DbElement el;
                try
                {
                    el = DbElement.GetElement(path);
                    if (!el.IsValid) { sb.Append("── ").Append(path).Append("\n   ✗ 不是有效元素。\n"); unknown++; continue; }
                }
                catch (Exception ex) { sb.Append("── ").Append(path).Append("\n   ✗ 解析抛异常：").Append(Inner(ex)).Append("\n"); unknown++; continue; }

                // ★类型必须是 PIPE —— 传 BRAN 进来 PFPipe 多半直接崩，那种崩会被读成"这根管有问题"
                string ty = "?";
                try { ty = el.GetElementType().Name; } catch { }
                sb.Append("── ").Append(path).Append("  [").Append(ty).Append("]\n");
                if (!string.Equals(ty, "PIPE", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("   ✗ 这个工具只吃 **PIPE**（预制是按管线划分的）。收到 ").Append(ty).Append("，跳过。\n");
                    unknown++; continue;
                }

                object p;
                try { p = ctor.Invoke(new object[] { el }); }
                catch (Exception ex) { sb.Append("   ✗ 构造 PFPipe 抛异常：").Append(Inner(ex)).Append("\n"); unknown++; continue; }

                // ★这一步之后的每个失败都单独报 —— 不让一个异常把整根管说成"不能预制"
                try
                {
                    var load = tPipe.GetMethod("LoadSpools", Type.EmptyTypes);
                    if (load != null) load.Invoke(p, null);
                    else sb.Append("   （没有 LoadSpools()，直接读结果）\n");
                }
                catch (Exception ex)
                {
                    sb.Append("   ✗ LoadSpools 抛异常：").Append(Inner(ex)).Append("\n")
                      .Append("   ★这是**算不出来**，不是\"不能预制\" —— 按 unknown 记。\n");
                    unknown++; continue;
                }

                bool? ok = null;
                try
                {
                    var mi = tPipe.GetMethod("IsReadyForFabrication", Type.EmptyTypes);
                    if (mi != null) { var r = mi.Invoke(p, null); if (r is bool) ok = (bool)r; }
                }
                catch (Exception ex) { sb.Append("   （IsReadyForFabrication 抛异常：").Append(Inner(ex)).Append("）\n"); }

                sb.Append("   可预制：").Append(ok == null ? "**判不了**" : (ok.Value ? "✅ 是" : "❌ 否")).Append("\n");
                Append(sb, "   一致性检查：", Get(p, "ConsistencyCheckPassed"));
                Append(sb, "   预制状态：", Get(p, "FabricationStatus"));

                var spools = Get(p, "PipeSpools") as IEnumerable;
                int ns = 0, nMsg = 0;
                if (spools != null)
                {
                    foreach (var sp in spools)
                    {
                        ns++;
                        if (ns > max) { sb.Append("   ★只列前 ").Append(max).Append(" 个 spool（不是没有）\n"); break; }
                        sb.Append("   · spool ").Append(Str(Get(sp, "Name")))
                          .Append("  类型=").Append(Str(Get(sp, "SpoolType")))
                          .Append(Truthy(Get(sp, "IsSite")) ? "  [现场]" : "  [车间]").Append("\n");
                        var pieces = Get(sp, "PipePieces") as IEnumerable;
                        if (pieces == null) continue;
                        foreach (var pc in pieces)
                        {
                            var msgs = Get(pc, "CheckResults") as IEnumerable;
                            if (msgs == null) continue;
                            foreach (var m in msgs)
                            {
                                if (++nMsg > max) break;
                                string txt = null;
                                try
                                {
                                    var gm = m.GetType().GetMethod("GetMessage", Type.EmptyTypes);
                                    if (gm != null) txt = Convert.ToString(gm.Invoke(m, null));
                                }
                                catch { }
                                sb.Append("       [").Append(Str(Get(m, "Level"))).Append("] ")
                                  .Append(string.IsNullOrWhiteSpace(txt) ? ("码 " + Str(Get(m, "Code"))) : txt)
                                  .Append("\n");
                            }
                        }
                    }
                }
                if (ns == 0) sb.Append("   （没划出 spool）\n");
                if (nMsg > max) sb.Append("   ★检查消息超过 ").Append(max).Append(" 条**已截断**（不是只有这些）\n");
                else if (nMsg == 0 && ok == true) sb.Append("   （没有检查消息）\n");

                done++;
                if (ok == true) ready++; else if (ok == false) notReady++; else unknown++;
            }

            var head = new StringBuilder();
            head.Append("fabcheck: 处理 ").Append(done).Append(" 根 —— 可预制 ").Append(ready)
                .Append(" · 不可预制 ").Append(notReady).Append(" · **判不了** ").Append(unknown).Append("\n")
                .Append("★「判不了」既不算过也不算不过，报结论时必须单列。\n")
                .Append("⚠ LoadSpools 是否会在库里留下临时元素**尚未验证** —— 本工具因此按写对待；\n")
                .Append("  它**从不**调 Create*/Delete*/Rename*（那些是真写库）。\n\n");
            return head + sb.ToString();
        }

        // ── 小工具 ──────────────────────────────────────────────────────────────
        private static void Append(StringBuilder sb, string label, object v)
        {
            if (v == null) return;
            sb.Append(label).Append(Convert.ToString(v)).Append("\n");
        }

        private static bool Truthy(object v) { return v is bool && (bool)v; }

        private static object Get(object o, string prop)
        {
            if (o == null) return null;
            try
            {
                var p = o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                return p == null ? null : p.GetValue(o, null);
            }
            catch { return null; }
        }

        private static string Str(object o) { return o == null ? "(null)" : Convert.ToString(o); }

        private static string Inner(Exception ex)
        {
            var e = ex;
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }
    }
}
