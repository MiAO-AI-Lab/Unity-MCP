#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace com.IvanMurzak.Unity.MCP.Editor.Utils
{
    public static partial class ScriptUtils
    {
        /// <summary>
        /// Checks if the provided C# code has valid syntax.
        /// This method uses Roslyn to parse the code and check for syntax errors.
        /// </summary>
        /// <param name="code">
        /// <param name="errors"></param>
        /// <returns>True if the code has valid syntax; otherwise, false.</returns>
        public static bool IsValidCSharpSyntax(string code, out IEnumerable<Diagnostic> errors)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var diagnostics = syntaxTree.GetDiagnostics();

            errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            return !errors.Any();
        }
    }
}