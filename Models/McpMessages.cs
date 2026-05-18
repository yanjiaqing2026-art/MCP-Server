using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DMcpServer.Models
{
    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public JToken Params { get; set; }
    }

    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError Error { get; set; }
    }

    public class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }
    }

    public class InitializeParams
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; }

        [JsonProperty("capabilities")]
        public JObject Capabilities { get; set; }

        [JsonProperty("clientInfo")]
        public ClientInfo ClientInfo { get; set; }
    }

    public class ClientInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }

    public class InitializeResult
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonProperty("capabilities")]
        public ServerCapabilities Capabilities { get; set; } = new ServerCapabilities();

        [JsonProperty("serverInfo")]
        public ServerInfo ServerInfo { get; set; } = new ServerInfo();
    }

    public class ServerCapabilities
    {
        [JsonProperty("tools")]
        public ToolsCapability Tools { get; set; } = new ToolsCapability();
    }

    public class ToolsCapability
    {
        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; } = false;
    }

    public class ServerInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "E3D-MCP-Server";

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";
    }

    public class ToolDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("inputSchema")]
        public ToolInputSchema InputSchema { get; set; }
    }

    public class ToolInputSchema
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properties")]
        public Dictionary<string, ToolProperty> Properties { get; set; } = new Dictionary<string, ToolProperty>();

        [JsonProperty("required", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Required { get; set; }
    }

    public class ToolProperty
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }

    public class ToolsListResult
    {
        [JsonProperty("tools")]
        public List<ToolDefinition> Tools { get; set; }
    }

    public class ToolCallParams
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public JObject Arguments { get; set; }
    }

    public class ToolCallResult
    {
        [JsonProperty("content")]
        public List<ContentBlock> Content { get; set; } = new List<ContentBlock>();

        [JsonProperty("isError")]
        public bool IsError { get; set; }
    }

    public class ContentBlock
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public static class JsonRpcErrors
    {
        public static readonly JsonRpcError ParseError = new JsonRpcError
        { Code = -32700, Message = "Parse error" };

        public static readonly JsonRpcError InvalidRequest = new JsonRpcError
        { Code = -32600, Message = "Invalid Request" };

        public static readonly JsonRpcError MethodNotFound = new JsonRpcError
        { Code = -32601, Message = "Method not found" };

        public static readonly JsonRpcError InvalidParams = new JsonRpcError
        { Code = -32602, Message = "Invalid params" };

        public static readonly JsonRpcError InternalError = new JsonRpcError
        { Code = -32603, Message = "Internal error" };
    }
}
