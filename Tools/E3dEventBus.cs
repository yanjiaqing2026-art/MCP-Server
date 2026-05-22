using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using E3DMcpServer.Models;
using Newtonsoft.Json.Linq;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// Minimal event subscription / polling exposed to the agent.
    ///
    /// Why: E3D fires <c>CurrentElement.CurrentElementChanged</c> when the
    /// operator clicks something in the GUI, but the agent (over HTTP) has
    /// no way to learn about that without polling. To keep the protocol
    /// simple we implement subscribe + poll: the agent calls subscribe to
    /// reserve a queue, then polls drain_events on its own cadence.
    ///
    /// Subscription state lives in process — restart the plug-in and all
    /// subscriptions are lost. That is fine for a research tool and avoids
    /// any persistence headache.
    /// </summary>
    public static class E3dEventBus
    {
        private sealed class Subscription
        {
            public string Id { get; set; }
            public DateTime CreatedAt { get; set; }
            public HashSet<string> Events { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public ConcurrentQueue<EventEntry> Queue { get; set; } = new ConcurrentQueue<EventEntry>();
        }

        private sealed class EventEntry
        {
            public string Kind { get; set; }
            public string ElementPath { get; set; }
            public string Detail { get; set; }
            public DateTime At { get; set; }
        }

        private static readonly ConcurrentDictionary<string, Subscription> _subs =
            new ConcurrentDictionary<string, Subscription>();

        private const int MaxQueueDepth = 500;

        // ── e3d_subscribe ───────────────────────────────────────────────
        // Input:
        //   events: csv of event kinds (e.g. "current_element_changed,attr_changed")
        // Output:
        //   subscription_id the agent must echo back on every poll.
        public static ToolCallResult HandleSubscribe(JObject args)
        {
            var raw = args?["events"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(raw)) return Err("请提供 events 参数, 逗号分隔。");

            var sub = new Subscription
            {
                Id = "sub-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                CreatedAt = DateTime.UtcNow,
            };
            foreach (var part in raw.Split(','))
            {
                var k = part?.Trim();
                if (!string.IsNullOrEmpty(k)) sub.Events.Add(k);
            }
            _subs[sub.Id] = sub;
            return Ok($"subscription_id: {sub.Id}\nevents: {string.Join(",", sub.Events)}");
        }

        // ── e3d_unsubscribe ─────────────────────────────────────────────
        public static ToolCallResult HandleUnsubscribe(JObject args)
        {
            var id = args?["subscription_id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id)) return Err("请提供 subscription_id。");
            return _subs.TryRemove(id, out _)
                ? Ok($"OK: unsubscribed {id}")
                : Err($"未找到 subscription {id}");
        }

        // ── e3d_poll_events ─────────────────────────────────────────────
        // Drain pending events for the subscription. Each call returns at
        // most `max` events; if more are buffered they remain in the queue.
        public static ToolCallResult HandlePollEvents(JObject args)
        {
            var id = args?["subscription_id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id)) return Err("请提供 subscription_id。");
            if (!_subs.TryGetValue(id, out var sub)) return Err($"未找到 subscription {id}");
            int max = 50;
            try { max = args?["max"]?.Value<int>() ?? 50; } catch { /* tolerate string ints */ }
            if (max <= 0 || max > 500) max = 50;

            var sb = new StringBuilder();
            int drained = 0;
            while (drained < max && sub.Queue.TryDequeue(out var ev))
            {
                sb.AppendLine($"  [{ev.At:HH:mm:ss}] {ev.Kind}: {ev.ElementPath} ({ev.Detail})");
                drained++;
            }
            return Ok(drained == 0
                ? $"subscription {id}: 0 events"
                : $"subscription {id}: {drained} events\n{sb}");
        }

        /// <summary>
        /// External hook — call from <c>McpServerAddin</c> when E3D fires
        /// CurrentElementChanged or any other notable event. Distributes to
        /// each interested subscription. Cheap if there are no subscribers.
        /// </summary>
        public static void Publish(string kind, string elementPath, string detail = "")
        {
            if (_subs.IsEmpty) return;
            foreach (var sub in _subs.Values)
            {
                if (sub.Events.Count > 0 && !sub.Events.Contains(kind) && !sub.Events.Contains("*")) continue;
                if (sub.Queue.Count >= MaxQueueDepth) continue; // drop oldest semantics: just refuse to grow further
                sub.Queue.Enqueue(new EventEntry
                {
                    Kind = kind,
                    ElementPath = elementPath ?? "",
                    Detail = detail ?? "",
                    At = DateTime.UtcNow,
                });
            }
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
    }
}
