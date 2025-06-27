#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
namespace com.IvanMurzak.ReflectorNet.Model.Unity
{
    [System.Serializable]
    public class GameObjectDataLight
    {
        public string name { get; set; } = string.Empty;
        public string tag { get; set; } = "Untagged";
        public int layer { get; set; }
        public int instanceID { get; set; }

        public GameObjectDataLight() { }
    }
}