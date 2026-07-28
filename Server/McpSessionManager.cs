using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace E3DMcpServer.Server
{
    public class McpSession
    {
        public string SessionId { get; set; }
        public SseWriter Sse { get; set; }
        public bool Initialized { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class McpSessionManager
    {
        private readonly ConcurrentDictionary<string, McpSession> _sessions
            = new ConcurrentDictionary<string, McpSession>();

        private int _counter;

        // P3-3（指导书 2026-07-02）：Broadcast 原是【调用线程同步网络写】——CE 事件在 E3D 主线程
        // 触发，高频变化下主线程做网络 IO = UI 卡顿。改"入 ConcurrentQueue + 单后台发送线程"，
        // 主线程只入队。后台线程 IsBackground=true，随进程退出，不阻塞 E3D 关闭。
        private readonly ConcurrentQueue<string> _outbox = new ConcurrentQueue<string>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private volatile bool _running = true;

        public McpSessionManager()
        {
            var sender = new Thread(SenderLoop) { IsBackground = true, Name = "McpBroadcastSender" };
            sender.Start();
        }

        private void SenderLoop()
        {
            while (_running)
            {
                _signal.WaitOne(1000);
                string msg;
                while (_outbox.TryDequeue(out msg))
                {
                    foreach (var session in _sessions.Values)
                    {
                        try { session.Sse.SendMessage(msg); }
                        catch { }
                    }
                }
            }
        }

        /// <summary>停止后台发送线程（Addin.Stop 时调；不调也无害——后台线程随进程退出）。</summary>
        public void Shutdown()
        {
            _running = false;
            try { _signal.Set(); } catch { }
        }

        public McpSession CreateSession(SseWriter sse)
        {
            var sessionId = $"s{Interlocked.Increment(ref _counter):x}{DateTime.UtcNow.Ticks:x}";
            var session = new McpSession
            {
                SessionId = sessionId,
                Sse = sse,
                Initialized = false
            };
            _sessions[sessionId] = session;
            return session;
        }

        public McpSession GetSession(string sessionId)
        {
            _sessions.TryGetValue(sessionId ?? "", out var session);
            return session;
        }

        public void RemoveSession(string sessionId)
        {
            if (sessionId != null)
                _sessions.TryRemove(sessionId, out _);
        }

        public IEnumerable<McpSession> AllSessions => _sessions.Values;

        public void Broadcast(string jsonRpcMessage)
        {
            // P3-3：只入队，绝不在调用线程（可能是 E3D 主线程）做网络写。
            _outbox.Enqueue(jsonRpcMessage);
            try { _signal.Set(); } catch { }
        }
    }
}
