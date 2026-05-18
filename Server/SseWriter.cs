using System.IO;
using System.Text;

namespace E3DMcpServer.Server
{
    public class SseWriter
    {
        private readonly Stream _stream;
        private readonly object _lock = new object();

        public SseWriter(Stream stream)
        {
            _stream = stream;
        }

        public void SendEvent(string eventType, string data)
        {
            lock (_lock)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat("event: {0}\n", eventType);

                    if (data.Contains("\n"))
                    {
                        foreach (var line in data.Split('\n'))
                            sb.AppendFormat("data: {0}\n", line);
                    }
                    else
                    {
                        sb.AppendFormat("data: {0}\n", data);
                    }

                    sb.Append("\n");

                    byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    _stream.Write(bytes, 0, bytes.Length);
                    _stream.Flush();
                }
                catch
                {
                    // Client disconnected - ignore write errors
                }
            }
        }

        public void SendEndpoint(string sessionId)
        {
            SendEvent("endpoint", $"/message?sessionId={sessionId}");
        }

        public void SendMessage(string json)
        {
            SendEvent("message", json);
        }
    }
}
