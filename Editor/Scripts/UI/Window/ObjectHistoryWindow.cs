using System;
using System.Collections.Generic;
using System.Linq;
using com.MiAO.Unity.MCP.Editor.Common;
using Unity.MCP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.MiAO.Unity.MCP.Editor
{
    /// <summary>
    /// Window to display modification history for a specific object
    /// </summary>
    public class ObjectHistoryWindow : EditorWindow
    {
        private string _objectName;
        private List<UnityUndoMonitor.UndoOperation> _operations;
        private VisualElement _rootElement;
        private ScrollView _historyScrollView;
        private Label _titleLabel;
        private Label _summaryLabel;
        
        // Diff functionality
        private VisualElement _diffContainer;
        private VisualElement _leftDropZone;
        private VisualElement _rightDropZone;
        private Label _leftDropLabel;
        private Label _rightDropLabel;
        private UnityUndoMonitor.UndoOperation? _leftOperation;
        private UnityUndoMonitor.UndoOperation? _rightOperation;
        private Button _diffButton;
        
        // Drag and drop state
        private VisualElement _dragPreview;
        private bool _isDragging = false;
        private UnityUndoMonitor.UndoOperation? _draggedOperation;
        private VisualElement _draggedElement;

        public void Initialize(string objectName, List<UnityUndoMonitor.UndoOperation> operations)
        {
            _objectName = objectName;
            _operations = operations ?? new List<UnityUndoMonitor.UndoOperation>();
            
            titleContent = new GUIContent($"Object History: {objectName}");
            minSize = new Vector2(400, 300);
            maxSize = new Vector2(800, 600);
        }

        private void CreateGUI()
        {
            _rootElement = rootVisualElement;
            _rootElement.style.paddingLeft = 10;
            _rootElement.style.paddingRight = 10;
            _rootElement.style.paddingTop = 10;
            _rootElement.style.paddingBottom = 10;

            // Title
            _titleLabel = new Label(LocalizationManager.GetText("object_history.modification_history", new object[] { _objectName }));
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.marginBottom = 10;
            _rootElement.Add(_titleLabel);

            // Summary
            _summaryLabel = new Label(LocalizationManager.GetText("object_history.total_operations", _operations.Count));
            _summaryLabel.style.fontSize = 12;
            _summaryLabel.style.marginBottom = 15;
            _summaryLabel.style.color = new StyleColor(Color.gray);
            _rootElement.Add(_summaryLabel);

            // Diff section
            CreateDiffSection();

            // History scroll view
            _historyScrollView = new ScrollView(ScrollViewMode.Vertical);
            _historyScrollView.style.flexGrow = 1;
            _historyScrollView.style.maxHeight = new StyleLength(StyleKeyword.None);
            _rootElement.Add(_historyScrollView);

            // Buttons container
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.marginTop = 10;
            buttonsContainer.style.justifyContent = Justify.SpaceBetween;

            var exportButton = new Button(() => ExportHistory())
            {
                text = LocalizationManager.GetText("common.export")
            };
            exportButton.style.width = 120;

            var copyButton = new Button(() => CopyHistoryToClipboard())
            {
                text = LocalizationManager.GetText("common.copy")
            };
            copyButton.style.width = 120;

            var closeButton = new Button(() => Close())
            {
                text = LocalizationManager.GetText("common.close")
            };
            closeButton.style.width = 80;

            buttonsContainer.Add(exportButton);
            buttonsContainer.Add(copyButton);
            buttonsContainer.Add(closeButton);
            
            _rootElement.Add(buttonsContainer);

            // Populate history
            PopulateHistory();
        }

        private void PopulateHistory()
        {
            _historyScrollView.Clear();

            if (_operations.Count == 0)
            {
                var emptyLabel = new Label(LocalizationManager.GetText("object_history.no_operations"));
                emptyLabel.style.marginTop = 50;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.color = new StyleColor(Color.gray);
                _historyScrollView.Add(emptyLabel);
                return;
            }

            // Group operations by date
            var groupedOperations = _operations
                .GroupBy(op => op.timestamp.Date)
                .OrderByDescending(group => group.Key)
                .ToList();

            foreach (var dateGroup in groupedOperations)
            {
                // Date header
                var dateHeader = new Label(dateGroup.Key.ToString("yyyy-MM-dd"));
                dateHeader.style.fontSize = 14;
                dateHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                dateHeader.style.marginTop = 15;
                dateHeader.style.marginBottom = 5;
                dateHeader.style.color = new StyleColor(new Color(0.8f, 0.8f, 1f));
                _historyScrollView.Add(dateHeader);

                // Operations for this date
                var sortedOperations = dateGroup.OrderByDescending(op => op.timestamp).ToList();
                for (int i = 0; i < sortedOperations.Count; i++)
                {
                    var operation = sortedOperations[i];
                    var isStateChanging = IsStateChangingOperation(operation.operationName);
                    CreateHistoryItem(operation, i, isStateChanging);
                }
            }
        }

        private void CopyOperationDetails(UnityUndoMonitor.UndoOperation operation)
        {
            var details = $"Operation: {operation.operationName}\n" +
                         $"Type: {(operation.isMcpOperation ? "MCP" : "Manual")}\n" +
                         $"Timestamp: {operation.timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Group ID: {operation.groupId}\n" +
                         $"GUID: {operation.operationGuid}";
            
            EditorGUIUtility.systemCopyBuffer = details;
        }

        private void ExportHistory()
        {
            // Export history to console for debugging purposes
            var text = $"Modification History for '{_objectName}'\n";
            text += $"Total operations: {_operations.Count}\n";
            text += "".PadRight(50, '=') + "\n\n";

            foreach (var operation in _operations.OrderBy(op => op.timestamp))
            {
                var operationType = operation.operationName;
                text += $"[{operation.timestamp:yyyy-MM-dd HH:mm:ss}] [{(operation.isMcpOperation ? "MCP" : "Manual")}] {operationType}\n";
                text += $"  Full: {operation.operationName}\n";
                text += $"  GUID: {operation.operationGuid}\n\n";
            }
            
            Debug.Log(text);
        }

        private void CopyHistoryToClipboard()
        {
            var text = $"Modification History for '{_objectName}'\n";
            text += $"Total operations: {_operations.Count}\n";
            text += "".PadRight(50, '=') + "\n\n";

            foreach (var operation in _operations.OrderBy(op => op.timestamp))
            {
                var operationType = operation.operationName;
                text += $"[{operation.timestamp:yyyy-MM-dd HH:mm:ss}] [{(operation.isMcpOperation ? "MCP" : "Manual")}] {operationType}\n";
                text += $"  Full: {operation.operationName}\n";
                text += $"  GUID: {operation.operationGuid}\n\n";
            }

            EditorGUIUtility.systemCopyBuffer = text;
        }

        /// <summary>
        /// Check if operation type represents a state-changing action (generic approach)
        /// </summary>
        private bool IsStateChangingOperation(string operationType)
        {
            if (string.IsNullOrEmpty(operationType))
                return false;
                
            var lowerType = operationType.ToLower();
            
            // Use the same logic as other parts of the system for consistency
            var stateChangingPrefixes = new[]
            {
                "set", "add", "remove", "create", "delete", "destroy", 
                "modify", "change", "update", "edit", "move", "copy", 
                "paste", "duplicate", "rename", "replace", "assign", 
                "apply", "toggle"
            };
            
            foreach (var prefix in stateChangingPrefixes)
            {
                if (lowerType.StartsWith(prefix))
                {
                    return true;
                }
            }
            
            // Also check for common patterns in operation types
            if (lowerType.Contains("position") || lowerType.Contains("rotation") || 
                lowerType.Contains("scale") || lowerType.Contains("transform") ||
                lowerType.Contains("property") || lowerType.Contains("component"))
            {
                return true;
            }
            
            return false;
        }

        private void CreateDiffSection()
        {
            // Diff container
            _diffContainer = new VisualElement();
            _diffContainer.style.marginBottom = 15;
            _diffContainer.style.paddingLeft = 10;
            _diffContainer.style.paddingRight = 10;
            _diffContainer.style.paddingTop = 10;
            _diffContainer.style.paddingBottom = 10;
            _diffContainer.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.3f));
            _diffContainer.style.borderTopLeftRadius = 5;
            _diffContainer.style.borderTopRightRadius = 5;
            _diffContainer.style.borderBottomLeftRadius = 5;
            _diffContainer.style.borderBottomRightRadius = 5;
            _rootElement.Add(_diffContainer);

            // Diff title
            var diffTitle = new Label(LocalizationManager.GetText("object_history.compare_operations_title"));
            diffTitle.style.fontSize = 14;
            diffTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            diffTitle.style.marginBottom = 10;
            diffTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _diffContainer.Add(diffTitle);

            // Drop zones container
            var dropZonesContainer = new VisualElement();
            dropZonesContainer.style.flexDirection = FlexDirection.Row;
            dropZonesContainer.style.justifyContent = Justify.SpaceBetween;
            _diffContainer.Add(dropZonesContainer);

            // Left drop zone
            _leftDropZone = new VisualElement();
            _leftDropZone.style.width = new StyleLength(new Length(45, LengthUnit.Percent));
            _leftDropZone.style.height = 80;
            _leftDropZone.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.4f));
            _leftDropZone.style.borderLeftWidth = 2;
            _leftDropZone.style.borderRightWidth = 2;
            _leftDropZone.style.borderTopWidth = 2;
            _leftDropZone.style.borderBottomWidth = 2;
            _leftDropZone.style.borderLeftColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _leftDropZone.style.borderRightColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _leftDropZone.style.borderTopColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _leftDropZone.style.borderBottomColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _leftDropZone.style.borderTopLeftRadius = 5;
            _leftDropZone.style.borderTopRightRadius = 5;
            _leftDropZone.style.borderBottomLeftRadius = 5;
            _leftDropZone.style.borderBottomRightRadius = 5;
            _leftDropZone.style.justifyContent = Justify.Center;
            _leftDropZone.style.alignItems = Align.Center;

            _leftDropLabel = new Label(LocalizationManager.GetText("object_history.drop_operation_a"));
            _leftDropLabel.style.fontSize = 12;
            _leftDropLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
            _leftDropLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _leftDropZone.Add(_leftDropLabel);

            // Right drop zone
            _rightDropZone = new VisualElement();
            _rightDropZone.style.width = new StyleLength(new Length(45, LengthUnit.Percent));
            _rightDropZone.style.height = 80;
            _rightDropZone.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.4f));
            _rightDropZone.style.borderLeftWidth = 2;
            _rightDropZone.style.borderRightWidth = 2;
            _rightDropZone.style.borderTopWidth = 2;
            _rightDropZone.style.borderBottomWidth = 2;
            _rightDropZone.style.borderLeftColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _rightDropZone.style.borderRightColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _rightDropZone.style.borderTopColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _rightDropZone.style.borderBottomColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _rightDropZone.style.borderTopLeftRadius = 5;
            _rightDropZone.style.borderTopRightRadius = 5;
            _rightDropZone.style.borderBottomLeftRadius = 5;
            _rightDropZone.style.borderBottomRightRadius = 5;
            _rightDropZone.style.justifyContent = Justify.Center;
            _rightDropZone.style.alignItems = Align.Center;

            _rightDropLabel = new Label(LocalizationManager.GetText("object_history.drop_operation_b"));
            _rightDropLabel.style.fontSize = 12;
            _rightDropLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
            _rightDropLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _rightDropZone.Add(_rightDropLabel);

            dropZonesContainer.Add(_leftDropZone);
            dropZonesContainer.Add(_rightDropZone);

            // Diff button
            _diffButton = new Button(() => PerformDiff())
            {
                text = LocalizationManager.GetText("object_history.compare_operations")
            };
            _diffButton.style.marginTop = 10;
            _diffButton.style.height = 30;
            _diffButton.SetEnabled(false);
            _diffContainer.Add(_diffButton);

            // Clear button
            var clearButton = new Button(() => ClearDiffSelection())
            {
                text = LocalizationManager.GetText("object_history.clear_selection")
            };
            clearButton.style.marginTop = 5;
            clearButton.style.height = 25;
            _diffContainer.Add(clearButton);
        }

        private void CreateHistoryItem(UnityUndoMonitor.UndoOperation operation, int index, bool isStateChanging)
        {
            var itemElement = new VisualElement();
            itemElement.style.marginBottom = 5;
            itemElement.style.paddingLeft = 10;
            itemElement.style.paddingRight = 10;
            itemElement.style.paddingTop = 8;
            itemElement.style.paddingBottom = 8;
            itemElement.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.3f));
            itemElement.style.borderTopLeftRadius = 5;
            itemElement.style.borderTopRightRadius = 5;
            itemElement.style.borderBottomLeftRadius = 5;
            itemElement.style.borderBottomRightRadius = 5;
            itemElement.style.borderLeftWidth = 3;
            itemElement.style.borderLeftColor = new StyleColor(operation.isMcpOperation ? Color.cyan : new Color(1f, 0.6f, 0f)); // Orange for manual

            // Make draggable with smooth visual feedback
            itemElement.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // Left mouse button
                {
                    StartDrag(operation, itemElement, evt.mousePosition);
                    evt.StopPropagation();
                    evt.PreventDefault();
                }
            });

            // Add hover effects for better UX
            itemElement.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!_isDragging)
                {
                    itemElement.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.4f));
                    itemElement.style.borderLeftWidth = 4;
                }
            });

            itemElement.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (!_isDragging)
                {
                    itemElement.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.3f));
                    itemElement.style.borderLeftWidth = 3;
                }
            });

            // Add context menu
            itemElement.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                evt.menu.AppendAction(LocalizationManager.GetText("object_history.copy_operation_details"), action => CopyOperationDetails(operation));
                evt.menu.AppendAction(LocalizationManager.GetText("object_history.copy_guid"), action => EditorGUIUtility.systemCopyBuffer = operation.operationGuid ?? "");
                evt.menu.AppendAction(LocalizationManager.GetText("object_history.copy_timestamp"), action => EditorGUIUtility.systemCopyBuffer = operation.timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            });

            // Header
            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.SpaceBetween;
            headerContainer.style.alignItems = Align.Center;

            var operationInfo = new Label($"#{index + 1} {operation.DisplayName}");
            operationInfo.style.fontSize = 12;
            operationInfo.style.unityFontStyleAndWeight = FontStyle.Bold;
            operationInfo.style.color = new StyleColor(operation.isMcpOperation ? Color.cyan : new Color(1f, 0.6f, 0f)); // Orange for manual

            var timeLabel = new Label(operation.timestamp.ToString("HH:mm:ss"));
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new StyleColor(Color.gray);

            headerContainer.Add(operationInfo);
            headerContainer.Add(timeLabel);
            itemElement.Add(headerContainer);

            // Details if state changing
            if (isStateChanging)
            {
                // Show operation details using the cleaned operation name
                var detailsLabel = new Label($"Operation: {operation.operationName}");
                detailsLabel.style.fontSize = 10;
                detailsLabel.style.color = new StyleColor(Color.gray);
                detailsLabel.style.marginTop = 2;
                itemElement.Add(detailsLabel);
            }

            if (operation.targetInstanceID != 0)
            {
                var instanceLabel = new Label($"Instance ID: {operation.targetInstanceID}");
                instanceLabel.style.fontSize = 9;
                instanceLabel.style.color = new StyleColor(Color.gray);
                instanceLabel.style.marginTop = 1;
                itemElement.Add(instanceLabel);
            }

            _historyScrollView.Add(itemElement);
        }

        private void StartDrag(UnityUndoMonitor.UndoOperation operation, VisualElement dragElement, Vector2 mousePosition)
        {
            _isDragging = true;
            _draggedOperation = operation;
            _draggedElement = dragElement;

            // Get operation color (MCP = cyan, Manual = orange)
            var operationColor = operation.isMcpOperation ? Color.cyan : new Color(1f, 0.6f, 0f); // Orange for manual

            // Make the original element semi-transparent while dragging
            dragElement.style.opacity = 0.5f;
            dragElement.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.2f));

            // Create enhanced drag preview with operation colors
            CreateDragPreview(operation, operationColor);

            // Register global mouse events for smooth dragging
            _rootElement.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _rootElement.RegisterCallback<MouseUpEvent>(OnMouseUp);

            // Animate drop zones to indicate they're ready
            AnimateDropZones(true);
        }

        private void CreateDragPreview(UnityUndoMonitor.UndoOperation operation, Color operationColor)
        {
            _dragPreview = new VisualElement();
            _dragPreview.style.position = Position.Absolute;
            _dragPreview.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.95f));
            _dragPreview.style.borderTopLeftRadius = 8;
            _dragPreview.style.borderTopRightRadius = 8;
            _dragPreview.style.borderBottomLeftRadius = 8;
            _dragPreview.style.borderBottomRightRadius = 8;
            _dragPreview.style.borderLeftWidth = 4;
            _dragPreview.style.borderLeftColor = new StyleColor(operationColor);
            _dragPreview.style.paddingLeft = 12;
            _dragPreview.style.paddingRight = 12;
            _dragPreview.style.paddingTop = 8;
            _dragPreview.style.paddingBottom = 8;
            _dragPreview.style.minWidth = 200;
            _dragPreview.style.maxWidth = 300;

            // Add shadow effect
            _dragPreview.style.borderTopWidth = 1;
            _dragPreview.style.borderRightWidth = 1;
            _dragPreview.style.borderBottomWidth = 1;
            _dragPreview.style.borderTopColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.3f));
            _dragPreview.style.borderRightColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.3f));
            _dragPreview.style.borderBottomColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.3f));

            // Operation type indicator
            var typeLabel = new Label(operation.isMcpOperation ? LocalizationManager.GetText("object_history.type_mcp") : LocalizationManager.GetText("object_history.type_manual"));
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new StyleColor(operationColor);
            typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            typeLabel.style.marginBottom = 2;
            _dragPreview.Add(typeLabel);

            // Operation name
            var nameLabel = new Label(operation.operationName);
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = new StyleColor(Color.white);
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            _dragPreview.Add(nameLabel);

            // Timestamp
            var timeLabel = new Label(operation.timestamp.ToString("HH:mm:ss"));
            timeLabel.style.fontSize = 9;
            timeLabel.style.color = new StyleColor(Color.gray);
            typeLabel.style.marginTop = 2;
            _dragPreview.Add(timeLabel);

            _rootElement.Add(_dragPreview);
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (_isDragging && _dragPreview != null)
            {
                // Update drag preview position with offset to follow cursor
                _dragPreview.style.left = evt.mousePosition.x + 10;
                _dragPreview.style.top = evt.mousePosition.y - 20;

                // Check if over drop zones and provide visual feedback
                UpdateDropZoneHighlight(evt.mousePosition);
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (_isDragging && evt.button == 0)
            {
                EndDrag(evt.mousePosition);
            }
        }

        private void UpdateDropZoneHighlight(Vector2 mousePos)
        {
            var leftRect = _leftDropZone.worldBound;
            var rightRect = _rightDropZone.worldBound;

            if (leftRect.Contains(mousePos))
            {
                _leftDropZone.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 0.7f));
                _leftDropZone.style.borderLeftColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
                _leftDropZone.style.borderLeftWidth = 3;
                _rightDropZone.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.4f));
                _rightDropZone.style.borderLeftWidth = 2;
            }
            else if (rightRect.Contains(mousePos))
            {
                _rightDropZone.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 0.7f));
                _rightDropZone.style.borderLeftColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
                _rightDropZone.style.borderLeftWidth = 3;
                _leftDropZone.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.4f));
                _leftDropZone.style.borderLeftWidth = 2;
            }
            else
            {
                ResetDropZoneHighlight();
            }
        }

        private void EndDrag(Vector2 mousePos)
        {
            if (!_isDragging || !_draggedOperation.HasValue) return;

            var leftRect = _leftDropZone.worldBound;
            var rightRect = _rightDropZone.worldBound;

            if (leftRect.Contains(mousePos))
            {
                DropOnLeftZone(_draggedOperation.Value);
            }
            else if (rightRect.Contains(mousePos))
            {
                DropOnRightZone(_draggedOperation.Value);
            }

            CleanupDrag();
        }

        private void CleanupDrag()
        {
            _isDragging = false;

            // Restore original element appearance
            if (_draggedElement != null)
            {
                _draggedElement.style.opacity = 1f;
                _draggedElement.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.3f));
            }

            // Remove drag preview
            if (_dragPreview != null)
            {
                _rootElement.Remove(_dragPreview);
                _dragPreview = null;
            }

            // Unregister mouse events
            _rootElement.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            _rootElement.UnregisterCallback<MouseUpEvent>(OnMouseUp);

            // Reset drop zones
            AnimateDropZones(false);
            ResetDropZoneHighlight();

            _draggedOperation = null;
            _draggedElement = null;
        }

        private void AnimateDropZones(bool activate)
        {
            if (activate)
            {
                _leftDropZone.style.borderLeftWidth = 3;
                _rightDropZone.style.borderLeftWidth = 3;
                _leftDropLabel.style.color = new StyleColor(Color.white);
                _rightDropLabel.style.color = new StyleColor(Color.white);
            }
            else
            {
                _leftDropZone.style.borderLeftWidth = 2;
                _rightDropZone.style.borderLeftWidth = 2;
                _leftDropLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
                _rightDropLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
            }
        }

        private void ResetDropZoneHighlight()
        {
            _leftDropZone.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.4f));
            _rightDropZone.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.4f));
            _leftDropZone.style.borderLeftColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
            _rightDropZone.style.borderLeftColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
        }

        private void DropOnLeftZone(UnityUndoMonitor.UndoOperation operation)
        {
            _leftOperation = operation;
            var operationColor = operation.isMcpOperation ? Color.cyan : new Color(1f, 0.6f, 0f); // Orange for manual
            var typePrefix = operation.isMcpOperation ? $"[{LocalizationManager.GetText("object_history.type_mcp")}]" : $"[{LocalizationManager.GetText("object_history.type_manual")}]";
            
            _leftDropLabel.text = $"A: {typePrefix} {operation.operationName.Substring(0, Math.Min(operation.operationName.Length, 25))}...";
            _leftDropLabel.style.fontSize = 10;
            _leftDropLabel.style.color = new StyleColor(operationColor);
            
            // Add colored border to indicate operation type
            _leftDropZone.style.borderTopWidth = 2;
            _leftDropZone.style.borderTopColor = new StyleColor(operationColor);
            
            UpdateDiffButton();
        }

        private void DropOnRightZone(UnityUndoMonitor.UndoOperation operation)
        {
            _rightOperation = operation;
            var operationColor = operation.isMcpOperation ? Color.cyan : new Color(1f, 0.6f, 0f); // Orange for manual
            var typePrefix = operation.isMcpOperation ? $"[{LocalizationManager.GetText("object_history.type_mcp")}]" : $"[{LocalizationManager.GetText("object_history.type_manual")}]";
            
            _rightDropLabel.text = $"B: {typePrefix} {operation.operationName.Substring(0, Math.Min(operation.operationName.Length, 25))}...";
            _rightDropLabel.style.fontSize = 10;
            _rightDropLabel.style.color = new StyleColor(operationColor);
            
            // Add colored border to indicate operation type
            _rightDropZone.style.borderTopWidth = 2;
            _rightDropZone.style.borderTopColor = new StyleColor(operationColor);
            
            UpdateDiffButton();
        }

        private void UpdateDiffButton()
        {
            _diffButton.SetEnabled(_leftOperation.HasValue && _rightOperation.HasValue);
        }

        private bool IsValidOperationForDiff(UnityUndoMonitor.UndoOperation operation)
        {
            // Operation is valid if it has:
            // 1. A valid operation name, OR
            // 2. Valid component snapshots, OR  
            // 3. A valid target instanceID
            
            var hasName = !string.IsNullOrEmpty(operation.operationName);
            var hasSnapshots = HasValidComponentSnapshots(operation);
            var hasInstanceID = operation.targetInstanceID != 0;
            
            var isValid = hasName || hasSnapshots || hasInstanceID;
            
            // Debug.Log($"[ObjectHistory] IsValidOperationForDiff: Name='{operation.operationName}' Valid={isValid}");
            
            return isValid;
        }
        
        private bool HasValidComponentSnapshots(UnityUndoMonitor.UndoOperation operation)
        {
            var hasBefore = operation.beforeState.captureTime != default;
            var hasAfter = operation.afterState.captureTime != default;
            var result = hasBefore || hasAfter;
            
            // Debug.Log($"[ObjectHistory] HasValidComponentSnapshots: Result={result}");
            
            return result;
        }

        private void PerformDiff()
        {
            if (_leftOperation.HasValue && _rightOperation.HasValue)
            {
                var leftOp = _leftOperation.Value;
                var rightOp = _rightOperation.Value;
                
                // Additional validation - check if operations have any valid data for diff
                var leftValid = IsValidOperationForDiff(leftOp);
                var rightValid = IsValidOperationForDiff(rightOp);
                
                // Debug.Log($"[ObjectHistory] Validation: Left={leftValid}, Right={rightValid}");
                
                if (!leftValid && !rightValid)
                {
                    Debug.LogWarning("[ObjectHistory] Both operations have no valid data for diff. Cannot perform meaningful diff.");
                    return;
                }
                
                Debug.Log($"[ObjectHistory] Performing diff between '{leftOp.operationName}' and '{rightOp.operationName}'");
                OperationDiffWindow.ShowDiff(leftOp, rightOp);
            }
            else
            {
                Debug.LogWarning("[ObjectHistory] Cannot perform diff: operations not properly selected.");
            }
        }

        private void ClearDiffSelection()
        {
            _leftOperation = null;
            _rightOperation = null;
            _leftDropLabel.text = LocalizationManager.GetText("object_history.drop_operation_a");
            _leftDropLabel.style.fontSize = 12;
            _leftDropLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
            _rightDropLabel.text = LocalizationManager.GetText("object_history.drop_operation_b");
            _rightDropLabel.style.fontSize = 12;
            _rightDropLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
            
            // Reset drop zone borders
            _leftDropZone.style.borderTopWidth = 0;
            _rightDropZone.style.borderTopWidth = 0;
            
            UpdateDiffButton();
        }
    }
}