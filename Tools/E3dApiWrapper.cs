using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aveva.Core.Database;
using Aveva.Core.Database.Filters;
using Aveva.Core.Geometry;

namespace E3DMcpServer.Tools
{
    public class E3dElementInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
    }

    public class E3dApiWrapper
    {
        // ── 查询 ──────────────────────────────────────────

        public E3dElementInfo GetCurrentElement()
        {
            try
            {
                DbElement ce = CurrentElement.Element;
                if (ce != null && ce.IsValid)
                    return BuildInfoFromDb(ce);
                return null;
            }
            catch { return null; }
        }

        public E3dElementInfo GetElement(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try
            {
                DbElement dbElem = DbElement.GetElement(name);
                if (dbElem != null && dbElem.IsValid)
                    return BuildInfoFromDb(dbElem);
                return null;
            }
            catch { return null; }
        }

        public List<E3dElementInfo> GetChildren(string name, string typeFilter)
        {
            var list = new List<E3dElementInfo>();
            try
            {
                DbElement dbParent;
                if (string.IsNullOrWhiteSpace(name))
                    dbParent = CurrentElement.Element;
                else
                    dbParent = DbElement.GetElement(name);
                if (dbParent == null || !dbParent.IsValid) return list;

                DbElement[] members = dbParent.Members();
                if (members == null) return list;

                foreach (var m in members)
                {
                    if (m == null || !m.IsValid) continue;
                    string ct = null;
                    try { ct = m.GetElementType().ToString(); } catch { }
                    if (!string.IsNullOrEmpty(typeFilter) &&
                        !string.Equals(ct, typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    list.Add(BuildInfoFromDb(m));
                }
            }
            catch { }
            return list;
        }

        public E3dElementInfo GetOwner(string name)
        {
            try
            {
                DbElement dbElem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (dbElem == null || !dbElem.IsValid) return null;

                DbElement owner = dbElem.Owner;
                if (owner != null && owner.IsValid)
                    return BuildInfoFromDb(owner);
                return null;
            }
            catch { return null; }
        }

        public Dictionary<string, object> ReadAttributes(string name, string[] attrNames)
        {
            var result = new Dictionary<string, object>();
            try
            {
                DbElement dbElem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (dbElem == null || !dbElem.IsValid) return result;

                if (attrNames == null || attrNames.Length == 0)
                    attrNames = new[] { "NAME", "TYPE", "DESC", "PURP", "POS", "OWNER" };

                foreach (var a in attrNames)
                {
                    try
                    {
                        var dbAttr = DbAttribute.GetDbAttribute(a);
                        if (dbAttr == null) continue;
                        string val = dbElem.GetAsString(dbAttr);
                        if (val != null) result[a] = val;
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        // Search using DBElementCollection + TypeFilter (E3DAddIn_9 pattern)
        public List<E3dElementInfo> Search(string type, string filter, int max)
        {
            var list = new List<E3dElementInfo>();
            if (string.IsNullOrWhiteSpace(type)) return list;
            if (max <= 0) max = 50;

            try
            {
                DbElementType dbType = ResolveType(type);
                if (dbType == null) return list;

                TypeFilter typeFilter = new TypeFilter(dbType);
                DBElementCollection collection;

                if (!string.IsNullOrEmpty(filter))
                {
                    // Build AndFilter with type + expression filter
                    // For now: just use type filter (expression filter via PML is complex)
                    collection = new DBElementCollection(DbElement.GetElement("/*"), typeFilter);
                }
                else
                {
                    collection = new DBElementCollection(DbElement.GetElement("/*"), typeFilter);
                }

                int count = 0;
                foreach (DbElement element in collection)
                {
                    if (element == null || !element.IsValid) continue;
                    if (count >= max) break;

                    // Apply expression filter manually if provided
                    if (!string.IsNullOrEmpty(filter))
                    {
                        if (!MatchesFilter(element, filter)) continue;
                    }

                    list.Add(BuildInfoFromDb(element));
                    count++;
                }
            }
            catch { }
            return list;
        }

        // 2026-06-15: 收集"有真实世界坐标的几何组件", 供实时 3D 镜像 E3D 真实显示。
        // 背景: 旧 e3d_search type=PIPE 返回的是 PIPE 头(容器节点), 其 POS 退化(≈0,0,0)→
        // 前端把几十根管头全画在一点 = "一根圆柱"。真正被 E3D 画出来、铺在空间里的是
        // BRAN 下的 TUBE/ELBO/法兰/阀门等组件——它们才有真实世界坐标。这里从 root 递归
        // 下钻, 凡有**有效非退化世界坐标**的元素就收集; 纯容器(SITE/ZONE/PIPE/BRAN, 坐标
        // 退化)只下钻不收集。GetPosition(POS) 与本类既有 Measure/BuildInfoFromDb 一致(世界系)。
        public List<E3dElementInfo> CollectGeometry(string root, int max)
        {
            var list = new List<E3dElementInfo>();
            if (max <= 0) max = 2000;
            try
            {
                DbElement start = (string.IsNullOrWhiteSpace(root) || root == "/*")
                    ? DbElement.GetElement("/*")
                    : DbElement.GetElement(root);
                if (start == null || !start.IsValid) start = DbElement.GetElement("/*");
                CollectGeometryRecurse(start, list, max, 0);
            }
            catch { }
            return list;
        }

        // 2026-06-21「从 E3D 拉真几何」：用 AVEVA 原生 EXPORT 把指定元素(或 CE)导成 RVM 网格到本机
        // temp，前端经 Electron fileOps 直接读、喂现成 loadRvm（与「模型审查」同款加载器，真 CAD 形状）。
        // 比 e3d_collect_geometry 重（几 MB / 几秒），按需调用。命令序列取 AVEVA RVM 导出最小式。
        // ⚠ EXPORT 在不同 E3D 配置/模块下行为可能不同（是否需先导航 CE、文件名带空格等），最终以真机
        // 为准——看 %TEMP%/pipingclaw_e3d_pml.log 里实际发出的命令 + 成败。同机假设：插件与本应用同台。
        public string ExportRvm(string root)
        {
            try
            {
                string exportArg = (string.IsNullOrWhiteSpace(root) || root == "/*") ? "CE" : root;
                string outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pipingclaw_live_e3d.rvm");
                try { if (System.IO.File.Exists(outPath)) System.IO.File.Delete(outPath); } catch { }

                // 最小序列：声明输出文件 OVER → 自动配色 → 导出元素 → FINISH。
                // P2-C 巡检修：改 TryRunPml 真判成败，首条失败的 E3D 真实错误经 _lastExportError 带内浮出
                //（原来 RunPml 丢成败，失败只能靠"文件不存在"反推，Agent 只见"查日志"话术）。
                _lastExportError = null;
                string expErr;
                if (!TryRunPml("EXPORT FILE /" + outPath + " OVER", out expErr)) { _lastExportError = expErr; return null; }
                if (!TryRunPml("EXPORT AUTOCOLOUR DISPLAYEXPORT ON", out expErr)) { _lastExportError = expErr; return null; }
                if (!TryRunPml("EXPORT " + exportArg, out expErr)) { _lastExportError = expErr; return null; }
                if (!TryRunPml("EXPORT FINISH", out expErr)) { _lastExportError = expErr; return null; }

                // 回读确认文件真生成（非空）。
                if (System.IO.File.Exists(outPath))
                {
                    var fi = new System.IO.FileInfo(outPath);
                    if (fi.Length > 0) return outPath;
                }
                _lastExportError = "EXPORT 序列执行完但输出文件不存在/为空（可能无可导出元素）";
                return null;
            }
            catch (Exception ex) { _lastExportError = ex.Message; return null; }
        }

        /// <summary>ExportRvm 最近一次失败的带内原因（P2-C：替代"查日志"话术）。</summary>
        public string LastExportError => _lastExportError;
        private string _lastExportError;

        private void CollectGeometryRecurse(DbElement elem, List<E3dElementInfo> list, int max, int depth)
        {
            if (elem == null || !elem.IsValid) return;
            if (list.Count >= max) return;
            if (depth > 40) return; // 防御异常深树

            // 本元素: 有有效、非退化世界坐标 → 当作几何组件收集(容器坐标退化会被跳过)。
            try
            {
                var pos = elem.GetPosition(DbAttributeInstance.POS);
                if (pos != null)
                {
                    double x = Math.Round(pos.X, 3), y = Math.Round(pos.Y, 3), z = Math.Round(pos.Z, 3);
                    bool degenerate = Math.Abs(x) < 0.001 && Math.Abs(y) < 0.001 && Math.Abs(z) < 0.001;
                    if (!degenerate)
                    {
                        var info = new E3dElementInfo();
                        try { info.Name = elem.GetAsString(DbAttributeInstance.NAME); } catch { }
                        try { info.Type = elem.GetElementType().ToString(); } catch { }
                        info.X = x; info.Y = y; info.Z = z;
                        // 2026-06-21 真几何：读真管径/壁厚/长度/朝向/头尾，供实时 3D 按真实尺寸+真实走向
                        // 渲染（取代前端硬编码圆柱）。用已验证的 GetAsString(GetDbAttribute(name)) 模式
                        // （见 ListAttributes 行 1031）——逐个 try，该类型没有的属性自动跳过；只用安全的
                        // 字符串读，不碰可能在本 AVEVA 版本不存在的 typed API（避免编译/运行风险）。
                        foreach (var an in new[] { "ODIA", "WALL", "LENGTH", "HEIGHT", "BORE", "SBOR", "RADIUS", "ANGLE", "ORI", "DIR", "HPOS", "TPOS" })
                        {
                            try
                            {
                                var dbA = DbAttribute.GetDbAttribute(an);
                                if (dbA == null) continue;
                                string v = elem.GetAsString(dbA);
                                if (!string.IsNullOrWhiteSpace(v)) info.Attributes[an] = v;
                            }
                            catch { }
                        }
                        if (!string.IsNullOrEmpty(info.Name)) list.Add(info);
                    }
                }
            }
            catch { }

            // 下钻子件(容器自身坐标退化未收集, 但其几何组件在下面)。
            try
            {
                DbElement[] members = elem.Members();
                if (members != null)
                {
                    foreach (var m in members)
                    {
                        if (list.Count >= max) break;
                        CollectGeometryRecurse(m, list, max, depth + 1);
                    }
                }
            }
            catch { }
        }

        private bool MatchesFilter(DbElement elem, string filter)
        {
            // Simple filter expressions: "ODIA GT 150", "NAME EQ PIPE-101"
            try
            {
                string[] parts = filter.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return true;

                string attrName = parts[0].ToUpper();
                string op = parts[1].ToUpper();
                string val = parts[2].Trim('\'', '"');

                var dbAttr = DbAttribute.GetDbAttribute(attrName);
                if (dbAttr == null) return true;

                string attrVal = elem.GetAsString(dbAttr);
                if (attrVal == null) return false;

                if (double.TryParse(attrVal, out double dAttr) && double.TryParse(val, out double dVal))
                {
                    switch (op)
                    {
                        case "GT": return dAttr > dVal;
                        case "LT": return dAttr < dVal;
                        case "GE": return dAttr >= dVal;
                        case "LE": return dAttr <= dVal;
                        case "EQ": return Math.Abs(dAttr - dVal) < 0.001;
                        case "NE": return Math.Abs(dAttr - dVal) >= 0.001;
                    }
                }
                else
                {
                    if (op == "EQ") return string.Equals(attrVal, val, StringComparison.OrdinalIgnoreCase);
                    if (op == "NE") return !string.Equals(attrVal, val, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return true;
        }

        public Dictionary<string, string> GetProjectInfo()
        {
            var info = new Dictionary<string, string>();
            try
            {
                info["project"] = GetPmlGlobal("!!PROJECT");
                info["mdb"] = GetPmlGlobal("!!MDB");
                info["user"] = GetPmlGlobal("!!USER");
                info["module"] = GetPmlGlobal("!!MODULE");
            }
            catch { }
            return info;
        }

        public string Measure(string name1, string name2)
        {
            try
            {
                DbElement e1 = string.IsNullOrWhiteSpace(name1) ? CurrentElement.Element : DbElement.GetElement(name1);
                DbElement e2 = string.IsNullOrWhiteSpace(name2) ? CurrentElement.Element : DbElement.GetElement(name2);
                if (e1 == null || e2 == null) return "Error: element not found";

                var p1 = e1.GetPosition(DbAttributeInstance.POS);
                var p2 = e2.GetPosition(DbAttributeInstance.POS);
                if (p1 == null || p2 == null) return "Error: position not available";

                double dx = p2.X - p1.X, dy = p2.Y - p1.Y, dz = p2.Z - p1.Z;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                return $"Distance: {dist:F3} mm  (dX={dx:F3} dY={dy:F3} dZ={dz:F3})";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string GetElementType(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "No element";
                return $"Type: {elem.GetElementType()}";
            }
            catch { return "Error"; }
        }

        public string GetElementPath(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "No element";
                return elem.FullName();
            }
            catch { return "Error"; }
        }

        // ── 修改 ──────────────────────────────────────────

        public string SetAttribute(string name, string attr, string value)
        {
            try
            {
                DbElement dbElem = DbElement.GetElement(name);
                if (dbElem == null || !dbElem.IsValid) return $"Error: element '{name}' not found";

                // 管等级是 PDMS 真实命令 Pspec/Ispec/Tspec（依据培训资料），**不是** SET SPREF。
                // 设管等级时 CE 导航到管/分支再发 Pspec 等。SPREF/PSPEC 都路由到 Pspec。
                string a = (attr ?? "").Trim().ToUpperInvariant();
                string specCmd = a == "SPREF" || a == "PSPEC" ? "Pspec"
                               : a == "ISPEC" ? "Ispec"
                               : a == "TSPEC" ? "Tspec" : null;
                if (specCmd != null)
                {
                    CurrentElement.Element = dbElem;             // CE→该元素
                    string spec = value.StartsWith("/") ? value : "/" + value;
                    string cmd = $"{specCmd} {spec}";
                    string specErr;
                    if (!TryRunPml(cmd, out specErr))
                        return $"Error: '{cmd}' 在 E3D 端被拒: {specErr}。检查管等级名是否存在。";
                    return $"OK: {cmd} on {name}";
                }

                // 位置/朝向(POS/HPOS/TPOS/ORI)是**类型化坐标属性**(Aveva.Core.Geometry.Position/Orientation)。
                // .NET 字符串 SetAttribute 只有 string 重载、Database 层无 string→Position 解析器（一手确证见
                // docs/E3D-COMMAND-REFERENCE-GROUNDED-2026-06-30.md §8.1），所以字符串 "E.. N.. U.." 写不进 → 之前静默失败。
                // 修：改走 PML —— CE 导航 + `{ATTR} {value}`，让 PML 自己把 ENU 解析成 Position（绕开字符串 SetAttribute）。
                // 这样 09 现有的 e3d_attr_set(HPOS/TPOS, "E.. N.. U.. WRT /*") 即可落地。⚠ 逐字形态按真 E3D 日志迭代。
                if (a == "POS" || a == "HPOS" || a == "TPOS" || a == "ORI")
                {
                    // N1（指导书 2026-07-02）：入库前用 Geometry 类型自带解析器校验坐标/朝向串
                    //（Position.Parse / Orientation.Parse，本机 DLL 已验证存在）——把"非法坐标串
                    // 静默失败"变成带内报错。⚠ Parse 的方言未必认 WRT 子句 → 含 WRT 的串跳过
                    // 预校验直接走 PML（主通道不变，PML 自己解析 ENU+WRT）。
                    string vTrim = (value ?? "").Trim();
                    bool hasWrt = vTrim.ToUpperInvariant().Contains("WRT");
                    if (!hasWrt && vTrim.Length > 0)
                    {
                        Aveva.Core.Utilities.Messaging.PdmsMessage gerr;
                        bool parseOk;
                        if (a == "ORI")
                        {
                            Orientation ori;
                            parseOk = Orientation.Parse(vTrim, out ori, out gerr);
                        }
                        else
                        {
                            Position pos;
                            parseOk = Position.Parse(vTrim, out pos, out gerr);
                        }
                        if (!parseOk)
                            return $"Error: {a} 坐标/朝向串非法（未发往 E3D）: '{vTrim}' — {gerr?.ToString() ?? "解析失败"}。格式如 'E 1000 N 2000 U 500'。";
                    }

                    CurrentElement.Element = dbElem;             // CE→该元素
                    string cmd = $"{a} {value}".Trim();
                    string posErr;
                    if (!TryRunPml(cmd, out posErr))
                        return $"Error: '{cmd}' 在 E3D 端被拒: {posErr}。检查坐标/朝向格式(ENU + 可选 WRT /*)。";
                    return $"OK: {cmd} on {name}";
                }

                var dbAttr = DbAttribute.GetDbAttribute(attr);
                if (dbAttr == null) return $"Error: attribute '{attr}' not found";
                dbElem.SetAttribute(dbAttr, value);
                return $"OK: set {attr}={value} on {name}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 建元素 = PDMS **真实建模范式**（依据用户培训资料 PDMS常用命令.pdf：`New Pipe /name`、
        // CE 在 pipe 上时 `New Branch /name` 自动建其下并继承等级）。做法：CE 导航到 owner，
        // 再发真实 PML `New {type} {name}`（在 CE 下建、并把 CE 推进到新元素）。
        // 取代此前两条错路：NEW…OWNER 自创语法 / MOVE…TO / !!CE= 不挪 CE；以及 .NET CreateLast
        // （对结构件可行，但与"管等级/几何/CE 流转"范式不一致，且我们从未在真 E3D 上证实其落地）。
        // ⚠ 命令逐字形态以真 E3D 接受度为准，按 %TEMP%\pipingclaw_e3d_pml.log 迭代。
        public string CreateElement(string type, string name, string owner)
        {
            try
            {
                if (!string.IsNullOrEmpty(owner))
                {
                    DbElement ownerEl = DbElement.GetElement(owner);
                    if (ownerEl == null || !ownerEl.IsValid)
                        return $"Error: owner '{owner}' 不存在或无效，无法在其下创建 {type} {name}";
                    CurrentElement.Element = ownerEl;   // CE→owner，New 在其下建
                }
                string nm = string.IsNullOrWhiteSpace(name) ? "" : (name.StartsWith("/") ? name : "/" + name);
                string cmd = $"New {type} {nm}".TrimEnd();
                string newErr;
                if (!TryRunPml(cmd, out newErr))
                    return $"Error: '{cmd}' 在 E3D 端被拒: {newErr}。检查 owner 层级是否合法、type 拼写。";
                // New 后 CE 应为新元素，回读确认
                string actual = "";
                try { var ce = CurrentElement.Element; if (ce != null && ce.IsValid) actual = ce.GetAsString(DbAttributeInstance.NAME); } catch { }
                return $"OK: {cmd}" + (string.IsNullOrWhiteSpace(actual) ? "" : $" → {actual}")
                     + (string.IsNullOrEmpty(owner) ? "" : $" (under {owner})");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 管道操作 ──────────────────────────────────────

        // 管件插入 = PDMS **真实建模范式**：CE 导航到目标(分支/选中的组件)，再从**管等级选型**建件（这样才带几何）。
        // 2026-07-03 官方校正（关键，headless 修）：官方 DRMPGC24.09.12/13 —— **CHOOSE 只在 DEV GRAPHICS
        // 模式**、弹图形选型器（无人值守会**卡住等 GUI 交互**）；**SELECT WITH** 是**文本命令、非交互**的孪生形
        // （`New Elbo SEL WITH STYP LR` / `New Tee SEL WI PBOR 3 150`）。MCP 里插件命令是无人值守执行，必须走
        // SELECT。取代旧 "Choose With Stype"（会挂）。给了 STYPE 用 `SELECT WITH STYPE X`；未给取等级缺省项
        // `CHOOSE DEFAULT`（官方文档为非交互"选规格默认项"，非弹窗；⚠ 若真机拒→改 SELECT WITH <criteria>）。
        // 绝不用 .NET CreateLast/CreateAfter 建管件（无几何空壳、看不到——没从等级选型）。⚠ 精确定位以真机为准。
        private string InsertComponent(string compTypeCode, string target, double at, string stype)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(target))
                {
                    DbElement el = DbElement.GetElement(target);
                    if (el == null || !el.IsValid) return $"Error: '{target}' 不存在或无效";
                    CurrentElement.Element = el;   // CE 导航到目标（在其后/分支末尾建件）
                }
                // 非交互选型：给了 STYPE 走官方文本形 "SELECT WITH STYPE X"；否则等级缺省项 "CHOOSE DEFAULT"。
                string select = string.IsNullOrEmpty(stype) ? "CHOOSE DEFAULT" : $"SELECT WITH STYPE {stype}";
                string cmd = $"New {compTypeCode} {select}";
                string chooseErr;
                if (!TryRunPml(cmd, out chooseErr))
                    return $"Error: '{cmd}' 在 E3D 端被拒: {chooseErr}。可能该管件不在当前分支管等级里，或选型条件无匹配。";
                return $"OK: {cmd}（CE={(string.IsNullOrWhiteSpace(target) ? "当前" : target)}）。⚠ 选型/定位以真 E3D 为准；定位用 Dist/Conn/POS 再微调。";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 2026-07-03 官方命令核实：CUT/GAP/JOIN 作为命令行动词【官方裁决不存在】(PDUV Pipe Splitting
        // 仅 GUI；DRMPGC 无逐字命令)。原来发 RunWrite("CUT/GAP/JOIN ...") = 发伪命令给 E3D 拿晦涩拒收。
        // 改为诚实拦回 + 正解提示，不再冒充"OK"。切管走"插入组件 + 新建 BRAN"，间隙用 CLEARANCE。
        public string CutPipe(string name, double at)
            => E3dCommandReference.BlacklistError("CUT");

        public string GapPipe(string name, double at, double gap = 10)
            => E3dCommandReference.BlacklistError("GAP");

        public string JoinPipe(string name1, string name2)
            => E3dCommandReference.BlacklistError("JOIN");

        public string BendPipe(string name, double at, string stype = null)
        {
            return InsertComponent("ELBO", name, at, stype);
        }

        public string TeePipe(string name, double at, string stype = null)
        {
            return InsertComponent("TEE", name, at, stype);
        }

        public string ValvePipe(string name, double at, string stype = null)
        {
            return InsertComponent("VALV", name, at, stype);
        }

        public string FlangePipe(string name, double at, string stype = null)
        {
            return InsertComponent("FLAN", name, at, stype);
        }

        public string ReducerPipe(string name, double at)
        {
            return InsertComponent("REDU", name, at, null);
        }

        public string RoutePipe(string pipe)
            => RunWrite($"ROUTE {pipe}", $"OK: auto-routed {pipe}");

        // ── 元素补充 ──────────────────────────────────────

        // P3-6 注释更正（2026-07-02）：这三个走统一的 CreateElement —— 其现实现是
        // 【CE 导航到 owner + 真实 PML `New {type} {name}`】，不是 .NET CreateLast
        //（.NET 建件是无几何空壳，已废弃；见 CreateElement 顶部注释）。
        public string CreateSupport(string name, string owner = null, string stype = null)
        {
            var r = CreateElement("SUPP", name, owner);
            // P2-C 巡检修（stype 被静默丢弃）：建成后真写 STYPE 并把结果并进回执——
            // Agent 以为建了 ANCHOR 型支架实际是裸 SUPP 的认知污染到此为止。
            if (r.StartsWith("OK") && !string.IsNullOrWhiteSpace(stype) && !string.IsNullOrWhiteSpace(name))
            {
                string sr;
                try { sr = SetAttribute(name.StartsWith("/") ? name : "/" + name, "STYPE", stype); }
                catch (Exception ex) { sr = $"Error: {ex.Message}"; }
                if (!sr.StartsWith("OK")) r += $" ⚠ STYPE={stype} 未写入: {sr}";
                else r += $"（STYPE={stype}）";
            }
            return r;
        }

        public string CreateWeld(string name, string owner = null)
        {
            return CreateElement("WELD", name, owner);
        }

        public string CreateLabel(string name, string owner, string text)
        {
            var r = CreateElement("LABE", name, owner);
            if (r.StartsWith("OK") && !string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(name))
            {
                // P2-C 巡检修：text（标签存在的意义）写失败原来被静默吞——现并进回执。
                string tr;
                try { tr = SetAttribute(name.StartsWith("/") ? name : "/" + name, "DESC", text); }
                catch (Exception ex) { tr = $"Error: {ex.Message}"; }
                if (!tr.StartsWith("OK")) r += $" ⚠ 标签文本未写入: {tr}";
            }
            return r;
        }

        // ── 属性补充 ──────────────────────────────────────

        // 2026-07-03 CE-导航重写（通病修）：清属性作用于 CE。先导航到 name、再对当前元素发 CLEARA。
        // ⚠ CLEARA 裸动词接受度以真机为准（harness 会验）；旧 "{name} CLEARA" 前导名形态真 E3D 拒收。
        public string ClearAttribute(string name, string attr)
            => NavigateThenWrite(name, $"CLEARA {attr}", $"OK: cleared {attr} on {name} (CE)");

        // 2026-07-03 CE-导航重写：官方 COPY ATTributes OF <element_id>（把 source 的属性拷进当前 CE），
        // 取代臆造的 "COPYA source target attrs"。做法=导航 CE 到 target、再 COPY ATTRIBUTES OF source。
        // ⚠ 官方形态拷【全部】属性，不支持 attrs 子集——attrs 参数在此形态下被忽略（如需子集逐条 SET）。
        public string CopyAttributes(string source, string target, string attrs)
            => NavigateThenWrite(target, $"COPY ATTRIBUTES OF {source}",
                   $"OK: copied attributes from {source} to {target} (CE)" + (string.IsNullOrWhiteSpace(attrs) ? "" : "（⚠ 官方形态拷全部属性，attrs 子集被忽略）"));

        // ── 可见性 ────────────────────────────────────────

        public string ShowElement(string name)
        {
            try { RunPml($"SHOW {name}"); return $"OK: showing {name}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string HideElement(string name)
        {
            try { RunPml($"HIDE {name}"); return $"OK: hiding {name}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ViewIso()
        {
            try { RunPml("VIEW ISO"); return "OK: switched to isometric view"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ViewPlan()
        {
            try { RunPml("VIEW PLAN"); return "OK: switched to plan view"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ViewElevation()
        {
            try { RunPml("VIEW ELEVATION"); return "OK: switched to elevation view"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 数据库补充 ────────────────────────────────────

        // P2-C 巡检修（假成功）：CLAIM/RELEASE 是多人协作最需要真结果的锁操作——被拒（他人锁定）
        // 也恒回 OK 会让 Agent 在没锁的元素上继续写。改 RunWrite 真判成败带内报错。
        public string ClaimElement(string name)
        {
            try { return RunWrite($"CLAIM {name}", $"OK: claimed {name}"); }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 2026-07-03 官方校正：user 级释放 claim 用 **UNCLAIM**（DBRM6.07.2）——顶层无独立 RELEASE
        //（RELEASE 只作为 `EXTRACT RELEASE` 出现）。旧 "RELEASE {name}" 非法。UNCLAIM <name>（非 ALL，
        // 避免放掉他人 claim；ALL 已在 ForbiddenPml 黑名单）。
        public string ReleaseElement(string name)
        {
            try { return RunWrite($"UNCLAIM {name}", $"OK: unclaimed {name}"); }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 出图 ──────────────────────────────────────────

        public string DraftIso(string name, string output)
        {
            // P2-C 巡检修（假成功）：原来不判命令成败也不查文件——Agent 会告诉用户"图已出"而文件不存在。
            // ⚠ 'DRAFT ISO' 裸命令形态未经真机验证（ISODRAFT 是独立模块），失败会带回 E3D 真实错误。
            try
            {
                string isoErr;
                if (!TryRunPml($"DRAFT ISO {name} OUTPUT {output}", out isoErr))
                    return $"Error: ISO 出图命令被拒: {isoErr}";
                bool exists = false;
                try { exists = !string.IsNullOrWhiteSpace(output) && System.IO.File.Exists(output); } catch { }
                return exists
                    ? $"OK: ISO draft of {name} -> {output}"
                    : $"Error: 命令已执行但输出文件不存在（{output}）——出图未落地，勿告知用户已出图。";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 定位补充 ──────────────────────────────────────

        // 2026-07-03 官方校正（对抗核对补漏）：AT 作用于 CE、无前导名（POSition <marke> AT <bpos>，DRMPGC23.08.08）。
        // 导航 CE 到 name、再发裸 `AT E.. N.. U..`（<bpos> 必带 E/N/U 轴前缀）。取代前导名形 "{name} AT …"。
        public string PositionAt(string name, double x, double y, double z)
            => NavigateThenWrite(name, $"AT E{x} N{y} U{z}", $"OK: positioned {name} at E{x} N{y} U{z} (CE)");

        // ⚠ 真机未验证（PML命令语法核查报告 2026-07 §6）：`{name} DIST {值}` 作为【定位命令】无任何
        // 官方/社区出处，高度疑与量距查询 DIST 混淆。E3D 元件精确定位的确证形态是 THRO PH/PT/PREV
        //（穿分支头/尾/上一件点，官方 DRMPGC + thepiping E3D 12.1）或 CONN 族。真机 `$Q` 探语法裁决前
        // 此命令可能被 E3D 拒收——已在工具描述标 ⚠，失败会带回真实错误（走 RunWrite）。
        // 2026-07-03 CE-导航重写：官方 [POSition <marke>] DISTance <uval>（marker 可选）。导航 CE 到
        // name、再发裸 DISTANCE——取代前导名形 "{name} DIST"（真 E3D 拒收/疑与量距查询混淆）。
        public string PositionDist(string name, double dist, string from = null)
        {
            string cmd = $"DISTANCE {dist}";
            if (!string.IsNullOrEmpty(from)) cmd += $" FROM {from}";
            return NavigateThenWrite(name, cmd, $"OK: set {name} distance {dist}mm (CE)" + (!string.IsNullOrEmpty(from) ? $" from {from}" : ""));
        }

        // 2026-07-03 官方校正（对抗核对补漏）：DIRECTION 作用于 CE、无前导名（DIRection [AND <marke> IS] <bdir>，
        // DRMPCM24.09.37）。导航 CE 再发裸 `DIR <bdir>`（bdir 如 E/N/U/U45E）。取代前导名形 "{name} DIR …"。
        public string PositionDir(string name, string direction)
            => NavigateThenWrite(name, $"DIRECTION {direction}", $"OK: set {name} direction to {direction} (CE)");

        // 2026-07-03 官方校正（对抗核对补漏）：ORIENTATE 作用于 CE、无前导名，且参数须是**成对方向子句**
        // `<bdir> IS <bdir> [AND <bdir> IS <bdir>]`（DRMPCM23.08.12/DRMPGC24.09.36，例 "Y IS N AND Z IS U"）。
        // 导航 CE 再发裸 `ORI <spec>`。⚠ 调用方须传方向子句（不是单个 p-point）——单个 p-point 无官方语法。
        public string PositionOri(string name, string ppoint)
            => NavigateThenWrite(name, $"ORI {ppoint}", $"OK: oriented {name} → {ppoint} (CE；⚠ 参数须为 '<bdir> IS <bdir>' 子句)");

        // THRO/THR 族有出处（核查报告 §6：THRO PH/PT/PREV 穿分支头/尾/上一件点）——但 target 应是
        // p-point 关键词(PH/PT/PREV)而非任意元素名；THR 缩写与逐字子句接受度以真机为准。
        // 2026-07-03 CE-导航重写：官方 POSition <marke> THRough <bpos>。导航 CE 到 name、再对其头点(PH)
        // 发 POSITION PH THROUGH <target>。⚠ marker 默认取 PH（头点）；实为尾点则应 PT——以真机为准(harness 验)。
        public string PositionThr(string name, string target)
            => NavigateThenWrite(name, $"POSITION PH THROUGH {target}", $"OK: positioned {name} PH through {target} (CE)");

        // ── 元素操作补充 ──────────────────────────────────

        // 2026-07-03 CE-导航重写：官方 FLIP 作用于 CE（互换 ARRIVE/LEAVE 或 Section 起止），无前导名。
        public string FlipElement(string name)
            => NavigateThenWrite(name, "FLIP", $"OK: flipped {name} (CE)");

        // 2026-07-03 CE-导航重写：官方 FCONnect [<marke>] [TO <marke>]。导航 CE 到 name、再 FCONNECT TO target。
        public string ForceConnect(string name, string target)
            => NavigateThenWrite(name, $"FCONNECT TO {target}", $"OK: force-connected {name} to {target} (CE)");

        // 2026-07-03 官方校正：旧 "{name} DELETE" 前导名形非法（官方 DELETE <element_type> 作用于 CE，
        // DBRM4.05.4）。改走**typed .NET `DbElement.Delete(ref msg)`**（本机 API 已确认）——按元素对象直接删、
        // 无需匹配类型关键字、带内错误回执；删除是纯 CRUD、无"几何空壳"问题（与 CreateLast 建管件不同）。
        // ⚠ 删元素前该 DB 须已 claim（未 claim 时 msg 会带回真实错误）。
        public string DeleteElement(string name)
        {
            try
            {
                DbElement el = DbElement.GetElement(name);
                if (el == null || !el.IsValid) return $"Error: 元素 '{name}' 不存在或无效，无法删除";
                string typeName = "";
                try { typeName = el.GetActualType().Name; } catch { }
                Aveva.Core.Utilities.Messaging.PdmsMessage msg;
                bool ok = el.Delete(out msg);
                LogPml($"[.NET] Delete {name}", ok);
                if (!ok)
                    return $"Error: 删除 {name} 失败: {(msg != null ? msg.ToString() : "E3D 未给出错误")}（删元素前须先 claim 其所在 DB）";
                return $"OK: deleted {name}" + (string.IsNullOrEmpty(typeName) ? "" : $" ({typeName})");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 2026-07-03 官方校正（对抗核对补漏）：无独立 RENAME 命令；改名=对 CE 设 NAME 属性（DBRM4.05.6：
        // RENAME 仅作 COPY 尾缀）。导航 CE 再发裸 `NAME {newName}`。取代前导名形 "{name} NAME …"。
        public string RenameElement(string name, string newName)
            => NavigateThenWrite(name, $"NAME {newName}", $"OK: renamed {name} to {newName} (CE)");

        // 2026-07-03 官方参考重写：COPY <element_id> = 把 source 结构拷进【当前 CE】（不是 "COPY x TO y"）。
        // 指定新名用 RENAME 子句（官方例 COPY /FRACT1/PIPE RENAME /FRACT1 /FRACT2）。⚠ 拷贝落点=调用前
        // CE 所在位置——调用方需先把 CE 导航到目标父级。dest 为空则只拷进 CE（保留 source 名）。
        public string CopyElement(string source, string dest)
        {
            string cmd = string.IsNullOrWhiteSpace(dest) ? $"COPY {source}" : $"COPY {source} RENAME {source} {dest}";
            return RunWrite(cmd, $"OK: copied {source}" + (string.IsNullOrWhiteSpace(dest) ? " into current CE" : $" as {dest} (into current CE)"));
        }

        // 2026-07-03 官方校正：层级搬迁的官方命令是 **INCLUDE**（DBRM4.05.5），不是 MOVE
        //（官方 MOVE 只移分支头/尾点）。`INCLUDE <element_id>` 把已存在元素并入 **CE** 的成员表、归属转到 CE
        //（如 /PIPE2 从 /ZONE2 转到 /ZONE1）。做法：CE 导航到目标 owner、再 INCLUDE 被搬元素。
        public string MoveElement(string name, string newOwner)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(newOwner))
                return "Error: MoveElement 需要 name(被搬元素) 与 newOwner(目标归属)。";
            return NavigateThenWrite(newOwner, $"INCLUDE {name}", $"OK: included {name} under {newOwner} (归属已转到 CE={newOwner})");
        }

        // ── PML ────────────────────────────────────────────

        // P1 第 3 层（指导书 2026-07-02）：逃生舱命令黑名单。DELETE/PURGE 类必须走专用工具
        //（带 confirm 确认门）；UNCLAIM ALL 会放掉他人正在编辑的 claim；FINISH/QUIT 会杀 E3D 会话。
        private static readonly string[] ForbiddenPml = { "DELETE ", "PURGE", "UNCLAIM ALL", "FINISH", "QUIT" };

        public string ExecutePml(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "Error: empty command";
            try
            {
                string upGuard = command.Trim().ToUpperInvariant();
                foreach (var f in ForbiddenPml)
                {
                    if (upGuard.StartsWith(f))
                        return "Error: 该命令属于危险操作，逃生舱不放行。请改用对应的专用工具（带 confirm=\"true\"）或由人在 E3D 内执行。";
                }
                // Detect query-style commands and try to capture output
                string upper = command.Trim().ToUpper();
                bool isReadOnly = upper.StartsWith("Q ") || upper.StartsWith("QVAR ")
                    || upper == "LIST" || upper == "LIST ALL" || upper.StartsWith("DUMP")
                    || upper.StartsWith("DISTANCE") || upper.StartsWith("Q ATT");

                if (isReadOnly)
                {
                    // P0-B 第二步（指导书 2026-07-02）：查询命令输出改 .Result 直采 ——
                    // RunInCurrentScope 在当前作用域执行（变量可留存），.Result 即命令行上
                    // 会输出的内容（Q ATT / Q ITLE 直接到手），.Error 是真实报错（带内返回）。
                    // ⚠ 真机验收：对 Q ATT / Q ITLE / Q MEM 各跑一次与命令窗比对 .Result 填充；
                    //   个别命令 .Result 为空时自动回退 EvalViaPmlGlobal 赋值式（真机已验证形态）。
                    var c = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(command);
                    bool okQ = c.RunInCurrentScope();
                    LogPml(command, okQ);
                    if (!okQ)
                        return $"Error: '{command}' 执行失败: {SafeErrorText(c)}";
                    string output = null;
                    try { output = c.Result; } catch { }
                    if (!string.IsNullOrWhiteSpace(output)) return output;
                    // .Result 空 → 回退 !! 全局变量赋值式读回
                    bool fok; string fval; string ferr;
                    EvalViaPmlGlobal(command, true, out fok, out fval, out ferr);
                    if (fok && !string.IsNullOrWhiteSpace(fval) && fval != "0") return fval;
                    return fok ? "(query executed, no output)" : $"Error: 查询无输出且变量读回失败: {ferr}";
                }

                // Modify commands: run directly（P0-C：失败带 E3D 真实错误文本，不再指去查日志）
                string werr;
                bool ok = TryRunPml(command, out werr);
                if (ok)
                {
                    // Try to verify what was changed by reading CE after the command
                    var ceAfter = GetCurrentElement();
                    if (ceAfter != null)
                        return $"OK: '{command}' executed successfully. CE: [{ceAfter.Type}] {ceAfter.Name}";
                    return $"OK: '{command}' executed successfully.";
                }
                // P2-C 巡检修：失败统一 'Error:' 前缀 —— 旧 'FAILED:' 既不被插件 Ok() 提升 IsError，
                // 也不被 09 侧 isE3DFailReceipt 正则命中 → 写失败双层穿透被当成功。
                return $"Error: '{command}' 执行失败: {werr}";
            }
            catch (Exception ex) { return $"PMLError: {ex.Message}"; }
        }

        public string EvaluatePml(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return "Error: empty expression";
            try
            {
                // Route simple !!CE.ATTR expressions through the DbAttribute API (faster, no PML overhead)
                var ceMatch = System.Text.RegularExpressions.Regex.Match(expression,
                    @"!!CE\.(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (ceMatch.Success)
                {
                    string attr = ceMatch.Groups[1].Value.ToUpper();
                    var dict = ReadAttributes(null, new[] { attr });
                    if (dict.ContainsKey(attr)) return dict[attr]?.ToString() ?? "";
                    return $"(attribute '{attr}' not set on CE)";
                }

                // P0-B（指导书 2026-07-02）：统一走 !! 全局变量 + GetStringFromPML 一步读回。
                // 旧 `VAR !x = ...` 局部变量在嵌套作用域可能即销毁 → 间歇性空读毒化 Agent 认知。
                // 表达式先试直赋（!!x = expr），失败再试 VAR 赋值式（Q 系命令形态）——双形态兜底。
                bool ok; string val; string err;
                EvalViaPmlGlobal(expression, false, out ok, out val, out err);
                if (!ok || string.IsNullOrEmpty(val))
                {
                    bool ok2; string val2; string err2;
                    EvalViaPmlGlobal(expression, true, out ok2, out val2, out err2);
                    if (ok2 && !string.IsNullOrEmpty(val2)) return val2;
                    // ★2026-07-29 第八跑修：原来是 `{err ?? err2}` —— err 非空时
                    //   **第二形态的错误被吞掉**，而我们真正想知道的恰恰是 VAR 形态为什么不行
                    //  （第一形态 `!!x = COLLECT …` 报语法错是意料之中的，没有信息量）。
                    //   两个都报出来，别让"兜底那条"的失败原因消失。
                    if (!ok && !ok2)
                        return "Error: 表达式求值失败\n"
                             + "  ① 直赋形态 `!!x = <expr>`: " + (err ?? "(无错误文本)") + "\n"
                             + "  ② VAR 形态 `VAR !!x <expr>`: " + (err2 ?? "(无错误文本)");
                }
                return string.IsNullOrEmpty(val) ? "(no return value)" : val;
            }
            catch (Exception ex) { return $"PMLError: {ex.Message}"; }
        }

        // ── 批量 ────────────────────────────────────────────

        public string BatchRead(string[] elements, string[] attrs)
        {
            var sb = new StringBuilder();
            foreach (var name in elements)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                try
                {
                    var attrs2 = ReadAttributes(name.Trim(), attrs);
                    sb.AppendLine($"--- {name.Trim()} ---");
                    foreach (var kv in attrs2) sb.AppendLine($"  {kv.Key}: {kv.Value}");
                }
                catch (Exception ex) { sb.AppendLine($"--- {name.Trim()} --- ERROR: {ex.Message}"); }
            }
            return sb.Length > 0 ? sb.ToString() : "No results";
        }

        public string BatchSet(string[] elements, string attr, string value)
        {
            var sb = new StringBuilder();
            foreach (var name in elements)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                sb.AppendLine(SetAttribute(name.Trim(), attr, value));
            }
            return sb.ToString();
        }

        public string CollectElements(string type, string filter, string[] attrs)
        {
            var elements = Search(type, filter, 200);
            if (elements.Count == 0) return $"No {type} elements found.";
            var sb = new StringBuilder();
            sb.AppendLine($"Found {elements.Count} {type} elements:");
            foreach (var elem in elements)
            {
                sb.AppendLine($"  [{elem.Type}] {elem.Name}");
                if (attrs != null && attrs.Length > 0)
                {
                    var attrVals = ReadAttributes(elem.Name, attrs);
                    foreach (var kv in attrVals) sb.AppendLine($"    {kv.Key}: {kv.Value}");
                }
            }
            return sb.ToString();
        }

        public string ExportPipeline(string pipeName)
        {
            try
            {
                DbElement pipe = string.IsNullOrWhiteSpace(pipeName)
                    ? CurrentElement.Element : DbElement.GetElement(pipeName);
                if (pipe == null || !pipe.IsValid) return "Error: pipe not found";

                var sb = new StringBuilder();
                AppendDbElement(sb, pipe, 0, new[] { "NAME", "ODIA", "WALL", "MTYP", "PRES", "TEMP", "SBOR", "SCHD", "FUNC", "PURP", "DESC" });
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 导航 ────────────────────────────────────────────

        public string Navigate(string path)
        {
            try
            {
                DbElement target = DbElement.GetElement(path);
                if (target != null && target.IsValid)
                {
                    CurrentElement.Element = target;   // .NET 真导航；不能用 "!!CE = path"（只是赋值全局变量，不移动 CE）
                    return $"Navigated to: [{target.GetElementType()}] {target.GetAsString(DbAttributeInstance.NAME)}";
                }
                return $"Error: '{path}' not found";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string Select(string name)
        {
            try { return Navigate(name); }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 数据库 ──────────────────────────────────────────

        public string DbSave()
        {
            // 2026-06-29 修「SAVEWORK 是假的」根因：原 RunPml 丢弃 RunInPdms() 的成功/失败 bool，
            // E3D 拒了保存（未 claim / 锁冲突）也照样回 "Database saved." → 09 侧抓不到 → 用户以为
            // 已落库实则全丢。改用 TryRunPml（已有，返回 bool），失败时回 Error 串让 09 isE3DFailReceipt
            // 抓住 → callE3DWriteOrThrow 抛错。SAVEWORK 是纯写命令，RunInPdms 返 false = 保存真失败。
            try
            {
                string saveErr;
                if (!TryRunPml("SAVEWORK", out saveErr))
                    return $"Error: SAVEWORK 在 E3D 端被拒（未真正保存）: {saveErr}";
                return "Database saved.";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string DbChanges()
        {
            try
            {
                // Try to capture GETCHANGES output to a variable
                var sb = new StringBuilder();
                try
                {
                    // P0：统一 !! 全局变量读回（原临时 ! 局部变量嵌套作用域可能即销毁）
                    string gcErr;
                    string result = ReadPmlViaGlobal("EVALUATE (GETCHANGES)", out gcErr);

                    if (!string.IsNullOrWhiteSpace(result) && result != "0")
                    {
                        sb.AppendLine("Changes since last SAVEWORK:");
                        sb.AppendLine($"  {result}");
                    }
                    else
                    {
                        sb.AppendLine("No changes since last SAVEWORK, or GETCHANGES output not capturable."
                            + (gcErr != null ? $" ({gcErr})" : ""));
                    }
                }
                catch (Exception gex)
                {
                    sb.AppendLine($"GETCHANGES 执行异常: {gex.Message}");
                }
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string DbUndo()
        {
            // 2026-07-03 官方校正：撤销最近 transaction 的真名是 **UNDODB**（DBRM7.08.1，带 DB 后缀），
            // 不是 UNDO。RunWrite 真判成败（被拒带内报错）。（REDODB=重做、MARKDB 'comment'=开新 undo 边界。）
            try { return RunWrite("UNDODB", "UNDODB executed (撤销最近一个 transaction)。"); }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 2026-07-03 官方校正（对抗核对补漏，诚实拦回）：`EXTRACT ALL {type}` 非官方形。官方 EXTRACT 是
        // **多写库提取/认领**（`EXTRACT CLAIM ALL <type> WHERE (<expr>) [HIERARCHY]` 等，DBRM6.07.4），
        // 不是"列出所有 X"的查询。列元素请用 COLLECT / .NET 检索。
        public string DbExtract(string type)
            => $"Error: 'EXTRACT ALL {type}' 非官方语法。官方 EXTRACT 是多写库提取/认领(EXTRACT CLAIM ALL <type> WHERE …，DBRM6.07.4)。要列出所有 {type} 元素请用 COLLECT ALL {type} 或 .NET 检索，不发此命令。";

        // ── 元素生命周期 ──────────────────────────────────────

        public string ElementExists(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Error: name required";
            try
            {
                DbElement elem = DbElement.GetElement(name);
                return (elem != null && elem.IsValid) ? $"TRUE: {name} exists." : $"FALSE: {name} does not exist.";
            }
            catch { return $"FALSE: {name} does not exist."; }
        }

        public string ElementEquals(string name1, string name2)
        {
            if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
                return "Error: name1 and name2 required";
            try
            {
                DbElement e1 = DbElement.GetElement(name1);
                DbElement e2 = DbElement.GetElement(name2);
                if (e1 == null || e2 == null) return "Error: element not found";
                return e1.Equals(e2) ? "TRUE: same element." : "FALSE: different elements.";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ElementRevert(string name, string attr)
        {
            // P2-C 巡检修（假成功）：改库命令改走 RunWrite 真判成败。
            try
            {
                string cmd = string.IsNullOrWhiteSpace(attr) ? $"{name} REVERT" : $"{name} REVERTATT {attr}";
                return RunWrite(cmd, $"Reverted {name}" + (attr != null ? $" attribute {attr}" : ""));
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ElementDump(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "Error: element not found";

                var sb = new StringBuilder();
                sb.AppendLine($"=== [{elem.GetElementType()}] {elem.GetAsString(DbAttributeInstance.NAME)} ===");

                // Dump what we can via DbElement
                var attrs = new[] {
                    "NAME","TYPE","OWNER","POS","ORI","DIR","DESC","PURP","FUNC",
                    "ODIA","WALL","MTYP","PRES","TEMP","SBOR","SCHD","SPREF",
                    "BORE","RADIUS","ANGLE","LENGTH","HEIGHT","WIDTH",
                    "CONN","ARRIVE","LEAVE","HEAD","TAIL",
                    "CTYPE","STYPE","TTYP","DTSE","SPWL","MTYS"
                };
                foreach (var a in attrs)
                {
                    try
                    {
                        var dbA = DbAttribute.GetDbAttribute(a);
                        if (dbA == null) continue;
                        string val = elem.GetAsString(dbA);
                        if (val != null) sb.AppendLine($"  {a}: {val}");
                    }
                    catch { }
                }
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 层次导航 ──────────────────────────────────────────

        public string Occurrence(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "Error: element not found";
                string elemName = elem.GetAsString(DbAttributeInstance.NAME);
                try
                {
                    string occErr;
                    string val = ReadPmlViaGlobal($"EVALUATE ({name} OCCURRENCE)", out occErr);
                    if (!string.IsNullOrWhiteSpace(val))
                        return $"Occurrence of {elemName}: {val}";
                }
                catch { }
                return $"Occurrence queried for [{elem.GetElementType()}] {elemName}. Use e3d_element_path for full path.";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string Siblings(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "Error: element not found";

                var owner = GetOwner(name);
                if (owner == null) return "Element has no owner (root).";

                var children = GetChildren(owner.Name, null);
                var sb = new StringBuilder();
                sb.AppendLine($"Siblings of [{elem.GetElementType()}] {elem.GetAsString(DbAttributeInstance.NAME)}:");
                foreach (var c in children)
                    sb.AppendLine($"  [{c.Type}] {c.Name}" + (c.X.HasValue ? $" @ ({c.X},{c.Y},{c.Z})" : ""));
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public E3dElementInfo World()
        {
            try { return GetElement("/*"); }
            catch { return null; }
        }

        public string Wrt(string name)
        {
            // P2-C 巡检修（名义获取实无数据）：原来算进 !!TMP_WRT 从不读回，恒回 "WRT queried."。
            // 改统一读回通道，把值真正带回；失败带真实错误。
            try
            {
                string wrtErr;
                string val = ReadPmlViaGlobal($"= WRT {name}", out wrtErr);
                if (!string.IsNullOrWhiteSpace(val)) return $"WRT of {name}: {val}";
                return $"Error: WRT 未取到值{(wrtErr != null ? $": {wrtErr}" : "")}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 属性 ──────────────────────────────────────────────

        public string ListAttributes(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "Error: element not found";

                var sb = new StringBuilder();
                sb.AppendLine($"Attributes of {elem.GetAsString(DbAttributeInstance.NAME)}:");

                var attrs = new[] {
                    "NAME","TYPE","OWNER","POS","ORI","DIR","DESC","PURP","FUNC",
                    "ODIA","WALL","MTYP","PRES","TEMP","SBOR","SCHD","SPREF",
                    "BORE","RADIUS","ANGLE","LENGTH","HEIGHT","WIDTH",
                    "CONN","ARRIVE","LEAVE","HEAD","TAIL",
                    "CTYPE","STYPE","TTYP","DTSE","SPWL","MTYS",
                    "CREATED","MODIFIED","LOCKED","CLAIMED"
                };
                foreach (var a in attrs)
                {
                    try
                    {
                        var dbA = DbAttribute.GetDbAttribute(a);
                        if (dbA == null) continue;
                        string val = elem.GetAsString(dbA);
                        if (val != null) sb.AppendLine($"  {a}: {val}");
                    }
                    catch { }
                }
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string AttrInfo(string attr, string type)
        {
            if (string.IsNullOrWhiteSpace(attr)) return "Error: attr required";
            try
            {
                var dbAttr = DbAttribute.GetDbAttribute(attr);
                if (dbAttr == null) return $"Error: attribute '{attr}' not found in E3D";

                var sb = new StringBuilder();
                sb.AppendLine($"Attribute: {attr}");
                try { sb.AppendLine($"  Type: {dbAttr.GetType().Name}"); } catch { }
                try
                {
                    // Try to read attribute properties
                    var props = dbAttr.GetType().GetProperties(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var p in props)
                    {
                        try
                        {
                            var val = p.GetValue(dbAttr);
                            if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                                sb.AppendLine($"  {p.Name}: {val}");
                        }
                        catch { }
                    }
                }
                catch { }

                // Also query via PML but capture the output instead of showing dialog
                // Use Q ATT in a way that stores result
                try
                {
                    string typeHint = string.IsNullOrWhiteSpace(type) ? "" : $" OF {type}";
                    string attrErr;
                    string qResult = ReadPmlViaGlobal($"= Q ATT {attr}{typeHint}", out attrErr);
                    if (!string.IsNullOrWhiteSpace(qResult))
                        sb.AppendLine($"  Definition: {qResult}");
                }
                catch { }

                return sb.Length > 50 ? sb.ToString() : $"Queried attribute '{attr}'" + (type != null ? $" for type '{type}'" : "") + ". Use e3d_attr_read to see current values.";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 几何 ──────────────────────────────────────────────

        // 2026-07-03 官方校正（对抗核对补漏）：POS 作用于 CE，坐标须 <bpos>=`E.. N.. U..`（带轴前缀，非裸三数）。
        // 导航 CE 再发裸 `POS E.. N.. U..`（与 SetAttribute 的 POS 分支同范式）。取代 "{name} POS {裸数}"。
        public string SetPosition(string name, double x, double y, double z)
            => NavigateThenWrite(name, $"POS E{x} N{y} U{z}", $"Set {name} POS to E{x} N{y} U{z} (CE)");

        // 2026-07-03 官方校正（对抗核对补漏）：BY 作用于 CE，参数是 <pos>（须 E/N/U 轴前缀，非裸三数）
        //（DRMPGC23.08.16，例 "BY E300 N400"）。导航 CE 再发裸 `BY E.. N.. U..`。取代 "{name} BY {裸数}"。
        public string MoveBy(string name, double dx, double dy, double dz)
            => NavigateThenWrite(name, $"BY E{dx} N{dy} U{dz}", $"Moved {name} by E{dx} N{dy} U{dz} (CE)");

        public string GetOrientation(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "Error: element not found";
                var sb = new StringBuilder();
                sb.AppendLine($"Orientation of {elem.GetAsString(DbAttributeInstance.NAME)}:");
                try { sb.AppendLine($"  ORI: {elem.GetAsString(DbAttribute.GetDbAttribute("ORI"))}"); } catch { }
                try { sb.AppendLine($"  DIR: {elem.GetAsString(DbAttribute.GetDbAttribute("DIR"))}"); } catch { }
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 2026-07-03 官方参考重写：官方 ROTate ABOut <bdir> [THRough <bpos>] BY <uval>，作用于 CE、无前导名
        // （例 "ROTATE ABOUT E BY 45"）。导航 CE 到 name、axis 归一到方向词(bdir)、再发裸 ROTATE。
        public string Rotate(string name, string axis, double angle)
        {
            string bdir = ToBdir(axis);
            return NavigateThenWrite(name, $"ROTATE ABOUT {bdir} BY {angle}", $"OK: rotated {name} about {bdir} by {angle}° (CE)");
        }

        // 2026-07-03 官方参考重写：官方无 "REVERSE <name>"；方向反转是裸 FLIP（作用于 CE）。导航 CE 再 FLIP。
        public string Reverse(string name)
            => NavigateThenWrite(name, "FLIP", $"OK: reversed/flipped {name} (CE)");

        // ── 连接 ──────────────────────────────────────────────

        // CONNECT/CONN 族有出处（核查报告 §6 + 官方 DRMPGC DRMPCM24.09.04：CONNECT 会设 HBOR/HCON/HPOS/HDIR）——
        // 确证形态是 CONN PH TO FIR MEM / CONN PT TO LAS MEM（分支头/尾连首/末成员）；`CONNECT A TO B` 直连
        // 两元素的逐字接受度以真机为准。
        // 2026-07-03 官方参考重写：官方 CONnect <marke> TO <marke>（作用于 CE 的 p-point，例 CONN PH TO /Nozz）。
        // 导航 CE 到 name1、再发 CONNECT PH TO name2（头点连到目标）。⚠ 默认取头点 PH——实为尾点则 PT；
        // 且【前提：连接前须先设 Branch 规格 PSPE】。语义以真机为准(harness 验)。
        public string Connect(string name1, string name2)
            => NavigateThenWrite(name1, $"CONNECT PH TO {name2}", $"OK: connected {name1} PH to {name2} (CE; ⚠ 需先设 PSPE，PT 端点以真机为准)");

        // 2026-07-03 官方校正（对抗核对补漏）：DISCONNECT 作用于 CE、无前导名。导航 CE 再发裸 `DISCONNECT`。
        // ⚠ 官方逐字断开是清连接引用属性 `HREF NULREF`/`TREF NULREF`(DRMPGC24.09.05)；DISCONNECT 逐字形以真机为准。
        public string Disconnect(string name)
            => NavigateThenWrite(name, "DISCONNECT", $"OK: disconnected {name} (CE；⚠ 或用 HREF/TREF NULREF)");

        // ── 阵列 ──────────────────────────────────────────────

        // 2026-07-03 官方校正（对抗核对补漏，诚实拦回）：官方 ADD/REMOVE 是**显示列表(Draw List)**命令
        // (ADD <selatt> / REMove <selatt>，DRMPGC15.05)，**不是**「往 DB 元素数组增删成员」。旧 "ADD x TO y"/
        // "REMOVE n FROM y" 是臆造命令（同 ArraySort 已拦）。改 DB 成员归属请用 INCLUDE / DELETE MEMBER。
        public string ArrayAdd(string arrayElem, string member)
            => $"Error: E3D 无「向 DB 元素数组加成员」命令。官方 ADD 是显示列表命令(ADD <selatt>)。把 {member} 并入 {arrayElem} 请用 INCLUDE（导航 CE 到 {arrayElem} 再 INCLUDE {member}）。";

        public string ArrayRemove(string arrayElem, int index)
            => $"Error: E3D 无「从 DB 元素数组删成员」命令。官方 REMOVE 是显示列表命令(REMove <selatt>)。删成员请用 DELETE <type> MEMBER {index}（导航 CE 到 {arrayElem} 再删）。";

        // 2026-07-03 官方参考重写（诚实拦回）：`.SORT()` 是 PML **数组变量**方法，套在 DB 元素路径上
        // 没有对应命令（真 E3D 拒）。E3D 无「排序数据库元素成员」的命令行动词——不发伪命令。
        public string ArraySort(string arrayElem)
            => $"Error: E3D 无「排序 DB 元素成员」命令。`.SORT()` 只是 PML 数组变量方法、不适用于元素路径 {arrayElem}。如需重排成员请用建模操作，不发此伪命令。";

        // ── 规格/目录 ──────────────────────────────────────────

        public string SpecQuery(string specName)
        {
            try
            {
                // Try to read spec data from the current element's SPREF
                if (string.IsNullOrWhiteSpace(specName))
                {
                    // Query all specs visible
                    RunPml("Q SPEC");
                    return "Queried all specs. Check E3D console for listing. Tip: use spec_query with a specific spec name for details, or use e3d_attr_read(attrs='SPREF') on a pipe element to see its spec reference.";
                }
                // Query specific spec
                RunPml($"Q SPEC {specName}");
                // Try to read spec component data
                var sb = new StringBuilder();
                sb.AppendLine($"Queried spec '{specName}'.");
                try
                {
                    string spcoErr;
                    string result = ReadPmlViaGlobal($"EVALUATE (Q SPCO {specName})", out spcoErr);
                    if (!string.IsNullOrWhiteSpace(result))
                        sb.AppendLine($"  Details: {result}");
                }
                catch { }
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 2026-07-04（指导书§3「列元素 = COLLECT / .NET 检索」）：枚举项目里全部管道规格(SPEC)名，
        // 让 agent「选管等级」时能拿到真实可选项，而非瞎猜（09 侧反馈"属性名/规格 AI 在瞎猜"的根治）。
        // **复用本类已验证的 .NET 检索范式**（与 Search line 126-169 同款 ResolveType + TypeFilter +
        // DBElementCollection 迭代），官方、类型安全、编译期可查——不发 PML、不猜逐字语法（守§0铁律）。
        // ⚠ 真机门（诚实边界）：规格常存于独立 catalogue/spec 世界，未必挂在设计世界 /* 下；此处 best-effort
        //   扫 /*，若枚举到 0 条 → **诚实说明**（可能在独立 spec 库，需真机核实根路径；临时可对已知规格名用
        //   e3d_spec_query 逐个验证），**绝不**把「没扫到」谎报成「项目无规格」。真机确认 spec 世界根路径后，
        //   在此加第二个 root 迭代即可（同款 DBElementCollection，零语法风险）。
        public string SpecList(int max)
        {
            if (max <= 0) max = 500;
            try
            {
                // ★2026-07-29 真机第七跑：这台 E3D 上 ResolveType("SPEC") 返回 **null**，
                //   于是函数在这里就 return 了 —— 我上一轮加的「目录库根」那段代码
                //   **一行都没执行到**。日志里那句"未解析出 SPEC 元素类型"就是这条早退。
                //   而同一跑 e3d_search type=SPECIFICATION 能解析出类型（回的是 "0 results"
                //   而不是"未解析出"）→ **类型码是全称 SPECIFICATION，不是 4 字缩写 SPEC**。
                //   ★教训：加了新分支要确认它**真的走得到**，别让一个早退把新代码变成死码
                //   （这正是本仓反复在治的「宣称生效实际不通」）。
                DbElementType specType = ResolveType("SPEC") ?? ResolveType("SPECIFICATION");
                if (specType == null)
                    return "SpecList: E3D 未解析出 SPEC / SPECIFICATION 元素类型（该 E3D 版本可能用别的类型码）。" +
                           "可对已知规格名用 e3d_spec_query 逐个验证其存在与参数。";

                var typeFilter = new TypeFilter(specType);
                var names = new List<string>();
                var scanned = new List<string>();

                // ★2026-07-29：**加上目录库（Catalogue）这个根**。
                //   上面那段注释里写的「真机确认 spec 世界根路径后，在此加第二个 root 迭代即可」——
                //   根路径现在有了，而且不必真机确认：AVEVA 自带的 .NET XML 文档写得明明白白
                //  （Aveva.Core.Database.xml）：
                //     DbType.Catalogue                "Catalogue database"
                //     MDB.CurrentMDB                  "Get hold of the current MDB. This is the only way to get at an MDB object."
                //     MDB.GetDBArray(DbType)          "List of DBs in MDB of given type."
                //     Db.World                        "Root 'world' Element for this Database"
                //   09 侧前六跑 e3d_spec_list / e3d_search 全 0 结果，根因就是**只扫了设计世界**。
                //   ✅ 已编译通过（本机 dotnet build net472，2026-07-29）；**真机仍未跑过**。
                //   整段包在 try 里 ——
                //     API 名字若有出入，退回「只扫设计世界」的旧行为，**绝不因为新根挂了就整个功能崩**。
                var roots = new List<DbElement>();
                try
                {
                    DbElement designWorld = DbElement.GetElement("/*");
                    if (designWorld != null && designWorld.IsValid) { roots.Add(designWorld); scanned.Add("设计世界 /*"); }
                }
                catch { }
                try
                {
                    var mdb = MDB.CurrentMDB;
                    if (mdb != null)
                    {
                        foreach (Db db in mdb.GetDBArray(DbType.Catalogue))
                        {
                            if (db == null) continue;
                            DbElement w = null;
                            try { w = db.World; } catch { }
                            if (w != null && w.IsValid) { roots.Add(w); scanned.Add("目录库 " + db.Name); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 如实记：目录库这条路没走通，不假装扫过了。
                    scanned.Add("目录库(取不到：" + ex.GetBaseException().Message + ")");
                }

                foreach (DbElement root in roots)
                {
                    if (names.Count >= max) break;
                    try
                    {
                        var collection = new DBElementCollection(root, typeFilter);
                        foreach (DbElement el in collection)
                        {
                            if (el == null || !el.IsValid) continue;
                            if (names.Count >= max) break;
                            string nm = null;
                            try { nm = el.GetAsString(DbAttributeInstance.NAME); } catch { }
                            if (!string.IsNullOrWhiteSpace(nm) && !names.Contains(nm.Trim())) names.Add(nm.Trim());
                        }
                    }
                    catch { /* 单个根扫不动不影响其余根 */ }
                }

                if (names.Count == 0)
                    return "Spec list: 0 specs（已扫：" + string.Join("、", scanned.ToArray()) + "）。" +
                           "扫过的根里没有 SPEC 元素。若目录库那一项显示『取不到』，说明这套 MDB 里没挂目录库，" +
                           "或 MDB/DbType API 与本版本不符——请把这行原文贴回来。" +
                           "临时可对已知规格名用 e3d_spec_query 逐个验证是否存在。";

                var sb = new StringBuilder();
                // ★把「扫了哪些根」一并回出去 —— 拿到 N 条却不知道来自哪个库，
                //   下次出问题就又要从头查一遍。闸/查询要说得出自己的覆盖面。
                sb.AppendLine($"Spec list: {names.Count} specs（已扫：{string.Join("、", scanned.ToArray())}）");
                foreach (var nm in names) sb.AppendLine("  " + nm);
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        /// <summary>
        /// 执行 PML 命令并**把 E3D 打印的输出一起带回来**。
        ///
        /// 与 <c>RunPml</c>/<c>TryRunPml</c> 的区别：那两个只回「成败 + 失败时的报错」，
        /// 成功时零内容；<c>Q SPEC</c> / <c>$Q</c> / <c>LIST</c> 这类**打印型**命令的结果
        /// 全进命令窗口，agent 完全拿不到（详见 <see cref="E3dOutputCapture"/> 头注）。
        ///
        /// ⚠ **捕获失败 ≠ 命令失败**，两者分开报：
        ///   · 命令成败 → 照旧由 <c>RunInPdms()</c> 决定
        ///   · 捕获成败 → 回执里单独一行说明
        /// 拿"我没听见"当"它没说"，是本仓头号红线的另一张脸。
        ///
        /// ✅ 已编译通过（2026-07-29 本机 dotnet build）；⚠ **真机未跑过** —— 见 E3dOutputCapture 头注的确认步骤。
        /// </summary>
        public string RunPmlVerbose(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return "Error: 请提供 command";
            var sb = new StringBuilder();
            bool ok;
            string err = null;
            using (var cap = E3dOutputCapture.Begin())
            {
                ok = TryRunPml(cmd, out err);
                sb.AppendLine(ok
                    ? "OK: 命令已执行"
                    : "REJECTED: " + (err ?? "E3D 未给出错误文本"));
                if (cap.Attached)
                {
                    string text = cap.Text();
                    if (string.IsNullOrEmpty(text))
                        sb.AppendLine("--- E3D 输出：(空 —— 这条命令没往命令窗口打印任何东西) ---");
                    else
                    {
                        sb.AppendLine("--- E3D 输出 ---");
                        sb.AppendLine(text);
                    }
                }
                else
                {
                    sb.AppendLine("--- E3D 输出：**未能捕获** ---");
                    sb.AppendLine("  原因: " + (cap.AttachError ?? "未知"));
                    sb.AppendLine("  注意: 这只说明【我们没听见】，不说明命令没输出，更不说明命令失败。");
                }
            }
            return sb.ToString();
        }

        public string BOM(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Error: name required";
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"BOM query for: {name}");

                // Try to capture BOM output
                try
                {
                    string bomErr;
                    string result = ReadPmlViaGlobal($"EVALUATE (BOM {name})", out bomErr);
                    if (!string.IsNullOrWhiteSpace(result) && result != "0")
                    {
                        sb.AppendLine("Materials:");
                        sb.AppendLine($"  {result}");
                    }
                    else
                    {
                        // Fallback: dump the element and its children with material info
                        var dump = ElementDump(name);
                        sb.AppendLine("Element data (for manual BOM extraction):");
                        sb.Append(dump);
                    }
                }
                catch (Exception bex)
                {
                    sb.AppendLine($"BOM 执行异常: {bex.Message}");
                }

                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ComponentInfo(string name)
        {
            try
            {
                DbElement elem = string.IsNullOrWhiteSpace(name)
                    ? CurrentElement.Element : DbElement.GetElement(name);
                if (elem == null || !elem.IsValid) return "Error: element not found";
                var sb = new StringBuilder();
                sb.AppendLine($"Component info for {elem.GetAsString(DbAttributeInstance.NAME)}:");
                foreach (var a in new[] { "CTYPE", "STYPE", "TTYP", "DTSE", "SPWL", "MTYS" })
                {
                    try
                    {
                        var dbA = DbAttribute.GetDbAttribute(a);
                        if (dbA == null) continue;
                        string val = elem.GetAsString(dbA);
                        if (val != null) sb.AppendLine($"  {a}: {val}");
                    }
                    catch { }
                }
                return sb.Length > 50 ? sb.ToString() : "No component type info available.";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 视图 ──────────────────────────────────────────────

        public string ViewZoom(string name)
        {
            try { RunPml($"ZOOM {name}"); return $"Zoomed to {name}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ViewFit()
        {
            try { RunPml("FIT"); return "View fitted."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ViewColour(string name, string colour)
        {
            try { RunPml($"COL {name} {colour}"); return $"Set colour of {name} to {colour}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 碰撞/检查 ─────────────────────────────────────────

        // N2（指导书 2026-07-02）：官方 Clasher API 通道 —— ClashOptions/ObstructionList/ClashSet
        // 工厂方法均经本机 DLL 元数据验证。成功返回结构化碰撞清单；任何失败返回 null → 调用方
        // 回退既有 PML 通道（additive，不动存量行为）。⚠ 真机验证前把握度中等。
        private string TryApiClashCheck(string name1, string name2)
        {
            try
            {
                var targets = new List<DbElement>();
                var el1 = DbElement.GetElement(name1);
                if (el1 == null || !el1.IsValid) return null;
                targets.Add(el1);
                if (!string.IsNullOrWhiteSpace(name2))
                {
                    var el2 = DbElement.GetElement(name2);
                    if (el2 != null && el2.IsValid) targets.Add(el2);
                }

                var options = Aveva.Core3D.Clasher.ClashOptions.Create();
                options.IncludeTouches = true;
                var obstructions = Aveva.Core3D.Clasher.ObstructionList.Create();
                if (string.IsNullOrWhiteSpace(name2))
                {
                    obstructions.AllObstructions = true;               // 目标 vs 全模型
                }
                else
                {
                    obstructions.AddObstructions(new[] { targets[targets.Count - 1] }); // 目标1 vs 目标2
                }
                var clashSet = Aveva.Core3D.Clasher.ClashSet.Create();

                // Clasher 是抽象类（编译器裁决）——具体实现在 Aveva.Core3D.Clasher.Implementation.dll
                //（E3D 进程内已加载）。运行时探测其非抽象子类实例化；拿不到 → 返回 null 诚实回退 PML。
                var clasher = CreateClasherInstance();
                if (clasher == null) return null;
                bool ok = clasher.Check(targets.ToArray(), options, obstructions, clashSet);
                if (!ok) return null;

                var sb = new StringBuilder();
                sb.AppendLine("=== CLASH REPORT (官方 Clasher API) ===");
                sb.AppendLine($"Target: {name1}" + (string.IsNullOrWhiteSpace(name2) ? " vs 全模型" : $" vs {name2}"));
                AppendClashGroup(sb, "硬碰撞", clashSet.Clashes);
                AppendClashGroup(sb, "接触", clashSet.Touches);
                AppendClashGroup(sb, "净距不足", clashSet.Clearances);
                AppendClashGroup(sb, "未能判定", clashSet.NotProven);
                int total = (clashSet.Clashes?.Length ?? 0) + (clashSet.Touches?.Length ?? 0)
                          + (clashSet.Clearances?.Length ?? 0);
                sb.AppendLine(total == 0 ? "未发现碰撞/接触/净距问题。" : $"共 {total} 项。");
                sb.AppendLine("=== END REPORT ===");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                LogPml($"[Clasher API 失败，回退 PML] {ex.Message}", false);
                return null;
            }
        }

        /// <summary>在 E3D 进程已加载程序集里找 Clasher 的具体实现类并实例化。失败返回 null（回退 PML）。⚠ 真机裁决。</summary>
        private static Aveva.Core3D.Clasher.Clasher CreateClasherInstance()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string an;
                    try { an = asm.GetName().Name; } catch { continue; }
                    if (an == null || !an.StartsWith("Aveva.Core3D.Clasher")) continue;
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        if (t.IsAbstract || !typeof(Aveva.Core3D.Clasher.Clasher).IsAssignableFrom(t)) continue;
                        try { return (Aveva.Core3D.Clasher.Clasher)Activator.CreateInstance(t, true); }
                        catch { }
                    }
                }
            }
            catch { }
            return null;
        }

        private static void AppendClashGroup(StringBuilder sb, string label, Aveva.Core3D.Clasher.Clash[] items)
        {
            if (items == null || items.Length == 0) return;
            sb.AppendLine($"{label}: {items.Length} 项");
            int shown = 0;
            foreach (var c in items)
            {
                if (shown++ >= 20) { sb.AppendLine($"  …（其余 {items.Length - 20} 项略）"); break; }
                string a = "", b = "";
                try { a = c.First != null && c.First.IsValid ? c.First.GetAsString(DbAttributeInstance.NAME) : "?"; } catch { }
                try { b = c.Second != null && c.Second.IsValid ? c.Second.GetAsString(DbAttributeInstance.NAME) : "?"; } catch { }
                string pos = "";
                try { var p = c.ClashPosition; pos = $" @ ({p.X:F0},{p.Y:F0},{p.Z:F0})"; } catch { }
                sb.AppendLine($"  [{c.Type}] {a} vs {b}{pos}");
            }
        }

        public string ClashCheck(string name1, string name2)
        {
            if (string.IsNullOrWhiteSpace(name1)) return "Error: type_or_name1 required";
            // N2：官方 Clasher API 优先，失败回退既有 PML 通道。
            var apiResult = TryApiClashCheck(name1, name2);
            if (apiResult != null) return apiResult;
            try
            {
                var sb = new StringBuilder();
                string cmdPml = string.IsNullOrWhiteSpace(name2)
                    ? $"CLASH ALL {name1}"
                    : $"CLASH {name1} {name2}";
                RunPml(cmdPml);

                sb.AppendLine($"=== CLASH REPORT ===");
                sb.AppendLine($"Target: {name1}" + (string.IsNullOrWhiteSpace(name2) ? " (all)" : $" vs {name2}"));
                sb.AppendLine($"Command: {cmdPml}");

                // Try to get clash count and summary
                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                bool hasData = false;

                // Approach 1: Check clash array/list via PML（P0：!! 全局读回）
                try
                {
                    string clErr;
                    string clashList = ReadPmlViaGlobal("EVALUATE (CLASHLIST)", out clErr);
                    if (!string.IsNullOrWhiteSpace(clashList) && clashList != "0")
                    {
                        sb.AppendLine($"Clash list: {clashList}");
                        hasData = true;
                    }
                }
                catch { }

                // Approach 2: Try to get individual clash objects (1-10)
                if (!hasData)
                {
                    try
                    {
                        int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                        string vn = $"MCC{rnd}";
                        RunPml($"VAR !{vn} STRING = ''");
                        RunPml($"DO !I FROM 1 TO 10");
                        RunPml($"  IF (CLASH $!I VALID) THEN");
                        RunPml($"    VAR !TCE = CLASH $!I OWNER");
                        RunPml($"    VAR !TCE2 = CLASH $!I OTHER");
                        RunPml($"    !{vn} = !{vn} + 'Clash ' + $!I.STRING() + ':'");
                        RunPml($"    IF (!TCE VALID) THEN !{vn} = !{vn} + ' ' + !TCE.NAME ENDIF");
                        RunPml($"    IF (!TCE2 VALID) THEN !{vn} = !{vn} + ' vs ' + !TCE2.NAME ENDIF");
                        RunPml($"    !{vn} = !{vn} + '|'");
                        RunPml($"  ENDIF");
                        RunPml($"ENDO");
                        string clashData = cmd.GetPMLVariableString(vn);
                        try { RunPml($"VAR !{vn} DELETE"); } catch { }
                        if (!string.IsNullOrWhiteSpace(clashData) && clashData.Length > 5)
                        {
                            sb.AppendLine("Clash details:");
                            var clashes = clashData.Split('|');
                            foreach (var c in clashes)
                                if (!string.IsNullOrWhiteSpace(c))
                                    sb.AppendLine($"  {c.Trim()}");
                            hasData = true;
                        }
                    }
                    catch { }
                }

                // Approach 3: Basic query
                if (!hasData)
                {
                    try
                    {
                        RunPml($"Q CLASH");
                        string ccErr;
                        string count = ReadPmlViaGlobal("EVALUATE (!!CLASHCOUNT)", out ccErr);
                        if (!string.IsNullOrWhiteSpace(count))
                        {
                            sb.AppendLine($"Total clashes detected: {count}");
                            hasData = true;
                        }
                    }
                    catch { }
                }

                if (!hasData)
                {
                    sb.AppendLine("Clash detection completed. Results output to E3D console.");
                    sb.AppendLine("Use 'Q CLASH' in E3D PML to see detail.");
                }

                sb.AppendLine($"=== END REPORT ===");
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string DesignCheck(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Error: name required";
            try
            {
                var sb = new StringBuilder();
                // First, dump element attributes to provide context
                var dumpText = ElementDump(name);
                sb.AppendLine($"Design check on: {name}");
                sb.AppendLine();

                // Run CHECK in E3D
                RunPml($"CHECK {name}");

                // Try to capture check output
                try
                {
                    string chkErr;
                    string result = ReadPmlViaGlobal($"EVALUATE (CHECK {name})", out chkErr);
                    if (!string.IsNullOrWhiteSpace(result) && result != "0")
                        sb.AppendLine($"Check result: {result}");
                }
                catch { }

                sb.AppendLine("CHECK command executed.");
                sb.AppendLine("Tip: For ASME B31.3 validation with detailed clause references, use the web Agent's E3D Validate feature.");
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 会话 ──────────────────────────────────────────────

        public string SessionStatus()
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("E3D Session Status:");
                var info = GetProjectInfo();
                foreach (var kv in info) sb.AppendLine($"  {kv.Key}: {kv.Value}");
                try
                {
                    var ce = GetCurrentElement();
                    if (ce != null) sb.AppendLine($"  current_element: [{ce.Type}] {ce.Name}");
                    else sb.AppendLine("  current_element: (none)");
                }
                catch { sb.AppendLine("  current_element: (unknown)"); }
                sb.AppendLine($"  mcp_server: E3D-MCP-Server v1.0.0");
                sb.AppendLine($"  status: connected");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
            return sb.ToString();
        }

        // ── 内部辅助 ──────────────────────────────────────

        // PDMS 4 字代号 → .NET DbElementTypeInstance 常见全称（.NET 字段名多为全称）。
        // 若 .NET 字段本就是代号，as-is 反射先命中，本表只作兜底。
        private static readonly Dictionary<string, string> _typeAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "BRAN", "BRANCH" }, { "ELBO", "ELBOW" }, { "VALV", "VALVE" }, { "FLAN", "FLANGE" },
            { "NOZZ", "NOZZLE" }, { "EQUI", "EQUIPMENT" }, { "STRU", "STRUCTURE" }, { "REDU", "REDUCER" },
            { "INST", "INSTRUMENT" }, { "GASK", "GASKET" }, { "COUP", "COUPLING" }, { "SUPP", "SUPPORT" },
            { "ATTA", "ATTACHMENT" },
        };

        private static DbElementType ResolveTypeReflect(string typeName)
        {
            try
            {
                var field = typeof(DbElementTypeInstance).GetField(typeName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.IgnoreCase);
                if (field != null && field.FieldType == typeof(DbElementType))
                    return (DbElementType)field.GetValue(null);

                foreach (var f in typeof(DbElementTypeInstance).GetFields(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (f.FieldType == typeof(DbElementType) &&
                        f.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                        return (DbElementType)f.GetValue(null);
                }
            }
            catch { }
            return null;
        }

        private static DbElementType ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            typeName = typeName.Trim();
            var t = ResolveTypeReflect(typeName);            // 原名（代号即字段名时直接命中）
            if (t != null) return t;
            string full;
            if (_typeAlias.TryGetValue(typeName, out full))   // 代号→全称兜底
                t = ResolveTypeReflect(full);
            return t;
        }

        private E3dElementInfo BuildInfoFromDb(DbElement elem)
        {
            if (elem == null || !elem.IsValid) return null;
            var info = new E3dElementInfo();
            try { info.Name = elem.GetAsString(DbAttributeInstance.NAME); } catch { }
            try { info.Type = elem.GetElementType().ToString(); } catch { }
            try
            {
                var pos = elem.GetPosition(DbAttributeInstance.POS);
                if (pos != null)
                {
                    info.X = Math.Round(pos.X, 3);
                    info.Y = Math.Round(pos.Y, 3);
                    info.Z = Math.Round(pos.Z, 3);
                }
            }
            catch { }
            return info;
        }

        private void AppendDbElement(StringBuilder sb, DbElement elem, int depth, string[] attrs)
        {
            string indent = new string(' ', depth * 2);
            string name = null, type = null;
            try { name = elem.GetAsString(DbAttributeInstance.NAME); } catch { }
            try { type = elem.GetElementType().ToString(); } catch { }
            sb.AppendLine($"{indent}[{type}] {name}");

            foreach (var a in attrs)
            {
                try
                {
                    var dbA = DbAttribute.GetDbAttribute(a);
                    if (dbA == null) continue;
                    string val = elem.GetAsString(dbA);
                    if (val != null) sb.AppendLine($"{indent}  {a}: {val}");
                }
                catch { }
            }

            // Recurse
            try
            {
                DbElement[] members = elem.Members();
                if (members != null)
                {
                    int count = 0;
                    foreach (var m in members)
                    {
                        if (m == null || !m.IsValid || count >= 50) break;
                        AppendDbElement(sb, m, depth + 1, attrs);
                        count++;
                    }
                }
            }
            catch { }
        }

        // PML execution (E3DAddIn_10 pattern: RunInPdms returns bool)
        private static void RunPml(string cmd)
        {
            LogPml(cmd, null);
            var c = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(cmd);
            c.RunInPdms();
        }

        private static bool TryRunPml(string cmd)
        {
            string _;
            return TryRunPml(cmd, out _);
        }

        // P0-C（指导书 2026-07-02）：带内错误版本 —— Agent 读不到 D:\ 日志也看不到命令窗，
        // 纠错依据必须在返回值里。error 取 Command.Error（E3D 的真实报错文本）。
        private static bool TryRunPml(string cmd, out string error)
        {
            var c = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(cmd);
            bool ok = c.RunInPdms();
            LogPml(cmd, ok);
            error = ok ? null : SafeErrorText(c);
            return ok;
        }

        /// <summary>取 Command.Error 的文本，任何异常都吞掉（错误通道自身绝不能抛）。</summary>
        /// <summary>
        /// 从 AVEVA 消息对象取**文本**。
        ///
        /// ★2026-07-29 真机第七跑抓到：全仓这三处都直接 <c>.ToString()</c>，
        ///   而 <c>PdmsMessageImpl</c> / <c>PdmsOutputImpl</c> **没重写 ToString()** →
        ///   回执里出现 <c>REJECTED: Aveva.Core.Utilities.Messaging.Implementation.PdmsMessageImpl</c>。
        ///   后果不是"难看"，是**把真相吞了** —— 那一跑四条 COLLECT 的真实报错全丢在这里，
        ///   于是"为什么不行"变成了"不知道"。
        /// 正路：官方 XML 文档 <c>PdmsMessage.MessageText</c>
        ///   "Obtains message text, filling in any substitutions"。
        /// 反射按 方法 → 属性 → ToString 三级降级（对象类型不确定，写死一种会再摔一次）。
        /// </summary>
        internal static string MsgText(object o)
        {
            if (o == null) return null;
            var str = o as string;
            if (str != null) return str;
            try
            {
                var t = o.GetType();
                // ── PdmsMessage 一族：MessageText() ──
                var m = t.GetMethod("MessageText", Type.EmptyTypes);
                if (m != null) { var v = m.Invoke(o, null); if (v != null) return v.ToString(); }
                var p = t.GetProperty("MessageText");
                if (p != null) { var v = p.GetValue(o, null); if (v != null) return v.ToString(); }

                // ── ★PdmsOutput 一族：没有 MessageText，文本在 Description / Details ──
                //   2026-07-29 真机第八跑：上一轮只按 PdmsMessage 修，结果输出捕获
                //   仍回 "(取不到消息文本，只拿到类型名 …PdmsOutputImpl)"。
                //   官方 XML 文档（Aveva.Core.Utilities.xml）里 PdmsOutput 的成员是：
                //     Type "Message type" · Description "Message description"
                //     · Details "Message details" · Category · Date
                //   ★两个类不一样，取法也不一样 —— 这就是为什么要按"有没有这个成员"降级，
                //     而不是认死一种类型。
                string desc = null, det = null;
                var pd = t.GetProperty("Description");
                if (pd != null) { var v = pd.GetValue(o, null); if (v != null) desc = v.ToString(); }
                var pt = t.GetProperty("Details");
                if (pt != null) { var v = pt.GetValue(o, null); if (v != null) det = v.ToString(); }
                if (!string.IsNullOrWhiteSpace(desc) || !string.IsNullOrWhiteSpace(det))
                {
                    if (string.IsNullOrWhiteSpace(det)) return desc;
                    if (string.IsNullOrWhiteSpace(desc)) return det;
                    return desc + "\n" + det;
                }
            }
            catch { }
            var fb = o.ToString();
            // 退到 ToString 时如实标注 —— 类型全名不是消息文本，别让它冒充内容。
            if (!string.IsNullOrEmpty(fb) && fb.StartsWith("Aveva."))
                return "(取不到消息文本，只拿到类型名 " + fb + ")";
            return fb;
        }

        private static string SafeErrorText(Aveva.Core.Utilities.CommandLine.Command c)
        {
            try
            {
                var e = c.Error;
                string s = MsgText(e);
                return string.IsNullOrWhiteSpace(s) ? "E3D 未给出错误文本" : s.Trim();
            }
            catch { return "E3D 未给出错误文本"; }
        }

        // P0-B（指导书 2026-07-02）：统一 PML 变量读回通道。
        // 旧模式（`VAR !x = ...` 局部变量 + 裸 GetPMLVariableString）三处不稳：① Run/RunInPdms
        // 在嵌套作用域执行，! 局部变量出作用域可能即销毁；② 写入带 ! 读取不带前缀，规则未定；
        // ③ 读回失败返回空串，Agent 无法区分"查询无结果"与"变量没读到"——静默毒化认知。
        // 新通道：!! 全局变量（跨作用域稳）+ 官方 GetStringFromPML 一步读回（抑制阻塞式 PML
        // 错误弹窗——无人值守关键）+ 错误带内返回。签名已经本机 Aveva.Core.Utilities.xml 验证。
        // varForm=true 用 `VAR !!x = <命令>` 赋值式（读类命令如 Q ATT；该形态已被旧代码真机验证），
        // varForm=false 用 `!!x = <表达式>` 直赋（PML 表达式）。⚠ 两种形态最终以真机日志闭环为准。
        // 实测（lib DLL 元数据 dump）：RunPMLCommand/GetStringFromPML/DeletePMLVar 均为**静态**方法，
        // 且 GetStringFromPML 的 out 实为 **ref**（static bool GetStringFromPML(string, ref string, ref PdmsMessage)）。
        // 用 RunPMLCommandInPDMS（与代码库真机验证过的 RunInPdms 同为 PDMS 上下文）。⚠ 形态以真机日志闭环为准。
        private static void EvalViaPmlGlobal(string expression, bool varForm, out bool ok, out string val, out string err)
        {
            int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
            string vn = "!!MCPQ" + rnd;
            try
            {
                // ★2026-07-29 真机第八跑修：这行原来是
                //     varForm ? $"VAR {vn} = {expression}" : $"{vn} = {expression}"
                //   **两条都带 `=`** —— 而 EvaluatePml 的注释写着"失败再试 VAR 赋值式
                //  （Q 系命令形态）"。也就是说所谓的"双形态兜底"其实是**同一种形态试两遍**，
                //   注释承诺的那条路**从来没被发送过**（又一例「宣称生效实际不通」）。
                //   证据：真机上 `COLLECT ALL SPEC` 与对照组 `COLLECT ALL EQUI`（绝对有效的类型）
                //   双双回 `CP: Syntax error` —— 错的不是类型名，是语法本身。
                //   官方 COLLECT 形态（DBRM 第 11 章，手册标注可作 ground truth）是
                //     VAR !Array COLLECT ALL <TYPE>        ← **没有等号**
                //   同文件的 ReadPmlViaGlobal 反而一直是对的（`VAR {vn} {assignTail}`）。
                string assign = varForm ? $"VAR {vn} {expression}" : $"{vn} = {expression}";
                LogPml(assign, null);
                Aveva.Core.Utilities.CommandLine.Command.RunPMLCommandInPDMS(assign);
                Aveva.Core.Utilities.Messaging.PdmsMessage perr;
                ok = Aveva.Core.Utilities.CommandLine.Command.GetStringFromPML(vn, out val, out perr);
                err = ok ? null : (perr != null ? MsgText(perr) : "PML 变量读回失败（无错误文本）");
            }
            // DeletePMLVar 在本机 lib DLL 里是 internal（metadata 可见但编译不可达）→ 用 PML 自删
            finally { try { Aveva.Core.Utilities.CommandLine.Command.RunPMLCommandInPDMS($"VAR {vn} DELETE"); } catch { } }
        }

        // P0：旧"临时 ! 局部变量 + 裸 GetPMLVariableString"读回的统一替身。assignTail 原样保留
        // 各调用点已有的命令形态（"EVALUATE (...)" / "= Q ATT ..."），只把变量换成 !! 全局 +
        // 官方 GetStringFromPML 读回。读不到/出错返回 ""（调用方按原有空值逻辑兜底），错误进 err。
        private static string ReadPmlViaGlobal(string assignTail, out string err)
        {
            int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
            string vn = "!!MCPQ" + rnd;
            try
            {
                string assign = $"VAR {vn} {assignTail}";
                LogPml(assign, null);
                Aveva.Core.Utilities.CommandLine.Command.RunPMLCommandInPDMS(assign);
                string val;
                Aveva.Core.Utilities.Messaging.PdmsMessage perr;
                bool ok = Aveva.Core.Utilities.CommandLine.Command.GetStringFromPML(vn, out val, out perr);
                err = ok ? null : (perr != null ? MsgText(perr) : "PML 变量读回失败（无错误文本）");
                return ok ? (val ?? "") : "";
            }
            catch (Exception ex) { err = ex.Message; return ""; }
            finally { try { Aveva.Core.Utilities.CommandLine.Command.RunPMLCommandInPDMS($"VAR {vn} DELETE"); } catch { } }
        }

        // 2026-06-29：纯写命令（改库类）统一走这条 —— 真判 RunInPdms() 成败，false 回 Error 串，
        // 让 09 侧 _e3dCallHelper.isE3DFailReceipt / callE3DWriteOrThrow 抓住。杜绝旧 RunPml「丢弃
        // 成败 bool + 无条件回 OK」造成的"说写了但模型没动 / SAVEWORK 假成功"。**仅用于改库命令**；
        // 查询/显示/视图类（SHOW/HIDE/VIEW/COL）保留 RunPml 宽松，避免 false-on-success 误判。
        private string RunWrite(string cmd, string okMsg)
        {
            try
            {
                // P0-C：失败带 E3D 真实错误文本（带内），日志仅供人诊断。
                string err;
                if (!TryRunPml(cmd, out err))
                    return $"Error: '{cmd}' 在 E3D 端被拒（未执行）: {err}";
                return okMsg;
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 2026-07-03：CE-导航 + 裸命令 helper（修 builder 通病）。真 PML 的定位/编辑/连接类动词
        // （ROTATE/FLIP/CONNECT/POSITION/COPY ATTR…）**作用于 CE(当前元素)、不接前导元素名**——
        // 正解是先把 CE 导航到目标、再发官方裸命令（范式同 CreateElement/InsertComponent）。取代旧
        // 伪范式 "{元素名} {动词}"（真 E3D 拒收）。elemName 空 = 作用于当前 CE。
        private string NavigateThenWrite(string elemName, string bareCmd, string okMsg)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(elemName))
                {
                    DbElement el = DbElement.GetElement(elemName);
                    if (el == null || !el.IsValid)
                        return $"Error: 元素 '{elemName}' 不存在或无效，无法执行 '{bareCmd}'";
                    CurrentElement.Element = el;   // CE→目标，命令作用于 CE
                }
                return RunWrite(bareCmd, okMsg);
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // 轴名归一到 E3D 方向词(bdir)：X→E / Y→N / Z→U，其余大写透传（U/N/E/D/S/W 直用）。
        private static string ToBdir(string axis)
        {
            string a = (axis ?? "").Trim().ToUpper();
            switch (a) { case "X": return "E"; case "Y": return "N"; case "Z": return "U"; }
            return string.IsNullOrEmpty(a) ? "U" : a;
        }

        // 2026-06-17：把每条发给 E3D 的 PML 原文 + 成败追加到 %TEMP%\pipingclaw_e3d_pml.log，
        // 便于在真 E3D 上诊断"命令在 E3D 端报错"——直接看到我们到底发了什么、E3D 收没收。
        // 永不抛（包 try/catch，绝不影响建模主流程）。tail 该文件即可定位错命令。
        private static void LogPml(string cmd, bool? ok)
        {
            var tag = ok == null ? "PML>" : (ok.Value ? "OK  " : "FAIL");
            var line = string.Format("{0:HH:mm:ss} {1} {2}{3}", System.DateTime.Now, tag, cmd, System.Environment.NewLine);
            // 2026-06-30：按用户要求，PML 日志落 D:\pipingclaw_e3d_pml.log（便于 tail 诊断真机命令成败）。
            // D:\ 不可用（无该盘/无写权限）时回退 %TEMP%，绝不抛、绝不影响建模主流程。
            try { System.IO.File.AppendAllText(@"D:\pipingclaw_e3d_pml.log", line); }
            catch
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pipingclaw_e3d_pml.log"), line); }
                catch { }
            }
        }

        private static string GetPmlGlobal(string varName)
        {
            try
            {
                // P0：全局变量统一走官方 GetStringFromPML（静态 + ref，带 !! 前缀，与 EvalViaPmlGlobal
                // 一致；旧 Replace("!!","") 剥前缀与写入侧规则互相矛盾）。⚠ 前缀口径以真机为准。
                string name = varName.StartsWith("!!") ? varName : "!!" + varName.TrimStart('!');
                string val;
                Aveva.Core.Utilities.Messaging.PdmsMessage perr;
                bool ok = Aveva.Core.Utilities.CommandLine.Command.GetStringFromPML(name.ToUpper(), out val, out perr);
                if (ok && !string.IsNullOrEmpty(val)) return val;
                // 兜底：旧口径（剥 !!）再试一次，兼容 GetPMLVariableString 时代写入的变量
                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                return cmd.GetPMLVariableString(varName.Replace("!!", "").ToUpper()) ?? "";
            }
            catch { return ""; }
        }
    }
}
