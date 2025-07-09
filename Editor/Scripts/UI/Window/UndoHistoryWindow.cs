using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.MCP;

namespace com.MiAO.Unity.MCP.Editor.UI
{
    /// <summary>
    /// 撤销历史面板窗口
    /// </summary>
    public class UndoHistoryWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool autoRefresh = true;
        private double lastRefreshTime = 0;
        private const double REFRESH_INTERVAL = 0.5; // 0.5秒刷新一次
        
        [MenuItem("Tools/MCP/Undo History")]
        public static void ShowWindow()
        {
            var window = GetWindow<UndoHistoryWindow>();
            window.titleContent = new GUIContent("Unity Undo History");
            window.Show();
        }
        
        private void OnEnable()
        {
            // 订阅撤销监听器的事件
            UnityUndoMonitor.OnOperationsChanged += Repaint;
        }
        
        private void OnDisable()
        {
            // 取消订阅
            UnityUndoMonitor.OnOperationsChanged -= Repaint;
        }
        
        private void OnDestroy()
        {
            // 窗口销毁时确保取消订阅
            UnityUndoMonitor.OnOperationsChanged -= Repaint;
        }
        
        private void Update()
        {
            // 自动刷新
            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > REFRESH_INTERVAL)
            {
                Repaint();
                lastRefreshTime = EditorApplication.timeSinceStartup;
            }
        }
        
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            // 标题和控制区域
            DrawHeader();
            
            EditorGUILayout.Space(10);
            
            // 撤销历史列表
            DrawUndoHistory();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUILayout.Label("Unity Undo History", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            // 自动刷新切换
            autoRefresh = GUILayout.Toggle(autoRefresh, "Auto Refresh", EditorStyles.toolbarButton);
            
            // 手动刷新按钮
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                Repaint();
            }
            
            // 清除历史按钮
            if (GUILayout.Button("Clear History", EditorStyles.toolbarButton))
            {
                UnityUndoMonitor.ClearHistory();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 统计信息
            var operations = UnityUndoMonitor.GetAllOperations();
            var mcpCount = UnityUndoMonitor.GetMcpOperationCount();
            var manualCount = UnityUndoMonitor.GetManualOperationCount();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Total Operations: {operations.Count}", EditorStyles.miniLabel);
            GUILayout.Space(20);
            GUILayout.Label($"⭐ MCP: {mcpCount}", EditorStyles.miniLabel);
            GUILayout.Space(20);
            GUILayout.Label($"🖱️ Manual: {manualCount}", EditorStyles.miniLabel);
            GUILayout.Space(20);
            GUILayout.Label($"Status: {UnityUndoMonitor.GetStatusInfo()}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Separator();
        }
        
        private void DrawUndoHistory()
        {
            var operations = UnityUndoMonitor.GetAllOperations();
            
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox("No operations recorded yet.\n\nPerform some MCP operations or manual Unity operations to see them here.", MessageType.Info);
                return;
            }
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // 从最新到最旧显示操作
            var reversedOperations = operations.AsEnumerable().Reverse().ToArray();
            
            for (int i = 0; i < reversedOperations.Length; i++)
            {
                var operation = reversedOperations[i];
                DrawOperationItem(operation, i == 0); // 第一个是最新的操作
            }
            
            EditorGUILayout.EndScrollView();
            
            // 底部操作按钮
            DrawBottomControls();
        }
        
        private void DrawOperationItem(UnityUndoMonitor.UndoOperation operation, bool isLatest)
        {
            EditorGUILayout.BeginHorizontal(isLatest ? EditorStyles.helpBox : GUIStyle.none);
            
            // 操作图标和名称
            var style = operation.isMcpOperation ? EditorStyles.boldLabel : EditorStyles.label;
            var color = operation.isMcpOperation ? Color.cyan : Color.white;
            
            var originalColor = GUI.color;
            GUI.color = color;
            
            GUILayout.Label(operation.DisplayName, style, GUILayout.ExpandWidth(true));
            
            GUI.color = originalColor;
            
            // 时间戳
            GUILayout.Label(operation.timestamp.ToString("HH:mm:ss"), EditorStyles.miniLabel, GUILayout.Width(60));
            
            // 操作按钮
            if (isLatest && GUILayout.Button("Undo", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                UnityUndoMonitor.PerformUndo();
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (isLatest)
            {
                EditorGUILayout.Space(2);
            }
        }
        
        private void DrawBottomControls()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.FlexibleSpace();
            
            // 撤销按钮
            EditorGUI.BeginDisabledGroup(UnityUndoMonitor.GetAllOperations().Count == 0);
            if (GUILayout.Button("⟲ Undo (Ctrl+Z)", GUILayout.Width(120), GUILayout.Height(25)))
            {
                UnityUndoMonitor.PerformUndo();
            }
            EditorGUI.EndDisabledGroup();
            
            GUILayout.Space(10);
            
            // 重做按钮（如果Unity支持的话）
            if (GUILayout.Button("⟳ Redo (Ctrl+Y)", GUILayout.Width(120), GUILayout.Height(25)))
            {
                UnityUndoMonitor.PerformRedo();
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 提示信息
            EditorGUILayout.HelpBox(
                "⭐ = MCP Operations (via tool calls)\n" +
                "🖱️ = Manual Operations (user actions in Unity)\n\n" +
                "Use Ctrl+Z/Ctrl+Y or the buttons above to undo/redo operations.\n" +
                "All operations use Unity's native undo system for optimal performance.",
                MessageType.Info
            );
        }
    }
} 