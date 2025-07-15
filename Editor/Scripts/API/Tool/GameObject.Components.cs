#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.ReflectorNet.Model.Unity;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Editor.API
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
            var addedComponents = new System.Collections.Generic.List<UnityEngine.Component>();

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
                addedComponents.Add(newComponent);
                stringBuilder.AppendLine($"[Success] Added component '{componentName}'. Component instanceID='{newComponent.GetInstanceID()}'.");
            }

            // Register undo for all added components
            if (addedComponents.Count > 0)
            {
                McpUndoHelper.RegisterCreatedObjects(addedComponents, "Add Component", go.name);
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
            var componentsToDestroy = new System.Collections.Generic.List<UnityEngine.Component>();

            var allComponents = go.GetComponents<UnityEngine.Component>();
            
            // Find components to destroy
            foreach (var component in allComponents)
            {    
                if (destroyComponentRefs.Any(cr => cr.Matches(component)))
                {
                    componentsToDestroy.Add(component);
                }
            }

            if (componentsToDestroy.Count == 0)
                return Error.NotFoundComponents(destroyComponentRefs, allComponents);

            // Register undo and destroy components
            McpUndoHelper.RegisterDestroyedObjects(componentsToDestroy, "Remove Component", true);

            foreach (var component in componentsToDestroy)
            {
                destroyCounter++;
                stringBuilder.AppendLine($"[Success] Destroyed component instanceID='{component.GetInstanceID()}', type='{component.GetType().FullName}'.");
            }

            return $"[Success] Destroyed {destroyCounter} components from GameObject.\n{stringBuilder.ToString()}";
        });
    }
} 