using System;

namespace E3DPlugin.Services
{
    /// <summary>
    /// E3D/PDMS 元素访问抽象 — 隔离 AVEVA API 依赖，便于单元测试。
    /// </summary>
    public interface IE3DElement
    {
        /// <summary>元素名称</summary>
        string Name { get; }

        /// <summary>元素类型 (PIPE/BRAN/SPEC/SPCO/NOZZ 等)</summary>
        string ElementType { get; }

        /// <summary>获取字符串属性</summary>
        string GetString(string attributeName);

        /// <summary>获取数值属性，失败返回 defaultValue</summary>
        double GetDouble(string attributeName, double defaultValue = 0.0);

        /// <summary>获取 bool 属性</summary>
        bool GetBool(string attributeName, bool defaultValue = false);

        /// <summary>获取父级元素</summary>
        IE3DElement GetParent();

        /// <summary>获取第一个指定类型的子元素</summary>
        IE3DElement GetFirstChild(string elementType);

        /// <summary>获取所有直接子元素</summary>
        IE3DElement[] GetChildren();

        /// <summary>通过引用属性获取目标元素（如 SPREF）</summary>
        IE3DElement GetReference(string attributeName);
    }

    /// <summary>
    /// E3D 应用上下文 — 提供当前选中元素和项目信息
    /// </summary>
    public interface IE3DContext
    {
        /// <summary>当前选中的元素</summary>
        IE3DElement CurrentElement { get; }

        /// <summary>项目 ID</summary>
        string ProjectId { get; }

        /// <summary>显示消息给用户</summary>
        void ShowMessage(string message);

        /// <summary>显示错误给用户</summary>
        void ShowError(string message);
    }
}
