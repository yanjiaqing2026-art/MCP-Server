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
                DbEvents.AddHandleUserChanges(new DbEvents.UserChangesEventHandler(OnDbUserChanges));
            }
            catch { }

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
                }
                catch { }
            });
        }

        // ── Push notifications ──────────────────────────

        private void OnCurrentElementChanged(object sender, CurrentElementChangedEventArgs e)
        {
            try
            {
                if (_sessionManager == null) return;
                var info = _api.GetCurrentElement();
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
