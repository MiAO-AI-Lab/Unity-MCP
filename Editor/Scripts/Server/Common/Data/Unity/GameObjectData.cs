#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System.Collections.Generic;
using com.IvanMurzak.ReflectorNet.Model.Unity;

namespace com.IvanMurzak.ReflectorNet.Model.Unity
{
    [System.Serializable]
    public class GameObjectData : GameObjectDataLight
    {
        public List<SerializedMember> components { get; set; } = new();

        public GameObjectData() { }
    }
}