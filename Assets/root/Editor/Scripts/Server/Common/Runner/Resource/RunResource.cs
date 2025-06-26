#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public class RunResource : IRunResource
    {
        public string Route { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? MimeType { get; set; }

        [JsonIgnore]
        public IRunResourceContent RunGetContent { get; set; }

        [JsonIgnore]
        public IRunResourceContext RunListContext { get; set; }

        public RunResource(string route, string name, IRunResourceContent runnerGetContent, IRunResourceContext runnerListContext, string? description = null, string? mimeType = null)
        {
            Route = route;
            Name = name;
            RunGetContent = runnerGetContent;
            RunListContext = runnerListContext;
            Description = description;
            MimeType = mimeType;
        }
    }
}