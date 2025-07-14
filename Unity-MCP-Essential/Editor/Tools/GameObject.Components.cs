#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet.Model.Unity;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_GameObject
    {
        [McpPluginTool
        (
            "GameObject_AddComponent",
            Title = "Add Component to a GameObject in opened Prefab or in a Scene"
        )]
        [Description("Add a component to a GameObject.")]
        public string AddComponent
        (
            [Description("Full name of the Component. It should include full namespace path and the class name.")]
            string[] componentNames,
            GameObjectRef gameObjectRef
        )
        => MainThread.Instance.Run(() =>
        {
            var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
            if (error != null)
                return error;

            if ((componentNames?.Length ?? 0) == 0)
                return $"[Error] No component names provided.";

            var stringBuilder = new StringBuilder();

            foreach (var componentName in componentNames)
            {
                var type = TypeUtils.GetType(componentName);
                if (type == null)
                {
                    stringBuilder.AppendLine(Tool_Component.Error.NotFoundComponentType(componentName));
                    continue;
                }

                // Check if type is a subclass of UnityEngine.Component
                if (!typeof(UnityEngine.Component).IsAssignableFrom(type))
                {
                    stringBuilder.AppendLine(Tool_Component.Error.TypeMustBeComponent(componentName));
                    continue;
                }

                var newComponent = go.AddComponent(type);

                stringBuilder.AppendLine($"[Success] Added component '{componentName}'. Component instanceID='{newComponent.GetInstanceID()}'.");
            }

            stringBuilder.AppendLine(go.Print());
            return stringBuilder.ToString();
        });

        [McpPluginTool
        (
            "GameObject_DestroyComponents",
            Title = "Destroy Components from a GameObject in opened Prefab or in a Scene"
        )]
        [Description("Destroy one or many components from target GameObject.")]
        public string DestroyComponents
        (
            GameObjectRef gameObjectRef,
            ComponentRefList destroyComponentRefs
        )
        => MainThread.Instance.Run(() =>
        {
            var go = GameObjectUtils.FindBy(gameObjectRef, out var error);
            if (error != null)
                return error;

            var destroyCounter = 0;
            var stringBuilder = new StringBuilder();

            var allComponents = go.GetComponents<UnityEngine.Component>();
            foreach (var component in allComponents)
            {
                if (destroyComponentRefs.Any(cr => cr.Matches(component)))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                    destroyCounter++;
                    stringBuilder.AppendLine($"[Success] Destroyed component instanceID='{component.GetInstanceID()}', type='{component.GetType().FullName}'.");
                }
            }

            if (destroyCounter == 0)
                return Error.NotFoundComponents(destroyComponentRefs, allComponents);

            return $"[Success] Destroyed {destroyCounter} components from GameObject.\n{stringBuilder.ToString()}";
        });
    }
} 