using System;
using System.Collections.Generic;
using System.Linq;

namespace E3DPlugin.Services
{
    /// <summary>
    /// Mock IE3DElement — 用于脱离 E3D 的独立测试和开发。
    /// 模拟典型 E3D 管道层级: BRAN → PIPE → (SPREF → SPEC)
    /// </summary>
    public class MockE3DElement : IE3DElement
    {
        private readonly Dictionary<string, object> _attributes
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public string Name { get; set; } = "";
        public string ElementType { get; set; } = "";

        private MockE3DElement _parent;
        private readonly List<MockE3DElement> _children = new List<MockE3DElement>();
        private MockE3DElement _reference;

        public MockE3DElement(string name, string elementType)
        {
            Name = name;
            ElementType = elementType;
        }

        /// <summary>便捷: 设置属性值</summary>
        public MockE3DElement SetAttr(string name, object value)
        {
            _attributes[name.ToUpperInvariant()] = value;
            return this;
        }

        /// <summary>便捷: 设置父元素</summary>
        public MockE3DElement SetParent(MockE3DElement parent)
        {
            _parent = parent;
            if (parent != null && !parent._children.Contains(this))
                parent._children.Add(this);
            return this;
        }

        /// <summary>便捷: 添加子元素</summary>
        public MockE3DElement AddChild(MockE3DElement child)
        {
            child._parent = this;
            _children.Add(child);
            return this;
        }

        /// <summary>便捷: 设置引用目标 (如 SPREF → SPEC)</summary>
        public MockE3DElement SetReference(MockE3DElement target)
        {
            _reference = target;
            return this;
        }

        public string GetString(string attributeName)
        {
            if (_attributes.TryGetValue(attributeName.ToUpperInvariant(), out var val))
                return val?.ToString() ?? "";
            return "";
        }

        public double GetDouble(string attributeName, double defaultValue = 0.0)
        {
            if (_attributes.TryGetValue(attributeName.ToUpperInvariant(), out var val))
            {
                if (val is double d) return d;
                if (val is int i) return i;
                if (val is float f) return f;
                if (double.TryParse(val?.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        public bool GetBool(string attributeName, bool defaultValue = false)
        {
            if (_attributes.TryGetValue(attributeName.ToUpperInvariant(), out var val))
            {
                if (val is bool b) return b;
                if (val is string s && bool.TryParse(s, out var parsed)) return parsed;
            }
            return defaultValue;
        }

        public IE3DElement GetParent() => _parent;

        public IE3DElement GetFirstChild(string elementType)
        {
            return _children.FirstOrDefault(c =>
                c.ElementType.IndexOf(elementType, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public IE3DElement[] GetChildren()
        {
            return _children.Cast<IE3DElement>().ToArray();
        }

        public IE3DElement GetReference(string attributeName)
        {
            return _reference;
        }
    }

    /// <summary>
    /// Mock IE3DContext — 用于脱离 E3D 的独立测试和开发。
    /// </summary>
    public class MockE3DContext : IE3DContext
    {
        public IE3DElement CurrentElement { get; set; }
        public string ProjectId { get; set; } = "TEST-PROJECT";

        public string LastMessage { get; private set; }
        public string LastError { get; private set; }

        public void ShowMessage(string message)
        {
            LastMessage = message;
            Console.WriteLine($"[INFO] {message}");
        }

        public void ShowError(string message)
        {
            LastError = message;
            Console.WriteLine($"[ERROR] {message}");
        }

        /// <summary>
        /// 创建一个典型的测试用管道层级:
        ///   BRAN "10-P-101" → PIPE "P-101" → SPREF → SPEC "A106_GRB_SCH40"
        /// </summary>
        public static MockE3DContext CreateSamplePipe()
        {
            var spec = new MockE3DElement("A106_GRB_SCH40", "SPEC")
                .SetAttr("DESC", "A106 Gr.B Carbon Steel Seamless");

            var pipe = new MockE3DElement("P-101", "PIPE")
                .SetAttr("ODIA", 168.3)
                .SetAttr("WALL", 7.11)
                .SetAttr("PRES", 1.6)
                .SetAttr("TEMP", 150.0)
                .SetAttr("MTYP", "A106 Gr.B")
                .SetAttr("FUNC", "BW")
                .SetReference(spec);

            var branch = new MockE3DElement("10-P-101", "BRAN")
                .SetAttr("SBOR", 6.0)
                .SetAttr("SCHD", "SCH40")
                .SetAttr("SPREF", "A106_GRB_SCH40");
            branch.AddChild(pipe);
            pipe.SetParent(branch);

            return new MockE3DContext
            {
                CurrentElement = pipe,
                ProjectId = "DEMO-PROJECT"
            };
        }
    }
}
