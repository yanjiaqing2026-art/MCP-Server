using Aveva.ApplicationFramework;
using Aveva.Core.Database;
using E3DMcpServer.Server;
using E3DMcpServer.Tools;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace E3DMcpServer
{
    public class McpServerAddin : IAddinInjected
    {
        public string Name => "E3D MCP Server";
        public string Description => "MCP Server enabling bidirectional AI agent control of E3D";

        private McpHttpServer _httpServer;
        private McpSessionManager _sessionManager;
        private E3dApiWrapper _api;
        private Control _mainCtrl;
        private bool _started;
        // P3-1（指导书）：保存 Add 时的委托引用，Stop() 才能对称 Remove（原来只退订 CE 事件）。
        private DbEvents.UserChangesEventHandler _userChangesHandler;
        // P3-4：ce_changed 200ms 合并窗（重置式 Timer）——人在树上连点不再向 Agent 洪泛。
        // CE 快照在主线程事件里读好（E3D API 线程亲和），Timer 回调只广播字符串。
        private System.Threading.Timer _ceDebounce;
        private volatile string _pendingCeJson;
        // N3：MDB 生命周期事件（DatabaseService.MDBOpened/MDBClosing，签名经本机 DLL 元数据验证——
        // 指导书写的 DbEvents.AddMdbOpenedEventHandler 在本 DLL 上不存在，以 DLL 为准）。
        private EventHandler _mdbOpenedHandler;
        private EventHandler _mdbClosingHandler;

        public void Start(ServiceManager serviceManager)
        {
            if (!_started) { _started = true; DoStart(); }
        }

        public void Start(IDependencyResolver resolver)
        {
            if (!_started) { _started = true; DoStart(); }
        }

        private void DoStart()
        {
            // Create hidden control on E3D MAIN THREAD for marshaling
            _mainCtrl = new Control();
            var _ = _mainCtrl.Handle;

            // Init E3D API on main thread
            _api = new E3dApiWrapper();

            // Subscribe to E3D events for push notifications (E3DAddIn_5 pattern)
            try
            {
                CurrentElement.CurrentElementChanged += OnCurrentElementChanged;
                _userChangesHandler = new DbEvents.UserChangesEventHandler(OnDbUserChanges);
                DbEvents.AddHandleUserChanges(_userChangesHandler);

                // N3：MDB 打开/关闭通知 → Agent 感知人切换了项目/模块，避免在错误的 MDB 上建模。
                _mdbOpenedHandler = (s, ev) => BroadcastMdbEvent("notifications/e3d/mdb_opened");
                _mdbClosingHandler = (s, ev) => BroadcastMdbEvent("notifications/e3d/mdb_closing");
                DatabaseService.MDBOpened += _mdbOpenedHandler;
                DatabaseService.MDBClosing += _mdbClosingHandler;
            }
            catch (Exception ex)
            {
                // P3-5：静默 catch 补日志——订阅失败推送通知全哑，至少留痕可诊断。
                McpHttpServer.LogSrv($"事件订阅失败（推送通知不可用）: {ex.Message}");
            }

            // Start MCP HTTP server on background thread — MUST NOT block Start()
            Task.Run(() =>
            {
                try
                {
                    var toolHandler = new E3DToolHandler(_api);
                    var protocol = new McpProtocol(toolHandler);
                    protocol.SetMainThreadControl(_mainCtrl);

                    _sessionManager = new McpSessionManager();

                    int port = 8286;
                    _httpServer = new McpHttpServer(port, _sessionManager, protocol);
                    _httpServer.Start();
                    McpHttpServer.LogSrv($"MCP HTTP server started on :{port}");
                }
                catch (Exception ex)
                {
                    // P3-5：原 catch { } 让服务器启动失败完全静默（端口占用/权限）→ "插件装了但连不上"无从诊断。
                    McpHttpServer.LogSrv($"MCP HTTP server 启动失败: {ex}");
                }
            });
        }

        // ── Push notifications ──────────────────────────

        private void OnCurrentElementChanged(object sender, CurrentElementChangedEventArgs e)
        {
            try
            {
                if (_sessionManager == null) return;
                var info = _api.GetCurrentElement();   // 主线程读 CE（E3D API 线程亲和），轻量
                if (info == null) return;

                var notification = new
                {
                    jsonrpc = "2.0",
                    method = "notifications/e3d/ce_changed",
                    @params = new
                    {
                        name = info.Name,
                        type = info.Type,
                        x = info.X,
                        y = info.Y,
                        z = info.Z
                    }
                };
                // P2-C 巡检修（事件总线空壳）：e3d_subscribe/poll_events 的发布端此前零调用
                // → poll 恒回 0 事件，Agent 会误判"E3D 无变化"。这里真喂 EventBus。
                try { Tools.E3dEventBus.Publish("ce_changed", info.Name ?? "", info.Type ?? ""); } catch { }

                // P3-4：不直接广播——存快照 + 重置 200ms 计时器，静默 200ms 后只发最后一次。
                _pendingCeJson = JsonConvert.SerializeObject(notification);
                if (_ceDebounce == null)
                {
                    _ceDebounce = new System.Threading.Timer(_ =>
                    {
                        try
                        {
                            var json = _pendingCeJson;
                            if (json != null && _sessionManager != null) _sessionManager.Broadcast(json);
                        }
                        catch { }
                    }, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                }
                _ceDebounce.Change(200, System.Threading.Timeout.Infinite);
            }
            catch { }
        }

        // N3：MDB 打开/关闭广播（EventHandler 无参上下文，项目信息由 Agent 需要时经 e3d_project_info 查）。
        private void BroadcastMdbEvent(string method)
        {
            try
            {
                if (_sessionManager == null) return;
                var notification = new { jsonrpc = "2.0", method, @params = new { at = DateTime.UtcNow.ToString("o") } };
                _sessionManager.Broadcast(JsonConvert.SerializeObject(notification));
            }
            catch { }
        }

        private void OnDbUserChanges(DbUserChanges changes)
        {
            try
            {
                if (_sessionManager == null) return;

                var created = changes.GetCreations();
                var deleted = changes.GetDeletions();

                int total = (created?.Length ?? 0) + (deleted?.Length ?? 0);
                if (total == 0) return;

                // P2-C 巡检修：db 变化也喂 EventBus（e3d_poll_events 的另一半事件源）。
                try { Tools.E3dEventBus.Publish("db_changed", "", $"created={created?.Length ?? 0} deleted={deleted?.Length ?? 0}"); } catch { }

                var notification = new
                {
                    jsonrpc = "2.0",
                    method = "notifications/e3d/db_changed",
                    @params = new
                    {
                        created = created?.Length ?? 0,
                        deleted = deleted?.Length ?? 0
                    }
                };
                _sessionManager.Broadcast(JsonConvert.SerializeObject(notification));
            }
            catch { }
        }

        public void Stop()
        {
            try
            {
                CurrentElement.CurrentElementChanged -= OnCurrentElementChanged;
            }
            catch { }

            // P3-1：对称退订 DbEvents（签名已经本机 DLL 元数据验证 RemoveHandleUserChanges 存在）。
            try
            {
                if (_userChangesHandler != null)
                {
                    DbEvents.RemoveHandleUserChanges(_userChangesHandler);
                    _userChangesHandler = null;
                }
            }
            catch { }

            // N3 退订 + P3-4 计时器清理 + P3-3 后台发送线程停机。
            try
            {
                if (_mdbOpenedHandler != null) { DatabaseService.MDBOpened -= _mdbOpenedHandler; _mdbOpenedHandler = null; }
                if (_mdbClosingHandler != null) { DatabaseService.MDBClosing -= _mdbClosingHandler; _mdbClosingHandler = null; }
            }
            catch { }
            try { _ceDebounce?.Dispose(); _ceDebounce = null; } catch { }
            try { _sessionManager?.Shutdown(); } catch { }

            try
            {
                _httpServer?.Stop();
                _httpServer?.Dispose();
                _mainCtrl?.Dispose();
            }
            catch { }
        }
    }
}
