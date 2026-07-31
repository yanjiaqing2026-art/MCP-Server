using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>分析批</b> —— 原生碰撞 / 空间盒查询 / 规则判定 / P&amp;ID 关联。
    ///
    /// ═══ 全量索引钻完之后筛出来的四块（2026-07-31）═══════════════════════════
    /// 这四块的共同点：**签名完整、有 `Create()` 工厂或静态入口、结果结构清楚**
    /// —— 也就是「能写、能读回、能判对错」。
    ///
    /// ① <b>原生碰撞</b> `Aveva.Core3D.Clasher`
    ///      `Clasher.Check(DbElement[], ClashOptions, ObstructionList, ClashSet)`
    ///      `ClashSet` 分**四类**结果：`Clashes` / `Touches` / `Clearances` / `NotProven`
    ///      `Clash` 带 `First` / `Second` / `Type` / **`ClashPosition`**
    ///      ★比我们自研的 `SpatialIndex` 多两样：**碰撞点坐标** 与 **NotProven（证不了的）**。
    ///        后者尤其重要 —— 自研那套没有"我判不了"这个档，只有"撞/不撞"。
    /// ② <b>空间盒查询</b> `SpatialImpl.ElementsInBox(LimitsBox, DbElementType[], bool)`
    ///      —— 给一个包围盒，回盒内的元素。这正是自研 `SpatialIndex` 的核心操作。
    /// ③ <b>规则判定</b> `AttributeTester.FromExpression(string)` + `PassesTest(DbElement)`
    ///      —— **E3D 内建的规则引擎**。写一条表达式，逐个元素判过不过。
    /// ④ <b>P&amp;ID ↔ 3D 关联</b> `AssociationManager`
    ///      `IsAssociated(a,b)` · `AssociatedDesignElements(schematic)` ·
    ///      `AssociatedSchematicElement(design)` —— 回答「这根管子对应 P&amp;ID 上哪条线」。
    ///      ★`Associate` / `Unassociate` 是**写**，本工具**只做只读那半**（建关联要人决策，
    ///        不该由 agent 顺手做）。
    ///
    /// ⚠ **未上真机**。每块**自己报自己通不通**，一跑能验完一批。
    /// ⚠ 全部走反射：`Aveva.Pdms.MMServices` 不在插件的静态引用里，
    ///    直接 `using` 会让**整个插件**在缺这个程序集的环境里加载失败。
    /// </summary>
    internal static class E3dAnalysis
    {
        public static string Run(string action, string names, string arg1, string arg2, int max)
        {
            if (max <= 0) max = 100;
            switch ((action ?? "").Trim().ToLowerInvariant())
            {
                case "clash":   return Clash(names, arg1, max);
                case "inbox":   return InBox(names, arg1, arg2, max);
                case "rule":    return Rule(names, arg1, max);
                case "assoc":   return Assoc(names, max);
                case "topo":    return Topo(names, max);
                default:
                    return "analysis: 不认识的 action「" + action + "」。可选："
                         + "clash(原生碰撞) · inbox(空间盒内有哪些元素) · "
                         + "rule(表达式规则判定) · assoc(P&ID↔3D 关联) · "
                         + "topo(管线归属/流向/首末段 —— 别再拿路径字符串算)";
            }
        }

        // ── ① 原生碰撞 ─────────────────────────────────────────────────────────
        private static string Clash(string names, string obstructions, int max)
        {
            var els = ResolveMany(names);
            if (els.Count == 0) return "clash: names 里一个元素都没解析出来。";
            try
            {
                var opt = Aveva.Core3D.Clasher.ClashOptions.Create();
                var obs = Aveva.Core3D.Clasher.ObstructionList.Create();
                var obsEls = ResolveMany(obstructions);
                if (obsEls.Count > 0) obs.AddObstructions(obsEls.ToArray());
                var set = Aveva.Core3D.Clasher.ClashSet.Create();

                // ★`Clasher.Check` 是**实例方法**（编译器 CS0120 抓出来的）——
                //   而 `Clasher` 是 abstract、带 `Public Static Clasher Instance`（反射实测）。
                //   ★又一次「看到签名 ≠ 知道怎么调」：索引里 `M|…Clasher|Boolean Check(...)`
                //     **看不出是静态还是实例**。必须编译过才算数。
                var clasher = Aveva.Core3D.Clasher.Clasher.Instance;
                if (clasher == null) return "clash: Clasher.Instance 是 null（模块没初始化？）。";
                bool ok = clasher.Check(els.ToArray(), opt, obs, set);

                var sb = new StringBuilder();
                sb.Append("clash: 检查 ").Append(els.Count).Append(" 个元素")
                  .Append(obsEls.Count > 0 ? "（障碍体 " + obsEls.Count + " 个）" : "")
                  .Append("  Check 返回 ").Append(ok).Append("\n");
                // ★四类分开报 —— 尤其 NotProven：那是**"证不了"**，既不是撞也不是不撞。
                //   自研 SpatialIndex 没有这一档，混进"不撞"里就是把不知道当成了没问题。
                Bucket(sb, "★撞上(Clashes)", set.Clashes, max);
                Bucket(sb, "接触(Touches)", set.Touches, max);
                Bucket(sb, "净距不足(Clearances)", set.Clearances, max);
                Bucket(sb, "★证不了(NotProven)", set.NotProven, max);
                sb.Append("★NotProven ≠ 没问题 —— 那是**E3D 自己也判不了**的那些，"
                        + "不能当成通过（自研 SpatialIndex 没有这一档）。\n");
                return sb.ToString();
            }
            catch (Exception ex) { return "clash: 抛异常：" + Inner(ex); }
        }

        private static void Bucket(StringBuilder sb, string label, Aveva.Core3D.Clasher.Clash[] arr, int max)
        {
            int n = arr == null ? 0 : arr.Length;
            sb.Append("  ").Append(label).Append(": ").Append(n).Append(" 条");
            if (n > max) sb.Append("（只列前 ").Append(max).Append("，**不是没有**）");
            sb.Append("\n");
            for (int i = 0; i < n && i < max; i++)
            {
                var c = arr[i];
                sb.Append("     ").Append(NameOf(c.First)).Append("  ×  ").Append(NameOf(c.Second));
                try { sb.Append("  [").Append(c.Type).Append("]"); } catch { }
                try
                {
                    var p = c.ClashPosition;
                    sb.Append("  @ (").Append(Math.Round(p.X)).Append(",")
                      .Append(Math.Round(p.Y)).Append(",").Append(Math.Round(p.Z)).Append(")");
                }
                catch { sb.Append("  @ (碰撞点取不到)"); }
                sb.Append("\n");
            }
        }

        // ── ② 空间盒查询 ───────────────────────────────────────────────────────
        private static string InBox(string corner1, string corner2, string typeFilter, int max)
        {
            double[] a = ParseXyz(corner1), b = ParseXyz(corner2);
            if (a == null || b == null)
                return "inbox: names=<x,y,z> arg1=<x,y,z> 两个对角点（mm）。收到：「" + corner1 + "」「" + corner2 + "」";
            try
            {
                var p1 = Aveva.Core.Geometry.Position.Create(a[0], a[1], a[2]);
                var p2 = Aveva.Core.Geometry.Position.Create(b[0], b[1], b[2]);
                var box = Aveva.Core.Geometry.LimitsBox.Create(p1, p2);

                // SpatialImpl 在 Implementation 命名空间，反射取（不静态引用 Impl）
                // ★程序集真名是 **Aveva.Core3D.Clasher.Implementation**（全量索引实证），
                //   我上一版写的 `Aveva.Core3D.Clasher` 是**命名空间** → 第十七跑回「找不到」。
                var t = FindType("Aveva.Core3D.Clasher.Implementation.SpatialImpl",
                                 "Aveva.Core3D.Clasher.Implementation", "Aveva.Core3D.Clasher")
                        ?? FindType("Aveva.Core3D.Clasher.SpatialImpl", "Aveva.Core3D.Clasher");
                if (t == null) return NotFound("inbox", "Aveva.Core3D.Clasher.Implementation.SpatialImpl",
                                               "Aveva.Core3D.Clasher.Implementation", "Aveva.Core3D.Clasher");
                MethodInfo mi = null;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (m.Name != "ElementsInBox") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[1].ParameterType.IsArray) { mi = m; break; }
                    if (ps.Length == 2 && mi == null) mi = m;
                }
                if (mi == null) return "inbox: SpatialImpl 上找不到 ElementsInBox。";

                object inst = mi.IsStatic ? null : Activator.CreateInstance(t);
                object[] args = mi.GetParameters().Length == 3
                    ? new object[] { box, TypeArray(typeFilter), true }
                    : new object[] { box, true };
                var res = mi.Invoke(inst, args) as DbElement[];
                if (res == null) return "inbox: ElementsInBox 回 null。";

                var sb = new StringBuilder();
                sb.Append("inbox: 盒内 ").Append(res.Length).Append(" 个元素");
                if (res.Length > max) sb.Append("（只列前 ").Append(max).Append("，**不是没有**）");
                sb.Append("\n");
                for (int i = 0; i < res.Length && i < max; i++)
                    sb.Append("  ").Append(TypeOf(res[i])).Append(" ").Append(NameOf(res[i])).Append("\n");
                return sb.ToString();
            }
            catch (Exception ex) { return "inbox: 抛异常：" + Inner(ex); }
        }

        // ── ③ 规则判定（E3D 内建规则引擎）──────────────────────────────────────
        private static string Rule(string names, string expression, int max)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return "rule: 要给 arg1=<PML 表达式>，如 `HBORE GT 100`。";
            var els = ResolveMany(names);
            if (els.Count == 0) return "rule: names 里一个元素都没解析出来。";
            try
            {
                // ★程序集真名 **MMServices**（§34 同一跑用这个名字调通了 Utilities）。
                //   上一版写 `Aveva.Pdms.MMServices`（命名空间）→ 假报「没装 MMServices」。
                var t = FindType("Aveva.Pdms.MMServices.AttributeTester", "MMServices", "Aveva.Pdms.MMServices");
                if (t == null) return NotFound("rule", "Aveva.Pdms.MMServices.AttributeTester",
                                               "MMServices", "Aveva.Pdms.MMServices");
                var from = t.GetMethod("FromExpression", BindingFlags.Public | BindingFlags.Static);
                if (from == null) return "rule: AttributeTester 上没有 FromExpression(String)。";
                object tester;
                try { tester = from.Invoke(null, new object[] { expression }); }
                catch (Exception ex) { return "rule: 表达式「" + expression + "」解析失败：" + Inner(ex); }
                if (tester == null) return "rule: FromExpression 回 null（表达式不合法？）。";
                var pass = t.GetMethod("PassesTest", new[] { typeof(DbElement) });
                if (pass == null) return "rule: AttributeTester 上没有 PassesTest(DbElement)。";

                var sb = new StringBuilder();
                int okN = 0, badN = 0, errN = 0;
                var lines = new StringBuilder();
                for (int i = 0; i < els.Count; i++)
                {
                    string verdict;
                    try
                    {
                        bool r = (bool)pass.Invoke(tester, new object[] { els[i] });
                        if (r) { okN++; verdict = "过"; } else { badN++; verdict = "**不过**"; }
                    }
                    // ★判不了单列 —— 混进"不过"里就是把"不知道"报成"违规"
                    catch (Exception ex) { errN++; verdict = "判不了(" + Inner(ex) + ")"; }
                    if (i < max) lines.Append("  ").Append(verdict).Append("  ").Append(NameOf(els[i])).Append("\n");
                }
                sb.Append("rule: 「").Append(expression).Append("」 过 ").Append(okN)
                  .Append(" · 不过 ").Append(badN).Append(" · **判不了 ").Append(errN).Append("**\n");
                sb.Append(lines);
                if (els.Count > max) sb.Append("  （只列前 ").Append(max).Append(" 条，**不是没有**）\n");
                if (errN > 0) sb.Append("★「判不了」既不算过也不算不过 —— 多半是该元素没有表达式里用的属性。\n");
                return sb.ToString();
            }
            catch (Exception ex) { return "rule: 抛异常：" + Inner(ex); }
        }

        // ── ⑤ 管线拓扑：归属 / 流向 / 首末段（2026-07-31 钻 Integrator + MMServices）─
        /// <summary>
        /// 四条**全是静态方法**，反射即可调，全部只读：
        ///   <c>CMUtility.GetOwnerLinePipe(el)</c> / <c>GetOwnerBranch(el)</c>
        ///     —— 从任一构件往上找它属于哪根管 / 哪条分支。
        ///     ★我们今天是**切路径字符串**算的，而 E3D 名字是**平名**、
        ///       路径不保证反映真实归属 —— 这条把它变成问出来的。
        ///   <c>CMUtility.IsReverseFlowDirection(el)</c> —— **流向反没反**。
        ///     我们对流向此前一个答案都没有，而它决定 arrive/leave 谁是谁。
        ///   <c>CMUtility.IsElementLinkedToAnything(el)</c> —— 这个构件挂没挂 P&ID 关联
        ///     （比 assoc 更轻，用来先筛）。
        ///   <c>MMServices.Utilities.GetFirstSegment/GetLastSegment(el)</c> —— 首段 / 末段。
        /// ★每条**各自报各自的成败** —— 一条不可用不拖累其余三条
        ///   （两个程序集是分开的，很可能只装了一个）。
        /// </summary>
        private static string Topo(string names, int max)
        {
            if (string.IsNullOrWhiteSpace(names)) return "topo: 缺 names（元素路径，逗号分隔）。";
            // ★这两个名字本来就是对的（第十七跑实证调通），改走 FindType 只为**同一套判据** ——
            //   §31 那三处正是因为各写各的才把命名空间写成了程序集名。
            var cm = FindType("Aveva.Pdms.Integrator.CMUtility", "Integrator", "Aveva.Pdms.Integrator");
            var ut = FindType("Aveva.Pdms.MMServices.Utilities", "MMServices", "Aveva.Pdms.MMServices");

            var sb = new StringBuilder();
            if (cm == null) sb.Append("⚠ 找不到 Integrator.CMUtility —— 归属/流向/关联三项**查不了**（不是'没有'）。\n");
            if (ut == null) sb.Append("⚠ 找不到 MMServices.Utilities —— 首末段**查不了**（不是'没有'）。\n");
            if (cm == null && ut == null) return sb + "topo: 两个程序集都不在，本动作无从谈起。";

            int n = 0;
            foreach (var raw in names.Split(','))
            {
                var p = (raw ?? "").Trim();
                if (p.Length == 0) continue;
                if (++n > max) { sb.Append("★只处理前 ").Append(max).Append(" 个（不是没有）\n"); break; }

                DbElement el;
                try
                {
                    el = E3dResolveOrInvalid(p);
                    if (!el.IsValid) { sb.Append("── ").Append(p).Append("\n   ✗ 不是有效元素。\n"); continue; }
                }
                catch (Exception ex) { sb.Append("── ").Append(p).Append("\n   ✗ 解析抛异常：").Append(Inner(ex)).Append("\n"); continue; }

                sb.Append("── ").Append(p).Append("\n");
                if (cm != null)
                {
                    sb.Append("   所属管线 PIPE：").Append(CallEl(cm, "GetOwnerLinePipe", el)).Append("\n");
                    sb.Append("   所属分支 BRAN：").Append(CallEl(cm, "GetOwnerBranch", el)).Append("\n");
                    sb.Append("   流向是否反：").Append(CallBool(cm, "IsReverseFlowDirection", el)).Append("\n");
                    sb.Append("   挂了 P&ID 关联：").Append(CallBool(cm, "IsElementLinkedToAnything", el)).Append("\n");
                }
                if (ut != null)
                {
                    sb.Append("   首段：").Append(CallEl(ut, "GetFirstSegment", el)).Append("\n");
                    sb.Append("   末段：").Append(CallEl(ut, "GetLastSegment", el)).Append("\n");
                }
            }
            return "topo:\n" + sb;
        }

        /// <summary>调一个 static(DbElement)→DbElement；**三种结果分开说**：抛异常 / 无效 / 真值。</summary>
        private static string CallEl(Type t, string m, DbElement el)
        {
            try
            {
                var mi = t.GetMethod(m, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(DbElement) }, null);
                if (mi == null) return "(这个版本没有 " + m + ")";
                var r = mi.Invoke(null, new object[] { el });
                if (!(r is DbElement)) return "(回的不是 DbElement)";
                var e = (DbElement)r;
                if (!e.IsValid) return "(无 —— 它上面没有这一级)";
                try { var nm = e.GetAsString(DbAttributeInstance.NAME); return string.IsNullOrWhiteSpace(nm) ? e.ToString() : nm; }
                catch { return e.ToString(); }
            }
            catch (Exception ex) { return "(抛异常：" + Inner(ex) + ")"; }
        }

        /// <summary>调一个 static(DbElement)→Boolean；★抛异常回"判不了"**不回 false**。</summary>
        private static string CallBool(Type t, string m, DbElement el)
        {
            try
            {
                var mi = t.GetMethod(m, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(DbElement) }, null);
                if (mi == null) return "(这个版本没有 " + m + ")";
                var r = mi.Invoke(null, new object[] { el });
                if (!(r is bool)) return "(回的不是 Boolean)";
                return ((bool)r) ? "是" : "否";
            }
            catch (Exception ex) { return "(**判不了** —— " + Inner(ex) + ")"; }
        }

        // ── ④ P&ID ↔ 3D 关联（只读那半）───────────────────────────────────────
        private static string Assoc(string names, int max)
        {
            var els = ResolveMany(names);
            if (els.Count == 0) return "assoc: names 里一个元素都没解析出来。";
            try
            {
                // ★同上：程序集是 **MMServices**。索引里另有一个同名类型
                //   `Aveva.CIE.Foundation.Platform.Tools.AssociationManager` —— **不是这个**
                //   （`AssociatedSchematicElement(DbElement)` 只在 MMServices 那个上）。
                var t = FindType("Aveva.Pdms.MMServices.AssociationManager", "MMServices", "Aveva.Pdms.MMServices");
                if (t == null) return NotFound("assoc", "Aveva.Pdms.MMServices.AssociationManager",
                                               "MMServices", "Aveva.Pdms.MMServices");
                object inst = null;
                var fi = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (fi != null) inst = fi.GetValue(null);
                if (inst == null) { try { inst = Activator.CreateInstance(t); } catch { } }
                if (inst == null) return "assoc: 拿不到 AssociationManager 实例（没有 Instance 字段也造不出来）。";

                var sb = new StringBuilder();
                sb.Append("assoc: ").Append(els.Count).Append(" 个元素\n");
                for (int i = 0; i < els.Count && i < max; i++)
                {
                    sb.Append("  ").Append(NameOf(els[i])).Append("\n");
                    foreach (var m in new[] { "AssociatedSchematicElement", "AssociatedEngineeringElement",
                                              "AssociatedManufacturingElement" })
                    {
                        var mi = t.GetMethod(m, new[] { typeof(DbElement) });
                        if (mi == null) { sb.Append("      ").Append(m).Append(": (这个版本没有)\n"); continue; }
                        try
                        {
                            // ★`DbElement` 是**结构体**不是引用类型 —— `as DbElement?` 在 C# 7.3 下
                            //   直接 CS8651（可空引用类型语法要 C# 8）。用 `is` + 显式转换。
                            var raw = mi.Invoke(inst, new object[] { els[i] });
                            bool has = (raw is DbElement) && ((DbElement)raw).IsValid;
                            sb.Append("      ").Append(m).Append(": ")
                              .Append(has ? NameOf((DbElement)raw) : "(无关联)").Append("\n");
                        }
                        catch (Exception ex) { sb.Append("      ").Append(m).Append(": 取不到(").Append(Inner(ex)).Append(")\n"); }
                    }
                    var ade = t.GetMethod("AssociatedDesignElements", new[] { typeof(DbElement) });
                    if (ade != null)
                    {
                        try
                        {
                            var lst = ade.Invoke(inst, new object[] { els[i] }) as IEnumerable;
                            int n = 0; var names2 = new StringBuilder();
                            if (lst != null) foreach (var o in lst) { if (n++ < 8 && o is DbElement) names2.Append(NameOf((DbElement)o)).Append(" "); }
                            sb.Append("      AssociatedDesignElements: ").Append(n).Append(" 个  ").Append(names2).Append("\n");
                        }
                        catch (Exception ex) { sb.Append("      AssociatedDesignElements: 取不到(").Append(Inner(ex)).Append(")\n"); }
                    }
                }
                sb.Append("★本工具**只做只读那半** —— `Associate`/`Unassociate` 是建/断关联，"
                        + "属于工程决策，不该由 agent 顺手做。\n");
                return sb.ToString();
            }
            catch (Exception ex) { return "assoc: 抛异常：" + Inner(ex); }
        }

        // ── 共用 ────────────────────────────────────────────────────────────────
        private static List<DbElement> ResolveMany(string names)
        {
            var outp = new List<DbElement>();
            if (string.IsNullOrWhiteSpace(names)) return outp;
            foreach (var raw in names.Split(','))
            {
                var p = (raw ?? "").Trim();
                if (p.Length == 0) continue;
                try { var e = E3dResolveOrInvalid(p); if (e.IsValid) outp.Add(e); } catch { }
            }
            return outp;
        }

        private static DbElementType[] TypeArray(string filter)
        {
            var lst = new List<DbElementType>();
            if (string.IsNullOrWhiteSpace(filter)) return lst.ToArray();
            foreach (var raw in filter.Split(','))
            {
                var p = (raw ?? "").Trim();
                if (p.Length == 0) continue;
                try { var t = DbElementType.GetElementType(p); if (t != null && t.IsValid) lst.Add(t); } catch { }
            }
            return lst.ToArray();
        }

        private static double[] ParseXyz(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var p = s.Split(',');
            if (p.Length != 3) return null;
            var r = new double[3];
            for (int i = 0; i < 3; i++) if (!double.TryParse(p[i].Trim(), out r[i])) return null;
            return r;
        }

        // 无名元素回 dbref —— **不回空串假装它没有身份**
        private static string NameOf(DbElement e)
        {
            try { var n = e.GetAsString(DbAttributeInstance.NAME); return string.IsNullOrWhiteSpace(n) ? e.ToString() : n; }
            catch { return "?"; }
        }

        private static string TypeOf(DbElement e)
        {
            try { return "[" + e.GetElementType().Name + "]"; } catch { return "[?]"; }
        }

        private static string Inner(Exception ex)
        {
            var e = ex;
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }

        // ── ⛔⛔ 2026-07-31 第十七跑：我把**命名空间当成了程序集名** ────────────────
        /// <summary>
        /// 按**类型全名**找类型，程序集名只作候选。
        ///
        /// ★为什么不能只写一个程序集名（第十七跑亲身证明）：
        ///   `rule` / `assoc` 我写的是 `..., Aveva.Pdms.MMServices`（那是**命名空间**），
        ///   真程序集叫 **`MMServices`** —— 于是两条都回了
        ///   「**这套 E3D 没装 MMServices**」。而**同一跑** §34 用 `, MMServices`
        ///   调 `Aveva.Pdms.MMServices.Utilities` **调通了**（回的是 `not SCBRANCH`
        ///   这种领域异常，不是找不到类型）。
        ///   → 那句诊断是**假的**，它会让人去装一个已经装着的模块。
        ///   同族第三处：`SpatialImpl` 真程序集是 `Aveva.Core3D.Clasher.Implementation`。
        ///
        /// 修法不是"把名字改对"（下一套 E3D 可能又不一样），是**别依赖名字**：
        ///   ① 逐个候选试 `Type.GetType(full + ", " + asm)`
        ///   ② ★兜底扫 **已加载的程序集**，按类型全名找 —— 这一步与程序集叫什么无关
        ///   ③ 都没有时，错误里**把试过的名字列出来**，让人分得开
        ///      「这个模块没装」与「我把名字写错了」。
        /// </summary>
        private static Type FindType(string fullName, params string[] asmCandidates)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            foreach (var asm in asmCandidates ?? new string[0])
            {
                if (string.IsNullOrWhiteSpace(asm)) continue;
                try { var t = Type.GetType(fullName + ", " + asm); if (t != null) return t; } catch { }
            }
            try { var t = Type.GetType(fullName); if (t != null) return t; } catch { }
            // ★兜底：扫已加载程序集。程序集名写错在这一步不再有杀伤力。
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { var t = a.GetType(fullName, false); if (t != null) return t; } catch { }
                }
            }
            catch { }
            return null;
        }

        /// <summary>找不到类型时的**可判定**说明 —— 分得开「没装」与「名字写错」。</summary>
        private static string NotFound(string label, string fullName, params string[] asmCandidates)
        {
            var sb = new StringBuilder();
            sb.Append(label).Append(": 找不到类型 ").Append(fullName);
            sb.Append("（试过程序集 [").Append(string.Join(", ", asmCandidates ?? new string[0]));
            sb.Append("]，并已扫过全部已加载程序集）。\n");
            sb.Append("  ★两种可能，别混：① 这套 E3D 真的没装这个模块；");
            sb.Append("② 该模块装了但**还没被加载进本进程**（反射只看得见已加载的）。\n");
            sb.Append("  → 先在 E3D 里用一次相关功能再重试；仍找不到才是 ①。");
            return sb.ToString();
        }

        /// <summary>★经 E3dResolve 三级解析（官方 GetElement(String) 已 DEPRECATED，只认当前 DB）。
        /// 拿不到就回一个 invalid 元素 —— 调用方原有的 `IsValid` 判断照旧生效，形态不变。</summary>
        private static DbElement E3dResolveOrInvalid(string path)
        {
            DbElement el; string why;
            return E3dResolve.Element(path, out el, out why) ? el : DbElement.GetElement();
        }
    }
}
