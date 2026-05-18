using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace E3DPlugin.Models
{
    /// <summary>
    /// E3D 管道属性 — 匹配中间层 e3d_adapter.py 的 E3DPipelineAttribute dataclass
    /// </summary>
    public class E3DPipelineAttribute
    {
        /// <summary>管道编号 (如 "P-101")</summary>
        [JsonProperty("pipeline_id")]
        public string PipelineId { get; set; } = "";

        /// <summary>管线号 (如 "10-P-101")</summary>
        [JsonProperty("line_number")]
        public string LineNumber { get; set; } = "";

        /// <summary>公称管径 NPS (inch)</summary>
        [JsonProperty("nominal_pipe_size")]
        public double NominalPipeSize { get; set; }

        /// <summary>管径等级 (如 "SCH40", "SCH80")</summary>
        [JsonProperty("schedule")]
        public string Schedule { get; set; } = "";

        /// <summary>外径 OD (mm)</summary>
        [JsonProperty("outer_diameter")]
        public double OuterDiameter { get; set; }

        /// <summary>壁厚 WT (mm)</summary>
        [JsonProperty("wall_thickness")]
        public double WallThickness { get; set; }

        /// <summary>材料规格 (如 "A106 Gr.B")</summary>
        [JsonProperty("material_spec")]
        public string MaterialSpec { get; set; } = "";

        /// <summary>材料类别 (如 "Carbon Steel")</summary>
        [JsonProperty("material_category")]
        public string MaterialCategory { get; set; } = "";

        /// <summary>设计压力 (MPa)</summary>
        [JsonProperty("design_pressure")]
        public double DesignPressure { get; set; }

        /// <summary>设计温度 (°C)</summary>
        [JsonProperty("design_temperature")]
        public double DesignTemperature { get; set; }

        /// <summary>
        /// 连接类型: butt_weld / socket_weld / threaded / flanged / unknown
        /// </summary>
        [JsonProperty("connection_type")]
        public string ConnectionType { get; set; } = "unknown";

        /// <summary>
        /// 实体类型: pipe / pressure_vessel / storage_tank / heat_exchanger / flange / valve
        /// </summary>
        [JsonProperty("entity_type")]
        public string EntityType { get; set; } = "pipe";
    }

    /// <summary>
    /// 验证请求 — 匹配中间层 e3d_middleware_api.py 的 ValidationRequest
    /// </summary>
    public class ValidationRequest
    {
        [JsonProperty("pipelines")]
        public List<E3DPipelineAttribute> Pipelines { get; set; } = new List<E3DPipelineAttribute>();

        [JsonProperty("project_id")]
        public string ProjectId { get; set; } = "GLOBAL";

        [JsonProperty("standard")]
        public string Standard { get; set; } = "ASME B31.3";

        [JsonProperty("top_k")]
        public int TopK { get; set; } = 5;
    }

    /// <summary>
    /// 验证结果响应 — 匹配中间层 ValidationResponse
    /// </summary>
    public class ValidationResponse
    {
        [JsonProperty("request_id")]
        public string RequestId { get; set; } = "";

        [JsonProperty("project_id")]
        public string ProjectId { get; set; } = "";

        [JsonProperty("standard")]
        public string Standard { get; set; } = "";

        [JsonProperty("total_pipelines")]
        public int TotalPipelines { get; set; }

        [JsonProperty("total_violations")]
        public int TotalViolations { get; set; }

        [JsonProperty("pipeline_results")]
        public List<PipelineValidationResult> PipelineResults { get; set; }
            = new List<PipelineValidationResult>();
    }

    public class PipelineValidationResult
    {
        [JsonProperty("pipeline_id")]
        public string PipelineId { get; set; } = "";

        [JsonProperty("line_number")]
        public string LineNumber { get; set; } = "";

        [JsonProperty("overall_status")]
        public string OverallStatus { get; set; } = "";

        [JsonProperty("violation_count")]
        public int ViolationCount { get; set; }

        [JsonProperty("rule_results")]
        public List<RuleResult> RuleResults { get; set; } = new List<RuleResult>();

        [JsonProperty("pml_workflows")]
        public List<object> PmlWorkflows { get; set; } = new List<object>();
    }

    public class RuleResult
    {
        [JsonProperty("rule_id")]
        public string RuleId { get; set; } = "";

        [JsonProperty("rule_type")]
        public string RuleType { get; set; } = "";

        [JsonProperty("status")]
        public string Status { get; set; } = "";

        [JsonProperty("severity")]
        public string Severity { get; set; } = "";

        [JsonProperty("clause_reference")]
        public string ClauseReference { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("calculated_values")]
        public Dictionary<string, double> CalculatedValues { get; set; }
            = new Dictionary<string, double>();

        [JsonProperty("formula")]
        public Dictionary<string, object> Formula { get; set; }
            = new Dictionary<string, object>();

        [JsonProperty("trigger_logic")]
        public string TriggerLogic { get; set; } = "";

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; }
            = new Dictionary<string, object>();
    }

    /// <summary>
    /// E3D 管道数据提取结果
    /// </summary>
    public class ExtractionResult
    {
        public bool Success { get; set; }
        public E3DPipelineAttribute Attribute { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string ElementName { get; set; } = "";
        public string ElementType { get; set; } = "";
    }
}
