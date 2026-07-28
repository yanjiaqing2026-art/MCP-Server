using System;
using System.Collections.Generic;
using System.Text;
using E3DMcpServer.Models;
using Newtonsoft.Json.Linq;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// True batch operations.
    ///
    /// The legacy <c>e3d_batch_set</c> only changes ONE attribute on N elements
    /// with the SAME value, and <c>e3d_element_create</c> is single-shot. For
    /// batch import / batch flange upgrade / batch nozzle creation workflows
    /// that is too coarse: the agent ends up calling the tool 100 times,
    /// each round-trip pays HTTP + PML overhead.
    ///
    /// These handlers accept a JSON array, route each operation through the
    /// existing API wrapper, and collect a structured per-item result.
    /// We do NOT try to wrap the whole array in a single E3D transaction —
    /// the existing API wrapper doesn't expose that, and the work to add it
    /// is out of scope. Instead we report per-item OK/FAIL so the agent can
    /// decide whether to rollback (using the runtime hook from Phase 2).
    /// </summary>
    public static class E3dBatchOps
    {
        // ── e3d_element_batch_create ────────────────────────────────────
        // Input:
        //   items: [ { type, name, owner? }, ... ]
        // Output:
        //   per-item OK / FAIL summary.
        public static ToolCallResult HandleBatchCreate(JObject args, E3dApiWrapper api)
        {
            var arr = args?["items"] as JArray;
            if (arr == null || arr.Count == 0) return Err("请提供 items 数组。");

            var sb = new StringBuilder();
            sb.AppendLine($"batch_create: {arr.Count} items");
            int ok = 0, fail = 0;

            for (int i = 0; i < arr.Count; i++)
            {
                var item = arr[i] as JObject;
                if (item == null) { fail++; sb.AppendLine($"  [{i}] FAIL: not an object"); continue; }
                var type = item["type"]?.Value<string>();
                var name = item["name"]?.Value<string>();
                var owner = item["owner"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
                {
                    fail++;
                    sb.AppendLine($"  [{i}] FAIL: missing type/name");
                    continue;
                }
                try
                {
                    var result = api.CreateElement(type, name, owner);
                    if (string.IsNullOrEmpty(result) || result.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        fail++;
                        sb.AppendLine($"  [{i}] FAIL {type} {name}: {result}");
                    }
                    else
                    {
                        ok++;
                        sb.AppendLine($"  [{i}] OK {type} {name}");
                    }
                }
                catch (Exception ex)
                {
                    fail++;
                    sb.AppendLine($"  [{i}] FAIL {type} {name}: {ex.Message}");
                }
            }
            sb.AppendLine($"summary: ok={ok} fail={fail}");
            return ok == arr.Count ? Ok(sb.ToString()) : MaybeErr(sb.ToString(), fail == arr.Count);
        }

        // ── e3d_attr_batch_set_multi ────────────────────────────────────
        // Input:
        //   updates: [ { name, attrs: { ATTR: VALUE, ... } }, ... ]
        // Output:
        //   per-element OK / FAIL summary, with attribute-level errors.
        public static ToolCallResult HandleAttrBatchSetMulti(JObject args, E3dApiWrapper api)
        {
            var arr = args?["updates"] as JArray;
            if (arr == null || arr.Count == 0) return Err("请提供 updates 数组。");

            var sb = new StringBuilder();
            sb.AppendLine($"attr_batch_set_multi: {arr.Count} elements");
            int okElems = 0, failElems = 0, attrCount = 0, attrFails = 0;

            for (int i = 0; i < arr.Count; i++)
            {
                var item = arr[i] as JObject;
                if (item == null) { failElems++; sb.AppendLine($"  [{i}] FAIL: not an object"); continue; }
                var name = item["name"]?.Value<string>();
                var attrs = item["attrs"] as JObject;
                if (string.IsNullOrWhiteSpace(name) || attrs == null || attrs.Count == 0)
                {
                    failElems++;
                    sb.AppendLine($"  [{i}] FAIL: missing name or attrs");
                    continue;
                }

                int innerFail = 0;
                foreach (var prop in attrs)
                {
                    attrCount++;
                    var attrName = prop.Key;
                    var value = prop.Value?.Value<string>() ?? string.Empty;
                    try
                    {
                        var result = api.SetAttribute(name, attrName, value);
                        if (string.IsNullOrEmpty(result) || result.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                        {
                            innerFail++;
                            attrFails++;
                            sb.AppendLine($"    {name}.{attrName} FAIL: {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        innerFail++;
                        attrFails++;
                        sb.AppendLine($"    {name}.{attrName} FAIL: {ex.Message}");
                    }
                }
                if (innerFail == 0) { okElems++; sb.AppendLine($"  [{i}] OK {name} ({attrs.Count} attrs)"); }
                else { failElems++; sb.AppendLine($"  [{i}] PARTIAL {name}: {innerFail}/{attrs.Count} attrs failed"); }
            }
            sb.AppendLine($"summary: elements_ok={okElems} elements_fail={failElems} attr_writes={attrCount} attr_fails={attrFails}");
            // P2-C 巡检修：全部元素失败时 IsError 也要为真（原来写死 false，与 batch_create 口径不一致）。
            return failElems == 0 ? Ok(sb.ToString()) : MaybeErr(sb.ToString(), okElems == 0);
        }

        // ── helpers ─────────────────────────────────────────────────────

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

        /// <summary>Mark as Err only when everything failed; otherwise return Ok so the agent can still see partials.</summary>
        private static ToolCallResult MaybeErr(string text, bool allFailed)
        {
            return allFailed ? Err(text) : Ok(text);
        }
    }
}
