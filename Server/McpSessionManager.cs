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
            foreach (var session in _sessions.Values)
            {
                try { session.Sse.SendMessage(jsonRpcMessage); }
                catch { }
            }
        }
    }
}
