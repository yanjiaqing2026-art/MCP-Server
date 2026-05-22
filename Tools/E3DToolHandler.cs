using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using E3DMcpServer.Models;
using Newtonsoft.Json.Linq;

namespace E3DMcpServer.Tools
{
    public class E3DToolHandler
    {
        private readonly E3dApiWrapper _api;

        public E3DToolHandler(E3dApiWrapper api)
        {
            _api = api;
        }

        public List<ToolDefinition> GetToolDefinitions()
        {
            return E3dToolDefinitions.All;
        }

        public ToolCallResult ExecuteTool(string toolName, JObject args)
        {
            try
            {
                switch (toolName)
                {
                    // ── 查询 ──
                    case "e3d_ce_get":             return HandleGetCurrent();
                    case "e3d_element_get":        return HandleGetElement(args);
                    case "e3d_element_children":   return HandleChildren(args);
                    case "e3d_element_owner":      return HandleOwner(args);
                    case "e3d_attr_read":          return HandleAttrRead(args);
                    case "e3d_search":             return HandleSearch(args);
                    case "e3d_project_info":       return HandleProjectInfo();
                    case "e3d_measure":            return HandleMeasure(args);
                    case "e3d_element_type":       return HandleElementType(args);
                    case "e3d_element_path":       return HandleElementPath(args);

                    // ── 修改 ──
                    case "e3d_attr_set":           return HandleAttrSet(args);
                    case "e3d_element_create":     return HandleCreate(args);
                    case "e3d_element_delete":     return HandleDelete(args);
                    case "e3d_element_rename":     return HandleRename(args);
                    case "e3d_element_copy":       return HandleCopy(args);
                    case "e3d_element_move":       return HandleMove(args);

                    // ── PML ──
                    case "e3d_pml_exec":           return HandlePmlExec(args);
                    case "e3d_pml_eval":           return HandlePmlEval(args);

                    // ── 批量 ──
                    case "e3d_batch_read":         return HandleBatchRead(args);
                    case "e3d_batch_set":          return HandleBatchSet(args);
                    case "e3d_collect":            return HandleCollect(args);
                    case "e3d_pipeline_export":    return HandlePipelineExport(args);

                    // ── 导航 ──
                    case "e3d_navigate":           return HandleNavigate(args);
                    case "e3d_select":             return HandleSelect(args);

                    // ── 数据库 ──
                    case "e3d_db_save":            return HandleDbSave(args);
                    case "e3d_db_changes":         return HandleDbChanges(args);
                    case "e3d_db_undo":            return HandleDbUndo(args);
                    case "e3d_db_extract":         return HandleDbExtract(args);

                    // ── 元素生命周期 ──
                    case "e3d_element_exists":     return HandleElementExists(args);
                    case "e3d_element_equals":     return HandleElementEquals(args);
                    case "e3d_element_revert":     return HandleElementRevert(args);
                    case "e3d_element_dump":       return HandleElementDump(args);

                    // ── 层次导航 ──
                    case "e3d_occurrence":         return HandleOccurrence(args);
                    case "e3d_siblings":           return HandleSiblings(args);
                    case "e3d_world":              return HandleWorld(args);
                    case "e3d_wrt":                return HandleWrt(args);

                    // ── 属性 ──
                    case "e3d_attr_list":          return HandleAttrList(args);
                    case "e3d_attr_info":          return HandleAttrInfo(args);

                    // ── 几何 ──
                    case "e3d_pos_set":            return HandlePosSet(args);
                    case "e3d_pos_move":           return HandlePosMove(args);
                    case "e3d_orientation_get":    return HandleOrientationGet(args);
                    case "e3d_rotate":             return HandleRotate(args);
                    case "e3d_reverse":            return HandleReverse(args);

                    // ── 连接 ──
                    case "e3d_connect":            return HandleConnect(args);
                    case "e3d_disconnect":         return HandleDisconnect(args);

                    // ── 阵列 ──
                    case "e3d_array_add":          return HandleArrayAdd(args);
                    case "e3d_array_remove":       return HandleArrayRemove(args);
                    case "e3d_array_sort":         return HandleArraySort(args);

                    // ── 规格/目录 ──
                    case "e3d_spec_query":         return HandleSpecQuery(args);
                    case "e3d_bom":                return HandleBom(args);
                    case "e3d_component_info":     return HandleComponentInfo(args);

                    // ── 视图 ──
                    case "e3d_view_zoom":          return HandleViewZoom(args);
                    case "e3d_view_fit":           return HandleViewFit(args);
                    case "e3d_view_colour":        return HandleViewColour(args);

                    // ── 碰撞/检查 ──
                    case "e3d_clash_check":        return HandleClashCheck(args);
                    case "e3d_design_check":       return HandleDesignCheck(args);

                    // ── 管道操作 ──
                    case "e3d_pipe_cut":           return HandlePipeCut(args);
                    case "e3d_pipe_gap":           return HandlePipeGap(args);
                    case "e3d_pipe_join":          return HandlePipeJoin(args);
                    case "e3d_pipe_bend":          return HandlePipeBend(args);
                    case "e3d_pipe_tee":           return HandlePipeTee(args);
                    case "e3d_pipe_valve":         return HandlePipeValve(args);
                    case "e3d_pipe_flange":        return HandlePipeFlange(args);
                    case "e3d_pipe_reducer":       return HandlePipeReducer(args);
                    case "e3d_pipe_route":         return HandlePipeRoute(args);

                    // ── 元素创建补充 ──
                    case "e3d_support_create":     return HandleSupportCreate(args);
                    case "e3d_weld_create":        return HandleWeldCreate(args);
                    case "e3d_label_create":       return HandleLabelCreate(args);

                    // ── 属性补充 ──
                    case "e3d_attr_clear":         return HandleAttrClear(args);
                    case "e3d_attr_copy":          return HandleAttrCopy(args);

                    // ── 可见性 ──
                    case "e3d_show":               return HandleShow(args);
                    case "e3d_hide":               return HandleHide(args);
                    case "e3d_view_iso":           return HandleViewIso(args);
                    case "e3d_view_plan":          return HandleViewPlan(args);
                    case "e3d_view_elevation":     return HandleViewElevation(args);

                    // ── 数据库补充 ──
                    case "e3d_db_claim":           return HandleDbClaim(args);
                    case "e3d_db_release":         return HandleDbRelease(args);

                    // ── 出图 ──
                    case "e3d_draft_iso":          return HandleDraftIso(args);

                    // ── 定位补充 ──
                    case "e3d_pos_at":             return HandlePosAt(args);
                    case "e3d_pos_dist":           return HandlePosDist(args);
                    case "e3d_pos_dir":            return HandlePosDir(args);
                    case "e3d_pos_ori":            return HandlePosOri(args);
                    case "e3d_pos_thr":            return HandlePosThr(args);

                    // ── 元素操作补充 ──
                    case "e3d_element_flip":       return HandleElementFlip(args);
                    case "e3d_force_connect":      return HandleForceConnect(args);

                    // ── 会话 ──
                    case "e3d_session_status":     return HandleSessionStatus(args);

                    // ── Phase 5 — 工程分析 ──
                    case "e3d_pipe_slope_check":       return E3dPipeAnalysis.HandleSlopeCheck(args, _api);
                    case "e3d_pipe_drain_holes":       return E3dPipeAnalysis.HandleDrainHoles(args, _api);
                    case "e3d_support_spacing_plan":   return E3dPipeAnalysis.HandleSupportSpacingPlan(args, _api);

                    // ── Phase 5 — 真批量 ──
                    case "e3d_element_batch_create":   return E3dBatchOps.HandleBatchCreate(args, _api);
                    case "e3d_attr_batch_set_multi":   return E3dBatchOps.HandleAttrBatchSetMulti(args, _api);

                    // ── Phase 5 — 事件订阅 ──
                    case "e3d_subscribe":              return E3dEventBus.HandleSubscribe(args);
                    case "e3d_unsubscribe":            return E3dEventBus.HandleUnsubscribe(args);
                    case "e3d_poll_events":            return E3dEventBus.HandlePollEvents(args);

                    default: return Err($"Unknown tool: {toolName}");
                }
            }
            catch (Exception ex) { return Err($"Tool error: {ex.Message}"); }
        }

        // ── 查询 handlers ──

        ToolCallResult HandleGetCurrent()
        {
            var e = _api.GetCurrentElement();
            if (e == null) return Err("当前没有选中元素。请在E3D中选中一个元素。");
            return Ok(Format(e));
        }

        ToolCallResult HandleGetElement(JObject a)
        {
            var name = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(name)) return Err("请提供元素名称参数'name'。");
            var e = _api.GetElement(name);
            if (e == null) return Err($"未找到元素'{name}'。请使用完整路径如/PIPE-101。");
            return Ok(Format(e));
        }

        ToolCallResult HandleChildren(JObject a)
        {
            var name = a?["name"]?.Value<string>();
            var filter = a?["type_filter"]?.Value<string>();
            var list = _api.GetChildren(name, filter);
            if (list.Count == 0) return Ok("(empty)");
            var sb = new StringBuilder();
            sb.AppendLine($"{list.Count} children:");
            foreach (var e in list)
                sb.AppendLine($"  [{e.Type}] {e.Name}" + (e.X.HasValue ? $" @ ({e.X},{e.Y},{e.Z})" : ""));
            return Ok(sb.ToString());
        }

        ToolCallResult HandleOwner(JObject a)
        {
            var e = _api.GetOwner(a?["name"]?.Value<string>());
            if (e == null) return Err("未找到父级元素。");
            return Ok(Format(e));
        }

        ToolCallResult HandleAttrRead(JObject a)
        {
            var name = a?["name"]?.Value<string>();
            var attrs = Split(a?["attrs"]?.Value<string>());
            var dict = _api.ReadAttributes(name, attrs);
            if (dict.Count == 0) return Err("未能读取属性。请确认元素存在且指定属性名正确。");
            var sb = new StringBuilder();
            foreach (var kv in dict) sb.AppendLine($"{kv.Key}: {kv.Value}");
            return Ok(sb.ToString().TrimEnd());
        }

        ToolCallResult HandleSearch(JObject a)
        {
            var type = a?["type"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(type)) return Err("请提供元素类型'type'。");
            var filter = a?["filter"]?.Value<string>();
            int max = a?["max"]?.Value<int>() ?? 50;
            if (max <= 0) max = 200;
            var results = _api.Search(type, filter, max);
            var sb = new StringBuilder();
            sb.AppendLine($"{type} search: {results.Count} results" + (!string.IsNullOrEmpty(filter) ? $" (filter: {filter})" : ""));
            foreach (var e in results)
                sb.AppendLine($"  [{e.Type}] {e.Name}" + (e.X.HasValue ? $" @ ({e.X},{e.Y},{e.Z})" : ""));
            return Ok(sb.ToString());
        }

        ToolCallResult HandleProjectInfo()
        {
            var info = _api.GetProjectInfo();
            var sb = new StringBuilder();
            foreach (var kv in info) sb.AppendLine($"{kv.Key}: {kv.Value}");
            return Ok(sb.ToString());
        }

        ToolCallResult HandleMeasure(JObject a)
        {
            var n1 = a?["name1"]?.Value<string>();
            var n2 = a?["name2"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n1) || string.IsNullOrWhiteSpace(n2))
                return Err("请提供两个元素名称: name1和name2。");
            return Ok(_api.Measure(n1, n2));
        }

        ToolCallResult HandleElementType(JObject a)
            => Ok(_api.GetElementType(a?["name"]?.Value<string>()));

        ToolCallResult HandleElementPath(JObject a)
            => Ok(_api.GetElementPath(a?["name"]?.Value<string>()));

        // ── 修改 handlers ──

        ToolCallResult HandleAttrSet(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var attr = a?["attr"]?.Value<string>();
            var val = a?["value"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(attr) || val == null)
                return Err("请提供name, attr, value参数。");
            return Ok(_api.SetAttribute(n, attr, val));
        }

        ToolCallResult HandleCreate(JObject a)
        {
            var t = a?["type"]?.Value<string>();
            var n = a?["name"]?.Value<string>();
            var owner = a?["owner"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(t) || string.IsNullOrWhiteSpace(n))
                return Err("请提供type和name参数。");
            return Ok(_api.CreateElement(t, n, owner));
        }

        ToolCallResult HandleDelete(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("请提供name参数。");
            return Ok(_api.DeleteElement(n));
        }

        ToolCallResult HandleRename(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var nn = a?["new_name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(nn))
                return Err("请提供name和new_name参数。");
            return Ok(_api.RenameElement(n, nn));
        }

        ToolCallResult HandleCopy(JObject a)
        {
            var src = a?["source"]?.Value<string>();
            var dst = a?["dest"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst))
                return Err("请提供source和dest参数。");
            return Ok(_api.CopyElement(src, dst));
        }

        ToolCallResult HandleMove(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var no = a?["new_owner"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(no))
                return Err("请提供name和new_owner参数。");
            return Ok(_api.MoveElement(n, no));
        }

        // ── PML handlers ──

        ToolCallResult HandlePmlExec(JObject a)
        {
            var cmd = a?["command"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(cmd)) return Err("请提供command参数。");
            return Ok(_api.ExecutePml(cmd));
        }

        ToolCallResult HandlePmlEval(JObject a)
        {
            var expr = a?["expression"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(expr)) return Err("请提供expression参数。");
            return Ok(_api.EvaluatePml(expr));
        }

        // ── 批量 handlers ──

        ToolCallResult HandleBatchRead(JObject a)
        {
            var elems = Split(a?["elements"]?.Value<string>());
            var attrs = Split(a?["attrs"]?.Value<string>());
            if (elems.Length == 0 || attrs.Length == 0) return Err("请提供elements和attrs参数。");
            return Ok(_api.BatchRead(elems, attrs));
        }

        ToolCallResult HandleBatchSet(JObject a)
        {
            var elems = Split(a?["elements"]?.Value<string>());
            var attr = a?["attr"]?.Value<string>();
            var val = a?["value"]?.Value<string>();
            if (elems.Length == 0 || string.IsNullOrWhiteSpace(attr) || val == null)
                return Err("请提供elements, attr, value参数。");
            return Ok(_api.BatchSet(elems, attr, val));
        }

        ToolCallResult HandleCollect(JObject a)
        {
            var type = a?["type"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(type)) return Err("请提供type参数。");
            var filter = a?["filter"]?.Value<string>();
            var attrs = Split(a?["attrs"]?.Value<string>());
            return Ok(_api.CollectElements(type, filter, attrs.Length > 0 ? attrs : null));
        }

        ToolCallResult HandlePipelineExport(JObject a)
            => Ok(_api.ExportPipeline(a?["pipe_name"]?.Value<string>()));

        // ── 导航 handlers ──

        ToolCallResult HandleNavigate(JObject a)
        {
            var path = a?["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return Err("请提供path参数。");
            return Ok(_api.Navigate(path));
        }

        ToolCallResult HandleSelect(JObject a)
        {
            var name = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(name)) return Err("请提供name参数。");
            return Ok(_api.Select(name));
        }

        // ── 数据库 ──
        ToolCallResult HandleDbSave(JObject a) => Ok(_api.DbSave());
        ToolCallResult HandleDbChanges(JObject a) => Ok(_api.DbChanges());
        ToolCallResult HandleDbUndo(JObject a) => Ok(_api.DbUndo());
        ToolCallResult HandleDbExtract(JObject a) { var t = a?["type"]?.Value<string>(); return string.IsNullOrWhiteSpace(t) ? Err("type required") : Ok(_api.DbExtract(t)); }

        // ── 元素生命周期 ──
        ToolCallResult HandleElementExists(JObject a) { var n = a?["name"]?.Value<string>(); return string.IsNullOrWhiteSpace(n) ? Err("name required") : Ok(_api.ElementExists(n)); }
        ToolCallResult HandleElementEquals(JObject a) => Ok(_api.ElementEquals(a?["name1"]?.Value<string>(), a?["name2"]?.Value<string>()));
        ToolCallResult HandleElementRevert(JObject a) => Ok(_api.ElementRevert(a?["name"]?.Value<string>(), a?["attr"]?.Value<string>()));
        ToolCallResult HandleElementDump(JObject a) => Ok(_api.ElementDump(a?["name"]?.Value<string>()));

        // ── 层次导航 ──
        ToolCallResult HandleOccurrence(JObject a) => Ok(_api.Occurrence(a?["name"]?.Value<string>()));
        ToolCallResult HandleSiblings(JObject a) => Ok(_api.Siblings(a?["name"]?.Value<string>()));
        ToolCallResult HandleWorld(JObject a) { var w = _api.World(); return w == null ? Err("World not found") : Ok(Format(w)); }
        ToolCallResult HandleWrt(JObject a) => Ok(_api.Wrt(a?["name"]?.Value<string>()));

        // ── 属性 ──
        ToolCallResult HandleAttrList(JObject a) => Ok(_api.ListAttributes(a?["name"]?.Value<string>()));
        ToolCallResult HandleAttrInfo(JObject a) => Ok(_api.AttrInfo(a?["attr"]?.Value<string>(), a?["type"]?.Value<string>()));

        // ── 几何 ──
        ToolCallResult HandlePosSet(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "x", out double x) || !TryNum(a, "y", out double y) || !TryNum(a, "z", out double z))
                return Err("x, y, z must be numbers");
            return Ok(_api.SetPosition(n, x, y, z));
        }
        ToolCallResult HandlePosMove(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "dx", out double dx) || !TryNum(a, "dy", out double dy) || !TryNum(a, "dz", out double dz))
                return Err("dx, dy, dz must be numbers");
            return Ok(_api.MoveBy(n, dx, dy, dz));
        }
        ToolCallResult HandleOrientationGet(JObject a) => Ok(_api.GetOrientation(a?["name"]?.Value<string>()));
        ToolCallResult HandleRotate(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var ax = a?["axis"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(ax)) return Err("name and axis required");
            if (!TryNum(a, "angle", out double angle)) return Err("angle must be a number");
            return Ok(_api.Rotate(n, ax, angle));
        }
        ToolCallResult HandleReverse(JObject a) { var n = a?["name"]?.Value<string>(); return string.IsNullOrWhiteSpace(n) ? Err("name required") : Ok(_api.Reverse(n)); }

        // ── 连接 ──
        ToolCallResult HandleConnect(JObject a) => Ok(_api.Connect(a?["name1"]?.Value<string>(), a?["name2"]?.Value<string>()));
        ToolCallResult HandleDisconnect(JObject a) { var n = a?["name"]?.Value<string>(); return string.IsNullOrWhiteSpace(n) ? Err("name required") : Ok(_api.Disconnect(n)); }

        // ── 阵列 ──
        ToolCallResult HandleArrayAdd(JObject a) => Ok(_api.ArrayAdd(a?["array"]?.Value<string>(), a?["member"]?.Value<string>()));
        ToolCallResult HandleArrayRemove(JObject a)
        {
            if (!TryNum(a, "index", out double idx)) return Err("index must be a number");
            return Ok(_api.ArrayRemove(a?["array"]?.Value<string>(), (int)idx));
        }
        ToolCallResult HandleArraySort(JObject a) => Ok(_api.ArraySort(a?["array"]?.Value<string>()));

        // ── 规格/目录 ──
        ToolCallResult HandleSpecQuery(JObject a) => Ok(_api.SpecQuery(a?["spec"]?.Value<string>()));
        ToolCallResult HandleBom(JObject a) { var n = a?["name"]?.Value<string>(); return string.IsNullOrWhiteSpace(n) ? Err("name required") : Ok(_api.BOM(n)); }
        ToolCallResult HandleComponentInfo(JObject a) => Ok(_api.ComponentInfo(a?["name"]?.Value<string>()));

        // ── 视图 ──
        ToolCallResult HandleViewZoom(JObject a) { var n = a?["name"]?.Value<string>(); return string.IsNullOrWhiteSpace(n) ? Err("name required") : Ok(_api.ViewZoom(n)); }
        ToolCallResult HandleViewFit(JObject a) => Ok(_api.ViewFit());
        ToolCallResult HandleViewColour(JObject a) => Ok(_api.ViewColour(a?["name"]?.Value<string>(), a?["colour"]?.Value<string>()));

        // ── 碰撞/检查 ──
        ToolCallResult HandleClashCheck(JObject a) => Ok(_api.ClashCheck(a?["type_or_name1"]?.Value<string>(), a?["name2"]?.Value<string>()));
        ToolCallResult HandleDesignCheck(JObject a) { var n = a?["name"]?.Value<string>(); return string.IsNullOrWhiteSpace(n) ? Err("name required") : Ok(_api.DesignCheck(n)); }

        // ── 会话 ──
        ToolCallResult HandleSessionStatus(JObject a) => Ok(_api.SessionStatus());

        // ── 管道操作 handlers ──

        ToolCallResult HandlePipeCut(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "at", out double at)) return Err("at must be a number");
            return Ok(_api.CutPipe(n, at));
        }

        ToolCallResult HandlePipeGap(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "at", out double at)) return Err("at must be a number");
            double gap = TryNumOut(a, "gap", out double g) ? g : 10;
            return Ok(_api.GapPipe(n, at, gap));
        }

        ToolCallResult HandlePipeJoin(JObject a)
        {
            var n1 = a?["name1"]?.Value<string>();
            var n2 = a?["name2"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n1) || string.IsNullOrWhiteSpace(n2))
                return Err("name1 and name2 required");
            return Ok(_api.JoinPipe(n1, n2));
        }

        ToolCallResult HandlePipeBend(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "at", out double at)) return Err("at must be a number");
            return Ok(_api.BendPipe(n, at, a?["stype"]?.Value<string>()));
        }

        ToolCallResult HandlePipeTee(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "at", out double at)) return Err("at must be a number");
            return Ok(_api.TeePipe(n, at, a?["stype"]?.Value<string>()));
        }

        ToolCallResult HandlePipeValve(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "at", out double at)) return Err("at must be a number");
            return Ok(_api.ValvePipe(n, at, a?["stype"]?.Value<string>()));
        }

        ToolCallResult HandlePipeFlange(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "at", out double at)) return Err("at must be a number");
            return Ok(_api.FlangePipe(n, at, a?["stype"]?.Value<string>()));
        }

        ToolCallResult HandlePipeReducer(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "at", out double at)) return Err("at must be a number");
            return Ok(_api.ReducerPipe(n, at));
        }

        ToolCallResult HandlePipeRoute(JObject a)
        {
            var p = a?["pipe"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(p)) return Err("pipe required");
            return Ok(_api.RoutePipe(p));
        }

        // ── 元素创建补充 handlers ──

        ToolCallResult HandleSupportCreate(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            return Ok(_api.CreateSupport(n, a?["owner"]?.Value<string>(), a?["stype"]?.Value<string>()));
        }

        ToolCallResult HandleWeldCreate(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            return Ok(_api.CreateWeld(n, a?["owner"]?.Value<string>()));
        }

        ToolCallResult HandleLabelCreate(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var o = a?["owner"]?.Value<string>();
            var t = a?["text"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(o) || string.IsNullOrWhiteSpace(t))
                return Err("name, owner, and text required");
            return Ok(_api.CreateLabel(n, o, t));
        }

        // ── 属性补充 handlers ──

        ToolCallResult HandleAttrClear(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var attr = a?["attr"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(attr)) return Err("name and attr required");
            return Ok(_api.ClearAttribute(n, attr));
        }

        ToolCallResult HandleAttrCopy(JObject a)
        {
            var src = a?["source"]?.Value<string>();
            var dst = a?["target"]?.Value<string>();
            var attrs = a?["attrs"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst) || string.IsNullOrWhiteSpace(attrs))
                return Err("source, target, and attrs required");
            return Ok(_api.CopyAttributes(src, dst, attrs));
        }

        // ── 可见性 handlers ──

        ToolCallResult HandleShow(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            return Ok(_api.ShowElement(n));
        }

        ToolCallResult HandleHide(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            return Ok(_api.HideElement(n));
        }

        ToolCallResult HandleViewIso(JObject a) => Ok(_api.ViewIso());
        ToolCallResult HandleViewPlan(JObject a) => Ok(_api.ViewPlan());
        ToolCallResult HandleViewElevation(JObject a) => Ok(_api.ViewElevation());

        // ── 数据库补充 handlers ──

        ToolCallResult HandleDbClaim(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            return Ok(_api.ClaimElement(n));
        }

        ToolCallResult HandleDbRelease(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            return Ok(_api.ReleaseElement(n));
        }

        // ── 出图 handler ──

        ToolCallResult HandleDraftIso(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var o = a?["output"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(o)) return Err("name and output required");
            return Ok(_api.DraftIso(n, o));
        }

        // ── 定位补充 handlers ──

        ToolCallResult HandlePosAt(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "x", out double x) || !TryNum(a, "y", out double y) || !TryNum(a, "z", out double z))
                return Err("x, y, z must be numbers");
            return Ok(_api.PositionAt(n, x, y, z));
        }

        ToolCallResult HandlePosDist(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            if (!TryNum(a, "dist", out double d)) return Err("dist must be a number");
            return Ok(_api.PositionDist(n, d, a?["from"]?.Value<string>()));
        }

        ToolCallResult HandlePosDir(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var d = a?["direction"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(d)) return Err("name and direction required");
            return Ok(_api.PositionDir(n, d));
        }

        ToolCallResult HandlePosOri(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var pp = a?["ppoint"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(pp)) return Err("name and ppoint required");
            return Ok(_api.PositionOri(n, pp));
        }

        ToolCallResult HandlePosThr(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var t = a?["target"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(t)) return Err("name and target required");
            return Ok(_api.PositionThr(n, t));
        }

        // ── 元素操作补充 handlers ──

        ToolCallResult HandleElementFlip(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n)) return Err("name required");
            return Ok(_api.FlipElement(n));
        }

        ToolCallResult HandleForceConnect(JObject a)
        {
            var n = a?["name"]?.Value<string>();
            var t = a?["target"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(t)) return Err("name and target required");
            return Ok(_api.ForceConnect(n, t));
        }

        bool TryNumOut(JObject args, string key, out double val)
        {
            val = 0;
            if (args == null) return false;
            var token = args[key];
            if (token == null) return false;
            try { val = token.Value<double>(); return true; }
            catch { return double.TryParse(token.Value<string>(), out val); }
        }

        // ── helpers ──

        static ToolCallResult Ok(string text) => new ToolCallResult
        {
            Content = new List<ContentBlock> { new ContentBlock { Type = "text", Text = text } },
            IsError = false
        };

        static ToolCallResult Err(string text) => new ToolCallResult
        {
            Content = new List<ContentBlock> { new ContentBlock { Type = "text", Text = text } },
            IsError = true
        };

        static string Format(E3dElementInfo e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"名称: {e.Name}");
            sb.AppendLine($"类型: {e.Type}");
            if (e.X.HasValue)
                sb.AppendLine($"坐标: X={e.X:F3} Y={e.Y:F3} Z={e.Z:F3}");
            return sb.ToString();
        }

        static string[] Split(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new string[0];
            return csv.Split(new[] { ',', '，', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        static bool TryNum(JObject args, string key, out double val)
        {
            val = 0;
            if (args == null) return false;
            var token = args[key];
            if (token == null) return false;
            try { val = token.Value<double>(); return true; }
            catch { return double.TryParse(token.Value<string>(), out val); }
        }
    }
}
