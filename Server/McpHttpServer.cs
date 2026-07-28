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

        // P1（指导书 2026-07-02）：鉴权 Token。env E3D_MCP_TOKEN 优先（可固定），否则每次启动随机生成；
        // 写入 %TEMP%\mcp_token.txt + 日志，同机客户端（09 中间件）从那里取并带 X-MCP-Token 头。
        private static readonly string Token = InitToken();

        private static string InitToken()
        {
            string t = null;
            try { t = Environment.GetEnvironmentVariable("E3D_MCP_TOKEN"); } catch { }
            // 2026-07-03：token 门改为【opt-in】。原来"env 空就随机生成"→ token 靠 %TEMP%\mcp_token.txt
            // 在插件↔中间件间同步；一旦两者跑在不同 Windows 账户(不同 %TEMP%)就对不上 → 401 连不上，
            // 这正是"加了 token 门后、以前能连的却连不上"的根因。localhost-only 绑定 + Origin 白名单已挡住
            // 远程访问/DNS-rebinding，单机 localhost 部署无 token 也安全。要强鉴权：插件机与中间件都设同一 env。
            if (string.IsNullOrWhiteSpace(t))
            {
                LogSrv("MCP token 门未启用（未设 env E3D_MCP_TOKEN）—— 靠 localhost 绑定 + Origin 白名单防护；中间件无需带 token 即可连。");
                return "";
            }
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "mcp_token.txt"), t); } catch { }
            LogSrv("MCP token 门已启用（env E3D_MCP_TOKEN）。中间件需带同一 token。");
            return t;
        }

        /// <summary>服务器事件落盘（与 PML 日志同文件，D:\ 不可写回退 %TEMP%）。永不抛。internal 供 Addin 复用。</summary>
        internal static void LogSrv(string msg)
        {
            var line = string.Format("{0:HH:mm:ss} SRV  {1}{2}", DateTime.Now, msg, Environment.NewLine);
            try { File.AppendAllText(@"D:\pipingclaw_e3d_pml.log", line); }
            catch
            {
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "pipingclaw_e3d_pml.log"), line); }
                catch { }
            }
        }

        // P1 第 1 层：Origin 白名单（防浏览器 DNS-rebinding/CSRF，MCP HTTP 传输规范要求）+ Token 门。
        // /health 免 token（探活）；带非本机 Origin 的请求直接 403。
        private bool Authorize(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string origin = req.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin)
                && !origin.StartsWith("http://localhost") && !origin.StartsWith("http://127.0.0.1"))
            {
                SendText(resp, 403, "Forbidden origin");
                return false;
            }
            string path = req.Url.AbsolutePath.ToLowerInvariant();
            // Token 为空 = opt-in 门未启用（未设 env E3D_MCP_TOKEN）→ 不校验，像加门之前一样直接放行；
            // Token 非空才校验（operator 显式设了 env 才启用强鉴权）。
            if (!string.IsNullOrEmpty(Token) && path != "/health" && req.Headers["X-MCP-Token"] != Token)
            {
                SendText(resp, 401, "Missing or bad X-MCP-Token（插件机与中间件需设同一个 env E3D_MCP_TOKEN）");
                return false;
            }
            return true;
        }

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
            // 2026-07-03：同时绑 127.0.0.1。只绑 localhost 时，用 http://127.0.0.1:PORT 访问会被
            // HttpListener 判主机名不匹配 → 回 400（浏览器/健康探针常用 127.0.0.1，易被误判成"没加载"）。
            // 127.0.0.1 前缀在非管理员下可能缺 URL ACL 令 Start() 抛异常 → try 失败就退回只绑 localhost，
            // 保持原有可用性，绝不因这条增强而起不来。
            try
            {
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Start();
            }
            catch
            {
                LogSrv("127.0.0.1 前缀绑定失败（多半缺 URL ACL），退回只绑 localhost。用 localhost 访问即可，或 netsh http add urlacl 放行 127.0.0.1。");
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
            }
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
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    // P3-5（指导书）：原 catch { break; } 让任何瞬时异常杀死监听循环 → 服务静默死亡。
                    // 非致命异常记日志后继续监听；服务已停时才退出。
                    LogSrv($"ListenLoop 非致命异常（继续监听）: {ex}");
                    if (!_isRunning) break;
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            // P1：不再无条件 ACAO "*" —— 仅对本机 Origin 回显该 Origin（非本机 Origin 在 Authorize 里 403）。
            string reqOrigin = request.Headers["Origin"];
            if (!string.IsNullOrEmpty(reqOrigin)
                && (reqOrigin.StartsWith("http://localhost") || reqOrigin.StartsWith("http://127.0.0.1")))
            {
                response.AppendHeader("Access-Control-Allow-Origin", reqOrigin);
            }

            string path = request.Url.AbsolutePath.ToLowerInvariant();
            string method = request.HttpMethod.ToUpperInvariant();

            try
            {
                // CORS preflight（预检不带自定义头，放行；真正的调用在下方 Authorize 把 token 门守住）
                if (method == "OPTIONS")
                {
                    response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, X-MCP-Token");
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // P1：Origin 白名单 + Token 门（/health 免 token）
                if (!Authorize(request, response)) return;

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
