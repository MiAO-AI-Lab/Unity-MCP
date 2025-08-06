using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using com.MiAO.Unity.MCP.Common;
using com.MiAO.Unity.MCP.Utils;
using com.MiAO.Unity.MCP.Editor.API;
using com.MiAO.Unity.MCP.Editor.Common;
using com.MiAO.Unity.MCP.Editor.Localization;
using com.MiAO.Unity.MCP.Editor.UI;
using Unity.MCP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.MiAO.Unity.MCP.Editor
{
    public partial class MainWindowEditor : EditorWindow
    {
        // Tab state
        private enum TabType
        {
            Connector,
            ModelConfig,
            UserInput,
            UndoHistory
        }

        private TabType _currentTab = TabType.Connector;
        private Button _tabConnectorButton;
        private Button _tabModelConfigButton;
        private Button _tabUserInputButton;
        private Button _tabUndoHistoryButton;
        private VisualElement _connectorContent;
        private VisualElement _modelConfigContent;
        private VisualElement _userInputContent;
        private VisualElement _undoContent;
        
        // Undo stack UI elements
        private Label _undoStackStatusText;
        private Button _btnUndoLast;
        private Button _btnRedoLast;
        private Button _btnRefreshUndoStack;
        private Button _btnClearUndoStack;
        private VisualElement _undoStackContainer;
        private Label _emptyUndoStackLabel;
        
        // State detector UI elements
        private TextField _snapshotIdField;
        private Button _btnCaptureSnapshot;
        private VisualElement _beforeDropZone;
        private VisualElement _afterDropZone;
        private Label _beforeDropLabel;
        private Label _afterDropLabel;
        private VisualElement _beforeSnapshotCard;
        private VisualElement _afterSnapshotCard;
        private Label _beforeSnapshotId;
        private Label _beforeSnapshotInfo;
        private Label _afterSnapshotId;
        private Label _afterSnapshotInfo;
        private Button _btnCompareSnapshots;
        private Toggle _showOnlyWithElementsToggle;
        private Button _btnGetWindowStats;
        private Button _btnListSnapshots;
        private Button _btnCleanSnapshots;
        private VisualElement _stateDetectionContainer;
        private Label _stateDetectionEmptyLabel;
        
        // Drag and drop state
        private string _beforeSnapshotIdValue = "";
        private string _afterSnapshotIdValue = "";

        private void InitializeTabSystem(VisualElement root)
        {
            // Get tab buttons
            _tabConnectorButton = root.Query<Button>("TabConnector").First();
            _tabModelConfigButton = root.Query<Button>("TabModelConfig").First();
            _tabUserInputButton = root.Query<Button>("TabUserInput").First();
            _tabUndoHistoryButton = root.Query<Button>("TabUndoHistory").First();
            
            // Get tab content
            _connectorContent = root.Query<VisualElement>("ConnectorContent").First();
            _modelConfigContent = root.Query<VisualElement>("ModelConfigContent").First();
            _userInputContent = root.Query<VisualElement>("UserInputContent").First();
            _undoContent = root.Query<VisualElement>("UndoContent").First();
            
            // Register tab switch events
            _tabConnectorButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.Connector));
            _tabModelConfigButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.ModelConfig));
            _tabUserInputButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.UserInput));
            _tabUndoHistoryButton.RegisterCallback<ClickEvent>(evt => SwitchTab(TabType.UndoHistory));

            // Initialize undo stack UI
            InitializeUndoStackUI(root);
            
            // Initialize state detector UI
            InitializeStateDetectorUI(root);

            // Initialize user input UI
            InitializeUserInputUI(root);

            // Initialize language from HubSettings
            InitializeLanguageFromHubSettings();

            // Register localization events
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            
            // Initialize localized text
            UpdateLocalizedTexts();
        }

        private void OnDestroy()
        {
            // Unregister localization events to avoid memory leaks
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            
            // Unregister EditorApplication.update event to avoid memory leaks
            EditorApplication.update -= UpdateSelectedObjectsDisplay;
        }
        
        /// <summary>
        /// Handle undo operations changes for auto-refresh
        /// </summary>
        private void OnUndoOperationsChanged()
        {
            // Only refresh if the undo tab is currently active to avoid unnecessary UI updates
            if (_currentTab == TabType.UndoHistory)
            {
                // Immediately refresh UI - reduce delay perception
                RefreshUndoStackUI();
            }
        }

        private void SwitchTab(TabType tabType)
        {
            _currentTab = tabType;
            
            // Update button states
            _tabConnectorButton.RemoveFromClassList("tab-button-active");
            _tabModelConfigButton.RemoveFromClassList("tab-button-active");
            _tabUserInputButton.RemoveFromClassList("tab-button-active");
            _tabUndoHistoryButton.RemoveFromClassList("tab-button-active");
            
            // Hide all content
            _connectorContent.style.display = DisplayStyle.None;
            _modelConfigContent.style.display = DisplayStyle.None;
            _userInputContent.style.display = DisplayStyle.None;
            _undoContent.style.display = DisplayStyle.None;
            
            // Show current tab content
            switch (tabType)
            {
                case TabType.Connector:
                    _tabConnectorButton.AddToClassList("tab-button-active");
                    _connectorContent.style.display = DisplayStyle.Flex;
                    break;
                case TabType.ModelConfig:
                    _tabModelConfigButton.AddToClassList("tab-button-active");
                    _modelConfigContent.style.display = DisplayStyle.Flex;
                    break;
                case TabType.UserInput:
                    _tabUserInputButton.AddToClassList("tab-button-active");
                    _userInputContent.style.display = DisplayStyle.Flex;
                    RefreshUserInputUI();
                    break;
                case TabType.UndoHistory:
                    _tabUndoHistoryButton.AddToClassList("tab-button-active");
                    _undoContent.style.display = DisplayStyle.Flex;
                    RefreshUndoStackUI();
                    break;
            }
        }

        private void InitializeUndoStackUI(VisualElement root)
        {
            // Get undo stack UI elements
            _undoStackStatusText = root.Query<Label>("undoStackStatusText").First();
            _btnUndoLast = root.Query<Button>("btnUndoLast").First();
            _btnRedoLast = root.Query<Button>("btnRedoLast").First();
            _btnRefreshUndoStack = root.Query<Button>("btnRefreshUndoStack").First();
            _btnClearUndoStack = root.Query<Button>("btnClearUndoStack").First();
            _undoStackContainer = root.Query<VisualElement>("undoStackContainer").First();
            _emptyUndoStackLabel = root.Query<Label>("emptyUndoStackLabel").First();
            
            // Register button events
            _btnUndoLast.RegisterCallback<ClickEvent>(evt => OnUndoLastClicked());
            _btnRedoLast.RegisterCallback<ClickEvent>(evt => OnRedoLastClicked());
            _btnRefreshUndoStack.RegisterCallback<ClickEvent>(evt => RefreshUndoStackUI());
            _btnClearUndoStack.RegisterCallback<ClickEvent>(evt => OnClearUndoStackClicked());
            
            // Initialize UI state
            RefreshUndoStackUI();
        }

        private void InitializeStateDetectorUI(VisualElement root)
        {
            // Get state detector UI elements
            _snapshotIdField = root.Query<TextField>("snapshotIdField").First();
            _btnCaptureSnapshot = root.Query<Button>("btnCaptureSnapshot").First();
            
            // Get drag and drop elements
            _beforeDropZone = root.Query<VisualElement>("beforeDropZone").First();
            _afterDropZone = root.Query<VisualElement>("afterDropZone").First();
            _beforeDropLabel = root.Query<Label>("beforeDropLabel").First();
            _afterDropLabel = root.Query<Label>("afterDropLabel").First();
            _beforeSnapshotCard = root.Query<VisualElement>("beforeSnapshotCard").First();
            _afterSnapshotCard = root.Query<VisualElement>("afterSnapshotCard").First();
            _beforeSnapshotId = root.Query<Label>("beforeSnapshotId").First();
            _beforeSnapshotInfo = root.Query<Label>("beforeSnapshotInfo").First();
            _afterSnapshotId = root.Query<Label>("afterSnapshotId").First();
            _afterSnapshotInfo = root.Query<Label>("afterSnapshotInfo").First();
            
            _btnCompareSnapshots = root.Query<Button>("btnCompareSnapshots").First();
            _showOnlyWithElementsToggle = root.Query<Toggle>("showOnlyWithElementsToggle").First();
            _btnGetWindowStats = root.Query<Button>("btnGetWindowStats").First();
            _btnListSnapshots = root.Query<Button>("btnListSnapshots").First();
            _btnCleanSnapshots = root.Query<Button>("btnCleanSnapshots").First();
            _stateDetectionContainer = root.Query<VisualElement>("stateDetectionContainer").First();
            _stateDetectionEmptyLabel = root.Query<Label>("stateDetectionEmptyLabel").First();
            
            // Initialize drag and drop functionality
            InitializeDragAndDrop();
            
            // Register button events
            _btnCaptureSnapshot.RegisterCallback<ClickEvent>(evt => OnCaptureSnapshotClicked());
            _btnCompareSnapshots.RegisterCallback<ClickEvent>(evt => OnCompareSnapshotsClicked());
            _btnGetWindowStats.RegisterCallback<ClickEvent>(evt => OnGetWindowStatsClicked());
            _btnListSnapshots.RegisterCallback<ClickEvent>(evt => OnListSnapshotsClicked());
            _btnCleanSnapshots.RegisterCallback<ClickEvent>(evt => OnCleanSnapshotsClicked());
            
            // Initialize default values
            _snapshotIdField.value = $"snapshot_{DateTime.Now:HHmmss}";
            _showOnlyWithElementsToggle.value = false;
            
            // Initialize State Detector
            InitializeStateDetector();
            
            // Initialize UI state
            RefreshStateDetectorUI();
        }

        private void InitializeDragAndDrop()
        {
            // Clear any existing snapshots in drop zones
            UpdateDropZone(_beforeDropZone, _beforeDropLabel, _beforeSnapshotCard, "", "", "");
            UpdateDropZone(_afterDropZone, _afterDropLabel, _afterSnapshotCard, "", "", "");
        }

        private void UpdateDropZone(VisualElement dropZone, Label dropLabel, VisualElement snapshotCard, 
                                   string snapshotId, string timestamp, string info)
        {
            if (string.IsNullOrEmpty(snapshotId))
            {
                // Show drop label, hide snapshot card
                dropLabel.style.display = DisplayStyle.Flex;
                snapshotCard.style.display = DisplayStyle.None;
            }
            else
            {
                // Hide drop label, show snapshot card
                dropLabel.style.display = DisplayStyle.None;
                snapshotCard.style.display = DisplayStyle.Flex;
                
                // Update snapshot card content
                var idLabel = snapshotCard.Q<Label>("beforeSnapshotId") ?? snapshotCard.Q<Label>("afterSnapshotId");
                var infoLabel = snapshotCard.Q<Label>("beforeSnapshotInfo") ?? snapshotCard.Q<Label>("afterSnapshotInfo");
                
                if (idLabel != null) idLabel.text = snapshotId;
                if (infoLabel != null) infoLabel.text = $"{timestamp} • {info}";
            }
        }

        private VisualElement CreateDraggableSnapshotCard(string snapshotId, string timestamp, 
                                                         string windowCount, string elementCount)
        {
            var card = new VisualElement();
            
            // Essential styles for interaction
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.marginBottom = 4;
            card.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            
            // Enable mouse events
            card.pickingMode = PickingMode.Position;
            card.style.cursor = StyleKeyword.Auto;
            
            // Store snapshot data FIRST
            card.userData = new { snapshotId, timestamp, windowCount, elementCount };
            
            // Add visual feedback for draggable state
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = new Color(0.2f, 0.6f, 1f, 0.8f);
            
            // Add hover effect
            card.RegisterCallback<MouseEnterEvent>(evt => {
                var targetCard = evt.currentTarget as VisualElement;
                if (targetCard != null)
                {
                    targetCard.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.9f);
                    targetCard.style.borderLeftColor = new Color(0.2f, 0.8f, 1f, 1f);
                }
            });
            
            card.RegisterCallback<MouseLeaveEvent>(evt => {
                var targetCard = evt.currentTarget as VisualElement;
                if (targetCard != null)
                {
                    targetCard.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                    targetCard.style.borderLeftColor = new Color(0.2f, 0.6f, 1f, 0.8f);
                }
            });
            
            // Header with snapshot ID
            var header = new Label(snapshotId);
            header.style.fontSize = 12;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Color.white;
            header.pickingMode = PickingMode.Ignore; // Let events pass through to parent
            card.Add(header);
            
            // Info line
            var info = new Label($"{timestamp} • W:{windowCount} E:{elementCount}");
            info.style.fontSize = 10;
            info.style.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);
            info.pickingMode = PickingMode.Ignore; // Let events pass through to parent
            card.Add(info);
            
            // Add drag instruction
            var dragHint = new Label("🖱️ Click to drag");
            dragHint.style.fontSize = 9;
            dragHint.style.color = new Color(0.6f, 0.8f, 1f, 0.7f);
            dragHint.style.unityFontStyleAndWeight = FontStyle.Italic;
            dragHint.style.marginTop = 2;
            dragHint.pickingMode = PickingMode.Ignore; // Let events pass through to parent
            card.Add(dragHint);
            
            // Register drag events
            card.RegisterCallback<MouseDownEvent>(OnSnapshotCardMouseDown);
            
            Debug.Log($"[TabManager] Created draggable card for snapshot: {snapshotId}");
            
            return card;
        }

        private void OnSnapshotCardMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0) return; // Only handle left mouse button
            
            // Find the card element (might be clicked on a child element like Label)
            var card = evt.currentTarget as VisualElement;
            if (card?.userData == null)
            {
                // Try to find parent card if clicked on a child element
                var current = evt.target as VisualElement;
                while (current != null && current.userData == null)
                {
                    current = current.parent;
                    if (current?.userData != null)
                    {
                        card = current;
                        break;
                    }
                }
            }
            
            if (card?.userData == null) 
            {
                Debug.LogWarning("[TabManager] Snapshot card userData not found");
                return;
            }
            
            // Prevent default behavior and stop propagation
            evt.PreventDefault();
            evt.StopPropagation();
            
            // Start drag operation
            var data = (dynamic)card.userData;
            
            Debug.Log($"[TabManager] Starting drag for snapshot: {data.snapshotId}");
            
            // Create a visual feedback element
            var dragFeedback = CreateDragFeedback(data.snapshotId, data.timestamp);
            
            // Start the drag
            StartDrag(evt, card, dragFeedback, data.snapshotId, data.timestamp, data.windowCount, data.elementCount);
        }

        private VisualElement CreateDragFeedback(string snapshotId, string timestamp)
        {
            var feedback = new VisualElement();
            feedback.style.position = Position.Absolute;
            feedback.style.paddingTop = 6;
            feedback.style.paddingBottom = 6;
            feedback.style.paddingLeft = 8;
            feedback.style.paddingRight = 8;
            feedback.style.backgroundColor = new Color(0.1f, 0.4f, 0.8f, 0.95f);
            feedback.style.borderTopLeftRadius = 6;
            feedback.style.borderTopRightRadius = 6;
            feedback.style.borderBottomLeftRadius = 6;
            feedback.style.borderBottomRightRadius = 6;
            feedback.style.borderLeftWidth = 2;
            feedback.style.borderRightWidth = 2;
            feedback.style.borderTopWidth = 2;
            feedback.style.borderBottomWidth = 2;
            feedback.style.borderLeftColor = new Color(0.3f, 0.7f, 1f, 1f);
            feedback.style.borderRightColor = new Color(0.3f, 0.7f, 1f, 1f);
            feedback.style.borderTopColor = new Color(0.3f, 0.7f, 1f, 1f);
            feedback.style.borderBottomColor = new Color(0.3f, 0.7f, 1f, 1f);
            
            // Make it not interfere with mouse events
            feedback.pickingMode = PickingMode.Ignore;
            
            var header = new Label($"[i] {snapshotId}");
            header.style.fontSize = 12;
            header.style.color = Color.white;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.pickingMode = PickingMode.Ignore;
            feedback.Add(header);
            
            var info = new Label(timestamp);
            info.style.fontSize = 10;
            info.style.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);
            info.pickingMode = PickingMode.Ignore;
            feedback.Add(info);
            
            Debug.Log($"[TabManager] Created drag feedback for: {snapshotId}");
            
            return feedback;
        }

        private void StartDrag(MouseDownEvent initialEvent, VisualElement sourceCard, VisualElement dragFeedback, 
                              string snapshotId, string timestamp, string windowCount, string elementCount)
        {
            Debug.Log($"[TabManager] StartDrag called for snapshot: {snapshotId}");
            
            var rootElement = sourceCard.panel.visualTree;
            if (rootElement == null)
            {
                Debug.LogError("[TabManager] Root element is null, cannot start drag");
                return;
            }
            
            // Add drag feedback to root
            rootElement.Add(dragFeedback);
            Debug.Log($"[TabManager] Added drag feedback to root element");
            
            // Set initial position FIRST
            var initialX = initialEvent.mousePosition.x + 10;
            var initialY = initialEvent.mousePosition.y - 10;
            dragFeedback.style.left = initialX;
            dragFeedback.style.top = initialY;
            Debug.Log($"[TabManager] Set initial drag position to ({initialX}, {initialY})");
            
            // Track mouse movement
            void OnMouseMove(MouseMoveEvent evt)
            {
                var newX = evt.mousePosition.x + 10;
                var newY = evt.mousePosition.y - 10;
                dragFeedback.style.left = newX;
                dragFeedback.style.top = newY;
                
                // Check for drop targets
                var elementUnderMouse = rootElement.panel.Pick(evt.mousePosition);
                UpdateDropTargetHighlight(elementUnderMouse);
            }
            
            void OnMouseUp(MouseUpEvent evt)
            {
                Debug.Log($"[TabManager] Mouse up - ending drag for snapshot: {snapshotId}");
                
                // Clean up
                if (rootElement.Contains(dragFeedback))
                {
                    rootElement.Remove(dragFeedback);
                    Debug.Log("[TabManager] Removed drag feedback");
                }
                
                rootElement.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                rootElement.UnregisterCallback<MouseUpEvent>(OnMouseUp);
                Debug.Log("[TabManager] Unregistered drag callbacks");
                
                // Handle drop
                var elementUnderMouse = rootElement.panel.Pick(evt.mousePosition);
                HandleDrop(elementUnderMouse, snapshotId, timestamp, windowCount, elementCount);
                
                // Clear highlights
                ClearDropTargetHighlights();
            }
            
            // Register callbacks
            rootElement.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            rootElement.RegisterCallback<MouseUpEvent>(OnMouseUp);
            Debug.Log("[TabManager] Registered drag callbacks - drag should now be active");
        }

        private void UpdateDropTargetHighlight(VisualElement element)
        {
            // Clear previous highlights
            ClearDropTargetHighlights();
            
            // Find drop zone
            var dropZone = FindDropZone(element);
            if (dropZone != null)
            {
                dropZone.style.borderLeftColor = new Color(0.2f, 0.8f, 0.2f, 1f);
                dropZone.style.borderRightColor = new Color(0.2f, 0.8f, 0.2f, 1f);
                dropZone.style.borderTopColor = new Color(0.2f, 0.8f, 0.2f, 1f);
                dropZone.style.borderBottomColor = new Color(0.2f, 0.8f, 0.2f, 1f);
                dropZone.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.2f);
            }
        }

        private void ClearDropTargetHighlights()
        {
            if (_beforeDropZone != null)
            {
                _beforeDropZone.style.borderLeftColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _beforeDropZone.style.borderRightColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _beforeDropZone.style.borderTopColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _beforeDropZone.style.borderBottomColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _beforeDropZone.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.3f);
            }
            if (_afterDropZone != null)
            {
                _afterDropZone.style.borderLeftColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _afterDropZone.style.borderRightColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _afterDropZone.style.borderTopColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _afterDropZone.style.borderBottomColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                _afterDropZone.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.3f);
            }
        }

        private VisualElement FindDropZone(VisualElement element)
        {
            if (element == null)
            {
                Debug.Log("[TabManager] FindDropZone: element is null");
                return null;
            }
            
            var current = element;
            var depth = 0;
            while (current != null && depth < 10) // Prevent infinite loop
            {
                if (current == _beforeDropZone)
                {
                    Debug.Log("[TabManager] Found BEFORE drop zone");
                    return current;
                }
                if (current == _afterDropZone)
                {
                    Debug.Log("[TabManager] Found AFTER drop zone");
                    return current;
                }
                current = current.parent;
                depth++;
            }
            
            Debug.Log("[TabManager] No drop zone found in element hierarchy");
            return null;
        }

        private void HandleDrop(VisualElement element, string snapshotId, string timestamp, 
                               string windowCount, string elementCount)
        {
            Debug.Log($"[TabManager] HandleDrop called for snapshot: {snapshotId}");
            
            var dropZone = FindDropZone(element);
            if (dropZone == null) 
            {
                Debug.Log("[TabManager] No valid drop zone found");
                return;
            }
            
            var info = $"W:{windowCount} E:{elementCount}";
            
            if (dropZone == _beforeDropZone)
            {
                Debug.Log($"[TabManager] Dropping snapshot {snapshotId} into BEFORE zone");
                _beforeSnapshotIdValue = snapshotId;
                UpdateDropZone(_beforeDropZone, _beforeDropLabel, _beforeSnapshotCard, 
                              snapshotId, timestamp, info);
                _beforeSnapshotId.text = snapshotId;
                _beforeSnapshotInfo.text = $"{timestamp} • {info}";
                Debug.Log($"[TabManager] Successfully set before snapshot to: {snapshotId}");
            }
            else if (dropZone == _afterDropZone)
            {
                Debug.Log($"[TabManager] Dropping snapshot {snapshotId} into AFTER zone");
                _afterSnapshotIdValue = snapshotId;
                UpdateDropZone(_afterDropZone, _afterDropLabel, _afterSnapshotCard, 
                              snapshotId, timestamp, info);
                _afterSnapshotId.text = snapshotId;
                _afterSnapshotInfo.text = $"{timestamp} • {info}";
                Debug.Log($"[TabManager] Successfully set after snapshot to: {snapshotId}");
            }
            else
            {
                Debug.LogWarning($"[TabManager] Unknown drop zone: {dropZone}");
            }
        }

        private void OnUndoLastClicked()
        {
            try
            {
                SetAllButtonsEnabled(false);
                
                // Force check for new operations before undo to prevent missing operations due to polling delay
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Use our UnityUndoMonitor system for proper stack management
                if (UnityUndoMonitor.GetUndoCount() > 0)
                {
                    UnityUndoMonitor.PerformUndo();
                    EditorApplication.delayCall += () =>
                    {
                        RefreshUndoStackUI();
                        // RefreshUndoStackUI() already called RefreshButtonStates(), no need to call SetAllButtonsEnabled(true) again
                    };
                }
                else
                {
                    RefreshButtonStates();
                    Debug.LogWarning("[!] No operations to undo");
                }
            }
            catch (Exception e)
            {
                RefreshButtonStates();
                Debug.LogError($"[X] Undo operation failed: {e.Message}");
            }
        }

        private void OnRedoLastClicked()
        {
            try
            {
                SetAllButtonsEnabled(false);
                
                // Force check for new operations before redo to prevent missing operations due to polling delay
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Use our UnityUndoMonitor system for proper stack management
                if (UnityUndoMonitor.GetRedoCount() > 0)
                {
                    UnityUndoMonitor.PerformRedo();
                    EditorApplication.delayCall += () =>
                    {
                        RefreshUndoStackUI();
                        // RefreshUndoStackUI() already called RefreshButtonStates(), no need to call SetAllButtonsEnabled(true) again
                    };
                }
                else
                {
                    RefreshButtonStates();
                    Debug.LogWarning("[!] No operations to redo");
                }
            }
            catch (Exception e)
            {
                RefreshButtonStates();
                Debug.LogError($"[X] Redo operation failed: {e.Message}");
            }
        }

        private void OnClearUndoStackClicked()
        {
            var title = LocalizationManager.GetText("dialog.clear_undo_stack_title");
            var message = LocalizationManager.GetText("dialog.clear_undo_stack_message");
            var clearButton = LocalizationManager.GetText("dialog.clear");
            var cancelButton = LocalizationManager.GetText("dialog.cancel");
                
            if (EditorUtility.DisplayDialog(title, message, clearButton, cancelButton))
            {
                // Force check for new operations before clear to prevent missing operations due to polling delay
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Use Unity's native undo clear + monitor clear
                Undo.ClearAll();
                UnityUndoMonitor.ClearHistory();
                RefreshUndoStackUI();
            }
        }

        private void RefreshUndoStackUI()
        {
            // Temporarily disable undo monitoring during UI refresh to prevent false detection of operations
            UnityUndoMonitor.SetUIRefreshState(true);
            
            try
            {
                // Force check for new operations before refreshing UI
                UnityUndoMonitor.ForceCheckNewOperations();
                
                // Update status text using UnityUndoMonitor
                var undoCount = UnityUndoMonitor.GetUndoCount();
                var redoCount = UnityUndoMonitor.GetRedoCount();
                var totalCount = undoCount + redoCount;
                _undoStackStatusText.text = LocalizationManager.GetText("operations.stack_status", totalCount);
                
                // Update operation history list
                UpdateUndoStackList();
                
                // Update button states (place at the end to ensure all UI elements are updated)
                RefreshButtonStates();
            }
            finally
            {
                // Immediately re-enable undo monitoring to reduce impact on subsequent operation detection
                UnityUndoMonitor.SetUIRefreshState(false);
            }
        }

        private void UpdateUndoStackList()
        {
            // Clear existing content
            _undoStackContainer.Clear();
            
            var undoCount = UnityUndoMonitor.GetUndoCount();
            var redoCount = UnityUndoMonitor.GetRedoCount();
            
            if (undoCount == 0 && redoCount == 0)
            {
                _undoStackContainer.Add(_emptyUndoStackLabel);
                return;
            }
            
            // Add undo stack header
            if (undoCount > 0)
            {
                var undoHeader = new Label(LocalizationManager.GetText("operations.undo_stack_header"));
                undoHeader.AddToClassList("undo-stack-section-header");
                _undoStackContainer.Add(undoHeader);
                
                // Add undo stack operations (display in order, newest at the top)
                var undoHistory = UnityUndoMonitor.GetUndoHistory();
                // Reverse the list to show newest operations first
                var reversedUndoHistory = undoHistory.AsEnumerable().Reverse().ToArray();
                for (int i = 0; i < reversedUndoHistory.Length; i++)
                {
                    var operation = reversedUndoHistory[i];
                    var operationElement = CreateUndoStackItemElement(operation, i, true);
                    _undoStackContainer.Add(operationElement);
                }
            }
            
            // Add redo stack header
            if (redoCount > 0)
            {
                var redoHeader = new Label(LocalizationManager.GetText("operations.redo_stack_header"));
                redoHeader.AddToClassList("undo-stack-section-header");
                _undoStackContainer.Add(redoHeader);
                
                // Add redo stack operations (display in order, newest at the top)
                var redoHistory = UnityUndoMonitor.GetRedoHistory();
                // GetRedoHistory() already returns reversed list (newest first), so no need to reverse again
                for (int i = 0; i < redoHistory.Count; i++)
                {
                    var operation = redoHistory[i];
                    var operationElement = CreateUndoStackItemElement(operation, i, false);
                    _undoStackContainer.Add(operationElement);
                }
            }
        }

        private VisualElement CreateUndoStackItemElement(UnityUndoMonitor.UndoOperation operation, int index, bool isUndoStack)
        {
            var itemElement = new VisualElement();
            itemElement.AddToClassList("undo-stack-item");
            
            // Add operation type specific class for styling
            if (operation.isMcpOperation)
            {
                itemElement.AddToClassList("mcp-operation");
            }
            else
            {
                itemElement.AddToClassList("manual-operation");
            }
            
            itemElement.pickingMode = PickingMode.Position; // Enable mouse event handling
            
            // Header row
            var headerElement = new VisualElement();
            headerElement.AddToClassList("undo-stack-item-header");
            
            // Source indicator (Manual/MCP)
            var sourceElement = new Label();
            sourceElement.AddToClassList("undo-stack-item-source");
            sourceElement.text = operation.isMcpOperation ? "[MCP]" : "[Manual]";
            
            // Operation name with source indicator
            var titleElement = new Label();
            titleElement.AddToClassList("undo-stack-item-title");
            titleElement.text = operation.DisplayName; // Use simplified DisplayName format: [Manual/MCP] 操作 -> ObjectName(instanceID)
            
            // Timestamp
            var timeElement = new Label();
            timeElement.AddToClassList("undo-stack-item-time");
            timeElement.text = operation.timestamp.ToString("HH:mm:ss");
            
            // Add index identifier (latest operation at the top)
            var indexElement = new Label();
            indexElement.AddToClassList("undo-stack-item-index");
            if (isUndoStack)
            {
                // For undo stack, index 0 is the latest operation
                indexElement.text = index == 0 ? LocalizationManager.GetText("operations.latest") : $"#{index + 1}";
            }
            else
            {
                // For redo stack, index 0 is the latest operation
                indexElement.text = index == 0 ? LocalizationManager.GetText("operations.latest") : $"#{index + 1}";
            }
            
            // Assemble header row
            headerElement.Add(sourceElement);
            headerElement.Add(titleElement);
            headerElement.Add(timeElement);
            headerElement.Add(indexElement);
            
            itemElement.Add(headerElement);
            
            // Add right-click context menu support
            itemElement.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button == 1) // Right mouse button
                {
                    ShowOperationContextMenu(operation, index, isUndoStack, evt.mousePosition);
                    evt.StopPropagation();
                    evt.PreventDefault();
                }
            });
            
            // Alternative: Also try ContextualMenuPopulateEvent as fallback
            itemElement.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                ShowOperationContextMenuFromEvent(evt.menu, operation, index, isUndoStack);
            });
            
            return itemElement;
        }

        private void ShowOperationContextMenu(UnityUndoMonitor.UndoOperation operation, int index, bool isUndoStack, Vector2 mousePosition)
        {
            // Extract object name from the operation
            var operationType = operation.operationName;
            
            // Create GenericMenu for right-click context menu
            var menu = new GenericMenu();
            
            // Check if we have a valid target object (by instanceID)
            bool hasValidTarget = operation.targetInstanceID != 0;
            
            // Add menu items
            if (hasValidTarget)
            {
                menu.AddItem(new GUIContent(LocalizationManager.GetText("operations.context_menu.show_object_history")), 
                    false, () => ShowObjectModificationHistoryByInstanceID(operation));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(LocalizationManager.GetText("operations.context_menu.show_object_history")));
            }
                
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent(LocalizationManager.GetText("operations.context_menu.copy_operation_details")), 
                false, () => CopyOperationDetails(operation));
                
            menu.AddItem(new GUIContent(LocalizationManager.GetText("operations.context_menu.copy_timestamp")), 
                false, () => CopyTimestamp(operation));
                
            menu.AddSeparator("");
            
            // Show operation info
            menu.AddItem(new GUIContent(LocalizationManager.GetText("operations.context_menu.show_operation_info")), 
                false, () => ShowOperationInfo(operation, index, isUndoStack));
            
            // Show the menu at mouse position
            menu.ShowAsContext();
        }

        private void ShowOperationContextMenuFromEvent(DropdownMenu menu, UnityUndoMonitor.UndoOperation operation, int index, bool isUndoStack)
        {
            // Extract object name from the operation
            var operationType = operation.operationName;
            
            // Check if we have a valid target object (by instanceID)
            bool hasValidTarget = operation.targetInstanceID != 0;
            
            // Add menu items using DropdownMenu API
            if (hasValidTarget)
            {
                menu.AppendAction(LocalizationManager.GetText("operations.context_menu.show_object_history"), 
                    action => ShowObjectModificationHistoryByInstanceID(operation), 
                    DropdownMenuAction.Status.Normal);
            }
            else
            {
                menu.AppendAction(LocalizationManager.GetText("operations.context_menu.show_object_history"), 
                    action => { }, 
                    DropdownMenuAction.Status.Disabled);
            }
                
            menu.AppendSeparator();
            
            menu.AppendAction(LocalizationManager.GetText("operations.context_menu.copy_operation_details"), 
                action => CopyOperationDetails(operation), 
                DropdownMenuAction.Status.Normal);
                
            menu.AppendAction(LocalizationManager.GetText("operations.context_menu.copy_timestamp"), 
                action => CopyTimestamp(operation), 
                DropdownMenuAction.Status.Normal);
                
            menu.AppendSeparator();
            
            // Show operation info
            menu.AppendAction(LocalizationManager.GetText("operations.context_menu.show_operation_info"), 
                action => ShowOperationInfo(operation, index, isUndoStack), 
                DropdownMenuAction.Status.Normal);
        }

        private void ShowObjectModificationHistory(UnityUndoMonitor.UndoOperation operation, string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                Debug.LogWarning("[UndoStack] No object name found for this operation.");
                return;
            }

            // Extract the actual object name from complex descriptions
            var actualObjectName = ExtractActualObjectName(objectName);

            // Filter all operations that affect the same object
            var undoHistory = UnityUndoMonitor.GetUndoHistory();
            var redoHistory = UnityUndoMonitor.GetRedoHistory();
            var allOperations = new List<UnityUndoMonitor.UndoOperation>();
            allOperations.AddRange(undoHistory);
            allOperations.AddRange(redoHistory);

            // Extract possible InstanceID from the current operation
            var targetInstanceId = ExtractInstanceIdFromOperation(operation.operationName);
            
            // Find all operations related to this object using improved matching
            var relatedOperations = FindRelatedOperations(allOperations, actualObjectName, targetInstanceId)
                .OrderBy(op => op.timestamp)
                .ToList();

            // Filter to only include actual modification operations (exclude select, view operations)
            var modificationOperations = relatedOperations
                .Where(op => IsActualModificationOperation(op))
                .ToList();

            if (modificationOperations.Count == 0)
            {
                Debug.LogWarning($"[UndoStack] No modification operations found for object: {actualObjectName}");
                return;
            }

            // Don't group operations - show each operation individually 
            // var groupedOperations = GroupConsecutiveInspectorOperations(relatedOperations);

            // Create and show a popup window with the object's modification history
            ShowObjectHistoryWindow(actualObjectName, modificationOperations);
        }

        /// <summary>
        /// Show object modification history using targetInstanceID for accurate tracking
        /// </summary>
        private void ShowObjectModificationHistoryByInstanceID(UnityUndoMonitor.UndoOperation operation)
        {
            string objectName = "";
            int targetInstanceID = operation.targetInstanceID;
            
            // If we have a valid instanceID, use it for precise matching
            if (targetInstanceID != 0)
            {
                // Try to get the GameObject from instanceID
                var targetObject = UnityEditor.EditorUtility.InstanceIDToObject(targetInstanceID) as GameObject;
                if (targetObject != null)
                {
                    objectName = targetObject.name;
                }
                else
                {
                    // Object might be destroyed, use instanceID for tracking
                    objectName = $"Object (ID: {targetInstanceID})";
                }
            }
            else
            {
                Debug.LogWarning("[UndoStack] No valid target instanceID found for this operation.");
                return;
            }

            // Get all operations
            var undoHistory = UnityUndoMonitor.GetUndoHistory();
            var redoHistory = UnityUndoMonitor.GetRedoHistory();
            var allOperations = new List<UnityUndoMonitor.UndoOperation>();
            allOperations.AddRange(undoHistory);
            allOperations.AddRange(redoHistory);

            // Find related operations using hybrid matching (instanceID + name-based)
            var relatedOperations = new List<UnityUndoMonitor.UndoOperation>();
            
            if (targetInstanceID != 0)
            {
                // Method 1: Match by targetInstanceID (for MCP operations)
                var instanceIdOperations = allOperations
                    .Where(op => op.targetInstanceID == targetInstanceID)
                    .ToList();
                relatedOperations.AddRange(instanceIdOperations);
                
                // Method 2: Also include name-based matches (for manual operations and older records)
                // This is important because manual operations often have targetInstanceID = 0
                if (!string.IsNullOrEmpty(objectName))
                {
                    var nameBasedOperations = FindRelatedOperations(allOperations, objectName, targetInstanceID);
                    foreach (var op in nameBasedOperations)
                    {
                        // Add if not already included (avoid duplicates by checking GUID)
                        if (!relatedOperations.Any(existing => existing.operationGuid == op.operationGuid))
                        {
                            relatedOperations.Add(op);
                        }
                    }
                }
            }
            else
            {
                // Fallback to name-based matching only
                relatedOperations = FindRelatedOperations(allOperations, objectName, 0);
            }

            // Sort by timestamp
            relatedOperations = relatedOperations.OrderBy(op => op.timestamp).ToList();

            // Filter to only include actual modification operations
            var modificationOperations = relatedOperations
                .Where(op => IsActualModificationOperation(op))
                .ToList();

            if (modificationOperations.Count == 0)
            {
                Debug.LogWarning($"[UndoStack] No modification operations found for object: {objectName} (InstanceID: {targetInstanceID})");
                return;
            }

            // Create and show a popup window with the object's modification history
            ShowObjectHistoryWindow(objectName, modificationOperations);
        }

        /// <summary>
        /// Extract actual object name from complex descriptions like "to (12.00, 10.00, 10.00) in Large Cube"
        /// </summary>
        private string ExtractActualObjectName(string objectDescription)
        {
            if (string.IsNullOrEmpty(objectDescription))
                return objectDescription;
            
            // Pattern 1: "to (...) in ObjectName" format
            var inMatch = System.Text.RegularExpressions.Regex.Match(objectDescription, @"\bin\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (inMatch.Success)
            {
                return inMatch.Groups[1].Value.Trim();
            }
            
            // Pattern 2: "ObjectName (InstanceID)" format
            var instanceMatch = System.Text.RegularExpressions.Regex.Match(objectDescription, @"^(.+?)\s*\(\d+\)$");
            if (instanceMatch.Success)
            {
                return instanceMatch.Groups[1].Value.Trim();
            }
            
            // Pattern 3: Remove common prefixes/suffixes
            var cleaned = objectDescription;
            
            // Remove "to (...)" parts
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"to\s+\([^)]+\)\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove trailing "(GameObject)" or similar
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\([^)]*\)\s*$", "");
            
            return cleaned.Trim();
        }

        /// <summary>
        /// Extract InstanceID from operation name if available
        /// </summary>
        private int ExtractInstanceIdFromOperation(string operationName)
        {
            // Look for patterns like "Select ObjectName (12345)" or "Modify ObjectName (12345)"
            var match = System.Text.RegularExpressions.Regex.Match(operationName, @"\((\d+)\)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int instanceId))
            {
                return instanceId;
            }
            return -1;
        }

        /// <summary>
        /// Find all operations related to the same object using multiple strategies
        /// </summary>
        private List<UnityUndoMonitor.UndoOperation> FindRelatedOperations(
            List<UnityUndoMonitor.UndoOperation> allOperations, 
            string targetObjectName, 
            int targetInstanceId)
        {
            var relatedOperations = new List<UnityUndoMonitor.UndoOperation>();
            
            foreach (var op in allOperations)
            {
                if (IsOperationRelatedToObject(op, targetObjectName, targetInstanceId))
                {
                    relatedOperations.Add(op);
                }
            }
            
            return relatedOperations;
        }

        /// <summary>
        /// Check if an operation is related to the target object
        /// </summary>
        private bool IsOperationRelatedToObject(UnityUndoMonitor.UndoOperation operation, string targetObjectName, int targetInstanceId)
        {
            var operationType = operation.operationName;
            
            // Method 1: Exact InstanceID match (most reliable)
            if (targetInstanceId != -1)
            {
                var opInstanceId = ExtractInstanceIdFromOperation(operation.operationName);
                if (opInstanceId == targetInstanceId)
                {
                    return true;
                }
            }
            
            // Method 2: Check if operation name contains the target object name
            if (operation.operationName.Contains(targetObjectName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            

            
            // Method 4: Smart object name matching (for renamed objects)
            if (CouldBeRenamedObject(operation, targetObjectName))
            {
                return true;
            }
            
            // Method 5: Handle operations without object info (like "Set Rotation")
            if (IsStateChangingOperation(operationType))
            {
                // For state-changing operations without object info, check if this specific operation
                // (identified by its unique GUID and timestamp) could be related by context
                return CouldBeRelatedByContext(operation, targetObjectName, targetInstanceId);
            }
            
            return false;
        }

        /// <summary>
        /// Check if operation type represents a state-changing action (generic approach)
        /// </summary>
        private bool IsStateChangingOperation(string operationType)
        {
            if (string.IsNullOrEmpty(operationType))
                return false;
                
            var lowerType = operationType.ToLower();
            
            // Use the same logic as UnityUndoMonitor for consistency
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

        /// <summary>
        /// Check if an operation could be related to target object by context (time, selection)
        /// </summary>
        private bool CouldBeRelatedByContext(UnityUndoMonitor.UndoOperation operation, string targetObjectName, int targetInstanceId)
        {
            // Get all operations to analyze context
            var undoHistory = UnityUndoMonitor.GetUndoHistory();
            var redoHistory = UnityUndoMonitor.GetRedoHistory();
            var allOperations = new List<UnityUndoMonitor.UndoOperation>();
            allOperations.AddRange(undoHistory);
            allOperations.AddRange(redoHistory);
            
            // Use a much smaller time window to be more precise
            var timeWindow = TimeSpan.FromSeconds(30); // Reduced to 30 seconds
            
            // Find the closest selection operation that clearly references the target object
            var nearestSelection = allOperations
                .Where(op => 
                    op.operationName.StartsWith("Select", StringComparison.OrdinalIgnoreCase) &&
                    (op.operationName.Contains(targetObjectName, StringComparison.OrdinalIgnoreCase) ||
                     ExtractInstanceIdFromOperation(op.operationName) == targetInstanceId) &&
                    op.timestamp <= operation.timestamp && // Selection must be before or at the same time
                    (operation.timestamp - op.timestamp).TotalSeconds <= timeWindow.TotalSeconds)
                .OrderByDescending(op => op.timestamp) // Get the most recent selection
                .FirstOrDefault();
            
            if (nearestSelection.operationName != null)
            {
                return true;
            }
            
            // Alternative: Check if there's a recent transform operation on the same object 
            // that's very close in time (within 5 seconds)
            var veryRecentTransforms = allOperations
                .Where(op => 
                {
                    var opType = op.operationName;
                    
                    return IsStateChangingOperation(opType) &&
                           op.targetInstanceID == operation.targetInstanceID && // Same object
                           Math.Abs((op.timestamp - operation.timestamp).TotalSeconds) <= 5 && // Very short window
                           op.operationGuid != operation.operationGuid; // Different operation
                })
                .ToList();
            
            return veryRecentTransforms.Count > 0;
        }

        /// <summary>
        /// Check if an operation represents an actual modification (not just viewing/selecting)
        /// </summary>
        private bool IsActualModificationOperation(UnityUndoMonitor.UndoOperation operation)
        {
            var operationType = operation.operationName;
            var lowerType = operationType.ToLower();
            var lowerOpName = operation.operationName.ToLower();
            
            // Exclude non-modification operations (viewing/selection operations)
            var nonModificationOperations = new[]
            {
                "select",
                "clear selection",
                "deselect",
                "focus",
                "view",
                "highlight",
                "inspect",
                "show",
                "hide", // Note: this might be controversial - hiding could be considered a modification
                "expand",
                "collapse"
            };
            
            // Check if this is a non-modification operation
            foreach (var excludeType in nonModificationOperations)
            {
                if (lowerType.Contains(excludeType) || lowerOpName.Contains(excludeType))
                {
                    return false;
                }
            }
            
            // Include all other operations as they represent actual modifications
            // This includes: Create, Delete, Modify, Set Position/Rotation/Scale, 
            // Add/Remove Component, Rename, Copy, Move, Drag and Drop, etc.
            return true;
        }

        /// <summary>
        /// Check if operation type is inspector-related
        /// </summary>
        private bool IsInspectorOperation(string operationType)
        {
            var lowerType = operationType.ToLower();
            return lowerType.Contains("property") || 
                   lowerType.Contains("component") ||
                   lowerType.Contains("field") ||
                   lowerType.Contains("modify") ||
                   lowerType.Contains("change") ||
                   lowerType.Contains("set") ||
                   lowerType.Contains("inspector");
        }

        /// <summary>
        /// Check if this could be the same object that was renamed
        /// </summary>
        private bool CouldBeRenamedObject(UnityUndoMonitor.UndoOperation operation, string targetObjectName)
        {
            var lowerOpName = operation.operationName.ToLower();
            var lowerTargetName = targetObjectName.ToLower();
            
            // If it's a rename operation and involves our target object
            if (lowerOpName.Contains("rename") && 
                (lowerOpName.Contains(lowerTargetName) || lowerOpName.Contains("from") || lowerOpName.Contains("to")))
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Group consecutive inspector operations together for better readability
        /// </summary>
        private List<UnityUndoMonitor.UndoOperation> GroupConsecutiveInspectorOperations(List<UnityUndoMonitor.UndoOperation> operations)
        {
            if (operations.Count <= 1)
                return operations;
            
            var result = new List<UnityUndoMonitor.UndoOperation>();
            var currentGroup = new List<UnityUndoMonitor.UndoOperation>();
            
            foreach (var op in operations)
            {
                var operationType = op.operationName;
                bool isInspectorOp = IsInspectorOperation(operationType);
                
                if (isInspectorOp && currentGroup.Count > 0 && 
                    IsInspectorOperation(currentGroup.Last().operationName))
                {
                    // Continue grouping inspector operations
                    currentGroup.Add(op);
                }
                else
                {
                    // Flush current group if exists
                    if (currentGroup.Count > 0)
                    {
                        if (currentGroup.Count > 1)
                        {
                            // Create a grouped operation for multiple inspector operations
                            var groupedOp = CreateGroupedInspectorOperation(currentGroup);
                            result.Add(groupedOp);
                        }
                        else
                        {
                            result.Add(currentGroup[0]);
                        }
                        currentGroup.Clear();
                    }
                    
                    // Start new group or add single operation
                    if (isInspectorOp)
                    {
                        currentGroup.Add(op);
                    }
                    else
                    {
                        result.Add(op);
                    }
                }
            }
            
            // Handle remaining group
            if (currentGroup.Count > 0)
            {
                if (currentGroup.Count > 1)
                {
                    var groupedOp = CreateGroupedInspectorOperation(currentGroup);
                    result.Add(groupedOp);
                }
                else
                {
                    result.Add(currentGroup[0]);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Create a grouped operation for multiple consecutive inspector operations
        /// </summary>
        private UnityUndoMonitor.UndoOperation CreateGroupedInspectorOperation(List<UnityUndoMonitor.UndoOperation> operations)
        {
            var firstOp = operations.First();
            var lastOp = operations.Last();
            var objectName = GetObjectNameFromInstanceID(firstOp.targetInstanceID);
            
            return new UnityUndoMonitor.UndoOperation
            {
                groupId = firstOp.groupId,
                operationName = $"Inspector Modifications ({operations.Count} changes) {objectName}",
                isMcpOperation = operations.Any(op => op.isMcpOperation),
                timestamp = firstOp.timestamp,
                operationGuid = $"GROUP:{firstOp.operationGuid}"
            };
        }

        private void ShowObjectHistoryWindow(string objectName, List<UnityUndoMonitor.UndoOperation> operations)
        {
            // Create a new popup window
            var window = CreateInstance<ObjectHistoryWindow>();
            window.Initialize(objectName, operations);
            window.ShowUtility();
        }

        private void CopyOperationDetails(UnityUndoMonitor.UndoOperation operation)
        {
            var details = $"Operation: {operation.operationName}\n" +
                         $"Type: {(operation.isMcpOperation ? "MCP" : "Manual")}\n" +
                         $"Timestamp: {operation.timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Group ID: {operation.groupId}\n" +
                         $"GUID: {operation.operationGuid}";
            
            EditorGUIUtility.systemCopyBuffer = details;
            // Operation details copied to clipboard
        }

        private void CopyTimestamp(UnityUndoMonitor.UndoOperation operation)
        {
            EditorGUIUtility.systemCopyBuffer = operation.timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void ShowOperationInfo(UnityUndoMonitor.UndoOperation operation, int index, bool isUndoStack)
        {
            var stackType = isUndoStack ? "Undo" : "Redo";
            var operationType = operation.operationName;
            
            var info = $"=== Operation Information ===\n" +
                      $"Stack: {stackType} (Position: {index + 1})\n" +
                      $"Operation: {operation.operationName}\n" +
                      $"Type: {operationType}\n" +
                      $"Target InstanceID: {operation.targetInstanceID}\n" +
                      $"Source: {(operation.isMcpOperation ? "MCP" : "Manual")}\n" +
                      $"Timestamp: {operation.timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                      $"Group ID: {operation.groupId}\n" +
                      $"GUID: {operation.operationGuid}";
            
            // Also show in a dialog for better visibility
            EditorUtility.DisplayDialog("Operation Information", info, "OK");
        }

        private string GetObjectNameFromInstanceID(int instanceID)
        {
            if (instanceID == 0) return "Unknown";
            
            try
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceID);
                return obj != null ? obj.name : $"Deleted({instanceID})";
            }
            catch
            {
                return $"Invalid({instanceID})";
            }
        }

        private string GetOperationIcon(string operationName)
        {
            // 使用解析后的操作类型来判断图标
            var operationType = operationName;
            var lowerOperationType = operationType.ToLower();
            
            if (lowerOperationType.Contains("create") || lowerOperationType.Contains("instantiate") || lowerOperationType.Contains("spawn"))
                return "Create";
            if (lowerOperationType.Contains("delete") || lowerOperationType.Contains("destroy"))
                return "Delete";
            if (lowerOperationType.Contains("modify") || lowerOperationType.Contains("edit") || lowerOperationType.Contains("change") || lowerOperationType.Contains("transform"))
                return "Modify";
            if (lowerOperationType.Contains("move") || lowerOperationType.Contains("parent") || lowerOperationType.Contains("position"))
                return "Move";
            if (lowerOperationType.Contains("copy") || lowerOperationType.Contains("duplicate") || lowerOperationType.Contains("clone") || lowerOperationType.Contains("drag"))
                return "Copy";
            if (lowerOperationType.Contains("rename"))
                return "Rename";
            if (lowerOperationType.Contains("select") || lowerOperationType.Contains("clear"))
                return "Select";
            
            return "Unknown";
        }

        private void SetAllButtonsEnabled(bool enabled)
        {
            if (enabled)
            {
                // When re-enabling, need to set buttons based on current state
                RefreshButtonStates();
            }
            else
            {
                // Disable all main buttons
                _btnUndoLast?.SetEnabled(false);
                _btnRedoLast?.SetEnabled(false);
                _btnRefreshUndoStack?.SetEnabled(false);
                _btnClearUndoStack?.SetEnabled(false);
            }
        }

        private void RefreshButtonStates()
        {
            var undoCount = UnityUndoMonitor.GetUndoCount();
            var redoCount = UnityUndoMonitor.GetRedoCount();
            
            // Set main buttons based on stack state
            _btnUndoLast?.SetEnabled(undoCount > 0);
            _btnRedoLast?.SetEnabled(redoCount > 0);
            _btnRefreshUndoStack?.SetEnabled(true); // Refresh button is always available
            _btnClearUndoStack?.SetEnabled(undoCount > 0 || redoCount > 0);
        }

        private void OnLanguageChanged(LocalizationManager.Language newLanguage)
        {
            UpdateLocalizedTexts();
        }

        private void UpdateLocalizedTexts()
        {
            try
            {
                // Update window title
                UpdateWindowTitle();
                
                // Update tab titles
                if (_tabConnectorButton != null)
                    _tabConnectorButton.text = LocalizationManager.GetText("tab.connector");
                if (_tabModelConfigButton != null)
                    _tabModelConfigButton.text = LocalizationManager.GetText("tab.modelconfig");
                if (_tabUserInputButton != null)
                    _tabUserInputButton.text = LocalizationManager.GetText("tab.userinput");
                if (_tabUndoHistoryButton != null)
                    _tabUndoHistoryButton.text = LocalizationManager.GetText("tab.operations");

                // Update MCP Connector tab content
                // Use new localization system instead of manual text updates
                LocalizationAdapter.LocalizeUITree(rootVisualElement);

                // All UI text updates (including Model Config) are now handled by the new localization system

                // Reload settings to update dropdown options
                // if (_languageSelector != null && _themeSelector != null) // Removed as per edit hint
                // {
                //     LoadSettings();
                // }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to update localized texts: {e.Message}");
            }
        }

        /// <summary>
        /// Refresh all client configuration status to update localized text
        /// </summary>
        private void RefreshAllClientConfigurationStatus()
        {
            try
            {
                // This method ensures that all client status buttons and labels 
                // are updated with the correct localized text when language changes
                
                var root = rootVisualElement;
                
                // Update all "Configured"/"Not Configured" status labels
                var configuredLabels = root.Query<Label>().Where(l => 
                    l.text == "Configured" || 
                    l.text == "Not Configured" ||
                    l.text == "已配置" || 
                    l.text == "未配置").ToList();
                
                foreach (var label in configuredLabels)
                {
                    // Update status text based on current state
                    if (label.text == "Configured" || label.text == "已配置")
                    {
                        label.text = LocalizationManager.GetText("connector.configured");
                    }
                    else if (label.text == "Not Configured" || label.text == "未配置")
                    {
                        label.text = LocalizationManager.GetText("connector.not_configured");
                    }
                }
                
                // Update all "Configure"/"Reconfigure" buttons
                var configButtons = root.Query<Button>().Where(b => 
                    b.text == "Configure" || 
                    b.text == "Reconfigure" ||
                    b.text == "配置" || 
                    b.text == "重新配置").ToList();
                
                foreach (var button in configButtons)
                {
                    // Update button text based on current state
                    if (button.text == "Reconfigure" || button.text == "重新配置")
                    {
                        button.text = LocalizationManager.GetText("connector.reconfigure");
                    }
                    else if (button.text == "Configure" || button.text == "配置")
                    {
                        button.text = LocalizationManager.GetText("connector.configure");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RefreshClientConfigurationStatus] Error refreshing client configuration status: {ex.Message}");
            }
        }

        // ==================== User Input Tab ====================
        
        // User Input UI elements
        private VisualElement _promptMessageSection;
        private Label _promptMessageText;
        private VisualElement _userInputSection;
        private TextField _userInputField;
        private VisualElement _objectSelectionSection;
        private Label _selectedObjectsText;
        private VisualElement _buttonSection;
        private Button _btnConfirmInput;
        private Button _btnCancelInput;
        private Label _statusText;
        
        // User Input state
        private bool _userInputActive = false;
        private string _currentWindowId = "";
        private System.Action<string> _userInputCallback;
        private string _mode = "full";

        private void InitializeUserInputUI(VisualElement root)
        {
            // Get UI elements
            _promptMessageSection = root.Query<VisualElement>("promptMessageSection").First();
            _promptMessageText = root.Query<Label>("promptMessageText").First();
            _userInputSection = root.Query<VisualElement>("userInputSection").First();
            _userInputField = root.Query<TextField>("userInputField").First();
            _objectSelectionSection = root.Query<VisualElement>("objectSelectionSection").First();
            _selectedObjectsText = root.Query<Label>("selectedObjectsText").First();
            _buttonSection = root.Query<VisualElement>("buttonSection").First();
            _btnConfirmInput = root.Query<Button>("btnConfirmInput").First();
            _btnCancelInput = root.Query<Button>("btnCancelInput").First();
            _statusText = root.Query<Label>("statusText").First();
            
            // Register button events
            _btnConfirmInput.RegisterCallback<ClickEvent>(evt => OnConfirmInputClicked());
            _btnCancelInput.RegisterCallback<ClickEvent>(evt => OnCancelInputClicked());
            
            // Update selected objects display periodically
            EditorApplication.update += UpdateSelectedObjectsDisplay;
            
            // Initialize UI state
            RefreshUserInputUI();
        }

        private void RefreshUserInputUI()
        {
            if (_userInputActive)
            {
                _promptMessageSection.style.display = DisplayStyle.Flex;
                _userInputSection.style.display = DisplayStyle.Flex;
                _objectSelectionSection.style.display = DisplayStyle.Flex;
                _buttonSection.style.display = DisplayStyle.Flex;
                _statusText.text = LocalizationManager.GetText("userinput.waiting_for_input");
                
                // Update selected objects display
                UpdateSelectedObjectsDisplay();
            }
            else
            {
                _promptMessageSection.style.display = DisplayStyle.None;
                _userInputSection.style.display = DisplayStyle.None;
                _objectSelectionSection.style.display = DisplayStyle.None;
                _buttonSection.style.display = DisplayStyle.None;
                _statusText.text = LocalizationManager.GetText("userinput.waiting_for_prompt");
            }
        }

        private void OnConfirmInputClicked()
        {
            var userInputText = _userInputField.value ?? "";
            var selectedObjects = Selection.objects;

            var result = "";
            if (_mode == "full")
            {
                result = $"[Success] User input completed.";
                result += $"\nUser input text: '{userInputText}'";
                
                if (selectedObjects.Length > 0)
                {
                    result += $"\nSelected objects count: {selectedObjects.Length}";
                    result += $"\nSelected objects list:";
                    for (int i = 0; i < selectedObjects.Length; i++)
                    {
                        if (selectedObjects[i] != null)
                        {
                            var obj = selectedObjects[i];
                            var path = "";
                            if (obj is GameObject go)
                            {
                                path = GetGameObjectPath(go);
                            }
                            result += $"\n  [{i + 1}] {obj.name} ({obj.GetType().Name}) - InstanceID: {obj.GetInstanceID()}";
                            if (!string.IsNullOrEmpty(path))
                            {
                                result += $" - Path: {path}";
                            }
                        }
                    }
                }
            }
            else if (_mode == "clean")
            {
                result = userInputText;
            }
            else if (_mode == "json")
            {
                result = JsonSerializer.Serialize(new {
                    userInput = userInputText,
                    selectedObjects = selectedObjects.Select(obj => new {
                        name = obj.name,
                        type = obj.GetType().Name,
                        instanceId = obj.GetInstanceID()
                    }).ToArray()
                });
            }
            
            CompleteUserInput(result);
        }

        private void OnCancelInputClicked()
        {
            CompleteUserInput("[Cancelled] User cancelled the input.");
        }

        private void CompleteUserInput(string result)
        {
            _userInputActive = false;
            _userInputCallback?.Invoke(result);
            _userInputCallback = null;
            _currentWindowId = "";
            
            // Clear input field
            _userInputField.value = "";
            
            // Refresh UI
            RefreshUserInputUI();
        }

        private void UpdateSelectedObjectsDisplay()
        {
            if (_selectedObjectsText != null)
            {
                var selectedObjects = Selection.objects;
                if (selectedObjects.Length > 0)
                {
                    var text = $"Selected {selectedObjects.Length} objects:\n";
                    for (int i = 0; i < selectedObjects.Length && i < 10; i++)
                    {
                        if (selectedObjects[i] != null)
                        {
                            text += $"• {selectedObjects[i].name} ({selectedObjects[i].GetType().Name})\n";
                        }
                    }
                    if (selectedObjects.Length > 10)
                    {
                        text += $"... and {selectedObjects.Length - 10} more objects";
                    }
                    _selectedObjectsText.text = text;
                }
                else
                {
                    _selectedObjectsText.text = LocalizationManager.GetText("userinput.no_objects");
                }
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "";
            
            var path = go.name;
            var parent = go.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        /// <summary>
        /// Show user input UI and switch to UserInput tab
        /// </summary>
        public void ShowUserInputUI(string promptMessage, string windowId, string mode, System.Action<string> callback)
        {
            _userInputActive = true;
            _currentWindowId = windowId;
            _userInputCallback = callback;
            _promptMessageText.text = promptMessage;
            _mode = mode;
            
            // Switch to UserInput tab
            SwitchTab(TabType.UserInput);
            
            // Focus the window
            Focus();
            
            // Refresh UI
            RefreshUserInputUI();
            
            // Focus the input field
            _userInputField.Focus();
        }

        /// <summary>
        /// Hide user input UI
        /// </summary>
        public void HideUserInputUI()
        {
            _userInputActive = false;
            _userInputCallback = null;
            _currentWindowId = "";
            _userInputField.value = "";
            RefreshUserInputUI();
        }

        /// <summary>
        /// Get current user input window ID
        /// </summary>
        public string GetCurrentUserInputWindowId()
        {
            return _currentWindowId;
        }

        /// <summary>
        /// Check if user input is active
        /// </summary>
        public bool IsUserInputActive()
        {
            return _userInputActive;
        }

        private void InitializeLanguageFromHubSettings()
        {
            // Initialize localization manager's current language from HubSettings
            var savedLanguage = McpHubSettingsWindow.Settings.CurrentLanguage;
            LocalizationManager.CurrentLanguage = LocalizationManager.StringToLanguage(savedLanguage);
        }

        // ==================== State Detector Event Handlers ====================
        
        private object _stateDetector;
        private Type _stateDetectorType;
        
        private void InitializeStateDetector()
        {
            try
            {
                // 使用反射查找 State Detector 类型
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    _stateDetectorType = assembly.GetType("com.MiAO.Unity.MCP.Essential.Tools.Tool_Unity_StateDetector");
                    if (_stateDetectorType != null)
                    {
                        _stateDetector = Activator.CreateInstance(_stateDetectorType);
                        break;
                    }
                }
                
                if (_stateDetector == null)
                {
                    Debug.LogWarning("[TabManager] State Detector not found. State detection functionality will be disabled.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TabManager] Failed to initialize State Detector: {e.Message}");
            }
        }
        
        private void OnCaptureSnapshotClicked()
        {
            try
            {
                if (_stateDetector == null || _stateDetectorType == null)
                {
                    DisplayStateDetectorResult("[x] State Detector not available");
                    return;
                }
                
                var snapshotId = _snapshotIdField.value?.Trim();
                if (string.IsNullOrEmpty(snapshotId))
                {
                    snapshotId = $"snapshot_{DateTime.Now:HHmmss}";
                    _snapshotIdField.value = snapshotId;
                }
                
                SetStateDetectorButtonsEnabled(false);
                
                // 使用反射调用 CaptureState 方法
                var captureMethod = _stateDetectorType.GetMethod("CaptureState");
                if (captureMethod == null)
                {
                    DisplayStateDetectorResult("[x] CaptureState method not found");
                    return;
                }
                
                var snapshot = captureMethod.Invoke(_stateDetector, new object[] { snapshotId, true });
                if (snapshot == null)
                {
                    DisplayStateDetectorResult("[x] CaptureState returned null");
                    return;
                }
                
                // 使用反射获取快照属性
                var windowCountProp = snapshot.GetType().GetProperty("TotalWindowCount");
                var elementCountProp = snapshot.GetType().GetProperty("TotalElementCount");
                
                var windowCount = windowCountProp?.GetValue(snapshot) ?? 0;
                var elementCount = elementCountProp?.GetValue(snapshot) ?? 0;
                
                DisplayStateDetectorResult($"[√] Snapshot captured: {snapshotId}\n" +
                                         $"Windows: {windowCount}, Elements: {elementCount}");
                
                // Generate new snapshot ID for next capture
                _snapshotIdField.value = $"snapshot_{DateTime.Now:HHmmss}";
            }
            catch (Exception e)
            {
                DisplayStateDetectorResult($"[x] Capture failed: {e.Message}");
                Debug.LogError($"[StateDetector] Capture snapshot failed: {e.Message}");
            }
            finally
            {
                SetStateDetectorButtonsEnabled(true);
            }
        }
        
        private void OnCompareSnapshotsClicked()
        {
            try
            {
                if (_stateDetector == null || _stateDetectorType == null)
                {
                    DisplayStateDetectorResult("[x] State Detector not available");
                    return;
                }
                
                var beforeId = _beforeSnapshotIdValue?.Trim();
                var afterId = _afterSnapshotIdValue?.Trim();
                
                if (string.IsNullOrEmpty(beforeId) || string.IsNullOrEmpty(afterId))
                {
                    DisplayStateDetectorResult("[!] Please drag snapshots to both Before and After areas first");
                    return;
                }
                
                SetStateDetectorButtonsEnabled(false);
                
                // 使用反射调用 CompareStates 方法
                var compareMethod = _stateDetectorType.GetMethod("CompareStates");
                if (compareMethod == null)
                {
                    DisplayStateDetectorResult("[x] CompareStates method not found");
                    return;
                }
                
                var comparison = compareMethod.Invoke(_stateDetector, new object[] { beforeId, afterId, true });
                if (comparison == null)
                {
                    DisplayStateDetectorResult("[x] CompareStates returned null");
                    return;
                }
                
                // 使用反射获取比较结果属性 (匿名类型使用属性而不是字段)
                var successProp = comparison.GetType().GetProperty("Success");
                var summaryProp = comparison.GetType().GetProperty("Summary");
                var messageProp = comparison.GetType().GetProperty("Message");
                
                if (successProp == null)
                {
                    DisplayStateDetectorResult("[x] Comparison result structure not recognized");
                    return;
                }
                
                var success = (bool)successProp.GetValue(comparison);
                if (success)
                {
                    var summary = summaryProp?.GetValue(comparison)?.ToString() ?? "No summary available";
                    DisplayStateDetectorResult($"[√] Comparison completed\n{summary}");
                }
                else
                {
                    var message = messageProp?.GetValue(comparison)?.ToString() ?? "Unknown error";
                    DisplayStateDetectorResult($"[x] Comparison failed: {message}");
                }
            }
            catch (Exception e)
            {
                DisplayStateDetectorResult($"[x] Comparison failed: {e.Message}");
                Debug.LogError($"[StateDetector] Compare snapshots failed: {e.Message}");
            }
            finally
            {
                SetStateDetectorButtonsEnabled(true);
            }
        }
        
        private void OnGetWindowStatsClicked()
        {
            try
            {
                if (_stateDetector == null || _stateDetectorType == null)
                {
                    DisplayStateDetectorResult("[x] State Detector not available");
                    return;
                }
                
                SetStateDetectorButtonsEnabled(false);
                
                var showOnlyWithElements = _showOnlyWithElementsToggle.value;
                
                // 使用反射调用 GetWindowStats 方法
                var statsMethod = _stateDetectorType.GetMethod("GetWindowStats");
                if (statsMethod == null)
                {
                    DisplayStateDetectorResult("[x] GetWindowStats method not found");
                    return;
                }
                
                var result = statsMethod.Invoke(_stateDetector, new object[] { showOnlyWithElements });
                if (result == null)
                {
                    DisplayStateDetectorResult("[x] GetWindowStats returned null");
                    return;
                }
                
                // 使用反射获取结果属性 (匿名类型使用属性而不是字段)
                var resultType = result.GetType();
                var successProperty = resultType.GetProperty("Success");
                var statisticsProperty = resultType.GetProperty("Statistics");
                var messageProperty = resultType.GetProperty("Message");
                
                if (successProperty == null)
                {
                    DisplayStateDetectorResult("[x] Result structure not recognized");
                    return;
                }
                
                var success = (bool)successProperty.GetValue(result);
                if (success)
                {
                    var stats = statisticsProperty?.GetValue(result);
                    if (stats == null)
                    {
                        DisplayStateDetectorResult("[x] Statistics data not available");
                        return;
                    }
                    
                    var statsType = stats.GetType();
                    
                    var totalWindows = statsType.GetProperty("TotalWindows")?.GetValue(stats) ?? 0;
                    var windowsWithElements = statsType.GetProperty("WindowsWithElements")?.GetValue(stats) ?? 0;
                    var windowsWithoutElements = statsType.GetProperty("WindowsWithoutElements")?.GetValue(stats) ?? 0;
                    var totalElements = statsType.GetProperty("TotalElements")?.GetValue(stats) ?? 0;
                    
                    var statsText = $"[√] Window Statistics\n" +
                                  $"Total Windows: {totalWindows}\n" +
                                  $"Windows with Elements: {windowsWithElements}\n" +
                                  $"Windows without Elements: {windowsWithoutElements}\n" +
                                  $"Total Elements: {totalElements}";
                    
                    DisplayStateDetectorResult(statsText);
                }
                else
                {
                    var message = messageProperty?.GetValue(result)?.ToString() ?? "Unknown error";
                    DisplayStateDetectorResult($"[x] Window stats failed: {message}");
                }
            }
            catch (Exception e)
            {
                DisplayStateDetectorResult($"[x] Window stats failed: {e.Message}");
                Debug.LogError($"[StateDetector] Get window stats failed: {e.Message}");
            }
            finally
            {
                SetStateDetectorButtonsEnabled(true);
            }
        }
        
        private void OnListSnapshotsClicked()
        {
            try
            {
                if (_stateDetector == null || _stateDetectorType == null)
                {
                    DisplayStateDetectorResult("[x] State Detector not available");
                    return;
                }
                
                SetStateDetectorButtonsEnabled(false);
                
                // 使用反射调用 ListSnapshots 方法
                var listMethod = _stateDetectorType.GetMethod("ListSnapshots");
                if (listMethod == null)
                {
                    DisplayStateDetectorResult("[x] ListSnapshots method not found");
                    return;
                }
                
                var result = listMethod.Invoke(_stateDetector, new object[] { });
                if (result == null)
                {
                    DisplayStateDetectorResult("[x] ListSnapshots returned null");
                    return;
                }
                
                // 使用反射获取结果属性 (匿名类型使用属性而不是字段)
                var resultType = result.GetType();
                var successProperty = resultType.GetProperty("Success");
                var snapshotsProperty = resultType.GetProperty("Snapshots");
                var totalCountProperty = resultType.GetProperty("TotalCount");
                var messageProperty = resultType.GetProperty("Message");
                
                if (successProperty == null)
                {
                    DisplayStateDetectorResult("[x] Result structure not recognized");
                    return;
                }
                
                var success = (bool)successProperty.GetValue(result);
                if (success)
                {
                    var snapshots = snapshotsProperty?.GetValue(result) as System.Collections.IEnumerable;
                    var totalCount = totalCountProperty?.GetValue(result) ?? 0;
                    
                    // Clear previous snapshot cards
                    _stateDetectionContainer.Clear();
                    
                    // Create header
                    var headerElement = new VisualElement();
                    headerElement.style.marginBottom = 10;
                    headerElement.style.paddingTop = 8;
                    headerElement.style.paddingBottom = 8;
                    headerElement.style.paddingLeft = 8;
                    headerElement.style.paddingRight = 8;
                    headerElement.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                    headerElement.style.borderTopLeftRadius = 4;
                    headerElement.style.borderTopRightRadius = 4;
                    headerElement.style.borderBottomLeftRadius = 4;
                    headerElement.style.borderBottomRightRadius = 4;
                    
                    var timestampLabel = new Label($"[{DateTime.Now:HH:mm:ss}]");
                    timestampLabel.style.fontSize = 11;
                    timestampLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    timestampLabel.style.marginBottom = 4;
                    headerElement.Add(timestampLabel);
                    
                    var titleLabel = new Label($"[√] Snapshots List ({totalCount} total)");
                    titleLabel.style.fontSize = 12;
                    titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    titleLabel.style.marginBottom = 6;
                    headerElement.Add(titleLabel);
                    
                    var instructionLabel = new Label("[i] Drag snapshots to comparison areas below");
                    instructionLabel.style.fontSize = 11;
                    instructionLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
                    instructionLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    headerElement.Add(instructionLabel);
                    
                    _stateDetectionContainer.Add(headerElement);
                    
                    if (snapshots != null)
                    {
                        foreach (var snapshot in snapshots)
                        {
                            var snapshotType = snapshot.GetType();
                            var snapshotId = snapshotType.GetProperty("SnapshotId")?.GetValue(snapshot)?.ToString();
                            var timestamp = snapshotType.GetProperty("Timestamp")?.GetValue(snapshot);
                            var windowCount = snapshotType.GetProperty("TotalWindowCount")?.GetValue(snapshot)?.ToString() ?? "0";
                            var elementCount = snapshotType.GetProperty("TotalElementCount")?.GetValue(snapshot)?.ToString() ?? "0";
                            
                            if (!string.IsNullOrEmpty(snapshotId) && timestamp != null)
                            {
                                var timestampStr = $"{timestamp:MM/dd HH:mm:ss}";
                                var card = CreateDraggableSnapshotCard(snapshotId, timestampStr, windowCount, elementCount);
                                _stateDetectionContainer.Add(card);
                            }
                        }
                    }
                    else
                    {
                        var emptyLabel = new Label("No snapshots available");
                        emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                        emptyLabel.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                        emptyLabel.style.marginTop = 20;
                        emptyLabel.style.marginBottom = 20;
                        _stateDetectionContainer.Add(emptyLabel);
                    }
                }
                else
                {
                    var message = messageProperty?.GetValue(result)?.ToString() ?? "Unknown error";
                    DisplayStateDetectorResult($"[x] List snapshots failed: {message}");
                }
            }
            catch (Exception e)
            {
                DisplayStateDetectorResult($"[x] List snapshots failed: {e.Message}");
                Debug.LogError($"[StateDetector] List snapshots failed: {e.Message}");
            }
            finally
            {
                SetStateDetectorButtonsEnabled(true);
            }
        }
        
        private void OnCleanSnapshotsClicked()
        {
            try
            {
                var title = LocalizationManager.GetText("operations.clean_snapshots_title");
                var message = LocalizationManager.GetText("operations.clean_snapshots_message");
                var cleanButton = LocalizationManager.GetText("operations.clean_all");
                var cancelButton = LocalizationManager.GetText("dialog.cancel");
                    
                if (EditorUtility.DisplayDialog(title, message, cleanButton, cancelButton))
                {
                    if (_stateDetector == null || _stateDetectorType == null)
                    {
                        DisplayStateDetectorResult("[x] State Detector not available");
                        return;
                    }
                    
                    SetStateDetectorButtonsEnabled(false);
                    
                    // use reflection to call CleanSnapshots method
                    var cleanMethod = _stateDetectorType.GetMethod("CleanSnapshots");
                    if (cleanMethod == null)
                    {
                        DisplayStateDetectorResult("[x] CleanSnapshots method not found");
                        return;
                    }
                    
                    var result = cleanMethod.Invoke(_stateDetector, new object[] { "" });
                    if (result == null)
                    {
                        DisplayStateDetectorResult("[x] CleanSnapshots returned null");
                        return;
                    }
                    
                    // use reflection to get result properties (anonymous types use properties instead of fields)
                    var resultType = result.GetType();
                    var successProperty = resultType.GetProperty("Success");
                    var messageProperty = resultType.GetProperty("Message");
                    
                    if (successProperty == null)
                    {
                        DisplayStateDetectorResult("[x] Result structure not recognized");
                        return;
                    }
                    
                    var success = (bool)successProperty.GetValue(result);
                    var resultMessage = messageProperty?.GetValue(result)?.ToString() ?? "Unknown result";
                    
                    if (success)
                    {
                        DisplayStateDetectorResult($"[√] {resultMessage}");
                    }
                    else
                    {
                        DisplayStateDetectorResult($"[x] Clean failed: {resultMessage}");
                    }
                }
            }
            catch (Exception e)
            {
                DisplayStateDetectorResult($"[x] Clean snapshots failed: {e.Message}");
                Debug.LogError($"[StateDetector] Clean snapshots failed: {e.Message}");
            }
            finally
            {
                SetStateDetectorButtonsEnabled(true);
            }
        }
        
        private void RefreshStateDetectorUI()
        {
            // Show empty state if no results
            if (_stateDetectionContainer.childCount <= 1) // Only contains empty label
            {
                _stateDetectionEmptyLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _stateDetectionEmptyLabel.style.display = DisplayStyle.None;
            }
        }
        
        private void DisplayStateDetectorResult(string resultText)
        {
            // Clear previous results
            _stateDetectionContainer.Clear();
            
            // Create result display element
            var resultElement = new VisualElement();
            resultElement.style.marginBottom = 10;
            resultElement.style.paddingTop = 8;
            resultElement.style.paddingBottom = 8;
            resultElement.style.paddingLeft = 8;
            resultElement.style.paddingRight = 8;
            resultElement.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            resultElement.style.borderTopLeftRadius = 4;
            resultElement.style.borderTopRightRadius = 4;
            resultElement.style.borderBottomLeftRadius = 4;
            resultElement.style.borderBottomRightRadius = 4;
            
            // Timestamp header
            var timestampLabel = new Label($"[{DateTime.Now:HH:mm:ss}]");
            timestampLabel.style.fontSize = 11;
            timestampLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            timestampLabel.style.marginBottom = 4;
            resultElement.Add(timestampLabel);
            
            // Result content
            var contentLabel = new Label(resultText);
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            contentLabel.style.fontSize = 12;
            resultElement.Add(contentLabel);
            
            _stateDetectionContainer.Add(resultElement);
            
            // Auto-scroll to bottom
            EditorApplication.delayCall += () => {
                var scrollView = _stateDetectionContainer.parent as ScrollView;
                if (scrollView != null)
                {
                    scrollView.verticalScroller.value = scrollView.verticalScroller.highValue;
                }
            };
            
            RefreshStateDetectorUI();
        }
        
        private void SetStateDetectorButtonsEnabled(bool enabled)
        {
            _btnCaptureSnapshot?.SetEnabled(enabled);
            _btnCompareSnapshots?.SetEnabled(enabled);
            _btnGetWindowStats?.SetEnabled(enabled);
            _btnListSnapshots?.SetEnabled(enabled);
            _btnCleanSnapshots?.SetEnabled(enabled);
        }

        /// <summary>
        /// Get current language setting (now from HubSettings)
        /// </summary>
        public static string GetCurrentLanguage()
        {
            return McpHubSettingsWindow.Settings.CurrentLanguage;
        }

        /// <summary>
        /// Get current theme setting (now from HubSettings)
        /// </summary>
        public static string GetCurrentTheme()
        {
            return McpHubSettingsWindow.Settings.CurrentTheme;
        }
    }
} 