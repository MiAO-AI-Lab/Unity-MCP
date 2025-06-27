#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Unity.MCP.Common
{
    public static partial class ErrorUtils
    {
        public static bool ExtractProcessId(string error, out int processId)
        {
            // Define a regex pattern to match the process ID
            var pattern = @"The file is locked by: ""[^""]+ \((\d+)\)""";
            var match = Regex.Match(error, pattern);

            processId = -1;

            return match.Success && int.TryParse(match.Groups[1].Value, out processId);
        }
    }
}