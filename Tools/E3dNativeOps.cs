using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Aveva.Core.Database;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>原生 API 批 —— 第一批五个</b>（2026-07-31，用户："为什么不把这些功能都做进去"）。
    ///
    /// ═══ 为什么是这五个 ═══════════════════════════════════════════════════════
    /// 全量索引（<c>docs/aveva-api-index.txt</c>，85,422 个成员）挖完之后，
    /// 按「签名清楚 + 直接对着已知痛点」筛出来的：
    ///   ① <b>通径换算</b>  `Units.Bore.NominalBoreValue(Double,Int32)`
    ///      —— 我们一直自己算，而**栽过"小 1000 倍"的量纲 bug**
    ///   ② <b>成员遍历</b>  `DbElement.Members()` / `Members(DbElementType)`
    ///      —— 解掉 `!!ce.MEM` 那个缺口（PML 数组拿不回来）
    ///   ③ <b>整棵子树复制</b> `DbElement.CreateCopyHierarchyAfter(DbElement, DbCopyOption)`
    ///      —— **typical 复用**，一直想做没做
    ///   ④ <b>类型的合法属性判定</b> `DbElementType.IsAttributeValid(DbAttribute)`
    ///      —— 此前靠发一条看回不回 `(2,201)`
    ///   ⑤ <b>名字合法性预检</b> `DbElement.IsValidNameFormat(name, type, dbType, out msg)`
    ///      —— 此前靠真机回 `(41,12)` 才知道撞名/名字非法
    ///
    /// ═══ 刻意**没做**的（读了签名才知道做了也没用）═══════════════════════════
    /// · `WeightCalculator.Calculate(IGeometryCSG, IGrade, WeightType)` —— 是真计算器，
    ///   但入参是 **Aveva.Fabrication 域**的对象，**不是设计库的 DbElement**。
    ///   从我们这条路过不去，做个包装只会得到一个永远调不通的工具。
    /// · `PipingMto` —— **0 个方法、全是属性**，它是 ERM 数据交换结构（DTO），
    ///   **不是"从模型算 MTO"的 API**。
    /// · `PMLReport` —— 签名是 `AddScope(Hashtable,…)`/`Print(string)`，
    ///   是**驱动报表管理器的封装**，不是生成材料表。
    /// ★这三条都是**读了签名才改的判断**（只看类型名时我以为它们能用）。
    ///
    /// ⚠ **未上真机**。每个动作**自己报自己通不通** —— 一跑能验完一批，
    ///   不会因为堆在一起而分不清是谁的问题。
    /// </summary>
    internal static class E3dNativeOps
    {
        public static string Run(string action, string name, string arg1, string arg2, int max)
        {
            if (max <= 0) max = 200;
            switch ((action ?? "").Trim().ToLowerInvariant())
            {
                case "bore":        return Bore(arg1, arg2);
                case "members":     return Members(name, arg1, max);
                case "copytree":    return CopyTree(name, arg1);
                case "attrvalid":   return AttrValid(name, arg1);
                case "namecheck":   return NameCheck(arg1, name);
                default:
                    return "nativeops: 不认识的 action「" + action + "」。可选："
                         + "bore(通径换算) · members(成员遍历) · copytree(整棵子树复制) · "
                         + "attrvalid(类型有没有这个属性) · namecheck(名字合法性预检)";
            }
        }

        // ── ① 通径换算 ──────────────────────────────────────────────────────────
        /// <summary>
        /// `Units.Bore.NominalBoreValue(Double, Int32)` —— 官方通径换算。
        /// ★第二个参数（Int32）文档没说是什么，**不猜**：调用方传什么就发什么，
        ///   并把两个入参原样回显 —— 第一次真机跑就能对出它的含义（多半是单位/标准代号）。
        /// </summary>
        private static string Bore(string valueText, string modeText)
        {
            double v;
            if (!double.TryParse((valueText ?? "").Trim(), out v))
                return "bore: value 要是数字（公称通径，如 150）。收到：" + valueText;
            int mode = 0;
            int.TryParse((modeText ?? "0").Trim(), out mode);
            try
            {
                var b = Aveva.Core.Utilities.Units.Bore.CreateBore();
                if (b == null) return "bore: Units.Bore.CreateBore() 回 null。";
                double r = b.NominalBoreValue(v, mode);
                return "bore: NominalBoreValue(" + v + ", " + mode + ") = " + r
                     + "\n★第二个参数的含义官方没写 —— 这里原样回显入参，"
                     + "拿几个已知通径对一下就能定它是什么（别猜）。";
            }
            catch (Exception ex) { return "bore: 抛异常：" + Inner(ex); }
        }

        // ── ② 成员遍历（解掉 `!!ce.MEM` 缺口）──────────────────────────────────
        private static string Members(string path, string typeFilter, int max)
        {
            DbElement el;
            string why;
            if (!Resolve(path, out el, out why)) return "members: " + why;
            DbElement[] ms;
            try
            {
                if (string.IsNullOrWhiteSpace(typeFilter)) ms = el.Members();
                else
                {
                    var t = DbElementType.GetElementType(typeFilter.Trim());
                    if (t == null || !t.IsValid)
                        return "members: 类型「" + typeFilter + "」解析不出 —— 用 e3d_type_schema 查全名/短名。";
                    ms = el.Members(t);
                }
            }
            catch (Exception ex) { return "members: 抛异常：" + Inner(ex); }
            if (ms == null) return "members: 回 null。";

            var sb = new StringBuilder();
            sb.Append("members: ").Append(ms.Length).Append(" 个")
              .Append(string.IsNullOrWhiteSpace(typeFilter) ? "" : "（类型过滤 " + typeFilter + "）");
            // ★截断说出来 —— 悄悄截断会被读成"就这些成员"。
            if (ms.Length > max) sb.Append("  ★只列前 ").Append(max).Append(" 个（不是没有）");
            sb.Append("\n");
            for (int i = 0; i < ms.Length && i < max; i++)
            {
                string nm = null;
                try { nm = ms[i].GetAsString(DbAttributeInstance.NAME); } catch { }
                string ty = null;
                try { ty = ms[i].GetElementType().Name; } catch { }
                // 无名元素回 dbref —— **不回空串假装它没有身份**（ATT 里绝大多数图元无名）
                sb.Append("  [").Append(ty ?? "?").Append("] ")
                  .Append(string.IsNullOrWhiteSpace(nm) ? ms[i].ToString() : nm).Append("\n");
            }
            return sb.ToString();
        }

        // ── ③ 整棵子树复制（typical 复用）──────────────────────────────────────
        /// <summary>
        /// `CreateCopyHierarchyAfter(DbElement source, DbCopyOption)` ——
        /// **把一整棵已建好的子树复制过来**（典型：把一台建好的泵/一榀框架复用到另一处）。
        ///
        /// ★这是**写操作**，且**不可逆**（复制出来的整棵树要手工删）——
        ///   工具定义里标 write，09 侧的写闸会要求人签。
        /// ★`DbCopyOption` 的取值官方没给说明，**不猜**：用默认重载，
        ///   真机第一次跑回来的行为就是答案。
        /// </summary>
        private static string CopyTree(string sourcePath, string afterPath)
        {
            DbElement src, after;
            string why;
            if (!Resolve(sourcePath, out src, out why)) return "copytree: 源 " + why;
            if (!Resolve(afterPath, out after, out why)) return "copytree: 目标位置 " + why;
            try
            {
                // 找 CreateCopyHierarchyAfter 的重载（DbCopyOption 的构造官方没说明 → 反射挑最简的那个）
                var mi = typeof(DbElement).GetMethod("CreateCopyHierarchyAfter",
                    new[] { typeof(DbElement), Type.GetType("Aveva.Core.Database.DbCopyOption, Aveva.Core.Database") });
                if (mi == null)
                    return "copytree: 找不到 CreateCopyHierarchyAfter(DbElement, DbCopyOption) —— 这个 E3D 版本签名不同。";
                var optType = mi.GetParameters()[1].ParameterType;
                object opt;
                try { opt = Activator.CreateInstance(optType); }
                catch { opt = Enum.GetValues(optType).GetValue(0); }   // 是枚举就取第一个值
                var res = mi.Invoke(after, new object[] { src, opt });
                if (res == null) return "copytree: 回 null（没复制出来）。";
                var ne = (DbElement)res;
                string nm = null;
                try { nm = ne.GetAsString(DbAttributeInstance.NAME); } catch { }
                return "copytree: 复制成功 → " + (string.IsNullOrWhiteSpace(nm) ? ne.ToString() : nm)
                     + "\n★注意：复制出来的整棵树的**名字是 E3D 自动给的**，"
                     + "而 E3D 名字**全局唯一** —— 要按自己的命名规则改名得逐个 rename。";
            }
            catch (Exception ex) { return "copytree: 抛异常：" + Inner(ex); }
        }

        // ── ④ 类型有没有这个属性（此前靠发一条看回不回 (2,201)）────────────────
        private static string AttrValid(string typeName, string attrName)
        {
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(attrName))
                return "attrvalid: 要给 name=<类型码> 和 arg1=<属性名>。";
            try
            {
                var t = DbElementType.GetElementType(typeName.Trim());
                if (t == null || !t.IsValid)
                    return "attrvalid: 类型「" + typeName + "」解析不出 —— 用 e3d_type_schema 查全名/短名。";
                var a = DbAttribute.GetDbAttribute(attrName.Trim());
                if (a == null) return "attrvalid: 属性名「" + attrName + "」E3D 不认识。";
                bool ok = t.IsAttributeValid(a);
                return "attrvalid: " + t.Name + " " + (ok ? "**有**" : "**没有**") + " 属性 " + attrName
                     + "\n（此前判这个要发一条命令看回不回 (2,201) —— 现在离线可答。）";
            }
            catch (Exception ex) { return "attrvalid: 抛异常：" + Inner(ex); }
        }

        // ── ⑤ 名字合法性预检（此前靠真机回 (41,12)）────────────────────────────
        private static string NameCheck(string candidate, string typeName)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return "namecheck: 要给 arg1=<候选名字>。";
            try
            {
                DbElementType t = null;
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    t = DbElementType.GetElementType(typeName.Trim());
                    if (t != null && !t.IsValid) t = null;
                }
                Aveva.Core.Utilities.Messaging.PdmsMessage msg;
                bool ok;
                if (t != null) ok = DbElement.IsValidNameFormat(candidate.Trim(), t, DbType.Design, out msg);
                else ok = DbElement.IsValidNameFormat(candidate.Trim(), DbType.Design, out msg);
                string why = ok ? null : E3dApiWrapper.MsgText(msg);
                return "namecheck: 「" + candidate + "」" + (ok ? "**格式合法**" : "**格式非法**")
                     + (ok ? "" : "  E3D 原话：" + (string.IsNullOrWhiteSpace(why) ? "(无文本)" : why))
                     + "\n★注意：这只查**格式**（空格之类），**不查重名** ——"
                     + " 重名要真发才知道（E3D 名字全局唯一，撞了回 (41,12)）。";
            }
            catch (Exception ex) { return "namecheck: 抛异常：" + Inner(ex); }
        }

        // ── 共用 ────────────────────────────────────────────────────────────────
        private static bool Resolve(string path, out DbElement el, out string why)
        {
            el = DbElement.GetElement();
            why = null;
            var p = (path ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p)) { why = "缺元素路径。"; return false; }
            try
            {
                el = DbElement.GetElement(p);
                if (!el.IsValid) { why = "「" + p + "」不是有效元素（名字不存在？）。"; return false; }
                return true;
            }
            catch (Exception ex) { why = "解析「" + p + "」抛异常：" + Inner(ex); return false; }
        }

        private static string Inner(Exception ex)
        {
            var e = ex;
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }
    }
}
