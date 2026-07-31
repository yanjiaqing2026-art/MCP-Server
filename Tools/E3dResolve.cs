using System;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>按名字拿元素 —— 唯一收口</b>。
    ///
    /// ═══ 为什么要它（2026-07-31 真机第十六跑 §32 逼出来的）═══════════════════
    /// §32 的现象：<c>e3d_collect_query</c> 明明找到了一个真元素 <c>/RF-3900-3153</c>，
    /// 把它交给 <c>e3d_status_read</c> 却回
    /// <code>status.read: 一个元素都没解析出来。解析不了的：/RF-3900-3153</code>
    ///
    /// 根因在官方 XML 里写得清清楚楚（`Aveva.Core.Database.xml` 逐字）：
    /// <code>
    ///   DbElement.GetElement(String)
    ///     "Get an Element instance using element name. If the element name is not known,
    ///      a null Element is returned. **DEPRECATED. Use MDB.FindElement(string) to get an
    ///      element of a given name or the Parse method to parse a PML1 string.**"
    /// </code>
    /// 而我**六个新工具全都用的是它**（Status / Precheck / FabCheck / NativeOps /
    /// Analysis / DatalDump）。
    ///
    /// ★后果比"一个方法过时"严重得多，而且**在测试里看不出来**：
    ///   验收脚本里绝大多数元素是**我们自己刚建的**（当前 DB、当前 session），
    ///   `GetElement` 拿得到 → 每一节都过。
    ///   而工程师的**真元素**在别的 DB 里 → 全部拿不到。
    ///   **"对我们建的东西有效、对真实项目无效"** —— 这正是本仓最怕的那种通过。
    ///
    /// ═══ 三级解析，每级都有出处 ═══════════════════════════════════════════════
    ///   ① <c>MDB.CurrentMDB.FindElement(name)</c> —— 官方指定的替代，**跨全部 DB 找**
    ///   ② <c>DbElement.Parse(text, out el, out msg)</c> —— 官方："e.g. /MYELE，
    ///      或 PML1 表达式如 `BOX 1 OF /VESS1`"。名字带表达式时靠它。
    ///   ③ <c>DbElement.GetElement(name)</c> —— 老路，**留作兜底**：
    ///      前两级在某些 E3D 版本上可能不可用，那时它至少还能认当前 DB 的元素。
    /// ★顺序不能反：把 ③ 放前面就等于什么都没改（它对当前 DB 的元素总是成功）。
    /// </summary>
    internal static class E3dResolve
    {
        /// <summary>
        /// 按名字拿元素。拿不到时 <paramref name="why"/> 说清**三级各自的结果**——
        /// 「三级都没找到」与「MDB 没开」是两件事，混成一句会让人去查错的方向。
        /// </summary>
        internal static bool Element(string path, out DbElement el, out string why)
        {
            el = DbElement.GetElement();
            why = null;
            var p = (path ?? "").Trim();
            if (p.Length == 0) { why = "名字为空。"; return false; }

            var notes = new System.Text.StringBuilder();

            // ① 官方指定的替代：跨全部 DB
            try
            {
                var mdb = MDB.CurrentMDB;
                if (mdb == null) notes.Append("[MDB 没开] ");
                else
                {
                    var found = mdb.FindElement(p);
                    if (found.IsValid) { el = found; return true; }
                    notes.Append("[MDB.FindElement 没找到] ");
                }
            }
            catch (Exception ex) { notes.Append("[MDB.FindElement 抛异常: ").Append(Short(ex)).Append("] "); }

            // ② Parse —— 官方说它还能吃 PML1 表达式
            try
            {
                DbElement parsed;
                Aveva.Core.Utilities.Messaging.PdmsMessage msg;
                if (DbElement.Parse(p, out parsed, out msg) && parsed.IsValid) { el = parsed; return true; }
                notes.Append("[Parse 没解析出] ");
            }
            catch (Exception ex) { notes.Append("[Parse 抛异常: ").Append(Short(ex)).Append("] "); }

            // ③ 老路兜底
            try
            {
                var legacy = DbElement.GetElement(p);
                if (legacy.IsValid)
                {
                    el = legacy;
                    return true;
                }
                notes.Append("[GetElement(已弃用) 也没找到] ");
            }
            catch (Exception ex) { notes.Append("[GetElement 抛异常: ").Append(Short(ex)).Append("] "); }

            why = "「" + p + "」三级解析都没拿到：" + notes
                + "\n★三级分别是 MDB.FindElement（跨库）/ DbElement.Parse / GetElement(已弃用)。"
                + "上面括号里是各自的结果 —— 「MDB 没开」与「三级都没找到」是两件事，别混。";
            return false;
        }

        private static string Short(Exception ex)
        {
            var e = ex;
            while (e is System.Reflection.TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name;
        }
    }
}
