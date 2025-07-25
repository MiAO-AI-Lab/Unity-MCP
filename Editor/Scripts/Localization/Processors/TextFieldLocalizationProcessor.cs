using System.Linq;
using UnityEngine.UIElements;
using com.MiAO.Unity.MCP.Editor.Common;

namespace com.MiAO.Unity.MCP.Editor.Localization.Processors
{
    /// <summary>
    /// TextField控件本地化处理器
    /// </summary>
    public class TextFieldLocalizationProcessor : ILocalizationProcessor
    {
        public int Priority => 100;
        
        public bool CanProcess(VisualElement element)
        {
            return element is TextField;
        }
        
        public void Process(VisualElement element, LocalizationConfig config, LocalizationContext context)
        {
            var textField = (TextField)element;
            
            // 处理标签文本
            if (!string.IsNullOrEmpty(config.LabelKey))
            {
                var labelText = GetLocalizedText(config.LabelKey, config, context);
                textField.label = labelText;
            }
            
            // 处理占位符文本
            if (!string.IsNullOrEmpty(config.PlaceholderKey))
            {
                var placeholderText = GetLocalizedText(config.PlaceholderKey, config, context);
                // 在Unity UI Toolkit中，TextField的占位符通过value设置或其他方式
                // 由于不同版本可能有差异，我们先注释掉这部分
                // textField.placeholder = placeholderText;
                
                // 作为替代方案，我们可以在自定义属性中处理
                if (config.CustomProperties.ContainsKey("placeholder"))
                {
                    // 可以在这里添加特定的占位符处理逻辑
                }
            }
            
            // 处理工具提示
            if (!string.IsNullOrEmpty(config.TooltipKey))
            {
                var tooltipText = GetLocalizedText(config.TooltipKey, config, context);
                textField.tooltip = tooltipText;
            }
            
            // 处理自定义属性
            ProcessCustomProperties(textField, config, context);
        }
        
        private string GetLocalizedText(string key, LocalizationConfig config, LocalizationContext context)
        {
            var text = LocalizationManager.GetText(key);
            
            // 处理格式化参数
            if (config.Parameters != null && config.Parameters.Count > 0)
            {
                var args = config.Parameters.Select(p => p.GetValue(context)).ToArray();
                text = string.Format(text, args);
            }
            
            return text;
        }
        
        private void ProcessCustomProperties(TextField textField, LocalizationConfig config, LocalizationContext context)
        {
            foreach (var kvp in config.CustomProperties)
            {
                var localizedValue = LocalizationManager.GetText(kvp.Value);
                
                switch (kvp.Key.ToLower())
                {
                    case "class":
                    case "css-class":
                        ApplyCssClass(textField, localizedValue);
                        break;
                    case "readonly":
                        ApplyReadOnlyState(textField, localizedValue);
                        break;
                    case "enabled":
                        ApplyEnabledState(textField, localizedValue);
                        break;
                    case "multiline":
                        ApplyMultilineState(textField, localizedValue);
                        break;
                    default:
                        UnityEngine.Debug.Log($"[TextFieldLocalizationProcessor] Unsupported custom property: {kvp.Key}");
                        break;
                }
            }
        }
        
        private void ApplyCssClass(TextField textField, string className)
        {
            if (!string.IsNullOrEmpty(className))
            {
                textField.AddToClassList(className);
            }
        }
        
        private void ApplyReadOnlyState(TextField textField, string readOnlyValue)
        {
            if (bool.TryParse(readOnlyValue, out var isReadOnly))
            {
                textField.isReadOnly = isReadOnly;
            }
        }
        
        private void ApplyEnabledState(TextField textField, string enabledValue)
        {
            if (bool.TryParse(enabledValue, out var isEnabled))
            {
                textField.SetEnabled(isEnabled);
            }
        }
        
        private void ApplyMultilineState(TextField textField, string multilineValue)
        {
            if (bool.TryParse(multilineValue, out var isMultiline))
            {
                textField.multiline = isMultiline;
            }
        }
    }
} 