#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using System.Text;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.Unity.MCP.Utils;
using UnityEditor;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace com.IvanMurzak.Unity.MCP.Reflection.Convertor
{
    public partial class RS_UnityEngineSprite : RS_UnityEngineObject<UnityEngine.Sprite>
    {
        public override StringBuilder Populate(Reflector reflector, ref object obj, SerializedMember data, int depth = 0, StringBuilder stringBuilder = null,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            ILogger? logger = null)
        {
            var instanceID = data.GetInstanceID();
            if (instanceID == 0)
            {
                obj = null;
                return stringBuilder?.AppendLine($"[Success] InstanceID is 0. Cleared the reference.");
            }
            var textureOrSprite = EditorUtility.InstanceIDToObject(instanceID);
            if (textureOrSprite == null)
                return stringBuilder?.AppendLine($"[Error] InstanceID {instanceID} not found.");

            if (textureOrSprite is UnityEngine.Texture2D texture)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .OfType<UnityEngine.Sprite>()
                    .ToArray();
                if (sprites.Length == 0)
                    return stringBuilder?.AppendLine($"[Error] No sprites found for texture at path: {path}.");

                obj = sprites[0]; // Assign the first sprite found
                return stringBuilder?.AppendLine($"[Success] Assigned sprite from texture: {path}.");
            }
            if (textureOrSprite is UnityEngine.Sprite sprite)
            {
                obj = sprite;
                return stringBuilder?.AppendLine($"[Success] Assigned sprite: {sprite.name}.");
            }
            return stringBuilder?.AppendLine($"[Error] InstanceID {instanceID} is not a Texture2D or Sprite.");
        }
    }
}
#endif