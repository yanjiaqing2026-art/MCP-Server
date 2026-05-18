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
                var dbAttr = DbAttribute.GetDbAttribute(attr);
                if (dbAttr == null) return $"Error: attribute '{attr}' not found";
                dbElem.SetAttribute(dbAttr, value);
                return $"OK: set {attr}={value} on {name}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string CreateElement(string type, string name, string owner)
        {
            try
            {
                RunPml($"NEW {type} {name}");
                if (!string.IsNullOrEmpty(owner))
                {
                    string ownerPath = owner == "/*" ? "/*" : owner;
                    RunPml($"MOVE {name} TO {ownerPath}");
                }
                return $"OK: created {type} {name}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 管道操作 ──────────────────────────────────────

        public string CutPipe(string name, double at)
        {
            try { RunPml($"CUT {name} AT {at}"); return $"OK: cut {name} at {at}mm"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string GapPipe(string name, double at, double gap = 10)
        {
            try
            {
                if (gap > 0) RunPml($"GAP {name} AT {at} GAP {gap}");
                else RunPml($"GAP {name} AT {at}");
                return $"OK: gap {name} at {at}mm" + (gap > 0 ? $" (gap={gap}mm)" : "");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string JoinPipe(string name1, string name2)
        {
            try { RunPml($"JOIN {name1} {name2}"); return $"OK: joined {name1} and {name2}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string BendPipe(string name, double at, string stype = null)
        {
            try
            {
                string cmd = $"BEND {name} AT {at}";
                if (!string.IsNullOrEmpty(stype)) cmd += $" STYPE {stype}";
                RunPml(cmd);
                return $"OK: inserted bend on {name} at {at}mm" + (!string.IsNullOrEmpty(stype) ? $" (STYPE={stype})" : "");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string TeePipe(string name, double at, string stype = null)
        {
            try
            {
                string cmd = $"TEE {name} AT {at}";
                if (!string.IsNullOrEmpty(stype)) cmd += $" STYPE {stype}";
                RunPml(cmd);
                return $"OK: inserted tee on {name} at {at}mm" + (!string.IsNullOrEmpty(stype) ? $" (STYPE={stype})" : "");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ValvePipe(string name, double at, string stype = null)
        {
            try
            {
                string cmd = $"VALVE {name} AT {at}";
                if (!string.IsNullOrEmpty(stype)) cmd += $" STYPE {stype}";
                RunPml(cmd);
                return $"OK: inserted valve on {name} at {at}mm" + (!string.IsNullOrEmpty(stype) ? $" (STYPE={stype})" : "");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string FlangePipe(string name, double at, string stype = null)
        {
            try
            {
                string cmd = $"FLANGE {name} AT {at}";
                if (!string.IsNullOrEmpty(stype)) cmd += $" STYPE {stype}";
                RunPml(cmd);
                return $"OK: inserted flange on {name} at {at}mm" + (!string.IsNullOrEmpty(stype) ? $" (STYPE={stype})" : "");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ReducerPipe(string name, double at)
        {
            try { RunPml($"REDUCER {name} AT {at}"); return $"OK: inserted reducer on {name} at {at}mm"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string RoutePipe(string pipe)
        {
            try { RunPml($"ROUTE {pipe}"); return $"OK: auto-routed {pipe}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 元素补充 ──────────────────────────────────────

        public string CreateSupport(string name, string owner = null, string stype = null)
        {
            try
            {
                RunPml($"NEW SUPP {name}");
                if (!string.IsNullOrEmpty(owner)) RunPml($"MOVE {name} TO {owner}");
                return $"OK: created support {name}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string CreateWeld(string name, string owner = null)
        {
            try
            {
                RunPml($"NEW WELD {name}");
                if (!string.IsNullOrEmpty(owner)) RunPml($"MOVE {name} TO {owner}");
                return $"OK: created weld {name}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string CreateLabel(string name, string owner, string text)
        {
            try
            {
                RunPml($"NEW LABE {name}");
                if (!string.IsNullOrEmpty(owner)) RunPml($"MOVE {name} TO {owner}");
                if (!string.IsNullOrEmpty(text)) RunPml($"{name} DESC '{text}'");
                return $"OK: created label {name}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 属性补充 ──────────────────────────────────────

        public string ClearAttribute(string name, string attr)
        {
            try { RunPml($"{name} CLEARA {attr}"); return $"OK: cleared {attr} on {name}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string CopyAttributes(string source, string target, string attrs)
        {
            try
            {
                string attrList = attrs.Replace(",", " ").Replace("，", " ");
                RunPml($"COPYA {source} {target} {attrList}");
                return $"OK: copied attributes from {source} to {target}";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

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

        public string ClaimElement(string name)
        {
            try { RunPml($"CLAIM {name}"); return $"OK: claimed {name}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ReleaseElement(string name)
        {
            try { RunPml($"RELEASE {name}"); return $"OK: released {name}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 出图 ──────────────────────────────────────────

        public string DraftIso(string name, string output)
        {
            try { RunPml($"DRAFT ISO {name} OUTPUT {output}"); return $"OK: ISO draft of {name} -> {output}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 定位补充 ──────────────────────────────────────

        public string PositionAt(string name, double x, double y, double z)
        {
            try { RunPml($"{name} AT E{x} N{y} U{z}"); return $"OK: positioned {name} at E{x} N{y} U{z}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string PositionDist(string name, double dist, string from = null)
        {
            try
            {
                string cmd = $"{name} DIST {dist}";
                if (!string.IsNullOrEmpty(from)) cmd += $" FROM {from}";
                RunPml(cmd);
                return $"OK: set {name} distance {dist}mm" + (!string.IsNullOrEmpty(from) ? $" from {from}" : "");
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string PositionDir(string name, string direction)
        {
            try { RunPml($"{name} DIR {direction}"); return $"OK: set {name} direction to {direction}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string PositionOri(string name, string ppoint)
        {
            try { RunPml($"{name} ORI {ppoint}"); return $"OK: oriented {name} around {ppoint}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string PositionThr(string name, string target)
        {
            try { RunPml($"{name} THR {target}"); return $"OK: through-point aligned {name} to {target}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 元素操作补充 ──────────────────────────────────

        public string FlipElement(string name)
        {
            try { RunPml($"{name} FLIP"); return $"OK: flipped {name}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ForceConnect(string name, string target)
        {
            try { RunPml($"FCONN {name} {target}"); return $"OK: force-connected {name} to {target}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string DeleteElement(string name)
        {
            try { RunPml($"{name} DELETE"); return $"OK: deleted {name}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string RenameElement(string name, string newName)
        {
            try { RunPml($"{name} NAME {newName}"); return $"OK: renamed {name} to {newName}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string CopyElement(string source, string dest)
        {
            try { RunPml($"COPY {source} TO {dest}"); return $"OK: copied {source} to {dest}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string MoveElement(string name, string newOwner)
        {
            try { RunPml($"MOVE {name} TO {newOwner}"); return $"OK: moved {name} to {newOwner}"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── PML ────────────────────────────────────────────

        public string ExecutePml(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "Error: empty command";
            try
            {
                // Detect query-style commands and try to capture output
                string upper = command.Trim().ToUpper();
                bool isReadOnly = upper.StartsWith("Q ") || upper.StartsWith("QVAR ")
                    || upper == "LIST" || upper == "LIST ALL" || upper.StartsWith("DUMP")
                    || upper.StartsWith("DISTANCE") || upper.StartsWith("Q ATT");

                if (isReadOnly)
                {
                    // Capture output via temp variable
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    string vn = $"MC{rnd}";
                    // For query commands, wrap in EVALUATE to capture result
                    RunPml($"VAR !{vn} EVALUATE ({command})");
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                    string result = cmd.GetPMLVariableString(vn);
                    try { RunPml($"VAR !{vn} DELETE"); } catch { }
                    if (!string.IsNullOrWhiteSpace(result) && result != "0")
                        return result;
                    return "(query executed, no return value)";
                }

                // Modify commands: run directly
                bool ok = TryRunPml(command);
                if (ok)
                {
                    // Try to verify what was changed by reading CE after the command
                    var ceAfter = GetCurrentElement();
                    if (ceAfter != null)
                        return $"OK: '{command}' executed successfully. CE: [{ceAfter.Type}] {ceAfter.Name}";
                    return $"OK: '{command}' executed successfully.";
                }
                return $"FAILED: '{command}' (check E3D console for errors)";
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

                // PML expression: assign to temp variable, read back, clean up
                int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                string vn = $"MCPE{rnd}";
                // Use VAR ... = ... (PML1 style) which is more reliable than !!global = EVALUATE(...)
                RunPml($"VAR !{vn} = {expression}");
                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                string val = cmd.GetPMLVariableString(vn);
                if (string.IsNullOrEmpty(val))
                {
                    // Try reading as global variable
                    val = cmd.GetPMLVariableString($"!!{vn}");
                }
                try { RunPml($"VAR !{vn} DELETE"); } catch { }
                return val ?? "(no return value)";
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
                    RunPml($"!!CE = {path}");
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
            try { RunPml("SAVEWORK"); return "Database saved."; }
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
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    // GETCHANGES outputs to console but we try to capture via variable
                    RunPml($"VAR !MCGC{rnd} EVALUATE (GETCHANGES)");
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                    string result = cmd.GetPMLVariableString($"MCGC{rnd}");
                    try { RunPml($"VAR !MCGC{rnd} DELETE"); } catch { }

                    if (!string.IsNullOrWhiteSpace(result) && result != "0")
                    {
                        sb.AppendLine("Changes since last SAVEWORK:");
                        sb.AppendLine($"  {result}");
                    }
                    else
                    {
                        sb.AppendLine("No changes since last SAVEWORK, or GETCHANGES output not capturable.");
                    }
                }
                catch
                {
                    sb.AppendLine("GETCHANGES executed. Check E3D console.");
                }
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string DbUndo()
        {
            try { RunPml("UNDO"); return "Undo executed."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string DbExtract(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "Error: type required";
            try { RunPml($"EXTRACT ALL {type}"); return $"Extracted all {type}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

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
            try
            {
                if (string.IsNullOrWhiteSpace(attr))
                    RunPml($"{name} REVERT");
                else
                    RunPml($"{name} REVERTATT {attr}");
                return $"Reverted {name}" + (attr != null ? $" attribute {attr}" : "");
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
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    RunPml($"VAR !MCPOCC{rnd} EVALUATE ({name} OCCURRENCE)");
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                    string val = cmd.GetPMLVariableString($"MCPOCC{rnd}");
                    try { RunPml($"VAR !MCPOCC{rnd} DELETE"); } catch { }
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
            try { RunPml($"!!TMP_WRT = WRT {name}"); return "WRT queried."; }
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
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    RunPml($"VAR !MCATTR{rnd} = Q ATT {attr}{typeHint}");
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                    string qResult = cmd.GetPMLVariableString($"MCATTR{rnd}");
                    if (!string.IsNullOrWhiteSpace(qResult))
                        sb.AppendLine($"  Definition: {qResult}");
                    try { RunPml($"VAR !MCATTR{rnd} DELETE"); } catch { }
                }
                catch { }

                return sb.Length > 50 ? sb.ToString() : $"Queried attribute '{attr}'" + (type != null ? $" for type '{type}'" : "") + ". Use e3d_attr_read to see current values.";
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 几何 ──────────────────────────────────────────────

        public string SetPosition(string name, double x, double y, double z)
        {
            try { RunPml($"{name} POS {x} {y} {z}"); return $"Set {name} position to ({x:F3}, {y:F3}, {z:F3})"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string MoveBy(string name, double dx, double dy, double dz)
        {
            try { RunPml($"{name} BY {dx} {dy} {dz}"); return $"Moved {name} by ({dx:F3}, {dy:F3}, {dz:F3})"; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

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

        public string Rotate(string name, string axis, double angle)
        {
            try { RunPml($"ROTATE {name} ABOUT {axis} BY {angle}"); return $"Rotated {name} about {axis} by {angle} deg."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string Reverse(string name)
        {
            try { RunPml($"REVERSE {name}"); return $"Reversed {name}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 连接 ──────────────────────────────────────────────

        public string Connect(string name1, string name2)
        {
            try { RunPml($"CONNECT {name1} TO {name2}"); return $"Connected {name1} to {name2}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string Disconnect(string name)
        {
            try { RunPml($"DISCONNECT {name}"); return $"Disconnected {name}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        // ── 阵列 ──────────────────────────────────────────────

        public string ArrayAdd(string arrayElem, string member)
        {
            try { RunPml($"ADD {member} TO {arrayElem}"); return $"Added {member} to {arrayElem}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ArrayRemove(string arrayElem, int index)
        {
            try { RunPml($"REMOVE {index} FROM {arrayElem}"); return $"Removed index {index} from {arrayElem}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

        public string ArraySort(string arrayElem)
        {
            try { RunPml($"{arrayElem}.SORT()"); return $"Sorted {arrayElem}."; }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
        }

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
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    RunPml($"VAR !MCPSPEC{rnd} EVALUATE (Q SPCO {specName})");
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                    string result = cmd.GetPMLVariableString($"MCPSPEC{rnd}");
                    if (!string.IsNullOrWhiteSpace(result))
                        sb.AppendLine($"  Details: {result}");
                    try { RunPml($"VAR !MCPSPEC{rnd} DELETE"); } catch { }
                }
                catch { }
                return sb.ToString();
            }
            catch (Exception ex) { return $"Error: {ex.Message}"; }
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
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    RunPml($"VAR !MCPBOM{rnd} EVALUATE (BOM {name})");
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                    string result = cmd.GetPMLVariableString($"MCPBOM{rnd}");
                    try { RunPml($"VAR !MCPBOM{rnd} DELETE"); } catch { }
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
                catch
                {
                    sb.AppendLine("BOM executed. Check E3D console for listing.");
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

        public string ClashCheck(string name1, string name2)
        {
            if (string.IsNullOrWhiteSpace(name1)) return "Error: type_or_name1 required";
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

                // Approach 1: Check clash array/list via PML
                try
                {
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    string vn = $"MCC{rnd}";
                    RunPml($"VAR !{vn} EVALUATE (CLASHLIST)");
                    string clashList = cmd.GetPMLVariableString(vn);
                    try { RunPml($"VAR !{vn} DELETE"); } catch { }
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
                        int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                        string vn = $"MCC{rnd}";
                        RunPml($"Q CLASH");
                        RunPml($"VAR !{vn} EVALUATE (!!CLASHCOUNT)");
                        string count = cmd.GetPMLVariableString(vn);
                        try { RunPml($"VAR !{vn} DELETE"); } catch { }
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
                    int rnd = Math.Abs(Guid.NewGuid().GetHashCode()) % 100000;
                    RunPml($"VAR !MCPCHK{rnd} EVALUATE (CHECK {name})");
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                    string result = cmd.GetPMLVariableString($"MCPCHK{rnd}");
                    try { RunPml($"VAR !MCPCHK{rnd} DELETE"); } catch { }
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

        private static DbElementType ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
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
            var c = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(cmd);
            c.RunInPdms();
        }

        private static bool TryRunPml(string cmd)
        {
            var c = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(cmd);
            return c.RunInPdms();
        }

        private static string GetPmlGlobal(string varName)
        {
            try
            {
                // Use CommandLine to read PML global variables
                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand("");
                string name = varName.Replace("!!", "").ToUpper();
                return cmd.GetPMLVariableString(name);
            }
            catch { return ""; }
        }
    }
}
