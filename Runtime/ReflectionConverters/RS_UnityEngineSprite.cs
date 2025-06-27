#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Reflection;
using System.Text;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.IvanMurzak.Unity.MCP.Reflection.Convertor
{
    public partial class RS_UnityEngineSprite : RS_UnityEngineObject<UnityEngine.Sprite>
    {
        protected override SerializedMember InternalSerialize(Reflector reflector, object obj, Type type, string name = null, bool recursive = true,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            ILogger? logger = null)
        {
            if (obj is UnityEngine.Texture2D texture)
            {
                var objectRef = new ObjectRef(texture.GetInstanceID());
                return SerializedMember.FromValue(type, objectRef, name);
            }

            return base.InternalSerialize(reflector, obj, type, name, recursive, flags);
        }
        public override bool SetAsField(Reflector reflector, ref object obj, Type type, FieldInfo fieldInfo, SerializedMember? value, StringBuilder? stringBuilder = null,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            ILogger? logger = null)
        {
            var currentValue = fieldInfo.GetValue(obj);
            Populate(reflector, ref currentValue, value, 0, null, flags, logger);
            fieldInfo.SetValue(obj, currentValue);
            stringBuilder?.AppendLine($"[Success] Field '{value.name}' modified to '{currentValue}'.");
            return true;
        }
        public override bool SetAsProperty(Reflector reflector, ref object obj, Type type, PropertyInfo propertyInfo, SerializedMember? value, StringBuilder? stringBuilder = null,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            ILogger? logger = null)
        {
            var currentValue = propertyInfo.GetValue(obj);
            Populate(reflector, ref currentValue, value, 0, null, flags, logger);
            propertyInfo.SetValue(obj, currentValue);
            stringBuilder?.AppendLine($"[Success] Property '{value.name}' modified to '{currentValue}'.");
            return true;
        }
    }
}