using System;
using System.IO;
using System.Reflection;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>DATAL 全量导出</b> —— 把元素的**全部数据**导出来，走 AVEVA 官方 <c>DatalListing</c>。
    ///
    /// ═══ 这条是「MCP 能不能完全拿到 E3D 反馈」的答案的一半 ═══════════════════
    /// 用户问："MCP 是不是能完全拿到 E3D 的反馈"。把全量索引里**所有出数通道**扫了一遍，
    /// 结论分两半，**必须分清**：
    ///
    /// ⚠ <b>命令窗口的正常打印 —— 这两条 .NET 通道拿不到</b>
    ///    · <c>PdmsOutputEvents.AddOutputListener</c> 真机实测**只流 `ERROR/`** 一类
    ///      （第十四跑：收到事件 18 个，Type/Category 全是 ERROR）
    ///    · <c>PdmsOutputEventsImpl</c> 上确实有 <c>PdmsStringWriter()</c> / <c>ConsoleTextWriter()</c>
    ///      —— 那是控制台文本流本身，**但它们是实例方法，而 `PdmsOutputEvents` 是 abstract、
    ///      只有 `protected static _outputListeners`，没有任何公开途径拿到 Impl 实例**。
    ///      ★所以 `Q NAME` / `LIST` 那类**正常打印**，这两条通道拿不到。
    ///
    /// ⛔⛔ <b>但我曾在这儿写「这是能力边界，不是我们没写」—— <u>那句话错了</u></b>。
    ///    用户当时反问：**「照道理不会的，一定有一个输出。」** 他是对的 ——
    ///    我证明的只是**我试过的那两条**（都是 .NET 通道）拿不到。
    ///    PDMS 自己有一条**命令级**的输出重定向：<c>ALPHA LOG &lt;file&gt; OVER</c> … <c>ALPHA LOG END</c>。
    ///    ✅ **第十九跑真机验证通过**（§39）：`Q NAME` 回 `Name /PCT39-204944`、
    ///    `$Q` 回 PDMS 语法提示表（`'MA/RKELE' 'UNM/ARK' …`）。见 <see cref="E3dAlphaLog"/>。
    ///    ★**看到两条路走不通，不等于只有两条路。**
    ///
    /// ✅ <b>元素数据本身 —— 拿得到，而且是全量</b>（本文件做的就是这条）
    ///    <c>DatalListing.Instance</c> 是 **Public Static**（反射实测），可直接用：
    ///      <c>Elements</c>(DbElement[]) 要导哪些 · <c>fileName</c> 导到哪个文件 ·
    ///      <c>brief</c>/<c>comments</c>/<c>tab</c>/<c>changes</c> 格式选项 · <c>OutputListing()</c> 执行
    ///    —— 这正是 E3D 里 `DATAL` 命令背后的引擎。
    ///
    /// ★**准确的说法**：MCP **拿不到"命令输出文本"，但拿得到"数据本身"** ——
    ///   而 agent 真正需要的是后者。（此前几跑一直在追前者，那条路是死的。）
    ///
    /// ⚠ 文件落在 **E3D 那台机器**上。本工具把内容**读回来放进回执**，
    ///   09 侧不需要那台机器的文件系统（宏路当年就是卡在这一点上）。
    /// ⚠ **未上真机**。每一步失败都如实报，不吞。
    /// </summary>
    internal static class E3dDatalDump
    {
        public static string Run(string names, bool brief, bool comments, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(names))
                return "datal: 缺 names（要导出的元素路径，逗号分隔）。";
            if (maxChars <= 0) maxChars = 60000;

            // ── ① 解析元素 ──────────────────────────────────────────────────────
            var parts = names.Split(',');
            var els = new System.Collections.Generic.List<DbElement>();
            var bad = new System.Collections.Generic.List<string>();
            foreach (var raw in parts)
            {
                var p = (raw ?? "").Trim();
                if (p.Length == 0) continue;
                try
                {
                    var e = E3dResolveOrInvalid(p);
                    if (e.IsValid) els.Add(e); else bad.Add(p);
                }
                catch { bad.Add(p); }
            }
            if (els.Count == 0)
                return "datal: 一个元素都没解析出来。解析不了的：" + string.Join(" ", bad.ToArray());

            // ── ② 拿 DatalListing.Instance（反射：Public Static 字段，实测存在）─────
            object inst;
            Type dt;
            try
            {
                dt = typeof(DatalListing);
                var fi = dt.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (fi == null)
                    return "datal: DatalListing 上找不到 Public Static 字段 `Instance` —— 这个 E3D 版本结构不同。";
                inst = fi.GetValue(null);
                if (inst == null) return "datal: DatalListing.Instance 是 null（模块还没初始化？）。";
            }
            catch (Exception ex) { return "datal: 取 DatalListing.Instance 抛异常：" + Inner(ex); }

            // ── ③ 配置 + 执行 ───────────────────────────────────────────────────
            // ★导到临时文件再读回 —— 插件跑在 E3D 那台机器上，写得了文件；
            //   09 隔着 MCP 拿不到那台机器的文件系统，所以**必须由我们读回来放进回执**。
            string tmp = Path.Combine(Path.GetTempPath(), "pcmcp-datal-" + Guid.NewGuid().ToString("N") + ".txt");
            var log = new StringBuilder();
            try
            {
                SetProp(dt, inst, "Elements", els.ToArray(), log);
                SetProp(dt, inst, "fileName", tmp, log);
                SetProp(dt, inst, "screen", false, log);      // 不往屏幕打，只写文件
                SetProp(dt, inst, "brief", brief, log);
                SetProp(dt, inst, "comments", comments, log);

                var mi = dt.GetMethod("OutputListing", Type.EmptyTypes);
                if (mi == null) return "datal: 找不到 OutputListing()。" + log;
                mi.Invoke(inst, null);
            }
            catch (Exception ex)
            {
                return "datal: 执行 OutputListing 抛异常：" + Inner(ex) + "\n" + log;
            }

            // ── ④ 读回来 ────────────────────────────────────────────────────────
            string text;
            try
            {
                if (!File.Exists(tmp))
                    return "datal: OutputListing 跑完了但**没生成文件**（" + tmp + "）。\n"
                         + "★这不是\"没有数据\"，是这条通路在这台机器上没写成 —— 把这句贴回来。\n" + log;
                text = File.ReadAllText(tmp);
            }
            catch (Exception ex) { return "datal: 读回文件失败：" + Inner(ex); }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }

            var head = new StringBuilder();
            head.Append("datal: ").Append(els.Count).Append(" 个元素")
                .Append(brief ? "（brief）" : "（完整）").Append("，共 ").Append(text.Length).Append(" 字符");
            if (bad.Count > 0) head.Append("  ★另有 ").Append(bad.Count).Append(" 个路径解析不出：")
                                   .Append(string.Join(" ", bad.ToArray()));
            if (text.Length > maxChars)
            {
                // ★截断必须说出来 —— 悄悄截断会被读成"数据就这些"。
                text = text.Substring(0, maxChars);
                head.Append("\n★内容超过 ").Append(maxChars).Append(" 字符**已截断**（不是没有，调大 max_chars 或分批导）");
            }
            if (log.Length > 0) head.Append("\n（设属性时的提示：").Append(log).Append("）");
            return head + "\n--- DATAL ---\n" + text;
        }

        /// <summary>设属性；设不上**如实记一行**，不静默跳过（静默 = 导出的东西和你以为的不一样）。</summary>
        private static void SetProp(Type t, object inst, string name, object val, StringBuilder log)
        {
            try
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (p == null || !p.CanWrite) { log.Append("[").Append(name).Append(" 设不上] "); return; }
                p.SetValue(p.GetSetMethod().IsStatic ? null : inst, val, null);
            }
            catch (Exception ex) { log.Append("[").Append(name).Append("=").Append(Inner(ex)).Append("] "); }
        }

        private static string Inner(Exception ex)
        {
            var e = ex;
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
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
