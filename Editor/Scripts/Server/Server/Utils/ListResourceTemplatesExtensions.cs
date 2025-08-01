#if !UNITY_5_3_OR_NEWER
using System;
using com.IvanMurzak.ReflectorNet.Model;
using ModelContextProtocol.Protocol;

namespace com.MiAO.MCP.Server
{
    public static class ListResourceTemplatesExtensions
    {
        public static ListResourceTemplatesResult SetError(this ListResourceTemplatesResult target, string message)
        {
            throw new Exception(message);
        }

        public static ResourceTemplate ToResourceTemplate(this IResponseResourceTemplate response)
        {
            return new ResourceTemplate()
            {
                UriTemplate = response.uriTemplate,
                Name = response.name,
                Description = response.description,
                MimeType = response.mimeType
            };
        }
    }
}
#endif