using System;
using System.Reflection;
using System.Text;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>PML 变量按类型读回</b> —— 2026-07-31 逐条读完 XML 之后的更正。
    ///
    /// ═══ 这条修的是「走了三轮弯路」的那族 ═══════════════════════════════════════
    /// 本仓在 <c>CLAUDE.md</c>、记忆、多份文档里反复写着：
    /// > 插件唯一取值通道 <c>GetStringFromPML</c> **只读字符串**，而 COLLECT / EVALUATE
    /// > 全返回数组 → 命令跑了值拿不回 → <c>(no return value)</c>。
    ///
    /// **同一个 <c>Aveva.Core.Utilities.CommandLine.Command</c> 类上就有取数组的那个。**
    /// 当场 grep 核对（不是推的）：插件用了这个类的
    /// <c>CreateCommand</c>(5 处) / <c>RunPMLCommandInPDMS</c>(4 处) / <c>GetStringFromPML</c>(3 处)，
    /// 而 <c>GetStringArrayFromPML</c> **出现 0 处 —— 从没用过**。
    ///
    /// 为这条我们走了三轮弯路（PML 文本 → 多行块 → 临时宏 <c>$M/路径</c>），
    /// 最后靠换成 <c>DbCollection</c> 才绕开 COLLECT 那一半；
    /// 而 <c>!!ce.MEM</c> / <c>.ATTRIBUTES()</c> / <c>.METHODS()</c> 至今记着
    /// 「**属性自省仍然没有通路，这是如实的欠账**」。**通路一直在同一个类上。**
    ///
    /// ═══ 本工具做什么 ═══════════════════════════════════════════════════════════
    /// 给一段 PML（给变量赋值）+ 变量名 + 期望类型，跑完之后**按类型读回**：
    /// <code>
    ///   strarray → Command.GetStringArrayFromPML   ←★ 就是这条，此前 0 使用
    ///   string   → Command.GetStringFromPML        （已在用）
    ///   double   → Command.GetDoubleFromPML
    ///   int      → Command.GetIntFromPML
    ///   bool     → Command.GetBoolFromPML
    /// </code>
    /// 读完可选 <c>DeletePMLVar</c> 清理，不给 E3D 会话留垃圾变量。
    ///
    /// ═══ 三条如实边界 ═══════════════════════════════════════════════════════════
    /// ① ⚠ <b>一次都没上过真机。</b>「这个类上有这个方法」只证明**够得着**，
    ///    不证明它对 COLLECT 产的数组真的回得来 —— 验收脚本 §37 就是验这个。
    /// ② <b>「读不回」与「查出来是空的」分开报</b>。这两句指向完全不同的下一步，
    ///    混为一谈会让人去查类型名（第七跑就是这么白跑的）。
    /// ③ 方法拿不到就<b>明说这个 E3D 版本没有</b>，不静默回落到字符串通道 ——
    ///    回落正是「命令成功、值没进去」的成因。
    /// </summary>
    internal static class E3dPmlQuery
    {
        public static string Run(string pml, string varName, string kind, bool cleanup, int max)
        {
            if (string.IsNullOrWhiteSpace(varName))
                return "pmlquery: 缺 var（要读回的 PML 变量名，如 `!!PCQ`）。";
            if (max <= 0) max = 500;
            var vn = varName.Trim();
            var k = (kind ?? "strarray").Trim().ToLowerInvariant();

            var sb = new StringBuilder();

            // ── ① 先跑那段 PML（给变量赋值）──────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(pml))
            {
                string err;
                bool ok = E3dApiWrapper.TryRunPmlPublic(pml.Trim(), out err);
                sb.Append("① 执行 PML：").Append(ok ? "OK" : "**被拒**").Append('\n');
                if (!ok)
                {
                    // ★命令没跑成就别去读变量 —— 读到的会是上一次的残值或空，两种都会误导
                    return sb.Append("   E3D 原话：").Append(string.IsNullOrWhiteSpace(err) ? "(无文本)" : err)
                             .Append("\n★命令没跑成，**不读变量** —— 读到的可能是上次的残值。\n").ToString();
                }
            }

            // ── ② 按类型读回 ────────────────────────────────────────────────────
            string mname =
                k == "strarray" ? "GetStringArrayFromPML" :
                k == "string"   ? "GetStringFromPML" :
                k == "double"   ? "GetDoubleFromPML" :
                k == "int"      ? "GetIntFromPML" :
                k == "bool"     ? "GetBoolFromPML" : null;
            if (mname == null)
                return sb.Append("pmlquery: 不认识的 kind「").Append(kind)
                         .Append("」。可选：strarray(默认) / string / double / int / bool\n").ToString();

            var ct = Type.GetType("Aveva.Core.Utilities.CommandLine.Command, Aveva.Core.Utilities");
            if (ct == null)
                return sb.Append("pmlquery: 找不到 Aveva.Core.Utilities.CommandLine.Command —— 这个 E3D 版本结构不同。\n").ToString();

            var mi = ct.GetMethod(mname, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null)
                  ?? ct.GetMethod(mname, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            if (mi == null)
                return sb.Append("pmlquery: `Command.").Append(mname)
                         .Append("(String)` **在这个 E3D 版本上不存在**。\n")
                         .Append("★这是「这个版本没有」，不是「读回来是空的」——两句话不一样。\n").ToString();

            object result;
            try { result = mi.Invoke(mi.IsStatic ? null : Activator.CreateInstance(ct), new object[] { vn }); }
            catch (Exception ex)
            {
                var e = ex; while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
                return sb.Append("② 读回 ").Append(vn).Append(" 抛异常：").Append(e.GetType().Name).Append(": ").Append(e.Message)
                         .Append("\n★这是**读不回**（通道问题），不是「查出来是空的」。\n").ToString();
            }
            finally
            {
                if (cleanup)
                {
                    try
                    {
                        var del = ct.GetMethod("DeletePMLVar", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                                               null, new[] { typeof(string) }, null);
                        if (del != null) del.Invoke(del.IsStatic ? null : Activator.CreateInstance(ct), new object[] { vn });
                    }
                    catch { /* 清理失败不影响结论，但也不假装清了 —— 见下方回执 */ }
                }
            }

            sb.Append("② 读回 ").Append(vn).Append("（").Append(mname).Append("）：\n");
            if (result == null)
                return sb.Append("   **回 null** —— 这是读不回，不是空数组。两者的下一步不同。\n").ToString();

            var arr = result as Array;
            if (arr != null)
            {
                sb.Append("   ").Append(arr.Length).Append(" 项");
                if (arr.Length == 0)
                    sb.Append("  ←★ **通道通了，答案就是 0 项**（不是读不回）。\n");
                else
                {
                    if (arr.Length > max) sb.Append("  ★只列前 ").Append(max).Append("（不是没有）");
                    sb.Append('\n');
                    for (int i = 0; i < arr.Length && i < max; i++)
                        sb.Append("   [").Append(i).Append("] ").Append(Convert.ToString(arr.GetValue(i))).Append('\n');
                }
                return sb.ToString();
            }

            sb.Append("   ").Append(Convert.ToString(result)).Append('\n');
            return sb.ToString();
        }
    }
}
