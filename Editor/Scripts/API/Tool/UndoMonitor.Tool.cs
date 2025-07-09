using System.Linq;
using System.Text;
using System.ComponentModel;
using UnityEngine;
using Unity.MCP;
using com.MiAO.Unity.MCP.Common;

namespace com.MiAO.Unity.MCP.Editor.API
{
    public partial class Tool_UndoMonitor
    {
        [McpPluginTool("UndoMonitor_GetStatus", Title = "Get Unity Undo Monitor Status")]
        [Description("Get the current status and operation history from Unity Undo Monitor")]
        public string GetStatus()
        {
            var sb = new StringBuilder();
            
            // 基本状态信息
            sb.AppendLine("=== Unity Undo Monitor Status ===");
            sb.AppendLine(UnityUndoMonitor.GetStatusInfo());
            sb.AppendLine();
            
            // 操作历史
            var operations = UnityUndoMonitor.GetAllOperations();
            sb.AppendLine($"=== Operation History ({operations.Count} total) ===");
            
            if (operations.Count == 0)
            {
                sb.AppendLine("No operations recorded yet.");
            }
            else
            {
                // 显示最近的10个操作（从新到旧）
                var recentOps = UnityUndoMonitor.GetRecentOperations(10);
                foreach (var op in recentOps.AsEnumerable().Reverse())
                {
                    sb.AppendLine($"{op.timestamp:HH:mm:ss} - {op.DisplayName}");
                }
                
                if (operations.Count > 10)
                {
                    sb.AppendLine($"... and {operations.Count - 10} more operations");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine($"🌟 MCP Operations: {UnityUndoMonitor.GetMcpOperationCount()}");
            sb.AppendLine($"🖱️ Manual Operations: {UnityUndoMonitor.GetManualOperationCount()}");
            
            return sb.ToString();
        }
        
        [McpPluginTool("UndoMonitor_ClearHistory", Title = "Clear Unity Undo Monitor History")]
        [Description("Clear the operation history in Unity Undo Monitor (for testing purposes)")]
        public string ClearHistory()
        {
            var beforeCount = UnityUndoMonitor.GetAllOperations().Count;
            UnityUndoMonitor.ClearHistory();
            return $"[Success] Cleared {beforeCount} operations from Unity Undo Monitor history.";
        }
        
        [McpPluginTool("UndoMonitor_PerformUndo", Title = "Perform Unity Undo via Monitor")]
        [Description("Perform undo operation using Unity's native undo system via the monitor")]
        public string PerformUndo()
        {
            try
            {
                UnityUndoMonitor.PerformUndo();
                return "[Success] Undo operation performed via Unity Undo Monitor.";
            }
            catch (System.Exception ex)
            {
                return $"[Error] Failed to perform undo: {ex.Message}";
            }
        }
    }
} 