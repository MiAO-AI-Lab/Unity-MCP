#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Model.Unity;

namespace com.MiAO.MCP.Utils
{
    public static class ExtensionsSerializedMember
    {
        public static int GetInstanceID(this SerializedMember member)
            => member.GetValue<ObjectRef>()?.instanceID
            ?? member.GetField("instanceID")?.GetValue<int>()
            ?? 0;
    }
}