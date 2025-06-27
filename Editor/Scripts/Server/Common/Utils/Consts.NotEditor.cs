#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if !UNITY_EDITOR
namespace com.IvanMurzak.Unity.MCP.Common
{
    public static partial class Consts
    {
        public static partial class Log
        {
            public const string Tag = "[AI]";

            public const string Trce = "trce: ";
            public const string Dbug = "dbug: ";
            public const string Info = "info: ";
            public const string Warn = "warn: ";
            public const string Fail = "fail: ";
            public const string Crit = "crit: ";

            public static partial class Color
            {
                public const string TagStart = "";
                public const string TagEnd = "";

                public const string LevelStart = "";
                public const string LevelEnd = "";

                public const string CategoryStart = "";
                public const string CategoryEnd = "";
            }
        }
    }
}
#endif