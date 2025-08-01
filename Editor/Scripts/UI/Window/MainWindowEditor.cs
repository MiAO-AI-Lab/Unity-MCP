using com.MiAO.MCP.Editor.Common;
using com.MiAO.MCP.Common;
using com.MiAO.MCP.Utils;
using R3;
using UnityEditor;
using UnityEngine;

namespace com.MiAO.MCP.Editor
{
    public partial class MainWindowEditor : EditorWindow
    {
        readonly CompositeDisposable _disposables = new();

        public static MainWindowEditor ShowWindow()
        {
            var window = GetWindow<MainWindowEditor>();
            window.UpdateWindowTitle();
            window.Focus();

            return window;
        }
        public static void ShowWindowVoid() => ShowWindow();

        public void Invalidate() => CreateGUI();
        void OnValidate() => McpPluginUnity.Validate();

        private void SaveChanges(string message)
        {
            saveChangesMessage = message;

            Undo.RecordObject(McpPluginUnity.AssetFile, message); // Undo record started
            base.SaveChanges();
            McpPluginUnity.Save();
            McpPluginUnity.InvalidateAssetFile();
            EditorUtility.SetDirty(McpPluginUnity.AssetFile); // Undo record completed
        }

        private void OnChanged(McpPluginUnity.Data data) => Repaint();

        private void OnEnable()
        {
            McpPluginUnity.SubscribeOnChanged(OnChanged);
            
            // Update window title
            UpdateWindowTitle();
            
            // Subscribe to language change events to update window title
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            
            // Subscribe to undo operations changes for auto-refresh
            UnityUndoMonitor.OnOperationsChanged += OnUndoOperationsChanged;
        }
        private void OnDisable()
        {
            McpPluginUnity.UnsubscribeOnChanged(OnChanged);
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            
            // Unsubscribe from undo operations changes
            UnityUndoMonitor.OnOperationsChanged -= OnUndoOperationsChanged;
            
            _disposables.Clear();
        }

        /// <summary>
        /// Update window title
        /// </summary>
        private void UpdateWindowTitle()
        {
            titleContent = new GUIContent(text: LocalizationManager.GetText("window.title"));
        }
    }
}