#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
namespace com.IvanMurzak.ReflectorNet.Model.Unity
{
    [System.Serializable]
    public class ComponentDataLight
    {
        public string typeName { get; set; } = string.Empty;
        public Enabled isEnabled { get; set; }
        public int instanceID { get; set; }

        public ComponentDataLight() { }

        public enum Enabled
        {
            NA = -1,
            False = 0,
            True = 1
        }
    }
    public static class ComponentDataLightExtension
    {
        public static bool IsEnabled(this ComponentDataLight componentData)
            => componentData.isEnabled == ComponentDataLight.Enabled.True;
    }
    public static class ComponentDataLightEnabledExtension
    {
        public static bool ToBool(this ComponentDataLight.Enabled enabled)
            => enabled == ComponentDataLight.Enabled.True;
    }
}