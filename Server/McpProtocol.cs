using System;
using System.Windows.Forms;
using E3DMcpServer.Models;
using E3DMcpServer.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DMcpServer.Server
{
    public class McpProtocol
    {
        private readonly E3DToolHandler _toolHandler;
        private Control _mainCtrl;

        public McpProtocol(E3DToolHandler toolHandler)
        {
            _toolHandler = toolHandler;
        }

        public void SetMainThreadControl(Control ctrl)
        {
            _mainCtrl = ctrl;
        }

        // SSE mode: send response via session SSE stream (fire-and-forget)
        public void ProcessMessage(McpSession session, string requestJson)
        {
            JsonRpcRequest request;
            try { request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson); }
            catch { SendToSession(session, null, null, JsonRpcErrors.ParseError); return; }

            if (request == null || string.IsNullOrEmpty(request.Method))
            { SendToSession(session, request?.Id, null, JsonRpcErrors.InvalidRequest); return; }

            if (request.Id == null)
                HandleNotification(session, request);
            else
                HandleRequest(session, request);
        }

        // Streamable HTTP mode: return JsonRpcResponse directly (blocking)
        public JsonRpcResponse ProcessMessageSync(string requestJson)
        {
            JsonRpcRequest request;
            try { request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson); }
            catch { return BuildError(null, JsonRpcErrors.ParseError); }

            if (request == null || string.IsNullOrEmpty(request.Method))
                return BuildError(request?.Id, JsonRpcErrors.InvalidRequest);

            if (request.Id == null)
                return HandleNotificationSync(request);

            return HandleRequestSync(request);
        }

        public string GetToolsJson()
        {
            var tools = new ToolsListResult { Tools = _toolHandler.GetToolDefinitions() };
            return JsonConvert.SerializeObject(tools, Formatting.Indented);
        }

        // ── Request routing ──────────────────────────────

        private void HandleRequest(McpSession session, JsonRpcRequest request)
        {
            switch (request.Method)
            {
                case "initialize":
                    SendToSession(session, request.Id, JToken.FromObject(MakeInitResult()), null);
                    break;
                case "tools/list":
                    SendToSession(session, request.Id, JToken.FromObject(new ToolsListResult
                    { Tools = _toolHandler.GetToolDefinitions() }), null);
                    break;
                case "tools/call":
                    HandleToolsCallToSession(session, request);
                    break;
                default:
                    SendToSession(session, request.Id, null, JsonRpcErrors.MethodNotFound);
                    break;
            }
        }

        private JsonRpcResponse HandleRequestSync(JsonRpcRequest request)
        {
            switch (request.Method)
            {
                case "initialize":
                    return BuildResult(request.Id, JToken.FromObject(MakeInitResult()));
                case "tools/list":
                    return BuildResult(request.Id, JToken.FromObject(new ToolsListResult
                    { Tools = _toolHandler.GetToolDefinitions() }));
                case "tools/call":
                    return HandleToolsCallSync(request);
                default:
                    return BuildError(request.Id, JsonRpcErrors.MethodNotFound);
            }
        }

        // ── Notifications ────────────────────────────────

        private void HandleNotification(McpSession session, JsonRpcRequest request)
        {
            if (request.Method == "notifications/initialized")
                session.Initialized = true;
        }

        private JsonRpcResponse HandleNotificationSync(JsonRpcRequest request)
        {
            return null; // no response for notifications
        }

        // ── Tools/call ───────────────────────────────────

        private void HandleToolsCallToSession(McpSession session, JsonRpcRequest request)
        {
            var callParams = request.Params?.ToObject<ToolCallParams>();
            if (callParams == null || string.IsNullOrEmpty(callParams.Name))
            { SendToSession(session, request.Id, null, JsonRpcErrors.InvalidParams); return; }

            MarshalToMainThread(() =>
            {
                var result = _toolHandler.ExecuteTool(callParams.Name, callParams.Arguments);
                SendToSession(session, request.Id, JToken.FromObject(result), null);
            });
        }

        private JsonRpcResponse HandleToolsCallSync(JsonRpcRequest request)
        {
            var callParams = request.Params?.ToObject<ToolCallParams>();
            if (callParams == null || string.IsNullOrEmpty(callParams.Name))
                return BuildError(request.Id, JsonRpcErrors.InvalidParams);

            ToolCallResult result = null;
            MarshalToMainThread(() =>
            {
                result = _toolHandler.ExecuteTool(callParams.Name, callParams.Arguments);
            });

            return BuildResult(request.Id, JToken.FromObject(result));
        }

        // ── Thread marshaling ────────────────────────────

        private void MarshalToMainThread(Action action)
        {
            if (_mainCtrl != null && _mainCtrl.InvokeRequired)
            {
                _mainCtrl.Invoke(action);
            }
            else
            {
                action();
            }
        }

        // ── Helpers ──────────────────────────────────────

        private static InitializeResult MakeInitResult()
        {
            return new InitializeResult
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ServerCapabilities
                { Tools = new ToolsCapability { ListChanged = false } },
                ServerInfo = new ServerInfo
                { Name = "E3D-MCP-Server", Version = "1.0.0" }
            };
        }

        private void SendToSession(McpSession session, object id, JToken result, JsonRpcError error)
        {
            var response = new JsonRpcResponse { Id = id, Result = result, Error = error };
            string json = JsonConvert.SerializeObject(response, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            session.Sse.SendMessage(json);
        }

        private static JsonRpcResponse BuildResult(object id, JToken result)
        {
            return new JsonRpcResponse { Id = id, Result = result };
        }

        private static JsonRpcResponse BuildError(object id, JsonRpcError error)
        {
            return new JsonRpcResponse { Id = id, Error = error };
        }
    }
}
