using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>改之前先问 —— 能不能改 / 有哪些属性 / 这个值合不合法</b>（全只读）。
    ///
    /// ═══ 为什么这条可能是今天挖到的最有用的一块 ═══════════════════════════════
    /// 本仓最反复的一个病是「**发出去、被拒、读报错、再改**」。而这三件事
    /// AVEVA 都提供了**发送前就能问**的 API，我们一件都没用：
    ///
    /// | 问题 | 现在怎么做 | 官方 API（全 static，反射直调） |
    /// |---|---|---|
    /// | 这个元素**能不能改** | 发命令、看它失败 | <c>DbElementExtensions.IsClaimed / IsReadOnly / IsLocked</c> |
    /// | 它**有哪些属性** | <c>e3d_attr_list</c> 里**写死的 34 个名单** | <c>DbElementExtensions.GetAllVisibleAttributes(el)</c> |
    /// | 这个**值**合不合法 | 发 <c>attr_set</c>、收 `Data of wrong type` | <c>DbAttribute.IsAllowed(DbElementType, Int32/Double/String)</c> |
    ///
    /// ★ <c>IsClaimed</c> 尤其关键：E3D 是**多人同库**。别人 claim 了的元素我们改不动，
    ///   而今天 agent 对此**一无所知** —— 它会发一批命令、一半失败、然后开始"修"
    ///   一个根本不是它造成的问题。**先问一句就全避开了。**
    /// ★ <c>GetAllVisibleAttributes</c> 直接顶掉那份写死的 34 个名单
    ///   （第六跑就栽在那上面：名单里没有 DIAMETER，而点名读得到）。
    ///
    /// ═══ 刻意**没做**的 ═══════════════════════════════════════════════════════
    /// · <c>ElementTreeListUtility.IsCreatable(DbElement, DbElement)</c> —— 看着正合用
    ///   （"能不能在这儿建"），但**两个参数都是 DbElement、官方没说哪个是父哪个是子**，
    ///   而它在 <c>Aveva.Supports</c>（支吊架）命名空间下、作用域可能是局部的。
    ///   **猜参数顺序 = 给一个很有信心的错答案**，不做。层级合法性走
    ///   <c>e3d_type_schema</c>（`OwnerTypes`/`MemberTypes`，那是权威的）。
    /// · <c>DbElementExtensions.SetAttributeValue</c> —— 是写，不进这个只读工具。
    ///
    /// ⚠ <b>未上真机</b>。每一项各报各的成败。
    /// </summary>
    internal static class E3dPrecheck
    {
        public static string Run(string names, string attr, string value, string newType, int max)
        {
            if (string.IsNullOrWhiteSpace(names))
                return "precheck: 缺 names（元素路径，逗号分隔）。";
            if (max <= 0) max = 60;

            var ext = typeof(DbElement).Assembly.GetType("Aveva.Core.Database.DbElementExtensions");
            var sb = new StringBuilder();
            if (ext == null)
                sb.Append("⚠ 找不到 DbElementExtensions —— 可改性与属性清单**查不了**（不是'可以改'）。\n");

            int n = 0;
            foreach (var raw in names.Split(','))
            {
                var p = (raw ?? "").Trim();
                if (p.Length == 0) continue;
                if (++n > max) { sb.Append("★只处理前 ").Append(max).Append(" 个（不是没有）\n"); break; }

                DbElement el;
                try
                {
                    el = DbElement.GetElement(p);
                    if (!el.IsValid) { sb.Append("── ").Append(p).Append("\n   ✗ 不是有效元素（名字不存在？）。\n"); continue; }
                }
                catch (Exception ex) { sb.Append("── ").Append(p).Append("\n   ✗ 解析抛异常：").Append(Inner(ex)).Append("\n"); continue; }

                DbElementType et = null;
                try { et = el.GetElementType(); } catch { }
                sb.Append("── ").Append(p).Append("  [").Append(et == null ? "?" : et.Name).Append("]\n");

                // ① 能不能改 —— ★三条分开问，因为原因不同、下一步也不同
                if (ext != null)
                {
                    string claimed = Bool(ext, "IsClaimed", el);
                    string ro = Bool(ext, "IsReadOnly", el);
                    string locked = Bool(ext, "IsLocked", el);
                    sb.Append("   已被 claim：").Append(claimed)
                      .Append("   只读：").Append(ro)
                      .Append("   已锁：").Append(locked).Append("\n");
                    if (claimed == "否")
                        sb.Append("   ★没被 claim —— **要改就得先 claim**（e3d_db_claim），不然写不进去。\n");
                    if (ro == "是" || locked == "是")
                        sb.Append("   ⛔ 只读或已锁 —— **别发写命令**，发了也只会失败。先弄清是谁锁的。\n");
                }

                // ② 它到底有哪些属性 —— 顶掉 e3d_attr_list 那份写死的 34 个名单
                if (ext != null)
                {
                    try
                    {
                        var mi = ext.GetMethod("GetAllVisibleAttributes", BindingFlags.Public | BindingFlags.Static,
                                               null, new[] { typeof(DbElement) }, null);
                        if (mi == null) sb.Append("   属性清单：(这个版本没有 GetAllVisibleAttributes)\n");
                        else
                        {
                            var it = mi.Invoke(null, new object[] { el }) as System.Collections.IEnumerable;
                            if (it == null) sb.Append("   属性清单：(回 null)\n");
                            else
                            {
                                var list = new List<string>();
                                foreach (var a in it)
                                {
                                    try { var nm = a.GetType().GetProperty("Name"); list.Add(nm == null ? Convert.ToString(a) : Convert.ToString(nm.GetValue(a, null))); }
                                    catch { }
                                }
                                sb.Append("   属性清单（**这个元素真有的**，不是写死名单）：")
                                  .Append(list.Count).Append(" 个\n     ");
                                for (int i = 0; i < list.Count && i < 60; i++) sb.Append(list[i]).Append(' ');
                                if (list.Count > 60) sb.Append("  ★只列前 60（不是没有）");
                                sb.Append("\n");
                            }
                        }
                    }
                    catch (Exception ex) { sb.Append("   属性清单：(抛异常 ").Append(Inner(ex)).Append(")\n"); }
                }

                // ③ 这个值合不合法 —— 发 attr_set 之前问，别等 `Data of wrong type`
                if (!string.IsNullOrWhiteSpace(attr) && et != null)
                    sb.Append(AllowedLine(et, attr.Trim(), value));

                // ④★2026-07-31 逐条读完 XML 后补的三条，**都比 ①②③ 更准**
                if (!string.IsNullOrWhiteSpace(attr))
                    sb.Append(ElementLevelLines(el, attr.Trim()));
            }

            // ⑤ 这个位置能不能建某类型 —— 此前靠真机回 (41,8)
            if (!string.IsNullOrWhiteSpace(newType))
            {
                sb.Append("── 能不能在上面这些元素下建 ").Append(newType).Append("\n");
                DbElementType nt = null;
                try { nt = DbElementType.GetElementType(newType.Trim()); } catch { }
                if (nt == null || !nt.IsValid)
                    sb.Append("   类型「").Append(newType).Append("」解析不出 —— 用 e3d_type_schema 查全名/短名。\n");
                else
                    foreach (var raw in (names ?? "").Split(','))
                    {
                        var p = (raw ?? "").Trim();
                        if (p.Length == 0) continue;
                        try
                        {
                            var el = DbElement.GetElement(p);
                            if (!el.IsValid) continue;
                            // ★`DbElement.IsCreatable(DbElementType)` 签名毫无歧义。
                            //   我此前写了"不做，因为 ElementTreeListUtility.IsCreatable(DbElement,DbElement)
                            //   猜不出参数顺序" —— 那个判断没错，**但我找错了地方**。
                            sb.Append("   ").Append(p).Append(" → ")
                              .Append(el.IsCreatable(nt) ? "**可以建**" : "**建不了**").Append('\n');
                        }
                        catch (Exception ex) { sb.Append("   ").Append(p).Append(" → (**判不了**: ").Append(Inner(ex)).Append(")\n"); }
                    }
            }

            return "precheck（**改之前先问** —— 全只读）：\n" + sb
                 + "★三件事的下一步各不相同：没 claim → 先 claim；只读/已锁 → 别发；"
                 + "值不合法 → 换值或换属性。别混成一句\"改不了\"。\n";
        }

        /// <summary>
        /// ★2026-07-31 —— 逐条读完 XML 后补的**元素级**三条，都比类型级的更准：
        ///   `element.IsAttributeSetable(attr)`          「**这个属性**能不能改」，比元素级 IsReadOnly 准
        ///   `element.GetAttributeAllowedValues(attr, out String[])`   「**能填哪些**」，不是「这个值行不行」
        ///   `element.GetAttributeAllowedRanges(attr, out Double[])`   数值型的合法区间
        ///   `DbAttribute.GetDefault(型, out T)`         属性的**官方默认值**（我们一直在猜）
        /// ★我搜到并用上了 `IsAllowed`，而 `AllowedValues` 就在它下面一行 —— 关键词不匹配，所以看不见。
        /// </summary>
        private static string ElementLevelLines(DbElement el, string attrName)
        {
            var sb = new StringBuilder();
            DbAttribute a;
            try { a = DbAttribute.GetDbAttribute(attrName); if (a == null) return ""; }
            catch { return ""; }

            // a) 这个属性能不能改（元素级 + 属性级，比 IsReadOnly 准）
            try { sb.Append("   ").Append(attrName).Append(" 能不能设：").Append(el.IsAttributeSetable(a) ? "**能**" : "**不能**").AppendLine(); }
            catch (Exception ex) { sb.Append("   ").Append(attrName).Append(" 能不能设：(**判不了** ").Append(Inner(ex)).Append(")").AppendLine(); }

            // b) ★能填哪些 —— 这条比「这个值行不行」有用得多
            try
            {
                string[] vals = null;
                if (el.GetAttributeAllowedValues(a, ref vals) && vals != null && vals.Length > 0)
                {
                    sb.Append("   ★合法取值（").Append(vals.Length).Append(" 个）：");
                    for (int i = 0; i < vals.Length && i < 40; i++) sb.Append(vals[i]).Append(' ');
                    if (vals.Length > 40) sb.Append("  ★只列前 40（不是没有）");
                    sb.AppendLine();
                }
            }
            catch { /* 不是枚举型属性就没有取值表，不报错 */ }

            // c) 数值型的合法区间
            try
            {
                double[] rng = null;
                if (el.GetAttributeAllowedRanges(a, ref rng) && rng != null && rng.Length > 0)
                {
                    sb.Append("   ★合法区间：");
                    for (int i = 0; i + 1 < rng.Length; i += 2) sb.Append('[').Append(rng[i]).Append(", ").Append(rng[i + 1]).Append("] ");
                    sb.AppendLine();
                }
            }
            catch { }

            // d) 官方默认值 —— 按属性类型挑重载
            try
            {
                var et = el.GetElementType();
                switch (a.Type)
                {
                    // ⛔ `GetDefault` 回的是 **PdmsMessage** 不是 bool —— 又一次「看到签名 ≠ 知道怎么调」。
                    //    非空消息 = 没拿到（如这个类型上根本没定义默认值），此时**什么都不说**，
                    //    不把「没有默认值」报成「默认值是 0」。
                    case DbAttributeType.INTEGER:
                    { int d; var m = a.GetDefault(et, out d); if (IsOk(m)) sb.Append("   官方默认值：").Append(d).AppendLine(); break; }
                    case DbAttributeType.DOUBLE:
                    { double d; var m = a.GetDefault(et, out d); if (IsOk(m)) sb.Append("   官方默认值：").Append(d).AppendLine(); break; }
                    case DbAttributeType.BOOL:
                    { bool d; var m = a.GetDefault(et, out d); if (IsOk(m)) sb.Append("   官方默认值：").Append(d).AppendLine(); break; }
                    case DbAttributeType.STRING:
                    { string d; var m = a.GetDefault(et, out d); if (IsOk(m) && !string.IsNullOrEmpty(d)) sb.Append("   官方默认值：").Append(d).AppendLine(); break; }
                }
            }
            catch { }

            // e) 单位与量纲 —— 「小 1000 倍」那个 bug 的根治面
            try
            {
                sb.Append("   类型/单位/量纲：").Append(a.Type).Append(" · ").Append(a.Units).Append(" · ").Append(a.Dimension).AppendLine();
            }
            catch { }
            return sb.ToString();
        }

        /// <summary>`DbAttribute.IsAllowed(DbElementType, Int32|Double|String)` —— 按值的形状挑重载。</summary>
        private static string AllowedLine(DbElementType et, string attrName, string value)
        {
            DbAttribute a;
            try
            {
                a = DbAttribute.GetDbAttribute(attrName);
                if (a == null) return "   属性「" + attrName + "」：E3D 不认识这个名字。\n";
            }
            catch (Exception ex) { return "   属性「" + attrName + "」：解析抛异常 " + Inner(ex) + "\n"; }

            var sb = new StringBuilder();
            // 先答"这个类型有没有这个属性"（比值合不合法更根本）
            try { sb.Append("   ").Append(et.Name).Append(" 有没有 ").Append(attrName).Append("：").Append(et.IsAttributeValid(a) ? "有" : "**没有**").Append("\n"); }
            catch (Exception ex) { sb.Append("   IsAttributeValid 抛异常：").Append(Inner(ex)).Append("\n"); }

            if (string.IsNullOrWhiteSpace(value)) return sb.ToString();

            // ★按值的形状选重载：整数 → Int32，小数 → Double，其余 → String。
            //   **不做隐式转换**（把 "150" 同时当 Int32 和 String 试，两条结果打架时说不清听谁的）。
            var v = value.Trim();
            Type argT; object arg;
            int iv; double dv;
            if (int.TryParse(v, out iv)) { argT = typeof(int); arg = iv; }
            else if (double.TryParse(v, out dv)) { argT = typeof(double); arg = dv; }
            else { argT = typeof(string); arg = v; }

            try
            {
                var mi = typeof(DbAttribute).GetMethod("IsAllowed", BindingFlags.Public | BindingFlags.Instance,
                                                       null, new[] { typeof(DbElementType), argT }, null);
                if (mi == null) { sb.Append("   值合法性：(这个版本没有 IsAllowed(DbElementType,").Append(argT.Name).Append("))\n"); return sb.ToString(); }
                var r = mi.Invoke(a, new object[] { et, arg });
                sb.Append("   值「").Append(v).Append("」（按 ").Append(argT.Name).Append(" 判）：")
                  .Append((r is bool && (bool)r) ? "✅ 合法" : "❌ **不合法**").Append("\n");
                sb.Append("   ★这是**发送前**就能问到的 —— 不用再靠发一条收 `Data of wrong type`。\n");
            }
            catch (Exception ex) { sb.Append("   值合法性：(**判不了** —— ").Append(Inner(ex)).Append(")\n"); }
            return sb.ToString();
        }

        /// <summary>PdmsMessage 空 = 成功。★拿不到默认值时**什么都不说**，不报成「默认值是 0」。</summary>
        private static bool IsOk(Aveva.Core.Utilities.Messaging.PdmsMessage m)
        {
            try { return m == null || string.IsNullOrWhiteSpace(E3dApiWrapper.MsgText(m)); }
            catch { return false; }
        }

        /// <summary>static(DbElement)→Boolean；★抛异常回「判不了」**不回 false**。</summary>
        private static string Bool(Type t, string m, DbElement el)
        {
            try
            {
                var mi = t.GetMethod(m, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(DbElement) }, null);
                if (mi == null) return "(无 " + m + ")";
                var r = mi.Invoke(null, new object[] { el });
                if (!(r is bool)) return "(非 Boolean)";
                return ((bool)r) ? "是" : "否";
            }
            catch (Exception ex) { return "(**判不了**:" + Inner(ex) + ")"; }
        }

        private static string Inner(Exception ex)
        {
            var e = ex;
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }
    }
}
