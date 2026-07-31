using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>状态签核 + 留言</b> —— E3D 自带的 <c>Status</c> / <c>CommentThread</c> 体系。
    ///
    /// ═══ 为什么加这个（2026-07-31 钻全量索引挖到）═══════════════════════════════
    /// 09 侧自研了 <c>SignoffLedger</c>（可验证交接）和 <c>FindingStatusStore</c>（整改闭环），
    /// 而 <b>E3D 本来就有一套状态签核体系，工程师在真实项目里用的就是它</b>：
    ///   <c>StatusManagement.Statuses</c>          项目里配了哪些状态体系（**静态属性**）
    ///   <c>StatusManagement.AssignedStatuses(el)</c> 这个元素当前挂着哪些状态（**静态方法**）
    ///   <c>AssignedStatus.CurrentValue / Comment</c>  当前值 + 备注
    ///   <c>StatusValue.ValidTransitions</c>        ★**从这个值合法能推到哪些值** ——
    ///                                              这条让"下一步能不能推进"是**查出来的**，不是猜的
    ///   <c>Status.Promote/Demote/Set/Assign/Remove/Comment</c>  推进/回退/直接置值/挂上/摘掉/留言
    ///   <c>CommentThread.Create/Get/FindCommentThreads</c> + <c>NewPost()</c>  留言串
    ///
    /// ★ <b>对 agent 的实际价值有两条，第一条比第二条重要</b>：
    ///   ① <b>读</b>：知道哪些元素**已经签发/已批准** → **不去改它们**。
    ///      今天 agent 对此一无所知，改一个已发布的元素和改一个草稿是一样的。
    ///   ② <b>写</b>：把发现的问题**留言在元素上**（工程师在 E3D 里直接看得到），
    ///      而不是只写进我们自己的库里 —— 那个库工程师根本不看。
    ///
    /// ═══ 为什么读写拆成两个工具 ═══════════════════════════════════════════════
    /// 读（<c>e3d_status_read</c>）是纯查询，随便调。
    /// 写（<c>e3d_status_write</c>）**登记进 NEVER_AUTO_TOOLS，任何档位都必须人签**：
    ///   · 状态推进 = **签字**。让 agent 免审推进状态，等于让它替人签工程文件。
    ///   · 留言也一起必人签 —— 因为**留言同样是发到全项目可见的地方**。
    ///     把留言拆出来免审，就等于给 agent 开了一条不用人看就能往共享库里写字的通道。
    ///     （宁可多按一次确认，也不要这条通道。）
    ///
    /// ═══ 如实边界 ═══════════════════════════════════════════════════════════════
    /// · 走**反射**不加编译期引用：<c>Aveva.Core.DataManagement</c> 不一定装在每套 E3D 上。
    ///   装了就用，没装就**明说"这套 E3D 没装 DataManagement"** —— 那不等于"这个项目没有状态"。
    /// · <c>Statuses</c> 是空 = 这个项目**没配状态体系**，与"读不到"是两回事，分开报。
    /// · 每个写动作回的是 API 的**真 Boolean**，false 就说 false（E3D 这套是返回值不是抛异常）。
    /// · ⚠ <b>未上真机</b>。
    /// </summary>
    internal static class E3dStatus
    {
        private const string Asm = "Aveva.Core.DataManagement";

        public static string Run(string action, string names, string status, string value, string text, int max)
        {
            if (max <= 0) max = 100;
            switch ((action ?? "").Trim().ToLowerInvariant())
            {
                case "defs":     return Defs();
                case "read":     return ReadStatus(names, max);
                case "comments": return ReadComments(names, max);

                case "assign":   return Change("assign", names, status, value, text);
                case "remove":   return Change("remove", names, status, value, text);
                case "promote":  return Change("promote", names, status, value, text);
                case "demote":   return Change("demote", names, status, value, text);
                case "set":      return Change("set", names, status, value, text);
                case "comment":  return Change("comment", names, status, value, text);

                case "post":     return Post(names, status, text);
                default:
                    return "status: 不认识的 action「" + action + "」。\n"
                         + "读：defs(项目里有哪些状态体系) · read(元素当前状态+合法去向) · comments(留言串)\n"
                         + "写：assign/remove(挂上/摘掉) · promote/demote(推进/回退) · set(直接置值) · "
                         + "comment(状态备注) · post(在元素上开留言串)";
            }
        }

        // ══ 读 ①：项目里配了哪些状态体系 ════════════════════════════════════════
        private static string Defs()
        {
            object[] defs;
            string why;
            if (!AllStatuses(out defs, out why)) return "status.defs: " + why;
            if (defs.Length == 0)
                return "status.defs: **这个项目没配状态体系**（StatusManagement.Statuses 是空的）。\n"
                     + "★这不是\"读不到\" —— 通道通了，答案就是 0 个。签核体系要在 E3D 里先配。";

            var sb = new StringBuilder();
            sb.Append("status.defs: ").Append(defs.Length).Append(" 套状态体系\n");
            foreach (var d in defs)
            {
                sb.Append("── ").Append(Str(Get(d, "Name"))).Append("  ")
                  .Append(Str(Get(d, "Description"))).Append("\n");
                var elig = Get(d, "EligebleFor") as Array;   // ★AVEVA 自己拼错的，照抄
                if (elig != null && elig.Length > 0)
                {
                    sb.Append("   适用类型：");
                    for (int i = 0; i < elig.Length && i < 20; i++)
                        sb.Append(TypeName(elig.GetValue(i))).Append(i + 1 < Math.Min(elig.Length, 20) ? " " : "");
                    if (elig.Length > 20) sb.Append("  ★只列前 20（共 ").Append(elig.Length).Append("）");
                    sb.Append("\n");
                }
                var vals = Get(d, "Values") as Array;
                if (vals != null)
                {
                    sb.Append("   取值：");
                    for (int i = 0; i < vals.Length; i++)
                    {
                        var v = vals.GetValue(i);
                        sb.Append(Str(Get(v, "Name"))).Append("(").Append(Str(Get(v, "Number"))).Append(") ");
                    }
                    sb.Append("\n");
                }
                var init = Get(d, "InitialValue");
                if (init != null) sb.Append("   初始值：").Append(Str(Get(init, "Name"))).Append("\n");
            }
            return sb.ToString();
        }

        // ══ 读 ②：元素当前状态 + **合法能推到哪** ════════════════════════════════
        private static string ReadStatus(string names, int max)
        {
            List<DbElement> els; List<string> bad;
            if (!ResolveMany(names, out els, out bad))
                return "status.read: 一个元素都没解析出来。解析不了的：" + string.Join(" ", bad.ToArray());

            Type sm;
            string why;
            if (!Mgr(out sm, out why)) return "status.read: " + why;
            var mi = sm.GetMethod("AssignedStatuses", BindingFlags.Public | BindingFlags.Static);
            if (mi == null) return "status.read: StatusManagement 上没有静态 AssignedStatuses(DbElement)。";

            var sb = new StringBuilder();
            if (bad.Count > 0) sb.Append("★解析不出的路径：").Append(string.Join(" ", bad.ToArray())).Append("\n");
            foreach (var el in els)
            {
                sb.Append("── ").Append(NameOf(el)).Append("\n");
                Array arr;
                try { arr = mi.Invoke(null, new object[] { el }) as Array; }
                catch (Exception ex) { sb.Append("   取状态抛异常：").Append(Inner(ex)).Append("\n"); continue; }
                if (arr == null || arr.Length == 0)
                {
                    sb.Append("   没挂任何状态。★这是\"没挂\"，不是\"没有状态体系\"（用 defs 看体系）。\n");
                    continue;
                }
                for (int i = 0; i < arr.Length && i < max; i++)
                {
                    var a = arr.GetValue(i);
                    var def = Get(a, "Definition");
                    var cur = Get(a, "CurrentValue");
                    sb.Append("   [").Append(Str(Get(def, "Name"))).Append("] = ")
                      .Append(Str(Get(cur, "Name"))).Append("(").Append(Str(Get(cur, "Number"))).Append(")");
                    var cmt = Str(Get(a, "Comment"));
                    if (!string.IsNullOrWhiteSpace(cmt) && cmt != "(null)") sb.Append("  备注：").Append(cmt);
                    sb.Append("\n");
                    // ★合法去向 —— 这条让"下一步能不能推进"变成查出来的
                    var tr = Get(cur, "ValidTransitions") as Array;
                    if (tr != null && tr.Length > 0)
                    {
                        sb.Append("       合法可转到：");
                        for (int k = 0; k < tr.Length; k++) sb.Append(Str(Get(tr.GetValue(k), "Name"))).Append(" ");
                        sb.Append("\n");
                    }
                    else sb.Append("       合法可转到：(空 —— 终态，或这套体系没定义转移)\n");
                }
                if (arr.Length > max) sb.Append("   ★只列前 ").Append(max).Append("（共 ").Append(arr.Length).Append("）\n");
            }
            return sb.ToString();
        }

        // ══ 读 ③：留言串 ════════════════════════════════════════════════════════
        private static string ReadComments(string names, int max)
        {
            List<DbElement> els; List<string> bad;
            if (!ResolveMany(names, out els, out bad))
                return "status.comments: 一个元素都没解析出来。解析不了的：" + string.Join(" ", bad.ToArray());

            Type ct = Type.GetType("Aveva.Core.DataManagement.Commenting.CommentThread, " + Asm);
            if (ct == null) return "status.comments: 找不到 CommentThread —— 这套 E3D 没装 DataManagement（不是\"没有留言\"）。";
            var find = ct.GetMethod("FindCommentThreads", BindingFlags.Public | BindingFlags.Static,
                                    null, new[] { typeof(DbElement) }, null);
            if (find == null) return "status.comments: CommentThread 上没有静态 FindCommentThreads(DbElement)。";

            var sb = new StringBuilder();
            foreach (var el in els)
            {
                sb.Append("── ").Append(NameOf(el)).Append("\n");
                IEnumerable list;
                try { list = find.Invoke(null, new object[] { el }) as IEnumerable; }
                catch (Exception ex) { sb.Append("   查留言抛异常：").Append(Inner(ex)).Append("\n"); continue; }
                if (list == null) { sb.Append("   回 null。\n"); continue; }
                int n = 0;
                foreach (var th in list)
                {
                    if (++n > max) { sb.Append("   ★只列前 ").Append(max).Append(" 串\n"); break; }
                    sb.Append("   《").Append(Str(Get(th, "Subject"))).Append("》 ")
                      .Append(Str(Get(th, "CreatedBy"))).Append(" ").Append(Str(Get(th, "CreatedDate")))
                      .Append("  状态=").Append(Str(Get(th, "State"))).Append("\n");
                    var posts = Get(th, "Posts") as IEnumerable;
                    if (posts == null) continue;
                    foreach (var p in posts)
                        sb.Append("      · ").Append(Str(Get(p, "CreatedBy"))).Append("：")
                          .Append(Str(Get(p, "Text"))).Append("\n");
                }
                if (n == 0) sb.Append("   没有留言串。\n");
            }
            return sb.ToString();
        }

        // ══ 写：状态动作 ════════════════════════════════════════════════════════
        private static string Change(string op, string names, string statusName, string value, string text)
        {
            List<DbElement> els; List<string> bad;
            if (!ResolveMany(names, out els, out bad))
                return "status." + op + ": 一个元素都没解析出来。解析不了的：" + string.Join(" ", bad.ToArray());
            if (string.IsNullOrWhiteSpace(statusName))
                return "status." + op + ": 要给 status=<状态体系名>（用 e3d_status_read action=defs 先看有哪些）。";

            object def;
            string why;
            if (!FindStatus(statusName, out def, out why)) return "status." + op + ": " + why;

            var sb = new StringBuilder();
            if (bad.Count > 0) sb.Append("★解析不出的路径（未处理）：").Append(string.Join(" ", bad.ToArray())).Append("\n");
            foreach (var el in els)
            {
                bool ok;
                string err = null;
                try { ok = Invoke(def, op, el, value, text, out err); }
                catch (Exception ex) { ok = false; err = Inner(ex); }
                sb.Append(ok ? "  ✅ " : "  ❌ ").Append(op).Append("  ").Append(NameOf(el));
                if (!ok) sb.Append("   ← ").Append(string.IsNullOrWhiteSpace(err) ? "API 回 false（E3D 没给文本）" : err);
                sb.Append("\n");
            }
            sb.Append("★注意：这套 API 回的是 Boolean，**false 时 E3D 往往不给原因** ——\n")
              .Append("  上面写 false 就是真 false，不要读成\"大概成了\"。\n");
            return "status." + op + "：\n" + sb;
        }

        private static bool Invoke(object def, string op, DbElement el, string value, string text, out string err)
        {
            err = null;
            bool hasText = !string.IsNullOrWhiteSpace(text);
            MethodInfo mi;
            object[] a;
            switch (op)
            {
                case "assign":
                case "promote":
                case "demote":
                    mi = Find(def, Cap(op), hasText ? new[] { typeof(DbElement), typeof(string) } : new[] { typeof(DbElement) });
                    a = hasText ? new object[] { el, text } : new object[] { el };
                    break;
                case "remove":
                    mi = Find(def, "Remove", new[] { typeof(DbElement) });
                    a = new object[] { el };
                    break;
                case "comment":
                    if (!hasText) { err = "comment 要给 text=<备注内容>。"; return false; }
                    mi = Find(def, "Comment", new[] { typeof(DbElement), typeof(string) });
                    a = new object[] { el, text };
                    break;
                case "set":
                    if (string.IsNullOrWhiteSpace(value)) { err = "set 要给 value=<状态值名>（defs 里能看到取值表）。"; return false; }
                    mi = Find(def, "Set", hasText ? new[] { typeof(DbElement), typeof(string), typeof(string) }
                                                  : new[] { typeof(DbElement), typeof(string) });
                    a = hasText ? new object[] { el, value, text } : new object[] { el, value };
                    break;
                default: err = "内部：不认识的 op " + op; return false;
            }
            if (mi == null) { err = "这个 E3D 版本的 Status 上找不到对应重载。"; return false; }
            var r = mi.Invoke(def, a);
            return r is bool && (bool)r;
        }

        // ══ 写：在元素上开一条留言串 ════════════════════════════════════════════
        private static string Post(string names, string subject, string text)
        {
            List<DbElement> els; List<string> bad;
            if (!ResolveMany(names, out els, out bad))
                return "status.post: 一个元素都没解析出来。解析不了的：" + string.Join(" ", bad.ToArray());
            if (string.IsNullOrWhiteSpace(text)) return "status.post: 要给 text=<留言内容>。";

            Type ct = Type.GetType("Aveva.Core.DataManagement.Commenting.CommentThread, " + Asm);
            if (ct == null) return "status.post: 找不到 CommentThread —— 这套 E3D 没装 DataManagement。";
            var create = ct.GetMethod("Create", BindingFlags.Public | BindingFlags.Static,
                                      null, new[] { typeof(DbElement) }, null);
            if (create == null) return "status.post: CommentThread 上没有静态 Create(DbElement)。";

            var sb = new StringBuilder();
            foreach (var el in els)
            {
                try
                {
                    var th = create.Invoke(null, new object[] { el });
                    if (th == null) { sb.Append("  ❌ ").Append(NameOf(el)).Append("   ← Create 回 null\n"); continue; }
                    if (!string.IsNullOrWhiteSpace(subject)) Set(th, "Subject", subject);
                    var np = th.GetType().GetMethod("NewPost", Type.EmptyTypes);
                    if (np == null) { sb.Append("  ❌ ").Append(NameOf(el)).Append("   ← 没有 NewPost()\n"); continue; }
                    var post = np.Invoke(th, null);
                    if (post == null) { sb.Append("  ❌ ").Append(NameOf(el)).Append("   ← NewPost 回 null\n"); continue; }
                    Set(post, "Text", text);
                    sb.Append("  ✅ ").Append(NameOf(el)).Append("  留言已建\n");
                }
                catch (Exception ex) { sb.Append("  ❌ ").Append(NameOf(el)).Append("   ← ").Append(Inner(ex)).Append("\n"); }
            }
            sb.Append("★留言是**全项目可见**的 —— 工程师在 E3D 里直接看得到，等同于对外发言。\n");
            return "status.post：\n" + sb;
        }

        // ══ 共用 ════════════════════════════════════════════════════════════════
        private static bool Mgr(out Type sm, out string why)
        {
            why = null;
            sm = Type.GetType("Aveva.Core.DataManagement.StatusManagement, " + Asm);
            if (sm == null)
            {
                why = "找不到 Aveva.Core.DataManagement.StatusManagement —— **这套 E3D 没装 DataManagement**。\n"
                    + "★这不等于\"这个项目没有状态体系\"，两件事分开（把这句原样贴回来）。";
                return false;
            }
            return true;
        }

        private static bool AllStatuses(out object[] defs, out string why)
        {
            defs = new object[0];
            Type sm;
            if (!Mgr(out sm, out why)) return false;
            try
            {
                var p = sm.GetProperty("Statuses", BindingFlags.Public | BindingFlags.Static);
                if (p == null) { why = "StatusManagement 上没有静态属性 Statuses。"; return false; }
                var arr = p.GetValue(null, null) as Array;
                if (arr == null) { why = "Statuses 回 null（不是空数组 —— 这是取值失败，不是 0 个）。"; return false; }
                defs = new object[arr.Length];
                for (int i = 0; i < arr.Length; i++) defs[i] = arr.GetValue(i);
                return true;
            }
            catch (Exception ex) { why = "取 Statuses 抛异常：" + Inner(ex); return false; }
        }

        private static bool FindStatus(string name, out object def, out string why)
        {
            def = null;
            object[] all;
            if (!AllStatuses(out all, out why)) return false;
            var got = new List<string>();
            foreach (var d in all)
            {
                var n = Str(Get(d, "Name"));
                got.Add(n);
                if (string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase)) { def = d; return true; }
            }
            // ★不猜、不模糊匹配 —— 把真有的都列出来让人挑
            why = "项目里没有叫「" + name + "」的状态体系。现有的是：" +
                  (got.Count == 0 ? "(一个都没配)" : string.Join(" · ", got.ToArray()));
            return false;
        }

        private static MethodInfo Find(object o, string name, Type[] sig)
        {
            return o.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, sig, null);
        }

        private static object Get(object o, string prop)
        {
            if (o == null) return null;
            try
            {
                var p = o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (p == null) return null;
                return p.GetValue(p.GetGetMethod().IsStatic ? null : o, null);
            }
            catch { return null; }
        }

        private static void Set(object o, string prop, object val)
        {
            var p = o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) throw new InvalidOperationException("属性 " + prop + " 写不了");
            p.SetValue(o, val, null);
        }

        private static string Str(object o) { return o == null ? "(null)" : Convert.ToString(o); }

        private static string TypeName(object dbElementType)
        {
            var n = Get(dbElementType, "Name");
            return n == null ? Convert.ToString(dbElementType) : Convert.ToString(n);
        }

        private static string NameOf(DbElement el)
        {
            try
            {
                var n = el.GetAsString(DbAttributeInstance.NAME);
                return string.IsNullOrWhiteSpace(n) ? el.ToString() : n;   // 无名元素回 dbref，不回空串
            }
            catch { return el.ToString(); }
        }

        private static bool ResolveMany(string names, out List<DbElement> els, out List<string> bad)
        {
            els = new List<DbElement>();
            bad = new List<string>();
            foreach (var raw in (names ?? "").Split(','))
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
            return els.Count > 0;
        }

        private static string Inner(Exception ex)
        {
            var e = ex;
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }

        private static string Cap(string s) { return char.ToUpperInvariant(s[0]) + s.Substring(1); }

        /// <summary>★经 E3dResolve 三级解析（官方 GetElement(String) 已 DEPRECATED，只认当前 DB）。
        /// 拿不到就回一个 invalid 元素 —— 调用方原有的 `IsValid` 判断照旧生效，形态不变。</summary>
        private static DbElement E3dResolveOrInvalid(string path)
        {
            DbElement el; string why;
            return E3dResolve.Element(path, out el, out why) ? el : DbElement.GetElement();
        }
    }
}
