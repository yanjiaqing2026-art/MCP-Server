using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>COLLECT 查询</b> —— 让 agent 能像工程师一样**按条件批量查 E3D**。
    ///
    /// ═══ 为什么要有它（这是全仓最久的一个「证明了能用、然后什么都没建」）═══════
    /// PML 的批量与自省能力（<c>COLLECT</c> / <c>EVALUATE</c> / <c>.ATTRIBUTES()</c> /
    /// <c>!!ce.mem</c>）**09 与 17 两侧零使用**。根因一句话：这些能力**全部返回数组**，
    /// 而插件唯一取值通道 <c>GetStringFromPML</c> **只读字符串** → 命令跑了、值拿不回来。
    /// 真机第十二/十三跑的 <c>(no return value)</c> 就是这个后果。
    ///
    /// 我们为此绕过两条弯路，**两条都写进注释防后人再走**：
    ///   ① 「一次调用发多行 PML，用 <c>do…enddo</c> 把数组拼成字符串」
    ///      → 第十三跑真机否决：<c>(46,80) PML: Invalid syntax in the current context</c>，
    ///        **断在第 3 行 `do`**（前两行都被接受了）。
    ///        结论：**多行没问题，块结构不行** —— <c>RunPMLCommandInPDMS</c> 跑的是
    ///        **命令上下文**，块结构要**宏上下文**。
    ///   ② 「插件写临时 <c>.pmlmac</c> 宏再 <c>$M/路径</c>」
    ///      → 能跑通（验收脚本实证 EQUI 84 / SPEC 118），但要落临时文件、
    ///        且插件对 <c>$M</c> 的成败判定本身有问题（回执报错而宏其实跑了）。
    ///
    /// ═══ ★正解：AVEVA 有原生 API，根本不用跑 PML 文本 ═══════════════════════
    /// 依据是 AVEVA 自带 .NET XML 文档（<c>Aveva.Core.Database.xml</c>），签名逐字：
    ///   <c>T:DbCollection</c>
    ///       "Representation of an Database (PML1) collection."
    ///   <c>DbCollection.Parse(string name, out DbCollection collection, out PdmsMessage error)</c>
    ///       "Create collection by parsing text. The text may be any PML1 collection.
    ///        e.g. ALL BOX WHERE (XLEN + 10) FOR /MYSITE"
    ///       name  = "collection text" · error = "Error object if invalid text entered"
    ///       返回   = "true if valid collection"
    ///   <c>DbCollection.Evaluate()</c>
    ///       "Evaluate collection."  返回 = <b>"List of elements matching collection"</b>
    ///
    /// —— 于是：**不跑 PML 文本 · 不写临时文件 · 不做字符串往返 · 错误从
    ///    <c>PdmsMessage</c> 出来（真文本，不会被 ToString() 吞成类型名）**。
    ///
    /// ★<b>输入是「选择准则」，不是整条 PML 语句</b>：传 <c>ALL SPEC</c>，
    ///   **不要**传 <c>VAR !x COLLECT ALL SPEC</c>。官方例子就是 <c>ALL BOX WHERE …</c>。
    ///   这一点容易搞错 —— 09 侧 <c>e3dArrayQuery</c> 原本产的是带 <c>VAR</c> 的整句。
    ///
    /// ═══ ⚠ 已知未确认项（如实标注，不假装）═══════════════════════════════════
    /// <c>Evaluate()</c> 的**具体返回类型**文档只写 "List of elements"，没给类型名。
    /// 本文件因此**按 IEnumerable 处理并逐个尝试转 DbElement**，转不动就如实报
    /// 「拿到了 N 个对象但不是 DbElement，实际类型是 X」——
    /// **绝不猜一个类型然后 cast 失败把整个插件带崩**。
    /// 第一次真机跑若走到那个分支，回执里就有真实类型名，照着改一行即可。
    /// </summary>
    internal static class E3dCollectQuery
    {
        /// <summary>
        /// 按 PML1 选择准则批量查元素。
        /// </summary>
        /// <param name="criteria">
        /// 选择准则原文，如 <c>ALL SPEC</c> · <c>ALL EQUI FOR /SITE-A</c> ·
        /// <c>ALL BOX WHERE (XLEN GT 1000)</c>。★**不含** <c>VAR !x COLLECT</c> 前缀。
        /// </param>
        /// <param name="max">最多返回多少个（防一次拉回几十万个把回执撑爆）。</param>
        /// <param name="attrs">可选：逐个元素额外读回的属性名（逗号分隔）。空 = 只回名字。</param>
        public static string Run(string criteria, int max, string attrs)
        {
            if (string.IsNullOrWhiteSpace(criteria))
                return "collect: 缺 criteria（选择准则）。例：'ALL SPEC' / 'ALL EQUI FOR /SITE-A'。" +
                       "★只传准则，不要带 'VAR !x COLLECT' 前缀。";
            criteria = criteria.Trim();
            if (max <= 0) max = 200;

            // ★把常见的错误输入形态挡在前面并**说清怎么改** —— 这比让 Parse 回一句
            //   看不懂的语法错有用得多（09 侧原本产的就是带 VAR 的整句）。
            var upper = criteria.ToUpperInvariant();
            if (upper.StartsWith("VAR ") || upper.Contains(" COLLECT "))
                return "collect: criteria 传成了整条 PML 语句。DbCollection.Parse 收的是**选择准则**，" +
                       "官方例子是 'ALL BOX WHERE (XLEN + 10) FOR /MYSITE'。" +
                       "把 'VAR !x COLLECT ' 去掉，只留后面那半：收到的是「" + criteria + "」。";

            DbCollection coll;
            Aveva.Core.Utilities.Messaging.PdmsMessage err;
            bool ok;
            try
            {
                ok = DbCollection.Parse(criteria, out coll, out err);
            }
            catch (Exception ex)
            {
                return "collect: DbCollection.Parse 抛异常：" + ex.Message;
            }

            if (!ok || coll == null)
            {
                // ★错误文本走既有的 MessageText 提取器 —— 直接 ToString() 会得到
                //   `…PdmsMessageImpl` 这种类型名（本仓第七跑白跑一整轮就是因为它）。
                string why = E3dApiWrapper.MsgText(err);
                return "collect: 准则解析失败（criteria=「" + criteria + "」）。" +
                       (string.IsNullOrWhiteSpace(why)
                            ? "E3D 未给出错误文本。"
                            : "E3D 原话：" + why);
            }

            object evaluated;
            try
            {
                evaluated = coll.Evaluate();
            }
            catch (Exception ex)
            {
                return "collect: 准则解析成功但 Evaluate() 抛异常：" + ex.Message +
                       "\n（准则本身是合法的 —— 这是求值阶段的问题，多半是作用域太大或库不可达。）";
            }

            if (evaluated == null)
                return "collect: Evaluate() 回 null（准则合法但没求出结果）。criteria=「" + criteria + "」";

            var list = evaluated as IEnumerable;
            if (list == null)
                return "collect: Evaluate() 回的不是可枚举对象，实际类型 = " + evaluated.GetType().FullName +
                       "\n★这是**未确认项**：官方文档只写 \"List of elements matching collection\"、没给类型名。" +
                       "把这行类型名贴回来，改一行即可。";

            var sb = new StringBuilder();
            var wantAttrs = new List<string>();
            if (!string.IsNullOrWhiteSpace(attrs))
            {
                foreach (var a in attrs.Split(','))
                    if (!string.IsNullOrWhiteSpace(a)) wantAttrs.Add(a.Trim());
            }

            int n = 0, skipped = 0;
            string oddType = null;
            foreach (var o in list)
            {
                if (n >= max) { skipped++; continue; }
                if (!(o is DbElement))
                {
                    // ★不猜、不静默丢：记下真实类型，最后如实报。
                    if (oddType == null && o != null) oddType = o.GetType().FullName;
                    skipped++;
                    continue;
                }
                var el = (DbElement)o;
                n++;
                sb.Append("  ");
                string nm;
                try { nm = el.GetAsString(DbAttributeInstance.NAME); }
                catch { nm = null; }
                // 无名元素在 E3D 里很常见（ATT 真值里绝大多数图元无名）→ 回 dbref，
                // **不要**回空串假装它没有身份。
                sb.Append(string.IsNullOrWhiteSpace(nm) ? el.ToString() : nm);

                foreach (var a in wantAttrs)
                {
                    string v = null;
                    try
                    {
                        var att = DbAttribute.GetDbAttribute(a);
                        if (att != null) v = el.GetAsString(att);
                    }
                    catch { v = null; }
                    sb.Append("|").Append(a).Append("=").Append(string.IsNullOrWhiteSpace(v) ? "(空)" : v);
                }
                sb.Append("\n");
            }

            var head = new StringBuilder();
            head.Append("collect: ").Append(n).Append(" elements");
            head.Append("  (criteria: ").Append(criteria).Append(")");
            if (skipped > 0) head.Append("  ★另有 ").Append(skipped).Append(" 个未列出");
            // ★"截断"必须说出来 —— 悄悄截断会被读成"机器上就这些"，那是假事实。
            if (skipped > 0 && oddType == null) head.Append("（超过 max=").Append(max).Append("，不是没有）");
            if (oddType != null)
                head.Append("\n★其中有非 DbElement 对象，实际类型 = ").Append(oddType)
                    .Append("（官方文档没给 Evaluate 的返回元素类型，这行就是答案，照着改一行即可）");
            head.Append("\n");
            return head.ToString() + sb.ToString();
        }
    }
}
