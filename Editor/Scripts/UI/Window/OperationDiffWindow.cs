using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.MCP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor
{
    /// <summary>
    /// Window to display diff between two undo operations
    /// </summary>
    public class OperationDiffWindow : EditorWindow
    {
        private UnityUndoMonitor.UndoOperation _leftOperation;
        private UnityUndoMonitor.UndoOperation _rightOperation;
        private VisualElement _rootElement;
        private ScrollView _diffScrollView;

        public static void ShowDiff(UnityUndoMonitor.UndoOperation leftOp, UnityUndoMonitor.UndoOperation rightOp)
        {
            var window = GetWindow<OperationDiffWindow>("Operation Diff");
            window.Initialize(leftOp, rightOp);
            window.Show();
        }

        public void Initialize(UnityUndoMonitor.UndoOperation leftOperation, UnityUndoMonitor.UndoOperation rightOperation)
        {
            _leftOperation = leftOperation;
            _rightOperation = rightOperation;
            
            // Debug information
            // Debug.Log($"[OperationDiff] Initializing diff window:");
            // Debug.Log($"  Left: Name='{leftOperation.operationName}', InstanceID={leftOperation.targetInstanceID}, HasBefore={leftOperation.beforeState.captureTime != default}, HasAfter={leftOperation.afterState.captureTime != default}");
            // Debug.Log($"  Right: Name='{rightOperation.operationName}', InstanceID={rightOperation.targetInstanceID}, HasBefore={rightOperation.beforeState.captureTime != default}, HasAfter={rightOperation.afterState.captureTime != default}");
            
            var leftName = GetDisplayNameForOperation(leftOperation);
            var rightName = GetDisplayNameForOperation(rightOperation);
            
            titleContent = new GUIContent($"Diff: {leftName} ↔ {rightName}");
            minSize = new Vector2(600, 400);
            
            // Generate diff content now that operations are initialized (only if UI is ready)
            if (_diffScrollView != null)
            {
                GenerateDiffContent();
            }
        }

        private void CreateGUI()
        {
            _rootElement = rootVisualElement;
            _rootElement.style.paddingLeft = 15;
            _rootElement.style.paddingRight = 15;
            _rootElement.style.paddingTop = 15;
            _rootElement.style.paddingBottom = 15;

            // Title
            var titleLabel = new Label(LocalizationManager.GetText("operation_diff.title"));
            titleLabel.style.fontSize = 18;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 15;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _rootElement.Add(titleLabel);

            // Operations info container
            var operationsContainer = new VisualElement();
            operationsContainer.style.flexDirection = FlexDirection.Row;
            operationsContainer.style.marginBottom = 20;
            
            // Left operation info
            var leftContainer = new VisualElement();
            leftContainer.style.flexGrow = 1;
            leftContainer.style.marginRight = 10;
            leftContainer.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.3f));
            leftContainer.style.borderLeftColor = new StyleColor(Color.red);
            leftContainer.style.borderLeftWidth = 4;
            leftContainer.style.paddingLeft = 10;
            leftContainer.style.paddingRight = 10;
            leftContainer.style.paddingTop = 10;
            leftContainer.style.paddingBottom = 10;
            leftContainer.style.borderTopLeftRadius = 5;
            leftContainer.style.borderTopRightRadius = 5;
            leftContainer.style.borderBottomLeftRadius = 5;
            leftContainer.style.borderBottomRightRadius = 5;

            var leftTitle = new Label(LocalizationManager.GetText("operation_diff.operation_a"));
            leftTitle.style.fontSize = 14;
            leftTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            leftTitle.style.color = new StyleColor(Color.red);
            leftContainer.Add(leftTitle);

            var leftName = new Label($"{LocalizationManager.GetText("common.name")}: {_leftOperation.operationName}");
            leftName.style.fontSize = 12;
            leftName.style.marginTop = 5;
            leftContainer.Add(leftName);

            var leftTime = new Label($"{LocalizationManager.GetText("operation_diff.timestamp")}: {_leftOperation.timestamp:yyyy-MM-dd HH:mm:ss}");
            leftTime.style.fontSize = 10;
            leftTime.style.color = new StyleColor(Color.gray);
            leftContainer.Add(leftTime);

            var leftType = new Label($"{LocalizationManager.GetText("operation_diff.operation_type")}: {(_leftOperation.isMcpOperation ? LocalizationManager.GetText("operation_diff.type_mcp") : LocalizationManager.GetText("operation_diff.type_manual"))}");
            leftType.style.fontSize = 10;
            leftType.style.color = new StyleColor(_leftOperation.isMcpOperation ? Color.cyan : Color.yellow);
            leftContainer.Add(leftType);

            // Right operation info
            var rightContainer = new VisualElement();
            rightContainer.style.flexGrow = 1;
            rightContainer.style.marginLeft = 10;
            rightContainer.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.3f));
            rightContainer.style.borderLeftColor = new StyleColor(Color.green);
            rightContainer.style.borderLeftWidth = 4;
            rightContainer.style.paddingLeft = 10;
            rightContainer.style.paddingRight = 10;
            rightContainer.style.paddingTop = 10;
            rightContainer.style.paddingBottom = 10;
            rightContainer.style.borderTopLeftRadius = 5;
            rightContainer.style.borderTopRightRadius = 5;
            rightContainer.style.borderBottomLeftRadius = 5;
            rightContainer.style.borderBottomRightRadius = 5;

            var rightTitle = new Label(LocalizationManager.GetText("operation_diff.operation_b"));
            rightTitle.style.fontSize = 14;
            rightTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            rightTitle.style.color = new StyleColor(Color.green);
            rightContainer.Add(rightTitle);

            var rightName = new Label($"{LocalizationManager.GetText("common.name")}: {_rightOperation.operationName}");
            rightName.style.fontSize = 12;
            rightName.style.marginTop = 5;
            rightContainer.Add(rightName);

            var rightTime = new Label($"{LocalizationManager.GetText("operation_diff.timestamp")}: {_rightOperation.timestamp:yyyy-MM-dd HH:mm:ss}");
            rightTime.style.fontSize = 10;
            rightTime.style.color = new StyleColor(Color.gray);
            rightContainer.Add(rightTime);

            var rightType = new Label($"{LocalizationManager.GetText("operation_diff.operation_type")}: {(_rightOperation.isMcpOperation ? LocalizationManager.GetText("operation_diff.type_mcp") : LocalizationManager.GetText("operation_diff.type_manual"))}");
            rightType.style.fontSize = 10;
            rightType.style.color = new StyleColor(_rightOperation.isMcpOperation ? Color.cyan : Color.yellow);
            rightContainer.Add(rightType);

            operationsContainer.Add(leftContainer);
            operationsContainer.Add(rightContainer);
            _rootElement.Add(operationsContainer);

            // Separator line
            var separator = new VisualElement();
            separator.style.height = 2;
            separator.style.backgroundColor = new StyleColor(Color.gray);
            separator.style.marginTop = 10;
            separator.style.marginBottom = 15;
            _rootElement.Add(separator);

            // Diff content
            var diffLabel = new Label(LocalizationManager.GetText("operation_diff.operation_differences"));
            diffLabel.style.fontSize = 16;
            diffLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            diffLabel.style.marginBottom = 10;
            _rootElement.Add(diffLabel);

            // Diff scroll view
            _diffScrollView = new ScrollView(ScrollViewMode.Vertical);
            _diffScrollView.style.flexGrow = 1;
            _diffScrollView.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f));
            _diffScrollView.style.borderTopLeftRadius = 5;
            _diffScrollView.style.borderTopRightRadius = 5;
            _diffScrollView.style.borderBottomLeftRadius = 5;
            _diffScrollView.style.borderBottomRightRadius = 5;
            _diffScrollView.style.paddingLeft = 10;
            _diffScrollView.style.paddingRight = 10;
            _diffScrollView.style.paddingTop = 10;
            _diffScrollView.style.paddingBottom = 10;
            _rootElement.Add(_diffScrollView);

            // Close button
            var closeButton = new Button(() => Close())
            {
                text = LocalizationManager.GetText("common.close")
            };
            closeButton.style.width = 100;
            closeButton.style.alignSelf = Align.Center;
            closeButton.style.marginTop = 15;
            _rootElement.Add(closeButton);

            // Generate diff content if operations are already initialized
            if (_leftOperation.timestamp != default || _rightOperation.timestamp != default)
            {
                GenerateDiffContent();
            }
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
            
            Debug.Log($"[OperationDiff] IsValidOperationForDiff: Name='{operation.operationName}' Valid={isValid}");
            
            return isValid;
        }
        
        private string GetDisplayNameForOperation(UnityUndoMonitor.UndoOperation operation)
        {
            // If operation has a name, use it
            if (!string.IsNullOrEmpty(operation.operationName))
            {
                return operation.operationName;
            }
            
            // If operation has component snapshots, generate description from them
            if (HasValidComponentSnapshots(operation))
            {
                var changes = ExtractStateChanges(operation);
                if (changes.Count > 0)
                {
                    var firstChange = changes.First();
                    if (changes.Count == 1)
                    {
                        return $"Component Change: {firstChange.Key}";
                    }
                    else
                    {
                        return $"Component Changes: {firstChange.Key} and {changes.Count - 1} more";
                    }
                }
            }
            
            // If operation has target instanceID, try to get object info
            if (operation.targetInstanceID != 0)
            {
                try
                {
                    var targetObject = UnityEditor.EditorUtility.InstanceIDToObject(operation.targetInstanceID);
                    if (targetObject != null)
                    {
                        return $"Operation on {targetObject.name}";
                    }
                    else
                    {
                        return $"Operation on Object({operation.targetInstanceID})";
                    }
                }
                catch
                {
                    return $"Operation on Object({operation.targetInstanceID})";
                }
            }
            
            // Fallback
            return LocalizationManager.GetText("operation_diff.unknown_operation");
        }

        private void GenerateDiffContent()
        {
            _diffScrollView.Clear();

            // Validate operations before processing
            var leftValid = IsValidOperationForDiff(_leftOperation);
            var rightValid = IsValidOperationForDiff(_rightOperation);
            
            Debug.Log($"[OperationDiff] Content validation: Left={leftValid}, Right={rightValid}");
            
            if (!leftValid && !rightValid)
            {
                Debug.LogWarning("[OperationDiff] Both operations invalid, showing error message");
                var errorLabel = new Label(LocalizationManager.GetText("operation_diff.error_invalid_data"));
                errorLabel.style.fontSize = 14;
                errorLabel.style.color = new StyleColor(Color.red);
                errorLabel.style.marginTop = 20;
                errorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _diffScrollView.Add(errorLabel);
                return;
            }

            // Parse operation names to get details
            var leftName = GetDisplayNameForOperation(_leftOperation);
            var rightName = GetDisplayNameForOperation(_rightOperation);
            
            var leftType = leftName;
            var rightType = rightName;

            // Operation Information Section
            CreateSectionHeader("Operation Information");
            
            var basicComparisons = new List<(string property, string leftValue, string rightValue)>
            {
                ("Operation Type", leftType, rightType),
                ("Target InstanceID", _leftOperation.targetInstanceID.ToString(), _rightOperation.targetInstanceID.ToString()),
                ("Source", _leftOperation.isMcpOperation ? "MCP" : "Manual", _rightOperation.isMcpOperation ? "MCP" : "Manual"),
                ("Timestamp", _leftOperation.timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"), _rightOperation.timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            };

            foreach (var (property, leftValue, rightValue) in basicComparisons)
            {
                CreateDiffRow(property, leftValue, rightValue);
            }

            // State Changes Section
            CreateSectionHeader("State Changes Analysis");
            
            // Extract and compare actual state changes
            var leftStateChanges = ExtractStateChanges(_leftOperation);
            var rightStateChanges = ExtractStateChanges(_rightOperation);
            
            CompareStateChanges(leftStateChanges, rightStateChanges);

            // Add time difference analysis
            var timeDiff = Math.Abs((_rightOperation.timestamp - _leftOperation.timestamp).TotalSeconds);
            var timeDiffContainer = new VisualElement();
            timeDiffContainer.style.marginTop = 15;
            timeDiffContainer.style.paddingTop = 10;
            timeDiffContainer.style.borderTopWidth = 1;
            timeDiffContainer.style.borderTopColor = new StyleColor(Color.gray);

            var timeDiffLabel = new Label(LocalizationManager.GetText("operation_diff.time_difference", timeDiff.ToString("F3")));
            timeDiffLabel.style.fontSize = 12;
            timeDiffLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            timeDiffLabel.style.color = new StyleColor(Color.cyan);
            timeDiffContainer.Add(timeDiffLabel);

            _diffScrollView.Add(timeDiffContainer);

            // Add analysis summary
            var analysisContainer = new VisualElement();
            analysisContainer.style.marginTop = 10;
            analysisContainer.style.paddingTop = 10;
            analysisContainer.style.borderTopWidth = 1;
            analysisContainer.style.borderTopColor = new StyleColor(Color.gray);

            var analysisTitle = new Label(LocalizationManager.GetText("operation_diff.analysis_summary"));
            analysisTitle.style.fontSize = 14;
            analysisTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            analysisTitle.style.marginBottom = 5;
            analysisContainer.Add(analysisTitle);

            var sameObject = _leftOperation.targetInstanceID != 0 && _leftOperation.targetInstanceID == _rightOperation.targetInstanceID;
            var sameType = leftType.Equals(rightType, StringComparison.OrdinalIgnoreCase);
            var sameSource = _leftOperation.isMcpOperation == _rightOperation.isMcpOperation;

            var analysis = new StringBuilder();
            analysis.AppendLine($"• Same Object: {(sameObject ? "✓ Yes" : "✗ No")}");
            analysis.AppendLine($"• Same Operation Type: {(sameType ? "✓ Yes" : "✗ No")}");
            analysis.AppendLine($"• Same Source: {(sameSource ? "✓ Yes" : "✗ No")}");
            
            if (timeDiff < 1.0)
                analysis.AppendLine("• These operations occurred very close in time (< 1 second)");
            else if (timeDiff < 10.0)
                analysis.AppendLine("• These operations occurred close in time (< 10 seconds)");
            else
                analysis.AppendLine("• These operations were separated by significant time");

            var analysisText = new Label(analysis.ToString());
            analysisText.style.fontSize = 11;
            analysisText.style.color = new StyleColor(Color.gray);
            analysisText.style.whiteSpace = WhiteSpace.Normal;
            analysisContainer.Add(analysisText);

            _diffScrollView.Add(analysisContainer);
        }

        private void CreateDiffRow(string property, string leftValue, string rightValue)
        {
            var rowContainer = new VisualElement();
            rowContainer.style.marginBottom = 8;
            rowContainer.style.paddingBottom = 8;
            rowContainer.style.borderBottomWidth = 1;
            rowContainer.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));

            // Property label
            var propertyLabel = new Label($"{property}:");
            propertyLabel.style.fontSize = 12;
            propertyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            propertyLabel.style.marginBottom = 3;
            rowContainer.Add(propertyLabel);

            // Values container
            var valuesContainer = new VisualElement();
            valuesContainer.style.flexDirection = FlexDirection.Row;

            // Left value (red if different)
            var leftContainer = new VisualElement();
            leftContainer.style.flexGrow = 1;
            leftContainer.style.marginRight = 5;

            var leftLabel = new Label("A:");
            leftLabel.style.fontSize = 10;
            leftLabel.style.color = new StyleColor(Color.red);
            leftLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            leftContainer.Add(leftLabel);

            var leftValueLabel = new Label(leftValue);
            leftValueLabel.style.fontSize = 11;
            leftValueLabel.style.color = new StyleColor(leftValue != rightValue ? Color.red : Color.white);
            leftValueLabel.style.backgroundColor = new StyleColor(leftValue != rightValue ? new Color(0.3f, 0.1f, 0.1f, 0.3f) : Color.clear);
            leftValueLabel.style.paddingLeft = 5;
            leftValueLabel.style.paddingRight = 5;
            leftValueLabel.style.paddingTop = 2;
            leftValueLabel.style.paddingBottom = 2;
            leftValueLabel.style.borderTopLeftRadius = 3;
            leftValueLabel.style.borderTopRightRadius = 3;
            leftValueLabel.style.borderBottomLeftRadius = 3;
            leftValueLabel.style.borderBottomRightRadius = 3;
            leftContainer.Add(leftValueLabel);

            // Right value (green if different)
            var rightContainer = new VisualElement();
            rightContainer.style.flexGrow = 1;
            rightContainer.style.marginLeft = 5;

            var rightLabel = new Label("B:");
            rightLabel.style.fontSize = 10;
            rightLabel.style.color = new StyleColor(Color.green);
            rightLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rightContainer.Add(rightLabel);

            var rightValueLabel = new Label(rightValue);
            rightValueLabel.style.fontSize = 11;
            rightValueLabel.style.color = new StyleColor(leftValue != rightValue ? Color.green : Color.white);
            rightValueLabel.style.backgroundColor = new StyleColor(leftValue != rightValue ? new Color(0.1f, 0.3f, 0.1f, 0.3f) : Color.clear);
            rightValueLabel.style.paddingLeft = 5;
            rightValueLabel.style.paddingRight = 5;
            rightValueLabel.style.paddingTop = 2;
            rightValueLabel.style.paddingBottom = 2;
            rightValueLabel.style.borderTopLeftRadius = 3;
            rightValueLabel.style.borderTopRightRadius = 3;
            rightValueLabel.style.borderBottomLeftRadius = 3;
            rightValueLabel.style.borderBottomRightRadius = 3;
            rightContainer.Add(rightValueLabel);

            valuesContainer.Add(leftContainer);
            valuesContainer.Add(rightContainer);
            rowContainer.Add(valuesContainer);

            _diffScrollView.Add(rowContainer);
        }

        private void CreateSectionHeader(string title)
        {
            var sectionHeader = new VisualElement();
            sectionHeader.style.marginTop = 20;
            sectionHeader.style.marginBottom = 10;
            sectionHeader.style.paddingTop = 10;
            sectionHeader.style.borderTopWidth = 2;
            sectionHeader.style.borderTopColor = new StyleColor(Color.cyan);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(Color.cyan);
            sectionHeader.Add(titleLabel);

            _diffScrollView.Add(sectionHeader);
        }

        private Dictionary<string, string> ExtractStateChanges(UnityUndoMonitor.UndoOperation operation)
        {
            var stateChanges = new Dictionary<string, string>();
            
            // First, try to use component snapshots if available
            if (HasValidComponentSnapshots(operation))
            {
                return ExtractFromComponentSnapshots(operation);
            }
            
            // Fallback to operation name parsing for backward compatibility
            var operationName = operation.operationName;
            
            // Check for null or empty operation name
            if (string.IsNullOrEmpty(operationName))
            {
                stateChanges["Change"] = "Operation name not available";
                return stateChanges;
            }
            
            // Extract state changes based on operation type
            if (operationName.Contains("Set Position"))
            {
                var positionInfo = ExtractPositionFromOperation(operationName);
                if (!string.IsNullOrEmpty(positionInfo))
                {
                    stateChanges["Position"] = positionInfo;
                }
            }
            else if (operationName.Contains("Set Rotation"))
            {
                var rotationInfo = ExtractRotationFromOperation(operationName);
                if (!string.IsNullOrEmpty(rotationInfo))
                {
                    stateChanges["Rotation"] = rotationInfo;
                }
            }
            else if (operationName.Contains("Set Scale"))
            {
                var scaleInfo = ExtractScaleFromOperation(operationName);
                if (!string.IsNullOrEmpty(scaleInfo))
                {
                    stateChanges["Scale"] = scaleInfo;
                }
            }
            else if (operationName.Contains("Add Component") || operationName.Contains("Add"))
            {
                var componentInfo = ExtractComponentFromOperation(operationName, true);
                if (!string.IsNullOrEmpty(componentInfo))
                {
                    stateChanges["Component Added"] = componentInfo;
                }
            }
            else if (operationName.Contains("Remove Component") || operationName.Contains("Remove"))
            {
                var componentInfo = ExtractComponentFromOperation(operationName, false);
                if (!string.IsNullOrEmpty(componentInfo))
                {
                    stateChanges["Component Removed"] = componentInfo;
                }
            }
            else if (operationName.Contains("Rename"))
            {
                var nameInfo = ExtractNameFromOperation(operationName);
                if (!string.IsNullOrEmpty(nameInfo))
                {
                    stateChanges["Object Name"] = nameInfo;
                }
            }
            else
            {
                // Try to extract from Unity native undo history
                var nativeInfo = ExtractFromNativeUndoHistory(operation);
                if (nativeInfo.Count > 0)
                {
                    foreach (var kvp in nativeInfo)
                    {
                        stateChanges[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    // Fallback: Generic property change
                    stateChanges["Change"] = $"Generic operation: {operationName}";
                }
            }
            
            return stateChanges;
        }
        
        private bool HasValidComponentSnapshots(UnityUndoMonitor.UndoOperation operation)
        {
            var hasBefore = operation.beforeState.captureTime != default;
            var hasAfter = operation.afterState.captureTime != default;
            var result = hasBefore || hasAfter;
            
            // Debug.Log($"[OperationDiff] HasValidComponentSnapshots: Result={result}");
            
            return result;
        }
        
        private Dictionary<string, string> ExtractFromComponentSnapshots(UnityUndoMonitor.UndoOperation operation)
        {
            var stateChanges = new Dictionary<string, string>();
            var beforeState = operation.beforeState;
            var afterState = operation.afterState;
            
            // Compare Transform states
            if (beforeState.captureTime != default && afterState.captureTime != default)
            {
                CompareTransformStates(beforeState.transformState, afterState.transformState, stateChanges);
                CompareComponentStates(beforeState.components, afterState.components, stateChanges);
            }
            else if (afterState.captureTime != default)
            {
                // Only after state available - show current values
                ExtractCurrentComponentStates(afterState, stateChanges);
            }
            else if (beforeState.captureTime != default)
            {
                // Only before state available - show previous values
                ExtractPreviousComponentStates(beforeState, stateChanges);
            }
            
            return stateChanges;
        }
        
        private void CompareTransformStates(TransformState before, TransformState after, Dictionary<string, string> stateChanges)
        {
            if (before.position != after.position)
            {
                stateChanges["Transform.Position"] = $"{before.position} → {after.position}";
            }
            
            if (before.rotation != after.rotation)
            {
                var beforeEuler = before.rotation.eulerAngles;
                var afterEuler = after.rotation.eulerAngles;
                stateChanges["Transform.Rotation"] = $"{beforeEuler} → {afterEuler}";
            }
            
            if (before.localScale != after.localScale)
            {
                stateChanges["Transform.Scale"] = $"{before.localScale} → {after.localScale}";
            }
            
            if (before.parent != after.parent)
            {
                var beforeParent = before.parent != 0 ? $"Parent({before.parent})" : "None";
                var afterParent = after.parent != 0 ? $"Parent({after.parent})" : "None";
                stateChanges["Transform.Parent"] = $"{beforeParent} → {afterParent}";
            }
        }
        
        private void CompareComponentStates(Dictionary<string, ComponentState> before, Dictionary<string, ComponentState> after, Dictionary<string, string> stateChanges)
        {
            if (before == null && after == null) return;
            
            var allComponentTypes = new HashSet<string>();
            if (before != null) foreach (var key in before.Keys) allComponentTypes.Add(key);
            if (after != null) foreach (var key in after.Keys) allComponentTypes.Add(key);
            
            foreach (var componentType in allComponentTypes)
            {
                var beforeExists = before?.ContainsKey(componentType) == true;
                var afterExists = after?.ContainsKey(componentType) == true;
                
                if (!beforeExists && afterExists)
                {
                    // Component was added
                    stateChanges[$"Component.{componentType}"] = "Added";
                    ExtractComponentProperties(after[componentType], componentType, stateChanges, "Added");
                }
                else if (beforeExists && !afterExists)
                {
                    // Component was removed
                    stateChanges[$"Component.{componentType}"] = "Removed";
                }
                else if (beforeExists && afterExists)
                {
                    // Component was modified
                    CompareComponentProperties(before[componentType], after[componentType], componentType, stateChanges);
                }
            }
        }
        
        private void CompareComponentProperties(ComponentState before, ComponentState after, string componentType, Dictionary<string, string> stateChanges)
        {
            // Compare enabled state
            if (before.enabled != after.enabled)
            {
                stateChanges[$"{componentType}.Enabled"] = $"{before.enabled} → {after.enabled}";
            }
            
            // Compare properties
            if (before.properties != null && after.properties != null)
            {
                var allProps = new HashSet<string>();
                foreach (var key in before.properties.Keys) allProps.Add(key);
                foreach (var key in after.properties.Keys) allProps.Add(key);
                
                foreach (var propName in allProps)
                {
                    var beforeHas = before.properties.ContainsKey(propName);
                    var afterHas = after.properties.ContainsKey(propName);
                    
                    if (beforeHas && afterHas)
                    {
                        var beforeValue = before.properties[propName];
                        var afterValue = after.properties[propName];
                        
                        if (!Equals(beforeValue, afterValue))
                        {
                            stateChanges[$"{componentType}.{propName}"] = $"{beforeValue} → {afterValue}";
                        }
                    }
                    else if (!beforeHas && afterHas)
                    {
                        stateChanges[$"{componentType}.{propName}"] = $"Added: {after.properties[propName]}";
                    }
                    else if (beforeHas && !afterHas)
                    {
                        stateChanges[$"{componentType}.{propName}"] = $"Removed: {before.properties[propName]}";
                    }
                }
            }
        }
        
        private void ExtractComponentProperties(ComponentState componentState, string componentType, Dictionary<string, string> stateChanges, string prefix)
        {
            if (componentState.properties != null)
            {
                foreach (var prop in componentState.properties)
                {
                    stateChanges[$"{componentType}.{prop.Key}"] = $"{prefix}: {prop.Value}";
                }
            }
        }
        
        private void ExtractCurrentComponentStates(ComponentSnapshot snapshot, Dictionary<string, string> stateChanges)
        {
            stateChanges["Transform.Position"] = snapshot.transformState.position.ToString();
            stateChanges["Transform.Rotation"] = snapshot.transformState.rotation.eulerAngles.ToString();
            stateChanges["Transform.Scale"] = snapshot.transformState.localScale.ToString();
            
            if (snapshot.components != null)
            {
                foreach (var component in snapshot.components)
                {
                    ExtractComponentProperties(component.Value, component.Key, stateChanges, "Current");
                }
            }
        }
        
        private void ExtractPreviousComponentStates(ComponentSnapshot snapshot, Dictionary<string, string> stateChanges)
        {
            stateChanges["Transform.Position"] = $"Previous: {snapshot.transformState.position}";
            stateChanges["Transform.Rotation"] = $"Previous: {snapshot.transformState.rotation.eulerAngles}";
            stateChanges["Transform.Scale"] = $"Previous: {snapshot.transformState.localScale}";
            
            if (snapshot.components != null)
            {
                foreach (var component in snapshot.components)
                {
                    ExtractComponentProperties(component.Value, component.Key, stateChanges, "Previous");
                }
            }
        }

        private string ExtractPositionFromOperation(string operationName)
        {
            // Try to extract position coordinates from operation name
            // Format examples: "Set Position to (1, 2, 3)", "Set Position TestPlane", etc.
            
            if (operationName.Contains("to (") && operationName.Contains(")"))
            {
                var start = operationName.IndexOf("to (") + 4;
                var end = operationName.IndexOf(")", start);
                if (end > start)
                {
                    return operationName.Substring(start, end - start);
                }
            }
            
            // Try to get current object position if we have instanceID
            return "Position changed (details not captured)";
        }

        private string ExtractRotationFromOperation(string operationName)
        {
            if (operationName.Contains("to (") && operationName.Contains(")"))
            {
                var start = operationName.IndexOf("to (") + 4;
                var end = operationName.IndexOf(")", start);
                if (end > start)
                {
                    return operationName.Substring(start, end - start);
                }
            }
            
            return "Rotation changed (details not captured)";
        }

        private string ExtractScaleFromOperation(string operationName)
        {
            if (operationName.Contains("to (") && operationName.Contains(")"))
            {
                var start = operationName.IndexOf("to (") + 4;
                var end = operationName.IndexOf(")", start);
                if (end > start)
                {
                    return operationName.Substring(start, end - start);
                }
            }
            
            return "Scale changed (details not captured)";
        }

        private string ExtractComponentFromOperation(string operationName, bool isAdd)
        {
            // Extract component type from operation name directly
            
            if (isAdd)
            {
                if (operationName.Contains("Add Component"))
                {
                    var componentStart = operationName.IndexOf("Add Component") + 13;
                    var remaining = operationName.Substring(componentStart).Trim();
                    return string.IsNullOrEmpty(remaining) ? "Unknown Component" : remaining;
                }
                else if (operationName.Contains("Add"))
                {
                    var addStart = operationName.IndexOf("Add") + 3;
                    var remaining = operationName.Substring(addStart).Trim();
                    return string.IsNullOrEmpty(remaining) ? "Unknown Item" : remaining;
                }
            }
            else
            {
                if (operationName.Contains("Remove Component"))
                {
                    var componentStart = operationName.IndexOf("Remove Component") + 16;
                    var remaining = operationName.Substring(componentStart).Trim();
                    return string.IsNullOrEmpty(remaining) ? "Unknown Component" : remaining;
                }
                else if (operationName.Contains("Remove"))
                {
                    var removeStart = operationName.IndexOf("Remove") + 6;
                    var remaining = operationName.Substring(removeStart).Trim();
                    return string.IsNullOrEmpty(remaining) ? "Unknown Item" : remaining;
                }
            }
            
            return "Unknown";
        }

        private string ExtractNameFromOperation(string operationName)
        {
            // Extract old and new names from rename operation
            if (operationName.Contains("Rename"))
            {
                var renameIndex = operationName.IndexOf("Rename");
                var nameInfo = operationName.Substring(renameIndex + 6).Trim();
                return nameInfo;
            }
            return "Name changed";
        }

        private void CompareStateChanges(Dictionary<string, string> leftChanges, Dictionary<string, string> rightChanges)
        {
            if (leftChanges.Count == 0 && rightChanges.Count == 0)
            {
                var noChangesLabel = new Label(LocalizationManager.GetText("operation_diff.no_changes"));
                noChangesLabel.style.fontSize = 12;
                noChangesLabel.style.color = new StyleColor(Color.gray);
                noChangesLabel.style.marginTop = 10;
                _diffScrollView.Add(noChangesLabel);
                return;
            }

            // Find all unique properties
            var allProperties = new HashSet<string>();
            foreach (var key in leftChanges.Keys) allProperties.Add(key);
            foreach (var key in rightChanges.Keys) allProperties.Add(key);

            foreach (var property in allProperties.OrderBy(p => p))
            {
                var leftValue = leftChanges.ContainsKey(property) ? leftChanges[property] : "No change";
                var rightValue = rightChanges.ContainsKey(property) ? rightChanges[property] : "No change";
                
                CreateStateChangeRow(property, leftValue, rightValue);
            }

            // Add delta analysis
            CreateDeltaAnalysis(leftChanges, rightChanges);
        }

        private void CreateStateChangeRow(string property, string leftValue, string rightValue)
        {
            var rowContainer = new VisualElement();
            rowContainer.style.marginBottom = 12;
            rowContainer.style.paddingBottom = 8;
            rowContainer.style.borderBottomWidth = 1;
            rowContainer.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));

            // Property label with icon
            var propertyContainer = new VisualElement();
            propertyContainer.style.flexDirection = FlexDirection.Row;
            propertyContainer.style.alignItems = Align.Center;
            propertyContainer.style.marginBottom = 5;

            var propertyLabel = new Label($"{property}:");
            propertyLabel.style.fontSize = 13;
            propertyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            propertyLabel.style.color = new StyleColor(Color.white);

            propertyContainer.Add(propertyLabel);
            rowContainer.Add(propertyContainer);

            // Values container
            var valuesContainer = new VisualElement();
            valuesContainer.style.flexDirection = FlexDirection.Row;
            valuesContainer.style.marginLeft = 22; // Indent to align with property text

            // Left value (Before - Red theme)
            var leftContainer = new VisualElement();
            leftContainer.style.flexGrow = 1;
            leftContainer.style.marginRight = 10;

            var leftLabel = new Label(LocalizationManager.GetText("operation_diff.before"));
            leftLabel.style.fontSize = 10;
            leftLabel.style.color = new StyleColor(Color.red);
            leftLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            leftContainer.Add(leftLabel);

            var leftValueLabel = new Label(leftValue);
            leftValueLabel.style.fontSize = 11;
            leftValueLabel.style.color = new StyleColor(leftValue != rightValue ? new Color(1f, 0.8f, 0.8f) : Color.white);
            leftValueLabel.style.backgroundColor = new StyleColor(leftValue != rightValue ? new Color(0.3f, 0.1f, 0.1f, 0.4f) : Color.clear);
            leftValueLabel.style.paddingLeft = 8;
            leftValueLabel.style.paddingRight = 8;
            leftValueLabel.style.paddingTop = 4;
            leftValueLabel.style.paddingBottom = 4;
            leftValueLabel.style.borderTopLeftRadius = 4;
            leftValueLabel.style.borderTopRightRadius = 4;
            leftValueLabel.style.borderBottomLeftRadius = 4;
            leftValueLabel.style.borderBottomRightRadius = 4;
            leftValueLabel.style.whiteSpace = WhiteSpace.Normal;
            leftContainer.Add(leftValueLabel);

            // Right value (After - Green theme)
            var rightContainer = new VisualElement();
            rightContainer.style.flexGrow = 1;
            rightContainer.style.marginLeft = 10;

            var rightLabel = new Label(LocalizationManager.GetText("operation_diff.after"));
            rightLabel.style.fontSize = 10;
            rightLabel.style.color = new StyleColor(Color.green);
            rightLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rightContainer.Add(rightLabel);

            var rightValueLabel = new Label(rightValue);
            rightValueLabel.style.fontSize = 11;
            rightValueLabel.style.color = new StyleColor(leftValue != rightValue ? new Color(0.8f, 1f, 0.8f) : Color.white);
            rightValueLabel.style.backgroundColor = new StyleColor(leftValue != rightValue ? new Color(0.1f, 0.3f, 0.1f, 0.4f) : Color.clear);
            rightValueLabel.style.paddingLeft = 8;
            rightValueLabel.style.paddingRight = 8;
            rightValueLabel.style.paddingTop = 4;
            rightValueLabel.style.paddingBottom = 4;
            rightValueLabel.style.borderTopLeftRadius = 4;
            rightValueLabel.style.borderTopRightRadius = 4;
            rightValueLabel.style.borderBottomLeftRadius = 4;
            rightValueLabel.style.borderBottomRightRadius = 4;
            rightValueLabel.style.whiteSpace = WhiteSpace.Normal;
            rightContainer.Add(rightValueLabel);

            valuesContainer.Add(leftContainer);
            valuesContainer.Add(rightContainer);
            rowContainer.Add(valuesContainer);

            _diffScrollView.Add(rowContainer);
        }



        private void CreateDeltaAnalysis(Dictionary<string, string> leftChanges, Dictionary<string, string> rightChanges)
        {
            var deltaContainer = new VisualElement();
            deltaContainer.style.marginTop = 20;
            deltaContainer.style.paddingTop = 15;
            deltaContainer.style.borderTopWidth = 2;
            deltaContainer.style.borderTopColor = new StyleColor(Color.yellow);

            var deltaTitle = new Label(LocalizationManager.GetText("operation_diff.change_analysis"));
            deltaTitle.style.fontSize = 14;
            deltaTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            deltaTitle.style.color = new StyleColor(Color.yellow);
            deltaTitle.style.marginBottom = 10;
            deltaContainer.Add(deltaTitle);

            var analysis = new StringBuilder();
            
            if (leftChanges.Count == 0 && rightChanges.Count == 0)
            {
                analysis.AppendLine("• No specific state changes detected in either operation");
            }
            else if (leftChanges.Count == 0)
            {
                analysis.AppendLine($"• Right operation contains {rightChanges.Count} state change(s), left operation has none");
            }
            else if (rightChanges.Count == 0)
            {
                analysis.AppendLine($"• Left operation contains {leftChanges.Count} state change(s), right operation has none");
            }
            else
            {
                var commonProperties = leftChanges.Keys.Intersect(rightChanges.Keys).ToList();
                if (commonProperties.Count > 0)
                {
                    analysis.AppendLine($"• Both operations affect similar properties: {string.Join(", ", commonProperties)}");
                }
                
                var leftOnly = leftChanges.Keys.Except(rightChanges.Keys).ToList();
                var rightOnly = rightChanges.Keys.Except(leftChanges.Keys).ToList();
                
                if (leftOnly.Count > 0)
                {
                    analysis.AppendLine($"• Left operation only: {string.Join(", ", leftOnly)}");
                }
                
                if (rightOnly.Count > 0)
                {
                    analysis.AppendLine($"• Right operation only: {string.Join(", ", rightOnly)}");
                }
            }

            var analysisText = new Label(analysis.ToString());
            analysisText.style.fontSize = 11;
            analysisText.style.color = new StyleColor(Color.gray);
            analysisText.style.whiteSpace = WhiteSpace.Normal;
            deltaContainer.Add(analysisText);

            _diffScrollView.Add(deltaContainer);
        }

        /// <summary>
        /// Extract information from Unity native undo history when no specific pattern matches
        /// </summary>
        private Dictionary<string, string> ExtractFromNativeUndoHistory(UnityUndoMonitor.UndoOperation operation)
        {
            var extractedInfo = new Dictionary<string, string>();
            var operationName = operation.operationName;
            
            if (string.IsNullOrEmpty(operationName))
                return extractedInfo;

            try
            {
                // Parse the raw operation name for useful information
                var cleanName = operationName.Trim();

                // Remove MCP prefix if present
                if (cleanName.StartsWith("[MCP]"))
                {
                    cleanName = cleanName.Substring(5).Trim();
                }

                // Try to extract meaningful parts from the operation name
                if (cleanName.Contains("→") || cleanName.Contains("->"))
                {
                    // Handle change operations like "property → newValue"
                    var parts = cleanName.Split(new[] { "→", "->" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var property = parts[0].Trim();
                        var newValue = parts[1].Trim();
                        extractedInfo[property] = newValue;
                    }
                }
                else if (cleanName.Contains(":"))
                {
                    // Handle property operations like "Set Position: (1,2,3)" or "GameObject: Action"
                    var colonIndex = cleanName.IndexOf(':');
                    var beforeColon = cleanName.Substring(0, colonIndex).Trim();
                    var afterColon = cleanName.Substring(colonIndex + 1).Trim();
                    
                    // Check if before colon looks like a property or action
                    if (beforeColon.StartsWith("Set ") || beforeColon.StartsWith("Change ") || 
                        beforeColon.StartsWith("Modify ") || beforeColon.StartsWith("Update "))
                    {
                        var property = beforeColon.Replace("Set ", "").Replace("Change ", "")
                                                 .Replace("Modify ", "").Replace("Update ", "").Trim();
                        extractedInfo[property] = afterColon;
                    }
                    else
                    {
                        // General format like "Object: Action"
                        extractedInfo[beforeColon] = afterColon;
                    }
                }
                else if (cleanName.Contains(" to "))
                {
                    // Handle "Set something to value" patterns
                    var toIndex = cleanName.IndexOf(" to ");
                    var action = cleanName.Substring(0, toIndex).Trim();
                    var value = cleanName.Substring(toIndex + 4).Trim();
                    
                    if (action.StartsWith("Set "))
                    {
                        var property = action.Substring(4).Trim();
                        extractedInfo[property] = value;
                    }
                    else
                    {
                        extractedInfo[action] = value;
                    }
                }
                else if (cleanName.Contains(" in "))
                {
                    // Handle "Action in Object" patterns
                    var inIndex = cleanName.IndexOf(" in ");
                    var action = cleanName.Substring(0, inIndex).Trim();
                    var target = cleanName.Substring(inIndex + 4).Trim();
                    extractedInfo["Target"] = target;
                    extractedInfo["Action"] = action;
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(cleanName, @"\([^)]+\)"))
                {
                    // Handle operations with parentheses like "Action (details)"
                    var match = System.Text.RegularExpressions.Regex.Match(cleanName, @"^(.+?)\s*\(([^)]+)\)");
                    if (match.Success)
                    {
                        var action = match.Groups[1].Value.Trim();
                        var details = match.Groups[2].Value.Trim();
                        extractedInfo["Action"] = action;
                        extractedInfo["Details"] = details;
                    }
                }
                else
                {
                    // For operations that don't match any pattern, try to extract key information
                    var words = cleanName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2)
                    {
                        // First word is likely the action, rest might be object or details
                        var action = words[0];
                        var details = string.Join(" ", words.Skip(1));
                        extractedInfo["Action"] = action;
                        if (!string.IsNullOrEmpty(details))
                        {
                            extractedInfo["Target/Details"] = details;
                        }
                    }
                    else if (words.Length == 1)
                    {
                        extractedInfo["Operation"] = words[0];
                    }
                }

                // Add additional context information if available
                if (operation.targetInstanceID != 0)
                {
                    extractedInfo["Target InstanceID"] = operation.targetInstanceID.ToString();
                }

                if (operation.timestamp != default(DateTime))
                {
                    extractedInfo["Timestamp"] = operation.timestamp.ToString("HH:mm:ss.fff");
                }

                // Add operation type context
                extractedInfo["Operation Type"] = operation.isMcpOperation ? "MCP Operation" : "Manual Operation";
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OperationDiffWindow] Error extracting from native undo history: {ex.Message}");
                // Fallback to basic info
                extractedInfo["Raw Operation"] = operationName;
            }

            return extractedInfo;
        }
    }
}