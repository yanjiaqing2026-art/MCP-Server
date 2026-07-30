using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>类型 Schema 查询</b> —— 把「这个类型能建在哪儿 / 有哪些属性 / 能不能连接」
    /// 从**真机试错**变成**一次查询**。
    ///
    /// ═══ 为什么这个工具最该早点做（用户 2026-07-30 点破）═══════════════════════
    /// 我们为下面这些问题跑了**四趟内网**、写了三节验收脚本：
    ///   · 「NCYL 该建在什么下面」→ §20 建 75 种类型的矩阵、§23 三选一对照
    ///   · 「ATTA / HANG / HBRAN 建在哪」→ §26 六条链，四条断在 owner 上
    ///   · 「CONNECT 为什么连不上」→ §9 §18 §22 §24，四跑
    ///   · 「这个类型有没有这个属性」→ 发一条看回不回 `(2,201)`
    ///   · 「SPEC 还是 SPECIFICATION」→ 第七跑整跑白费
    /// 而 AVEVA 自带 .NET 文档里，<c>DbElementType</c> 上**每一条都有现成 API**：
    ///   <c>OwnerTypes</c>       "Return list of allowed owner types for Elements of this type."
    ///   <c>MemberTypes</c>      "Return list of allowed member types for Elements of this type."
    ///   <c>ValidConnections</c> "1 or 2, if this Element type has valid connections"
    ///   <c>SystemAttributes</c> "Return system attributes and UDAs on this Element type."
    ///   <c>IsAttributeValid</c> "Check whether a given attribute is valid for this Element type."
    ///   <c>ShortName</c> / <c>Description</c> / <c>DatabaseTypes</c> / <c>IsPseudo</c>
    ///   <c>GetAllElementTypes</c> "List of all system element types"
    ///
    /// ★**元教训**（比这个工具本身值钱）：不是没有数据，是**设计在先、查证在后**。
    ///   13,573 个成员的官方文档就在仓里，一条 grep 5 秒能搜到，
    ///   而我每次都是「遇到问题 → 想个办法 → 上真机试 → 失败 → 再想一个」。
    ///   **动手设计任何 E3D 机制之前，先搜这份 XML。**
    ///
    /// ═══ 这个工具解掉的既有坑 ═══════════════════════════════════════════════
    /// · <c>e3d_attr_list</c> 用的是插件**写死的 34 个属性名单**（里面连 DIAMETER 都没有），
    ///   照它下结论会把对的改成错的 → <c>SystemAttributes</c> 是**这个类型真有的**。
    /// · <c>ResolveType("SPEC")</c> 返回 null（真名 SPECIFICATION）害第七跑整跑白费
    ///   → <c>ShortName</c> + <c>Name</c> 一起回，别名问题当场可见。
    /// · 09 侧 <c>e3dModelState</c> 的 owner 规则是从 ATT 统计**推**的，
    ///   而 ATT 只能证明「允许」、不能证明「禁止」 → <c>OwnerTypes</c> 是**权威的禁止面**。
    ///
    /// ⚠ **未编译验证、未上真机**（写在没有 AVEVA 编译环境的机器上）。
    ///   每个字段单独包 try —— 某个成员在这个 E3D 版本上不存在时**只缺那一行**，
    ///   不会把整个查询带崩，也不会假装它是空的（会写 `(取不到)`）。
    /// </summary>
    internal static class E3dTypeSchema
    {
        // ★★2026-07-30 第一次真编译抓到的：`OwnerTypes` / `MemberTypes` / `DatabaseTypes`
        //   是**方法**（要加括号），不是属性。我按属性写，编译器直接报
        //   `CS0428 无法将方法组转换为非委托类型`。
        //   ★这正是**DLL 层证据等级**的实例：反射列出来是 `MemberTypes`（不带括号），
        //     **看不出是方法还是属性** —— XML 层有 `M:` / `P:` 前缀能分清，DLL 层没有。
        //     结论：拿 DLL 签名写代码，**必须编译过才算数**。
        /// <summary>
        /// 查一个类型的完整 schema；<paramref name="typeName"/> 为空则列出全部类型。
        /// </summary>
        public static string Run(string typeName, int max)
        {
            if (max <= 0) max = 400;
            return string.IsNullOrWhiteSpace(typeName) ? ListAll(max) : Describe(typeName.Trim());
        }

        private static string ListAll(int max)
        {
            DbElementType[] all;
            try { all = DbElementType.GetAllElementTypes(); }
            catch (Exception ex) { return "typeschema: GetAllElementTypes 抛异常：" + ex.Message; }
            if (all == null) return "typeschema: GetAllElementTypes 回 null。";

            var sb = new StringBuilder();
            int n = 0, skipped = 0;
            foreach (var t in all)
            {
                if (n >= max) { skipped++; continue; }
                n++;
                sb.Append("  ").Append(Safe(() => t.Name, "?"));
                var sn = Safe(() => t.ShortName, "");
                // ★短名与全名不同的**正是 SPEC/SPECIFICATION 那一族** —— 单独标出来，
                //   因为它就是第七跑整跑白费的根因（`ResolveType("SPEC")` 返回 null）。
                if (!string.IsNullOrWhiteSpace(sn) && sn != Safe(() => t.Name, "")) sb.Append("  (短名 ").Append(sn).Append(")");
                var d = Safe(() => t.Description, "");
                if (!string.IsNullOrWhiteSpace(d)) sb.Append("  — ").Append(d);
                sb.Append("\n");
            }
            var head = "typeschema: " + n + " element types";
            // ★截断必须说出来 —— 悄悄截断会被读成"就这些类型"，那是假事实。
            if (skipped > 0) head += "  ★另有 " + skipped + " 个未列出（超过 max=" + max + "，不是没有）";
            return head + "\n" + sb;
        }

        private static string Describe(string typeName)
        {
            DbElementType t;
            try { t = DbElementType.GetElementType(typeName); }
            catch (Exception ex) { return "typeschema: 解析类型「" + typeName + "」抛异常：" + ex.Message; }

            bool valid;
            try { valid = t.IsValid; } catch { valid = false; }
            if (!valid)
            {
                // ★不说"这个类型不存在" —— 说清是**这个名字**解析不出，并给可操作的下一步。
                return "typeschema: 「" + typeName + "」解析不出元素类型。"
                     + "\n★常见原因：用了 4 字缩写而这台 E3D 认全称（`SPEC` → `SPECIFICATION` 就是这个坑，"
                     + "害第七跑整跑白费）。不带参数调本工具可列出**全部类型的全名与短名**。";
            }

            var sb = new StringBuilder();
            sb.Append("typeschema: ").Append(Safe(() => t.Name, typeName));
            var sn = Safe(() => t.ShortName, "");
            if (!string.IsNullOrWhiteSpace(sn)) sb.Append("  短名=").Append(sn);
            sb.Append("\n");
            Line(sb, "说明", Safe(() => t.Description, ""));
            Line(sb, "基类型", SafeName(() => t.BaseType));
            Line(sb, "伪类型(pseudo)", Safe(() => t.IsPseudo.ToString(), "?"));

            // ★★这两行就是 §20/§23/§26 试了三跑的答案。
            Line(sb, "★合法 owner 类型", Names(() => t.OwnerTypes()));
            Line(sb, "★可包含的成员类型", Names(() => t.MemberTypes()));

            // ★CONNECT 那四跑的答案。文档原话："1 or 2, if this Element type has valid connections"
            Line(sb, "★可连接数(ValidConnections)", Safe(() => t.ValidConnections.ToString(), "(取不到)")
                 + "  ← 官方原话 \"1 or 2, if this Element type has valid connections\"；0/取不到 = 这个类型不参与连接");

            Line(sb, "所在数据库类型", Names2(() => t.DatabaseTypes()));

            // ★这一行取代插件写死的 34 个属性名单。
            string attrs;
            try
            {
                // ★同上：`SystemAttributes` 也是**方法**不是属性（第二次栽在同一处）。
                var a = t.SystemAttributes();
                if (a == null) { attrs = "(取不到)"; }
                else
                {
                    // ★不用 lambda —— C# 7.3 推断不出委托类型（CS8370）。写成显式循环最省事。
                    var names = new List<string>();
                    for (int i = 0; i < a.Length && i < 200; i++)
                    {
                        string nm = null;
                        try { nm = a[i].Name; } catch { nm = null; }
                        names.Add(string.IsNullOrWhiteSpace(nm) ? "?" : nm);
                    }
                    attrs = string.Join(" ", names);
                    if (a.Length > 200) attrs += "  ★另有 " + (a.Length - 200) + " 个未列出";
                }
            }
            catch (Exception ex) { attrs = "(取不到：" + ex.Message + ")"; }
            Line(sb, "★真实属性(SystemAttributes)", attrs);

            return sb.ToString();
        }

        // ── 下面几个小工具的共同原则：取不到就写 `(取不到)`，**绝不写空串假装没有** ──
        private static void Line(StringBuilder sb, string k, string v)
        {
            sb.Append("  ").Append(k).Append(": ").Append(string.IsNullOrWhiteSpace(v) ? "(取不到)" : v).Append("\n");
        }

        private static string Safe(System.Func<string> f, string dft)
        {
            try { var v = f(); return string.IsNullOrWhiteSpace(v) ? dft : v; } catch { return dft; }
        }

        private static string SafeName(System.Func<DbElementType> f)
        {
            try { var t = f(); return t.IsValid ? t.Name : "(取不到)"; } catch { return "(取不到)"; }
        }

        private static string Names(System.Func<DbElementType[]> f)
        {
            try
            {
                var a = f();
                if (a == null || a.Length == 0) return "(空 —— 注意这与\"取不到\"不同：空 = 官方说没有)";
                return string.Join(" ", a.Select(t => Safe(() => t.Name, "?")));
            }
            catch (Exception ex) { return "(取不到：" + ex.Message + ")"; }
        }

        private static string Names2(System.Func<Array> f)
        {
            try
            {
                var a = f();
                if (a == null || a.Length == 0) return "(空)";
                var xs = new List<string>();
                foreach (var o in a) xs.Add(o == null ? "?" : o.ToString());
                return string.Join(" ", xs);
            }
            catch (Exception ex) { return "(取不到：" + ex.Message + ")"; }
        }
    }
}
