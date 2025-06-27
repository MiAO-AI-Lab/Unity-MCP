#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using com.IvanMurzak.ReflectorNet.Model.Unity;

namespace com.IvanMurzak.Unity.MCP.Utils
{
    public static class ExtensionsComponentRef
    {
        public static bool Matches(this ComponentRef componentRef, UnityEngine.Component component, int? index = null)
        {
            if (componentRef.InstanceID != 0)
            {
                return componentRef.InstanceID == component.GetInstanceID();
            }
            if (componentRef.Index >= 0 && index != null)
            {
                return componentRef.Index == index.Value;
            }
            if (!string.IsNullOrEmpty(componentRef.TypeName))
            {
                return component.GetType().FullName == componentRef.TypeName;
            }
            return false;
        }
    }
}