#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.ReflectorNet.Model.Unity
{
    [Description(@"GameObject reference. Used to find GameObject in opened Prefab or in a Scene.
Use one of the following properties:
1. 'instanceID' (int) - recommended. It finds the exact GameObject.
2. 'path' (string) - finds GameObject by path. It may find a wrong GameObject.
3. 'name' (string) - finds GameObject by name. It may find a wrong GameObject.")]
    public class GameObjectRef
    {
        [Description("GameObject 'instanceID' (int). Priority: 1. (Recommended)")]
        public int instanceID { get; set; } = 0;
        [Description("GameObject 'path'. Priority: 2.")]
        public string? path { get; set; } = null;
        [Description("GameObject 'name'. Priority: 3.")]
        public string? name { get; set; } = null;

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (instanceID != 0)
                    return true;
                if (!string.IsNullOrEmpty(path))
                    return true;
                if (!string.IsNullOrEmpty(name))
                    return true;
                return false;
            }
        }

        public GameObjectRef() { }

        public override string ToString()
        {
            if (instanceID != 0)
                return $"GameObject instanceID='{instanceID}'";
            if (!string.IsNullOrEmpty(path))
                return $"GameObject path='{path}'";
            if (!string.IsNullOrEmpty(name))
                return $"GameObject name='{name}'";
            return "GameObject unknown";
        }
    }
}