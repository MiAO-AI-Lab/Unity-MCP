#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public static class ExtensionsObject
    {
        public static Task<T> TaskFromResult<T>(this T response)
            => Task.FromResult(response);

        public static T[] MakeArray<T>(this T item) => new T[] { item };
        public static List<T> MakeList<T>(this T item) => new List<T> { item };

        public static string JoinString(this IEnumerable<string> items, string seperator)
            => string.Join(seperator, items);
        public static string JoinString(this IEnumerable<int> items, string seperator)
            => string.Join(seperator, items);

        public static JsonElement ToJsonElement(this object obj)
        {
            // Serialize the object to a UTF-8 byte array
            var json = JsonUtils.Serialize(obj);

            // Parse the JSON string into a JsonDocument and return the root element
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone(); // Clone to make it usable outside the 'using' block
        }
    }
}