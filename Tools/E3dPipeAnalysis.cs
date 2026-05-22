using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using E3DMcpServer.Models;
using Newtonsoft.Json.Linq;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// Pipe-design analysis helpers exposed as MCP tools.
    ///
    /// Phase 5 of the production-agent plan: the existing 86-tool surface had
    /// no slope check, no drain-hole suggestion, and no support-spacing logic.
    /// Those are hard requirements for any reasonably complete piping agent
    /// (process codes mandate liquid-line slope and low-point drains).
    ///
    /// The handlers are intentionally conservative: they read attributes via
    /// the existing E3dApiWrapper, compute deterministically, and return text
    /// summaries in the same shape as the rest of the tools. Threshold tables
    /// for slope and spacing are seeded with industry defaults; production
    /// callers should override via the input arguments.
    /// </summary>
    public static class E3dPipeAnalysis
    {
        // ── e3d_pipe_slope_check ────────────────────────────────────────
        // Walk a pipe's children (BRAN → component) in route order, read each
        // component's POS, compute the rise/run between consecutive ones, and
        // flag any segment that violates the requested slope tolerance.
        //
        // Input:
        //   pipe:string        full path to the pipe element (required)
        //   min_slope:number   minimum acceptable signed slope in %, default 0.5
        //                      (positive = slope downward in route direction)
        //   tolerance:number   slope absolute tolerance in %, default 0.1
        //
        // Output:
        //   Multiline summary with one line per violating segment plus a
        //   final overall verdict.
        public static ToolCallResult HandleSlopeCheck(JObject args, E3dApiWrapper api)
        {
            var pipe = args?["pipe"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(pipe)) return Err("请提供管道路径参数 'pipe'。");

            double minSlope = TryGetDouble(args, "min_slope", 0.5);
            double tolerance = TryGetDouble(args, "tolerance", 0.1);

            var components = api.GetChildren(pipe, null);
            if (components == null || components.Count < 2)
            {
                return Err($"管道 {pipe} 下没有足够的组件计算坡度 (≥2 个组件)。");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Slope check for {pipe}");
            sb.AppendLine($"  min_slope={minSlope:F2}%  tolerance=±{tolerance:F2}%");

            int violations = 0;
            for (int i = 1; i < components.Count; i++)
            {
                var prev = components[i - 1];
                var curr = components[i];
                if (!prev.X.HasValue || !curr.X.HasValue) continue;

                double dx = (curr.X ?? 0) - (prev.X ?? 0);
                double dy = (curr.Y ?? 0) - (prev.Y ?? 0);
                double dz = (curr.Z ?? 0) - (prev.Z ?? 0);
                double horizontal = Math.Sqrt(dx * dx + dy * dy);
                if (horizontal < 1.0) continue; // skip vertical runs entirely
                double slopePct = (-dz / horizontal) * 100.0;
                double drift = slopePct - minSlope;
                string verdict = Math.Abs(drift) <= tolerance
                    ? "OK"
                    : (drift < 0 ? "FLAT/REVERSED" : "STEEPER_THAN_TARGET");
                sb.AppendLine(
                    string.Format(CultureInfo.InvariantCulture,
                        "  [{0}] {1} -> {2}: slope={3:F2}% ({4})",
                        i, prev.Name, curr.Name, slopePct, verdict));
                if (verdict != "OK") violations++;
            }

            sb.AppendLine($"violations: {violations}");
            return Ok(sb.ToString());
        }

        // ── e3d_pipe_drain_holes ────────────────────────────────────────
        // Heuristic low-point finder: scan components, return the indices
        // whose Z is a local minimum relative to neighbours. Process design
        // typically asks for a drain at each such point.
        public static ToolCallResult HandleDrainHoles(JObject args, E3dApiWrapper api)
        {
            var pipe = args?["pipe"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(pipe)) return Err("请提供管道路径参数 'pipe'。");

            var components = api.GetChildren(pipe, null);
            if (components == null || components.Count < 3)
            {
                return Err($"管道 {pipe} 组件 <3, 无法判断低点。");
            }

            var lows = new List<int>();
            for (int i = 1; i < components.Count - 1; i++)
            {
                if (!components[i].Z.HasValue) continue;
                double z = components[i].Z ?? 0;
                double zPrev = components[i - 1].Z ?? z;
                double zNext = components[i + 1].Z ?? z;
                if (z < zPrev && z < zNext) lows.Add(i);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Drain hole suggestions for {pipe}: {lows.Count} low points");
            foreach (var i in lows)
            {
                var c = components[i];
                sb.AppendLine(
                    string.Format(CultureInfo.InvariantCulture,
                        "  低点: {0} @ ({1:F1},{2:F1},{3:F1}) — 建议安装 DN20 排液孔",
                        c.Name, c.X ?? 0, c.Y ?? 0, c.Z ?? 0));
            }
            return Ok(sb.ToString());
        }

        // ── e3d_support_spacing_plan ────────────────────────────────────
        // Compute suggested support positions along a pipe using a simple
        // ODIA-driven spacing table (ASME B31.3 Table 121.5-style defaults).
        // The piping engineer can tighten via the `spacing` argument.
        public static ToolCallResult HandleSupportSpacingPlan(JObject args, E3dApiWrapper api)
        {
            var pipe = args?["pipe"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(pipe)) return Err("请提供管道路径参数 'pipe'。");

            // Read OD so we can pick a default spacing if not overridden.
            var attrs = api.ReadAttributes(pipe, new[] { "ODIA" });
            double od = 0;
            if (attrs != null && attrs.TryGetValue("ODIA", out var raw) && raw != null)
            {
                double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out od);
            }

            double spacing = TryGetDouble(args, "spacing", DefaultSpacingForOD(od));
            if (spacing <= 0)
            {
                return Err($"无法决定支架间距 (OD={od}). 请提供 'spacing' 显式间距(mm)。");
            }

            var components = api.GetChildren(pipe, null);
            if (components == null || components.Count < 2)
            {
                return Err($"管道 {pipe} 组件 <2, 无法规划支架。");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Support spacing plan for {pipe} (OD={od}mm, spacing={spacing}mm)");
            double accum = 0;
            int suggestions = 0;
            for (int i = 1; i < components.Count; i++)
            {
                var prev = components[i - 1];
                var curr = components[i];
                if (!prev.X.HasValue || !curr.X.HasValue) continue;
                double dx = (curr.X ?? 0) - (prev.X ?? 0);
                double dy = (curr.Y ?? 0) - (prev.Y ?? 0);
                double dz = (curr.Z ?? 0) - (prev.Z ?? 0);
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                accum += len;
                if (accum >= spacing)
                {
                    sb.AppendLine(
                        string.Format(CultureInfo.InvariantCulture,
                            "  支架 {0}: {1} -> {2} 累计 {3:F0}mm",
                            ++suggestions, prev.Name, curr.Name, accum));
                    accum = 0;
                }
            }
            if (suggestions == 0) sb.AppendLine("  无需新增支架 (总长不足一个间距)。");
            sb.AppendLine($"total_suggestions: {suggestions}");
            return Ok(sb.ToString());
        }

        // ── helpers ─────────────────────────────────────────────────────

        private static double DefaultSpacingForOD(double od)
        {
            // ASME B31.3 Table 121.5 baseline (water service, mm).
            if (od <= 25) return 2100;
            if (od <= 50) return 3000;
            if (od <= 80) return 3700;
            if (od <= 100) return 4300;
            if (od <= 150) return 5200;
            if (od <= 200) return 5800;
            if (od <= 250) return 7000;
            if (od <= 300) return 7600;
            return 8500;
        }

        private static double TryGetDouble(JObject args, string key, double fallback)
        {
            if (args == null) return fallback;
            var t = args[key];
            if (t == null) return fallback;
            try { return t.Value<double>(); }
            catch
            {
                if (double.TryParse(t.Value<string>(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
                return fallback;
            }
        }

        private static ToolCallResult Ok(string text) => new ToolCallResult
        {
            Content = new List<ContentBlock> { new ContentBlock { Type = "text", Text = text } },
            IsError = false
        };

        private static ToolCallResult Err(string text) => new ToolCallResult
        {
            Content = new List<ContentBlock> { new ContentBlock { Type = "text", Text = text } },
            IsError = true
        };
    }
}
