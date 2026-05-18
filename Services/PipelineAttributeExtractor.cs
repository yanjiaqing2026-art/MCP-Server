using System;
using System.Collections.Generic;
using E3DPlugin.Models;

namespace E3DPlugin.Services
{
    /// <summary>
    /// 从 E3D 选中元素提取管道属性。
    /// 管道属性分布在 PDMS/E3D 的多个层级: BRAN(分支) → PIPE(管道段) → SPEC(规格)
    /// </summary>
    public class PipelineAttributeExtractor
    {
        /// <summary>
        /// 从当前选中的 E3D 元素提取管道属性。
        /// 选中元素可以是 PIPE、BRAN、SPEC 或管道子组件，提取器会自动导航到正确的层级。
        /// </summary>
        public ExtractionResult Extract(IE3DContext context)
        {
            return ExtractFromElement(context.CurrentElement);
        }

        /// <summary>
        /// 从指定元素提取管道属性
        /// </summary>
        public ExtractionResult ExtractFromElement(IE3DElement element)
        {
            var result = new ExtractionResult
            {
                ElementName = element?.Name ?? "(null)",
                ElementType = element?.ElementType ?? "(null)"
            };

            if (element == null)
            {
                result.ErrorMessage = "未选中任何元素";
                return result;
            }

            try
            {
                // 导航到正确的层级
                var pipe = NavigateToPipe(element);
                if (pipe == null)
                {
                    result.ErrorMessage = $"无法找到 PIPE 元素（当前选中: {element.ElementType} {element.Name}）";
                    return result;
                }

                var branch = pipe.GetParent();
                var spec = pipe.GetReference("SPREF");
                if (spec == null)
                {
                    spec = branch?.GetReference("SPREF");
                }

                var attr = new E3DPipelineAttribute
                {
                    PipelineId = pipe.Name,
                    LineNumber = branch?.Name ?? pipe.Name,

                    // 尺寸 — 来自 PIPE
                    OuterDiameter = pipe.GetDouble("ODIA"),
                    WallThickness = pipe.GetDouble("WALL"),

                    // 口径 — 来自 BRAN/BRANCH
                    NominalPipeSize = branch?.GetDouble("SBOR") ?? pipe.GetDouble("SBOR"),
                    Schedule = branch?.GetString("SCHD") ?? "",

                    // 材料 — 来自 SPEC 或 PIPE 的 MTYP/SPREF 描述
                    MaterialSpec = ExtractMaterialSpec(pipe, spec),
                    MaterialCategory = ExtractMaterialCategory(spec),

                    // 设计条件 — 来自 PIPE
                    DesignPressure = pipe.GetDouble("PRES"),
                    DesignTemperature = pipe.GetDouble("TEMP"),

                    // 连接类型
                    ConnectionType = ExtractConnectionType(pipe),

                    EntityType = "pipe"
                };

                // 校正: 如果 OD 为 0 但 NPS 有值，用 NPS 推算 OD
                if (attr.OuterDiameter <= 0 && attr.NominalPipeSize > 0)
                {
                    attr.OuterDiameter = NpsToOd(attr.NominalPipeSize);
                }

                // 校正: 如果 Schedule 为空但找到了 SPEC 名称，尝试从中提取
                if (string.IsNullOrEmpty(attr.Schedule) && spec != null)
                {
                    attr.Schedule = ExtractScheduleFromSpec(spec);
                }

                result.Success = true;
                result.Attribute = attr;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"提取属性失败: {ex.Message}";
            }

            return result;
        }

        private IE3DElement NavigateToPipe(IE3DElement element)
        {
            var etype = element.ElementType.ToUpperInvariant();

            // 直接就是 PIPE
            if (etype == "PIPE" || etype == "PIPE_COMPONENT")
                return element;

            // BRAN/BRANCH → 取第一个 PIPE 子元素
            if (etype == "BRAN" || etype == "BRANCH")
                return element.GetFirstChild("PIPE")
                    ?? element.GetFirstChild("PCOM");

            // SPEC → 从第一个 PIPE 子元素上找
            if (etype == "SPEC")
                return element.GetFirstChild("PIPE");

            // SPCO/SPCOM → 从父级找
            if (etype == "SPCO" || etype == "SPCOM")
                return NavigateToPipe(element.GetParent());

            // NOZZ/VALV/FLAN/ELBO/TEE 等 → 父级
            if (etype == "NOZZ" || etype == "NOZZLE"
                || etype == "VALV" || etype == "VALVE"
                || etype == "FLAN" || etype == "FLANGE"
                || etype == "ELBO" || etype == "ELBOW"
                || etype == "TEE" || etype == "REDUCER"
                || etype == "GASK" || etype == "GASKET"
                || etype == "BOLT")
            {
                var parent = element.GetParent();
                return parent != null ? NavigateToPipe(parent) : null;
            }

            // 其他 — 向上找 PIPE
            var current = element;
            for (int i = 0; i < 5 && current != null; i++)
            {
                current = current.GetParent();
                if (current != null)
                {
                    var ct = current.ElementType.ToUpperInvariant();
                    if (ct == "PIPE" || ct == "PIPE_COMPONENT")
                        return current;
                    if (ct == "BRAN" || ct == "BRANCH")
                        return current.GetFirstChild("PIPE");
                }
            }

            return null;
        }

        private string ExtractMaterialSpec(IE3DElement pipe, IE3DElement spec)
        {
            // 优先使用 MTYP 属性
            var mtyp = pipe.GetString("MTYP");
            if (!string.IsNullOrEmpty(mtyp))
                return mtyp;

            // 从 SPEC 元素取
            if (spec != null)
            {
                var name = spec.Name;
                if (!string.IsNullOrEmpty(name))
                    return name;

                var spDesc = spec.GetString("DESC");
                if (!string.IsNullOrEmpty(spDesc))
                    return spDesc;
            }

            // 从 SPREF 属性描述中取
            var spref = pipe.GetString("SPREF");
            if (!string.IsNullOrEmpty(spref))
                return spref;

            return "";
        }

        private string ExtractMaterialCategory(IE3DElement spec)
        {
            if (spec == null)
                return "";

            // TXSP/MTYP/PCOM 可能包含材料类别信息
            var cat = spec.GetString("TXSP");
            if (!string.IsNullOrEmpty(cat))
                return cat;

            cat = spec.GetString("MTYP");
            if (!string.IsNullOrEmpty(cat))
                return cat;

            cat = spec.GetString("PURP");
            if (!string.IsNullOrEmpty(cat))
                return cat;

            return "";
        }

        private string ExtractConnectionType(IE3DElement pipe)
        {
            // 管道端的连接类型通常记录在 FUNC/PURP 或通过末端检测
            var func = pipe.GetString("FUNC");
            if (!string.IsNullOrEmpty(func))
            {
                var fl = func.ToUpperInvariant();
                if (fl.Contains("WELD") || fl.Contains("BW")) return "butt_weld";
                if (fl.Contains("SW") || fl.Contains("SOCKET")) return "socket_weld";
                if (fl.Contains("THD") || fl.Contains("THREAD")) return "threaded";
                if (fl.Contains("FLG") || fl.Contains("FLANGE")) return "flanged";
            }

            // 默认: 对接焊是最常见的工业管道连接
            return "butt_weld";
        }

        private string ExtractScheduleFromSpec(IE3DElement spec)
        {
            var name = spec.Name?.ToUpperInvariant() ?? "";
            // SPEC 名称常见格式: "A106_GRB_SCH40" 或包含 "SCH40"
            foreach (var sch in new[] { "SCH5", "SCH10", "SCH20", "SCH30",
                "SCH40", "SCH60", "SCH80", "SCH100", "SCH120",
                "SCH140", "SCH160", "SCH5S", "SCH10S", "SCH40S", "SCH80S",
                "XS", "XXS", "STD" })
            {
                if (name.Contains(sch))
                    return sch;
            }
            return "";
        }

        /// <summary>
        /// NPS (inch) 到 外径 (mm) 的近似对照 — ASME B36.10
        /// </summary>
        private static double NpsToOd(double nps)
        {
            var map = new Dictionary<double, double>
            {
                [0.5] = 21.3,  [0.75] = 26.7, [1.0] = 33.4,
                [1.25] = 42.2, [1.5] = 48.3,  [2.0] = 60.3,
                [2.5] = 73.0,  [3.0] = 88.9,  [3.5] = 101.6,
                [4.0] = 114.3, [5.0] = 141.3, [6.0] = 168.3,
                [8.0] = 219.1, [10.0] = 273.1, [12.0] = 323.9,
                [14.0] = 355.6, [16.0] = 406.4, [18.0] = 457.0,
                [20.0] = 508.0, [24.0] = 610.0, [30.0] = 762.0,
                [36.0] = 914.0, [42.0] = 1067.0, [48.0] = 1219.0,
            };

            if (map.TryGetValue(nps, out var od))
                return od;

            // 大于 48" 的: OD(inch) ≈ NPS(inch) = NPS*25.4
            if (nps > 48)
                return nps * 25.4;

            return 0.0;
        }
    }
}
