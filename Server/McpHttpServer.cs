using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace E3DMcpServer.Server
{
    public class McpHttpServer : IDisposable
    {
        private readonly int _port;
        private readonly McpSessionManager _sessionManager;
        private readonly McpProtocol _protocol;
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public McpHttpServer(int port, McpSessionManager sessionManager, McpProtocol protocol)
        {
            _port = port;
            _sessionManager = sessionManager;
            _protocol = protocol;
        }

        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();
            _isRunning = true;

            Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();

            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var _ = Task.Run(() => ProcessRequest(context), token);
                }
                catch (OperationCanceledException) { break; }
                catch (HttpListenerException) { break; }
                catch { break; }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            response.AppendHeader("Access-Control-Allow-Origin", "*");

            string path = request.Url.AbsolutePath.ToLowerInvariant();
            string method = request.HttpMethod.ToUpperInvariant();

            try
            {
                // CORS preflight
                if (method == "OPTIONS")
                {
                    response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    response.AppendHeader("Access-Control-Allow-Headers", "Content-Type");
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // Health check
                if (path == "/health" && method == "GET")
                {
                    SendText(response, 200, "{\"status\":\"ok\",\"port\":" + _port + "}");
                    return;
                }

                // Debug: list tools
                if (path == "/tools" && method == "GET")
                {
                    string toolsJson = _protocol.GetToolsJson();
                    SendJson(response, 200, toolsJson);
                    return;
                }

                // Debug: diagnose threading/CE issue
                if (path == "/debug" && method == "GET")
                {
                    HandleDebug(context);
                    return;
                }

                // Streamable HTTP: single-endpoint MCP
                if (path == "/mcp" && method == "POST")
                {
                    HandleMcp(context);
                    return;
                }

                // SSE: server-sent events connection
                if (path == "/sse" && method == "GET")
                {
                    HandleSseConnection(context);
                    return;
                }

                // SSE: message posting
                if (path == "/message" && method == "POST")
                {
                    HandleMessage(context);
                    return;
                }

                SendText(response, 404, "Not Found");
            }
            catch (Exception ex)
            {
                try { SendText(response, 500, ex.Message); } catch { }
            }
        }

        // ── Streamable HTTP ──────────────────────────────────────

        private void HandleDebug(HttpListenerContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            try
            {
                // Call through MCP protocol which will marshal to main thread
                var response = _protocol.ProcessMessageSync(
                    "{\"jsonrpc\":\"2.0\",\"id\":99,\"method\":\"tools/call\",\"params\":{\"name\":\"e3d_ce_get\",\"arguments\":{}}}");
                sb.AppendLine($"  \"mcp_ce_get\": {Newtonsoft.Json.JsonConvert.SerializeObject(response)},");
            }
            catch (Exception ex) { sb.AppendLine($"  \"mcp_error\": \"{ex.Message}\","); }

            // Additional: try PML eval via MCP tools/call
            try
            {
                var response2 = _protocol.ProcessMessageSync(
                    "{\"jsonrpc\":\"2.0\",\"id\":98,\"method\":\"tools/call\",\"params\":{\"name\":\"e3d_pml_eval\",\"arguments\":{\"expression\":\"!!CE.NAME\"}}}");
                sb.AppendLine($"  \"pml_eval_ce_name\": {Newtonsoft.Json.JsonConvert.SerializeObject(response2)},");
            }
            catch (Exception ex) { sb.AppendLine($"  \"pml_error\": \"{ex.Message}\","); }

            // Thread info
            sb.AppendLine($"  \"managed_thread_id\": {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            sb.AppendLine("}");
            SendJson(context.Response, 200, sb.ToString());
        }

        private void HandleMcp(HttpListenerContext context)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            var response = _protocol.ProcessMessageSync(body);
            if (response == null)
            {
                // Notification — no response body (but return 200)
                SendText(context.Response, 200, "");
                return;
            }

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(response, Newtonsoft.Json.Formatting.None,
                new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });

            SendJson(context.Response, 200, json);
        }

        // ── SSE ──────────────────────────────────────────────────

        private void HandleSseConnection(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            response.StatusCode = 200;
            response.ContentType = "text/event-stream";
            response.AppendHeader("Cache-Control", "no-cache");
            response.AppendHeader("Connection", "keep-alive");
            response.AppendHeader("X-Accel-Buffering", "no");

            var sse = new SseWriter(response.OutputStream);
            var session = _sessionManager.CreateSession(sse);
            sse.SendEndpoint(session.SessionId);

            try
            {
                byte[] buffer = new byte[1];
                while (_isRunning)
                {
                    int read = context.Request.InputStream.Read(buffer, 0, 1);
                    if (read == 0) break;
                }
            }
            catch { }
            finally
            {
                _sessionManager.RemoveSession(session.SessionId);
                try { response.Close(); } catch { }
            }
        }

        private void HandleMessage(HttpListenerContext context)
        {
            string sessionId = context.Request.QueryString["sessionId"];
            var session = _sessionManager.GetSession(sessionId);

            if (session == null)
            {
                SendText(context.Response, 400, "Invalid or missing sessionId");
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            SendText(context.Response, 202, "Accepted");
            _protocol.ProcessMessage(session, body);
        }

        // ── Response helpers ─────────────────────────────────────

        private static void SendText(HttpListenerResponse response, int statusCode, string text)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(text);
                response.StatusCode = statusCode;
                response.ContentType = "text/plain; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch { }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private static void SendJson(HttpListenerResponse response, int statusCode, string json)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                response.StatusCode = statusCode;
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch { }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            Stop();
            _listener?.Close();
            _cts?.Dispose();
        }
    }
}
