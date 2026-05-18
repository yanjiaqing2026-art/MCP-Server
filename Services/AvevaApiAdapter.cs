using System;
using Aveva.Core.PMLNet;

namespace E3DPlugin.Services
{
    /// <summary>
    /// IE3DElement 实现 — 包装 PMLNetAny (E3D 3.1 PMLNet API)
    /// </summary>
    public class AvevaElement : IE3DElement
    {
        private readonly PMLNetAny _pml;

        public AvevaElement(PMLNetAny pml)
        {
            _pml = pml ?? throw new ArgumentNullException(nameof(pml));
        }

        public string Name => GetPmlString("NAME");

        public string ElementType
        {
            get
            {
                try { return _pml.type(); }
                catch { return "UNKNOWN"; }
            }
        }

        public string GetString(string attributeName)
        {
            return GetPmlString(attributeName.ToUpper());
        }

        public double GetDouble(string attributeName, double defaultValue = 0.0)
        {
            try
            {
                int token = _pml.token();
                object val = null;
                int rc = PMLNetEngine.GetProperty(token, attributeName.ToUpper(), ref val);
                if (rc == 0 && val != null)
                {
                    if (val is double d) return d;
                    if (val is float f) return f;
                    if (val is int i) return i;
                    if (double.TryParse(val.ToString(), out var parsed)) return parsed;
                }
            }
            catch { }
            return defaultValue;
        }

        public bool GetBool(string attributeName, bool defaultValue = false)
        {
            try
            {
                int token = _pml.token();
                object val = null;
                int rc = PMLNetEngine.GetProperty(token, attributeName.ToUpper(), ref val);
                if (rc == 0 && val != null)
                {
                    if (val is bool b) return b;
                    if (bool.TryParse(val.ToString(), out var parsed)) return parsed;
                }
            }
            catch { }
            return defaultValue;
        }

        public IE3DElement GetParent()
        {
            try
            {
                // OWNER is a structure member (navigable parent relationship)
                object result = null;
                if (_pml.getStructureMember("OWNER", ref result) && result is PMLNetAny owner)
                    return new AvevaElement(owner);
            }
            catch { }

            try
            {
                // Fallback: OWNER may also be readable as a pseudo-attribute
                int token = _pml.token();
                object val = null;
                int rc = PMLNetEngine.GetProperty(token, "OWNER", ref val);
                if (rc == 0 && val is PMLNetAny owner)
                    return new AvevaElement(owner);
            }
            catch { }
            return null;
        }

        public IE3DElement GetFirstChild(string elementType)
        {
            try
            {
                // PML: !!CE.MEMBERS() 返回成员数组
                object membersResult = null;
                var args = new object[] { };
                _pml.invokeMethod("MEMBERS", args, 0, ref membersResult);

                if (!(membersResult is PMLNetAny members))
                    return null;

                int token = members.token();
                object sizeObj = null;
                PMLNetEngine.GetProperty(token, "SIZE", ref sizeObj);

                int size = Convert.ToInt32(sizeObj);
                // PML 数组从 1 开始
                for (int i = 1; i <= size && i <= 1000; i++)
                {
                    try
                    {
                        object childResult = null;
                        var idxArgs = new object[] { i };
                        members.invokeMethod("GET", idxArgs, 1, ref childResult);

                        if (childResult is PMLNetAny child)
                        {
                            var childType = child.type();
                            if (childType.IndexOf(elementType,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                                return new AvevaElement(child);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        public IE3DElement[] GetChildren()
        {
            return Array.Empty<IE3DElement>();
        }

        public IE3DElement GetReference(string attributeName)
        {
            var attr = attributeName.ToUpper();
            try
            {
                // PML: !!CE.SPREF navigates to the referenced element directly.
                // Try getStructureMember first — this is the native PML navigation.
                object result = null;
                if (_pml.getStructureMember(attr, ref result) && result is PMLNetAny pmlRef)
                    return new AvevaElement(pmlRef);
            }
            catch { }

            try
            {
                // Fallback: GetProperty may return the reference as a PMLNetAny or a numeric token.
                int token = _pml.token();
                object val = null;
                int rc = PMLNetEngine.GetProperty(token, attr, ref val);
                if (rc == 0 && val != null)
                {
                    if (val is PMLNetAny pmlRef)
                        return new AvevaElement(pmlRef);

                    // If it's a numeric reference token, try resolving via invokeMethod.
                    // In PML, ref attributes can be dereferenced by calling them as a no-arg method.
                    if (val is int refToken && refToken > 0)
                    {
                        object deref = null;
                        _pml.invokeMethod(attr, new object[] { }, 0, ref deref);
                        if (deref is PMLNetAny derefElement)
                            return new AvevaElement(derefElement);
                    }
                }
            }
            catch { }
            return null;
        }

        private string GetPmlString(string attrName)
        {
            try
            {
                int token = _pml.token();
                object val = null;
                int rc = PMLNetEngine.GetProperty(token, attrName, ref val);
                if (rc == 0 && val != null)
                    return val.ToString();
            }
            catch { }
            return "";
        }
    }

    /// <summary>
    /// IE3DContext 实现 — 通过 PMLNetAny.getGlobalVariable("!!CE") 获取 E3D 当前选中元素
    /// </summary>
    public class AvevaE3DContext : IE3DContext
    {
        public IE3DElement CurrentElement
        {
            get
            {
                try
                {
                    // E3D 3.1: try the standard PMLNet global variable for current element
                    var ce = GetGlobalVariableSafe("!!CE")
                          ?? GetGlobalVariableSafe("CE");
                    if (ce == null) return null;
                    var t = ce.type();
                    if (string.IsNullOrEmpty(t) || t == "UNSET" || t == "??" || t == "UNSET ")
                        return null;
                    return new AvevaElement(ce);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>Safe wrapper: try getGlobalVariable, return null on failure.</summary>
        private static PMLNetAny GetGlobalVariableSafe(string name)
        {
            try { return PMLNetAny.getGlobalVariable(name); }
            catch { return null; }
        }

        public string ProjectId
        {
            get
            {
                try
                {
                    var proj = PMLNetAny.getGlobalVariable("!!PROJECT");
                    if (proj != null)
                    {
                        int token = proj.token();
                        object val = null;
                        PMLNetEngine.GetProperty(token, "NAME", ref val);
                        return val?.ToString() ?? "GLOBAL";
                    }
                }
                catch { }
                return "GLOBAL";
            }
        }

        public void ShowMessage(string message)
        {
            try
            {
                // 输出到 E3D 控制台: $P 'message'
                var msg = PMLNetAny.newTemporary("STRING", new object[] { message }, 1);
                var printFunc = PMLNetAny.getGlobalVariable("!!$P");
                if (printFunc != null)
                {
                    object unused = null;
                    printFunc.invokeMethod("", new object[] { message }, 1, ref unused);
                }
            }
            catch { }
        }

        public void ShowError(string message)
        {
            ShowMessage("ERROR: " + message);
        }
    }
}
