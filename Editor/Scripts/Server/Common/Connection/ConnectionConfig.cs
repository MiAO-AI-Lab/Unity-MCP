#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class ConnectionConfig
    {
        public string Endpoint { get; set; } = "http://localhost:60606";

        public override string ToString()
            => $"Endpoint: {Endpoint}";
    }
}