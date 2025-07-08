using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections;
using System.Reflection;
using System.Linq;

namespace com.miao.unity.mcp.Editor.API.Tool
{
    /// <summary>
    /// UXML to uGUI conversion tool
    /// </summary>
    public static class UI
    {
        /// <summary>
        /// Convert UXML and USS files to uGUI
        /// </summary>
        /// <param name="uxmlPath">UXML file path</param>
        /// <param name="ussPath">USS file path (optional)</param>
        /// <param name="parentTransform">Parent transform component</param>
        /// <param name="rootWidth">Root element width (optional, default 1920)</param>
        /// <param name="rootHeight">Root element height (optional, default 1080)</param>
        /// <returns>Conversion result information</returns>
        public static string ConvertUXMLToUGUI(string uxmlPath, string ussPath = null, Transform parentTransform = null, float rootWidth = 1920f, float rootHeight = 1080f)
        {
            try
            {
                // Validate file path
                if (!File.Exists(uxmlPath))
                {
                    return $"Error: UXML file does not exist: {uxmlPath}";
                }

                // Parse UXML file
                var uxmlParser = new UXMLParser();
                var uxmlDocument = uxmlParser.Parse(uxmlPath);

                // Parse USS file (if provided)
                USSParser ussParser = null;
                int ussRulesCount = 0;
                if (!string.IsNullOrEmpty(ussPath) && File.Exists(ussPath))
                {
                    ussParser = new USSParser();
                    ussParser.Parse(ussPath);
                    ussRulesCount = ussParser.Rules.Count;
                }

                // Create converter
                var converter = new UXMLToUGUIConverter(ussParser);
                
                // Execute conversion
                var result = converter.Convert(uxmlDocument, parentTransform, rootWidth, rootHeight);

                var resultMessage = $"Successfully converted UXML to uGUI. Created {result.CreatedObjectsCount} objects";
                if (ussRulesCount > 0)
                {
                    resultMessage += $", applied {ussRulesCount} style rules";
                }
                resultMessage += ".";

                if (result.Warnings.Count > 0)
                {
                    resultMessage += $"\nWarnings ({result.Warnings.Count}):\n" + string.Join("\n", result.Warnings);
                }

                return resultMessage;
            }
            catch (Exception ex)
            {
                return $"Conversion failed: {ex.Message}\nStack trace: {ex.StackTrace}";
            }
        }

        /// <summary>
        /// Batch convert UXML files in a folder
        /// </summary>
        /// <param name="folderPath">Folder path</param>
        /// <param name="parentTransform">Parent transform component</param>
        /// <param name="rootWidth">Root element width (optional, default 1920)</param>
        /// <param name="rootHeight">Root element height (optional, default 1080)</param>
        /// <returns>Conversion result information</returns>
        public static string BatchConvertUXMLToUGUI(string folderPath, Transform parentTransform = null, float rootWidth = 1920f, float rootHeight = 1080f)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    return $"Error: Folder does not exist: {folderPath}";
                }

                var uxmlFiles = Directory.GetFiles(folderPath, "*.uxml", SearchOption.AllDirectories);
                int successCount = 0;
                int failCount = 0;
                var errors = new List<string>();

                foreach (var uxmlFile in uxmlFiles)
                {
                    try
                    {
                        var ussFile = Path.ChangeExtension(uxmlFile, ".uss");
                        var result = ConvertUXMLToUGUI(uxmlFile, ussFile, parentTransform, rootWidth, rootHeight);
                        
                        if (result.StartsWith("Successfully"))
                        {
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                            errors.Add($"{Path.GetFileName(uxmlFile)}: {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        errors.Add($"{Path.GetFileName(uxmlFile)}: {ex.Message}");
                    }
                }

                var resultMessage = $"Batch conversion completed. Success: {successCount}, Failed: {failCount}";
                if (errors.Count > 0)
                {
                    resultMessage += "\nError details:\n" + string.Join("\n", errors);
                }

                return resultMessage;
            }
            catch (Exception ex)
            {
                return $"Batch conversion failed: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// UXML document data structure
    /// </summary>
    public class UXMLDocument
    {
        public UXMLElement RootElement { get; set; }
    }

    /// <summary>
    /// UXML element data structure
    /// </summary>
    public class UXMLElement
    {
        public string Name { get; set; }
        public string LocalName { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public List<UXMLElement> Children { get; set; } = new List<UXMLElement>();
        public string TextContent { get; set; }
    }

    /// <summary>
    /// USS rule data structure
    /// </summary>
    public class USSRule
    {
        public string Selector { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Conversion result data structure
    /// </summary>
    public class ConversionResult
    {
        public int CreatedObjectsCount { get; set; }
        public List<GameObject> CreatedObjects { get; set; } = new List<GameObject>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// UXML parser
    /// </summary>
    public class UXMLParser
    {
        public UXMLDocument Parse(string filePath)
        {
            var document = new UXMLDocument();
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            // Parse root node
            var rootNode = xmlDoc.DocumentElement;
            if (rootNode != null)
            {
                document.RootElement = ParseElement(rootNode);
            }

            return document;
        }

        private UXMLElement ParseElement(XmlNode xmlNode)
        {
            var element = new UXMLElement
            {
                Name = xmlNode.Name,
                LocalName = xmlNode.LocalName
            };

            // Parse attributes
            if (xmlNode.Attributes != null)
            {
                foreach (XmlAttribute attr in xmlNode.Attributes)
                {
                    element.Attributes[attr.Name] = attr.Value;
                }
            }

            // Parse child elements
            foreach (XmlNode childNode in xmlNode.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    element.Children.Add(ParseElement(childNode));
                }
                else if (childNode.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(childNode.Value))
                {
                    element.TextContent = childNode.Value.Trim();
                }
            }

            return element;
        }
    }

    /// <summary>
    /// USS parser
    /// </summary>
    public class USSParser
    {
        public Dictionary<string, USSRule> Rules { get; private set; } = new Dictionary<string, USSRule>();

        public void Parse(string filePath)
        {
            var content = File.ReadAllText(filePath);
            ParseContent(content);
        }

        private void ParseContent(string content)
        {
            // Remove comments
            content = Regex.Replace(content, @"/\*.*?\*/", "", RegexOptions.Singleline);
            
            // Parse CSS rules
            var ruleMatches = Regex.Matches(content, @"([^{]+)\{([^}]+)\}", RegexOptions.Singleline);
            
            foreach (Match match in ruleMatches)
            {
                var selector = match.Groups[1].Value.Trim();
                var properties = match.Groups[2].Value.Trim();
                
                var rule = new USSRule { Selector = selector };
                ParseProperties(properties, rule);
                
                Rules[selector] = rule;
            }
        }

        private void ParseProperties(string properties, USSRule rule)
        {
            var propertyMatches = Regex.Matches(properties, @"([^:]+):([^;]+);?");
            
            foreach (Match match in propertyMatches)
            {
                var property = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                rule.Properties[property] = value;
            }
        }
    }

    /// <summary>
    /// UXML to uGUI converter
    /// </summary>
    public class UXMLToUGUIConverter
    {
        private USSParser _ussParser;
        private ConversionResult _result;

        public UXMLToUGUIConverter(USSParser ussParser = null)
        {
            _ussParser = ussParser;
        }

        public ConversionResult Convert(UXMLDocument document, Transform parentTransform = null, float rootWidth = 1920f, float rootHeight = 1080f)
        {
            _result = new ConversionResult();
            
            if (document.RootElement != null)
            {
                ConvertElement(document.RootElement, parentTransform, true, rootWidth, rootHeight);
            }

            return _result;
        }

        private GameObject ConvertElement(UXMLElement element, Transform parent, bool isRootElement = false, float rootWidth = 1920f, float rootHeight = 1080f)
        {
            GameObject gameObject = null;

            // Determine what type of uGUI component should be created based on type inheritance
            var elementType = GetUIElementType(element.LocalName);
            if (elementType != null)
            {
                gameObject = CreateGameObjectByType(element, parent, elementType);
            }
            else
            {
                gameObject = CreateGenericElement(element, parent);
            }

            if (gameObject != null)
            {
                // Establish element mapping relationship
                var rectTransform = gameObject.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    _elementMap[rectTransform] = element;
                    
                    // If root element, set specified dimensions and add Canvas component
                    if (isRootElement)
                    {
                        rectTransform.sizeDelta = new Vector2(rootWidth, rootHeight);
                        
                        // Add Canvas component
                        var canvas = gameObject.AddComponent<Canvas>();
                        canvas.sortingOrder = 0;
                        
                        // // Add CanvasScaler component for responsive scaling
                        // var canvasScaler = gameObject.AddComponent<CanvasScaler>();
                        // canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        // canvasScaler.referenceResolution = new Vector2(rootWidth, rootHeight);
                        // canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                        // canvasScaler.matchWidthOrHeight = 0.5f;
                        
                        // // Add GraphicRaycaster component for UI interaction
                        // var graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
                        // graphicRaycaster.ignoreReversedGraphics = true;
                        // graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                    }
                }
                
                // Apply styles
                ApplyStyles(gameObject, element, isRootElement);
                
                // Process child elements
                foreach (var child in element.Children)
                {
                    ConvertElement(child, gameObject.transform, false, rootWidth, rootHeight);
                }

                _result.CreatedObjects.Add(gameObject);
                _result.CreatedObjectsCount++;
            }

            return gameObject;
        }

        /// <summary>
        /// Get the corresponding type based on element name (supports custom namespaces)
        /// </summary>
        private Type GetUIElementType(string elementName)
        {
            if (string.IsNullOrEmpty(elementName))
                return null;

            try
            {
                // Strategy 1: If elementName contains namespace, try to get type directly
                if (elementName.Contains("."))
                {
                    var type = Type.GetType(elementName);
                    if (type != null)
                    {
                        return type;
                    }

                    // Try to find complete type name in all loaded assemblies
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            type = assembly.GetType(elementName);
                            if (type != null)
                            {
                                return type;
                            }
                        }
                        catch
                        {
                            // Ignore inaccessible assemblies
                        }
                    }
                }

                // Strategy 2: Find standard UI elements in Unity's UI namespaces
                var unityUINamespaces = new[]
                {
                    "UnityEngine.UIElements",
                    "UnityEditor.UIElements"
                };

                foreach (var ns in unityUINamespaces)
                {
                    var fullTypeName = $"{ns}.{elementName}";
                    var type = Type.GetType(fullTypeName);
                    if (type != null)
                    {
                        return type;
                    }

                    // Try different case combinations
                    var capitalizedName = char.ToUpper(elementName[0]) + elementName.Substring(1).ToLower();
                    fullTypeName = $"{ns}.{capitalizedName}";
                    type = Type.GetType(fullTypeName);
                    if (type != null)
                    {
                        return type;
                    }
                }

                // Strategy 3: Find by class name in all loaded assemblies (not restricted to namespace)
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var types = assembly.GetTypes();
                        
                        // First find exact match type
                        var exactMatchType = types.FirstOrDefault(t => 
                            t.Name.Equals(elementName, StringComparison.Ordinal));
                        if (exactMatchType != null)
                        {
                            return exactMatchType;
                        }

                        // Then find case-insensitive match
                        var caseInsensitiveMatchType = types.FirstOrDefault(t => 
                            t.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase));
                        if (caseInsensitiveMatchType != null)
                        {
                            return caseInsensitiveMatchType;
                        }

                        // Finally find types containing elementName (for handling simplified names)
                        var containsMatchType = types.FirstOrDefault(t => 
                            t.Name.Contains(elementName) || 
                            t.FullName.Contains(elementName));
                        if (containsMatchType != null)
                        {
                            return containsMatchType;
                        }
                    }
                    catch
                    {
                        // Ignore inaccessible assemblies
                    }
                }
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to get type {elementName}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Create corresponding uGUI GameObject based on UI Toolkit type inheritance
        /// </summary>
        private GameObject CreateGameObjectByType(UXMLElement element, Transform parent, Type elementType, bool verbose = false)
        {
            try
            {
                // Get UI Toolkit base types - using more reliable method
                var uiElementsTypes = GetUIElementsTypes();

                // Check types by inheritance hierarchy (from most specific to most general)
                if (uiElementsTypes.TryGetValue("Button", out var buttonType) && IsAssignableFrom(buttonType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as Button: {elementType.Name}");
                    return CreateButton(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("TextField", out var textFieldType) && IsAssignableFrom(textFieldType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as TextField: {elementType.Name}");
                    return CreateTextField(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("DropdownField", out var dropdownFieldType) && IsAssignableFrom(dropdownFieldType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as DropdownField: {elementType.Name}");
                    return CreateDropdown(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("Slider", out var sliderType) && IsAssignableFrom(sliderType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as Slider: {elementType.Name}");
                    return CreateSlider(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("Toggle", out var toggleType) && IsAssignableFrom(toggleType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as Toggle: {elementType.Name}");
                    return CreateToggle(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("Label", out var labelType) && IsAssignableFrom(labelType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as Label: {elementType.Name}");
                    return CreateLabel(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("Image", out var imageType) && IsAssignableFrom(imageType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as Image: {elementType.Name}");
                    return CreateImage(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("ScrollView", out var scrollViewType) && IsAssignableFrom(scrollViewType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as ScrollView: {elementType.Name}");
                    return CreateScrollView(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("BaseField", out var baseFieldType) && IsGenericAssignableFrom(baseFieldType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as BaseField: {elementType.Name}");
                    // Handle custom fields inheriting from BaseField<T>
                    return CreateTextField(element, parent);
                }
                else if (uiElementsTypes.TryGetValue("VisualElement", out var visualElementType) && IsAssignableFrom(visualElementType, elementType))
                {
                    if (verbose) Debug.Log($"Detected as VisualElement: {elementType.Name}");
                    // All other types inheriting from VisualElement
                    return CreateVisualElement(element, parent);
                }
                else
                {
                    Debug.Log($"Unrecognized type: {elementType.Name}, using generic element");
                    _result.Warnings.Add($"Unrecognized UI element type: {elementType.Name}, using generic element");
                    return CreateGenericElement(element, parent);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"CreateGameObjectByType failed: {ex.Message}");
                _result.Warnings.Add($"Failed to create GameObject by type {elementType.Name}: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        /// <summary>
        /// Get UI Toolkit basic types dictionary
        /// </summary>
        private Dictionary<string, Type> GetUIElementsTypes()
        {
            var types = new Dictionary<string, Type>();
            
            try
            {
                // Find assemblies containing UI Toolkit types
                var uiElementsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Contains("UnityEngine.UIElementsModule") || 
                                        a.GetName().Name.Contains("UnityEngine.CoreModule"));
                
                if (uiElementsAssembly == null)
                {
                    // If specific assembly not found, search in all assemblies
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            var foundTypes = assembly.GetTypes()
                                .Where(t => t.Namespace == "UnityEngine.UIElements")
                                .ToList();
                            
                            foreach (var type in foundTypes)
                            {
                                if (!types.ContainsKey(type.Name))
                                {
                                    types[type.Name] = type;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore inaccessible assemblies
                        }
                    }
                }
                else
                {
                    // Find UI types in specific assembly
                    var uiTypes = uiElementsAssembly.GetTypes()
                        .Where(t => t.Namespace == "UnityEngine.UIElements")
                        .ToList();
                    
                    foreach (var type in uiTypes)
                    {
                        types[type.Name] = type;
                    }
                }

                // Ensure we have basic UI types
                var requiredTypes = new[] { "VisualElement", "Button", "Label", "TextField", "Image", "ScrollView", "Slider", "Toggle", "DropdownField" };
                foreach (var requiredType in requiredTypes)
                {
                    if (!types.ContainsKey(requiredType))
                    {
                        // Try to get type directly
                        var type = Type.GetType($"UnityEngine.UIElements.{requiredType}, UnityEngine.UIElementsModule");
                        if (type == null)
                        {
                            type = Type.GetType($"UnityEngine.UIElements.{requiredType}, UnityEngine.CoreModule");
                        }
                        if (type == null)
                        {
                            type = Type.GetType($"UnityEngine.UIElements.{requiredType}");
                        }
                        
                        if (type != null)
                        {
                            types[requiredType] = type;
                        }
                    }
                }

                // Add BaseField generic type
                if (!types.ContainsKey("BaseField"))
                {
                    var baseFieldType = Type.GetType("UnityEngine.UIElements.BaseField`1, UnityEngine.UIElementsModule");
                    if (baseFieldType == null)
                    {
                        baseFieldType = Type.GetType("UnityEngine.UIElements.BaseField`1, UnityEngine.CoreModule");
                    }
                    if (baseFieldType == null)
                    {
                        baseFieldType = Type.GetType("UnityEngine.UIElements.BaseField`1");
                    }
                    
                    if (baseFieldType != null)
                    {
                        types["BaseField"] = baseFieldType;
                    }
                }
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to get UI types: {ex.Message}");
            }
            
            return types;
        }

        /// <summary>
        /// Check if type is assignable (similar to IsAssignableFrom, but handles null cases)
        /// </summary>
        private bool IsAssignableFrom(Type baseType, Type derivedType)
        {
            if (baseType == null || derivedType == null)
                return false;
            
            return baseType.IsAssignableFrom(derivedType);
        }

        /// <summary>
        /// Check if generic type is assignable (for generic base classes like BaseField&lt;T&gt;)
        /// </summary>
        private bool IsGenericAssignableFrom(Type genericBaseType, Type derivedType)
        {
            if (genericBaseType == null || derivedType == null)
                return false;

            // Check direct inheritance
            var currentType = derivedType;
            while (currentType != null)
            {
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == genericBaseType)
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }

            // Check interface implementation
            if (genericBaseType.IsInterface)
            {
                var interfaces = derivedType.GetInterfaces();
                foreach (var iface in interfaces)
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericBaseType)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Common method to create base GameObject
        /// </summary>
        private GameObject CreateBaseGameObject(UXMLElement element, Transform parent, string defaultName = "Element")
        {
            var go = new GameObject(GetElementName(element, defaultName));
            go.transform.SetParent(parent, false);
            
            var rectTransform = go.AddComponent<RectTransform>();
            SetupRectTransform(rectTransform, element);
            
            return go;
        }

        /// <summary>
        /// Common method to create Text component
        /// </summary>
        private Text CreateTextComponent(GameObject parent, string text = "", TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var textComponent = parent.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = GetDefaultFont();
            textComponent.fontSize = 14;
            textComponent.color = Color.black;
            textComponent.alignment = alignment;
            
            return textComponent;
        }

        /// <summary>
        /// Common method to create Image component
        /// </summary>
        private Image CreateImageComponent(GameObject parent, Color color = default)
        {
            var image = parent.AddComponent<Image>();
            image.color = color == default ? Color.white : color;
            
            return image;
        }

        /// <summary>
        /// Common method to create child GameObject
        /// </summary>
        private GameObject CreateChildObject(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            
            var rectTransform = child.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            
            return child;
        }

        /// <summary>
        /// Common method to set RectTransform to fill parent
        /// </summary>
        private void SetFillParent(RectTransform rectTransform, float leftPadding = 0, float rightPadding = 0, float topPadding = 0, float bottomPadding = 0)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(leftPadding, bottomPadding);
            rectTransform.offsetMax = new Vector2(-rightPadding, -topPadding);
        }

        private GameObject CreateVisualElement(UXMLElement element, Transform parent)
        {
            var go = CreateBaseGameObject(element, parent);
            
            // Add Image component by default as background (VisualElement usually needs background)
            var image = CreateImageComponent(go, new Color(1, 1, 1, 0));

            return go;
        }

        private GameObject CreateLabel(UXMLElement element, Transform parent)
        {
            var go = CreateBaseGameObject(element, parent, "Label");
            
            var text = CreateTextComponent(go, element.TextContent ?? element.Attributes.GetValueOrDefault("text", "Label"));

            return go;
        }

        private GameObject CreateButton(UXMLElement element, Transform parent)
        {
            try
            {
                var buttonGO = CreateBaseGameObject(element, parent, "Button");
                
                // Add Button component
                var button = buttonGO.AddComponent<Button>();
                
                // Add Image component as background
                var image = CreateImageComponent(buttonGO, new Color(1f, 1f, 1f, 1f));
                
                // Create text child object
                // var textGO = CreateChildObject(buttonGO, "Text", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                // var text = CreateTextComponent(textGO, 
                //     element.TextContent ?? element.Attributes.GetValueOrDefault("text", "Button"), 
                //     TextAnchor.MiddleCenter);
                
                // Set Button's target graphic
                button.targetGraphic = image;
                
                return buttonGO;
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to create button: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        /// <summary>
        /// Get default font (compatible with different Unity versions)
        /// </summary>
        private Font GetDefaultFont()
        {
            // Try to get LegacyRuntime font (recommended for newer Unity versions)
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;
                
            // If not found, try Arial (older Unity versions)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
                return font;
                
            // Finally try to get any available font
            font = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault();
            if (font != null)
                return font;
                
            // If none found, return null (Text component will use default font)
            return null;
        }

        private GameObject CreateTextField(UXMLElement element, Transform parent)
        {
            try
            {
                var inputFieldGO = CreateBaseGameObject(element, parent, "TextField");
                
                // Add Image component as background
                var image = CreateImageComponent(inputFieldGO, Color.white);
                
                // Add InputField component
                var inputField = inputFieldGO.AddComponent<InputField>();
                
                // Create text child object
                var textGO = CreateChildObject(inputFieldGO, "Text", Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -7));
                
                var text = CreateTextComponent(textGO);
                text.supportRichText = false;
                
                // Create placeholder text
                var placeholderGO = CreateChildObject(inputFieldGO, "Placeholder", Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -7));
                
                var placeholder = CreateTextComponent(placeholderGO, element.Attributes.GetValueOrDefault("placeholder-text", "Enter text..."));
                placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                placeholder.supportRichText = false;
                
                // Set InputField references
                inputField.targetGraphic = image;
                inputField.textComponent = text;
                inputField.placeholder = placeholder;
                inputField.text = element.Attributes.GetValueOrDefault("value", "");
                
                return inputFieldGO;
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to create text field: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        private GameObject CreateImage(UXMLElement element, Transform parent)
        {
            try
            {
                var imageGO = CreateBaseGameObject(element, parent, "Image");
                
                // Add Image component
                var image = CreateImageComponent(imageGO, Color.white);
                
                // Try to load image from attributes
                var imageSource = element.Attributes.GetValueOrDefault("src", 
                                 element.Attributes.GetValueOrDefault("source", 
                                 element.Attributes.GetValueOrDefault("image", "")));
                
                if (!string.IsNullOrEmpty(imageSource))
                {
                    var sprite = LoadSpriteFromPath(imageSource);
                    if (sprite != null)
                    {
                        image.sprite = sprite;
                        image.preserveAspect = true;
                    }
                }
                
                return imageGO;
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to create image: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        private GameObject CreateScrollView(UXMLElement element, Transform parent)
        {
            try
            {
                var scrollViewGO = CreateBaseGameObject(element, parent, "ScrollView");
                
                // Add Image component as background
                var image = CreateImageComponent(scrollViewGO, Color.white);
                
                // Add ScrollRect component
                var scrollRect = scrollViewGO.AddComponent<ScrollRect>();
                
                // Create Viewport
                var viewportGO = CreateChildObject(scrollViewGO, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                var viewportMask = viewportGO.AddComponent<Mask>();
                viewportMask.showMaskGraphic = false;
                
                var viewportImage = CreateImageComponent(viewportGO, Color.white);
                
                // Create Content
                var contentGO = CreateChildObject(viewportGO, "Content", 
                    new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                
                var contentRect = contentGO.GetComponent<RectTransform>();
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.sizeDelta = new Vector2(0, 300);
                contentRect.anchoredPosition = Vector2.zero;
                
                // Set ScrollRect references
                scrollRect.content = contentRect;
                scrollRect.viewport = viewportGO.GetComponent<RectTransform>();
                scrollRect.horizontal = true;
                scrollRect.vertical = true;
                
                return scrollViewGO;
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to create scroll view: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        private GameObject CreateSlider(UXMLElement element, Transform parent)
        {
            try
            {
                var sliderGO = CreateBaseGameObject(element, parent, "Slider");
                
                // Add Slider component
                var slider = sliderGO.AddComponent<Slider>();
                
                // Create Background
                var backgroundGO = CreateChildObject(sliderGO, "Background", 
                    new Vector2(0, 0.25f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero);
                
                var backgroundImage = CreateImageComponent(backgroundGO, new Color(1f, 1f, 1f, 0.5f));
                
                // Create Fill Area
                var fillAreaGO = CreateChildObject(sliderGO, "Fill Area", 
                    Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
                
                // Create Fill
                var fillGO = CreateChildObject(fillAreaGO, "Fill", 
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                var fillRect = fillGO.GetComponent<RectTransform>();
                fillRect.sizeDelta = Vector2.zero;
                
                var fillImage = CreateImageComponent(fillGO, Color.blue);
                
                // Create Handle Slide Area
                var handleSlideAreaGO = CreateChildObject(sliderGO, "Handle Slide Area", 
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                var handleSlideAreaRect = handleSlideAreaGO.GetComponent<RectTransform>();
                handleSlideAreaRect.sizeDelta = new Vector2(-20, 0);
                
                // Create Handle
                var handleGO = CreateChildObject(handleSlideAreaGO, "Handle", 
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                var handleRect = handleGO.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(20, 0);
                
                var handleImage = CreateImageComponent(handleGO, Color.white);
                
                // Set Slider references
                slider.fillRect = fillRect;
                slider.handleRect = handleRect;
                slider.targetGraphic = handleImage;
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 0;
                slider.maxValue = 1;
                slider.value = 0.5f;
                
                return sliderGO;
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to create slider: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        private GameObject CreateToggle(UXMLElement element, Transform parent)
        {
            try
            {
                var toggleGO = CreateBaseGameObject(element, parent, "Toggle");
                
                // Add Toggle component
                var toggle = toggleGO.AddComponent<Toggle>();
                
                // Create Background
                var backgroundGO = CreateChildObject(toggleGO, "Background", 
                    new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
                
                var backgroundRect = backgroundGO.GetComponent<RectTransform>();
                backgroundRect.anchoredPosition = new Vector2(10, -10);
                backgroundRect.sizeDelta = new Vector2(20, 20);
                
                var backgroundImage = CreateImageComponent(backgroundGO, Color.white);
                
                // Create Checkmark
                var checkmarkGO = CreateChildObject(backgroundGO, "Checkmark", 
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                var checkmarkImage = CreateImageComponent(checkmarkGO, new Color(0.2f, 0.8f, 0.2f, 1f));
                
                // Create Label
                var labelGO = CreateChildObject(toggleGO, "Label", 
                    Vector2.zero, Vector2.one, new Vector2(23, 1), new Vector2(-5, -2));
                
                var label = CreateTextComponent(labelGO, 
                    element.TextContent ?? element.Attributes.GetValueOrDefault("text", "Toggle"), 
                    TextAnchor.MiddleLeft);
                
                // Set Toggle references
                toggle.targetGraphic = backgroundImage;
                toggle.graphic = checkmarkImage;
                toggle.isOn = element.Attributes.GetValueOrDefault("value", "false").ToLower() == "true";
                
                return toggleGO;
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to create toggle: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        private GameObject CreateDropdown(UXMLElement element, Transform parent)
        {
            try
            {
                var dropdownGO = CreateBaseGameObject(element, parent, "Dropdown");
                var image = CreateImageComponent(dropdownGO, Color.white);
                
                // Add Dropdown component
                var dropdown = dropdownGO.AddComponent<Dropdown>();
                
                // Create Label
                var labelGO = CreateChildObject(dropdownGO, "Label", 
                    Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-25, -7));
                
                var label = CreateTextComponent(labelGO, "", TextAnchor.MiddleLeft);
                
                // Create Arrow
                var arrowGO = CreateChildObject(dropdownGO, "Arrow", 
                    new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
                
                var arrowRect = arrowGO.GetComponent<RectTransform>();
                arrowRect.sizeDelta = new Vector2(20, 20);
                arrowRect.anchoredPosition = new Vector2(-15, 0);
                
                var arrow = CreateImageComponent(arrowGO, Color.black);
                
                // Create Template (dropdown list template)
                var templateGO = CreateChildObject(dropdownGO, "Template", 
                    new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero);
                templateGO.SetActive(false);
                
                var templateRect = templateGO.GetComponent<RectTransform>();
                templateRect.pivot = new Vector2(0.5f, 1);
                templateRect.anchoredPosition = new Vector2(0, 2);
                templateRect.sizeDelta = new Vector2(0, 150);
                
                var templateImage = CreateImageComponent(templateGO, Color.white);
                var templateScrollRect = templateGO.AddComponent<ScrollRect>();
                
                // Create Viewport
                var viewportGO = CreateChildObject(templateGO, "Viewport", 
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                var viewportMask = viewportGO.AddComponent<Mask>();
                viewportMask.showMaskGraphic = false;
                
                var viewportImage = CreateImageComponent(viewportGO, Color.white);
                
                // Create Content
                var contentGO = CreateChildObject(viewportGO, "Content", 
                    new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                
                var contentRect = contentGO.GetComponent<RectTransform>();
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.anchoredPosition = new Vector2(0, 0);
                contentRect.sizeDelta = new Vector2(0, 28);
                
                // Create Item
                var itemGO = CreateChildObject(contentGO, "Item", 
                    Vector2.zero, new Vector2(1, 1), Vector2.zero, Vector2.zero);
                
                var itemToggle = itemGO.AddComponent<Toggle>();
                
                // Create Item Background
                var itemBgGO = CreateChildObject(itemGO, "Item Background", 
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                
                var itemBgImage = CreateImageComponent(itemBgGO, new Color(0.95f, 0.95f, 0.95f, 1f));
                
                // Create Item Label
                var itemLabelGO = CreateChildObject(itemGO, "Item Label", 
                    Vector2.zero, Vector2.one, new Vector2(10, 1), new Vector2(-10, -2));
                
                var itemLabel = CreateTextComponent(itemLabelGO);
                
                // Set references
                templateScrollRect.content = contentRect;
                templateScrollRect.viewport = viewportGO.GetComponent<RectTransform>();
                templateScrollRect.horizontal = false;
                templateScrollRect.vertical = true;
                
                itemToggle.targetGraphic = itemBgImage;
                
                dropdown.targetGraphic = image;
                dropdown.captionText = label;
                dropdown.template = templateRect;
                dropdown.itemText = itemLabel;
                
                // Add default options
                dropdown.options.Add(new Dropdown.OptionData("Option A"));
                dropdown.options.Add(new Dropdown.OptionData("Option B"));
                dropdown.options.Add(new Dropdown.OptionData("Option C"));
                
                dropdown.RefreshShownValue();
                
                return dropdownGO;
            }
            catch (Exception ex)
            {
                _result.Warnings.Add($"Failed to create dropdown: {ex.Message}");
                return CreateGenericElement(element, parent);
            }
        }

        private GameObject CreateGenericElement(UXMLElement element, Transform parent, string name = null)
        {
            var go = CreateBaseGameObject(element, parent, name ?? "Element");

            // Also add Image component for generic elements to display background
            var image = CreateImageComponent(go, new Color(0.8f, 0.8f, 0.8f, 0.5f)); // Semi-transparent gray background

            return go;
        }


        private string GetElementName(UXMLElement element, string defaultName = "Element")
        {
            return element.Attributes.GetValueOrDefault("name", defaultName);
        }

        private void SetupRectTransform(RectTransform rectTransform, UXMLElement element)
        {
            // Set default anchors and size
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.sizeDelta = new Vector2(100, 30);
            
            // Initialize position to zero, specific position will be set later through style system
            rectTransform.anchoredPosition = Vector2.zero;
            
            // Add automatic layout support
            SetupLayoutForElement(rectTransform, element);
        }

        private void SetupLayoutForElement(RectTransform rectTransform, UXMLElement element)
        {
            var parent = rectTransform.parent as RectTransform;
            if (parent == null) return;

            // Check if parent element should use layout group component
            var parentElement = GetElementFromTransform(parent);
            if (parentElement != null && ShouldUseLayoutGroup(parentElement))
            {
                EnsureLayoutGroup(parent, parentElement);
            }
        }

        private bool ShouldUseLayoutGroup(UXMLElement element)
        {
            // Check if there are flex layout properties
            var className = element.Attributes.GetValueOrDefault("class", "");
            if (!string.IsNullOrEmpty(className) && _ussParser != null)
            {
                foreach (var rule in _ussParser.Rules.Values)
                {
                    if (IsStyleMatch(rule.Selector, className, element.Attributes.GetValueOrDefault("name", ""), element.LocalName))
                    {
                        if (rule.Properties.ContainsKey("flex-direction") || 
                            rule.Properties.ContainsKey("justify-content") ||
                            rule.Properties.ContainsKey("align-items"))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void EnsureLayoutGroup(RectTransform parent, UXMLElement parentElement)
        {
            if (parent.GetComponent<LayoutGroup>() != null) return;

            var className = parentElement.Attributes.GetValueOrDefault("class", "");
            var flexDirection = "column"; // Default value
            
            // Get flex-direction from USS
            if (!string.IsNullOrEmpty(className) && _ussParser != null)
            {
                foreach (var rule in _ussParser.Rules.Values)
                {
                    if (IsStyleMatch(rule.Selector, className, parentElement.Attributes.GetValueOrDefault("name", ""), parentElement.LocalName))
                    {
                        if (rule.Properties.TryGetValue("flex-direction", out string direction))
                        {
                            flexDirection = direction;
                            break;
                        }
                    }
                }
            }

            // Add corresponding layout component based on flex-direction
            if (flexDirection == "row")
            {
                var layoutGroup = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }
            else
            {
                var layoutGroup = parent.gameObject.AddComponent<VerticalLayoutGroup>();
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }
        }

        private Dictionary<RectTransform, UXMLElement> _elementMap = new Dictionary<RectTransform, UXMLElement>();

        private UXMLElement GetElementFromTransform(RectTransform rectTransform)
        {
            return _elementMap.GetValueOrDefault(rectTransform);
        }

        private bool HasBackgroundStyle(UXMLElement element)
        {
            // Check if there are background styles
            var className = element.Attributes.GetValueOrDefault("class", "");
            if (!string.IsNullOrEmpty(className) && _ussParser != null)
            {
                foreach (var rule in _ussParser.Rules.Values)
                {
                    if (rule.Selector.Contains(className))
                    {
                        return rule.Properties.ContainsKey("background-color") || 
                               rule.Properties.ContainsKey("background-image");
                    }
                }
            }
            return false;
        }

        private void ApplyStyles(GameObject gameObject, UXMLElement element, bool isRootElement = false)
        {
            // First apply default styles
            ApplyDefaultStyles(gameObject, element, isRootElement);
            
            // Then apply USS styles
            if (_ussParser != null)
            {
                var className = element.Attributes.GetValueOrDefault("class", "");
                var elementName = element.Attributes.GetValueOrDefault("name", "");
                
                // Find matching style rules
                foreach (var rule in _ussParser.Rules.Values)
                {
                    if (IsStyleMatch(rule.Selector, className, elementName, element.LocalName))
                    {
                        ApplyStyleRule(gameObject, rule);
                    }
                }
            }
            
            // Finally apply inline styles (UXML attributes)
            ApplyInlineStyles(gameObject, element);

            // Final check if rectTransform width and height match USS and UXML styles; if not, set to width and height from USS or UXML styles
            DoubleCheckSize(gameObject, element);
        }


        private void DoubleCheckSize(GameObject gameObject, UXMLElement element)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var className = element.Attributes.GetValueOrDefault("class", "");
                var elementName = element.Attributes.GetValueOrDefault("name", "");
                var sizeDelta = rectTransform.sizeDelta;
                bool sizeChanged = false;
                
                // Priority: UXML inline styles > USS styles
                // 1. First check width and height in USS style rules
                if (_ussParser != null)
                {
                    foreach (var rule in _ussParser.Rules.Values)
                    {
                        if (IsStyleMatch(rule.Selector, className, elementName, element.LocalName))
                        {
                            if (rule.Properties.TryGetValue("width", out string ussWidthStr))
                            {
                                // Debug.Log($"[Style Application] Element: {gameObject.name}, USS width: {ussWidthStr}, Current sizeDelta.x: {sizeDelta.x}");
                                var ussWidth = ParseSizeValue(ussWidthStr, rectTransform, true);
                                if (ussWidth.HasValue && Math.Abs(sizeDelta.x - ussWidth.Value) > 0.1f)
                                {
                                    sizeDelta.x = ussWidth.Value;
                                    sizeChanged = true;
                                    // Debug.Log($"[Style Application] Applied USS width: {ussWidth.Value}");
                                }
                            }
                            
                            if (rule.Properties.TryGetValue("height", out string ussHeightStr))
                            {
                                // Debug.Log($"[Style Application] Element: {gameObject.name}, USS height: {ussHeightStr}, Current sizeDelta.y: {sizeDelta.y}");
                                var ussHeight = ParseSizeValue(ussHeightStr, rectTransform, false);
                                if (ussHeight.HasValue && Math.Abs(sizeDelta.y - ussHeight.Value) > 0.1f)
                                {
                                    sizeDelta.y = ussHeight.Value;
                                    sizeChanged = true;
                                    // Debug.Log($"[Style Application] Applied USS height: {ussHeight.Value}");
                                }
                            }
                        }
                    }
                }
                
                // 2. Then check UXML inline styles (higher priority, will override USS styles)
                // 2.1 Check direct width and height attributes
                if (element.Attributes.TryGetValue("width", out string uxmlWidthStr))
                {
                    // Debug.Log($"[Style Application] Element: {gameObject.name}, UXML width attribute: {uxmlWidthStr}, Current sizeDelta.x: {sizeDelta.x}");
                    var uxmlWidth = ParseSizeValue(uxmlWidthStr, rectTransform, true);
                    if (uxmlWidth.HasValue && Math.Abs(sizeDelta.x - uxmlWidth.Value) > 0.1f)
                    {
                        sizeDelta.x = uxmlWidth.Value;
                        sizeChanged = true;
                        // Debug.Log($"[Style Application] Applied UXML width attribute: {uxmlWidth.Value}");
                    }
                }
                
                if (element.Attributes.TryGetValue("height", out string uxmlHeightStr))
                {
                    // Debug.Log($"[Style Application] Element: {gameObject.name}, UXML height attribute: {uxmlHeightStr}, Current sizeDelta.y: {sizeDelta.y}");
                    var uxmlHeight = ParseSizeValue(uxmlHeightStr, rectTransform, false);
                    if (uxmlHeight.HasValue && Math.Abs(sizeDelta.y - uxmlHeight.Value) > 0.1f)
                    {
                        sizeDelta.y = uxmlHeight.Value;
                        sizeChanged = true;
                        // Debug.Log($"[Style Application] Applied UXML height attribute: {uxmlHeight.Value}");
                    }
                }
                
                // 2.2 Check width and height in style attribute
                if (element.Attributes.TryGetValue("style", out string styleStr) && !string.IsNullOrEmpty(styleStr))
                {
                    // Debug.Log($"[Style Application] Element: {gameObject.name}, UXML style attribute: {styleStr}");
                    
                    // Parse CSS styles in style attribute
                    var styleProperties = ParseInlineStyleProperties(styleStr);
                    
                    if (styleProperties.TryGetValue("width", out string styleWidthStr))
                    {
                        // Debug.Log($"[Style Application] Element: {gameObject.name}, width in style: {styleWidthStr}, Current sizeDelta.x: {sizeDelta.x}");
                        var styleWidth = ParseSizeValue(styleWidthStr, rectTransform, true);
                        if (styleWidth.HasValue && Math.Abs(sizeDelta.x - styleWidth.Value) > 0.1f)
                        {
                            sizeDelta.x = styleWidth.Value;
                            sizeChanged = true;
                            // Debug.Log($"[Style Application] Applied width in style: {styleWidth.Value}");
                        }
                    }
                    
                    if (styleProperties.TryGetValue("height", out string styleHeightStr))
                    {
                        // Debug.Log($"[Style Application] Element: {gameObject.name}, height in style: {styleHeightStr}, Current sizeDelta.y: {sizeDelta.y}");
                        var styleHeight = ParseSizeValue(styleHeightStr, rectTransform, false);
                        if (styleHeight.HasValue && Math.Abs(sizeDelta.y - styleHeight.Value) > 0.1f)
                        {
                            sizeDelta.y = styleHeight.Value;
                            sizeChanged = true;
                        }
                    }
                }
                
                // Apply final size changes
                if (sizeChanged)
                {
                    rectTransform.sizeDelta = sizeDelta;
                }
            }
        }


        private void ApplyDefaultStyles(GameObject gameObject, UXMLElement element, bool isRootElement = false)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform != null && !isRootElement)
            {
                // Set default size (root Element size is already set in ConvertElement method, no need to override)
                rectTransform.sizeDelta = new Vector2(100, 30);
            }

            // Set default styles for Text component
            var text = gameObject.GetComponent<Text>();
            if (text != null)
            {
                text.font = GetDefaultFont();
                text.fontSize = 14;
                text.color = Color.black;
                text.alignment = TextAnchor.MiddleLeft;
            }

            // Set default styles for Image component
            var image = gameObject.GetComponent<Image>();
            if (image != null)
            {
                // Only set default color when there's no sprite
                if (image.sprite == null)
                {
                    // Check if it's an Image element, if so keep transparent, otherwise set semi-transparent background
                    if (element.LocalName?.ToLower() == "image")
                    {
                        image.color = new Color(1, 1, 1, 0); // Image element default transparent
                    }
                    else
                    {
                        image.color = new Color(1, 1, 1, 0); // Other elements also default transparent, waiting for style setup
                    }
                }
                else
                {
                    // If sprite already exists, ensure color is visible
                    if (image.color.a == 0)
                    {
                        image.color = Color.white;
                    }
                }
            }
        }

        private void ApplyInlineStyles(GameObject gameObject, UXMLElement element)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            var text = gameObject.GetComponent<Text>();
            var image = gameObject.GetComponent<Image>();

            // Process inline style attributes
            foreach (var attr in element.Attributes)
            {
                switch (attr.Key.ToLower())
                {
                    case "text":
                        if (text != null)
                            text.text = attr.Value;
                        break;
                    case "placeholder-text":
                        var inputField = gameObject.GetComponent<InputField>();
                        if (inputField != null && inputField.placeholder is Text placeholder)
                            placeholder.text = attr.Value;
                        break;
                    case "style":
                        // Process inline style string
                        ApplyInlineStyleString(gameObject, attr.Value);
                        break;
                    // Process image attributes in UXML
                    case "src":
                    case "source":
                    case "image":
                        if (image == null)
                        {
                            image = gameObject.AddComponent<Image>();
                        }
                        var sprite = LoadSpriteFromPath(attr.Value);
                        if (sprite != null)
                        {
                            image.sprite = sprite;
                            // Ensure image is visible
                            if (image.color.a == 0)
                            {
                                image.color = Color.white; // Set to visible white
                            }
                            _result.Warnings.Add($"Successfully loaded image: {attr.Value}");
                        }
                        else
                        {
                            _result.Warnings.Add($"Failed to load image: {attr.Value}");
                        }
                        break;
                    case "background-image":
                        if (image == null)
                        {
                            image = gameObject.AddComponent<Image>();
                        }
                        var bgSprite = LoadSpriteFromPath(attr.Value);
                        if (bgSprite != null)
                        {
                            image.sprite = bgSprite;
                            // Ensure background image is visible
                            if (image.color.a == 0)
                            {
                                image.color = Color.white; // Set to visible white
                            }
                            _result.Warnings.Add($"Successfully loaded background image: {attr.Value}");
                        }
                        else
                        {
                            _result.Warnings.Add($"Failed to load background image: {attr.Value}");
                        }
                        break;
                    case "tint-color":
                        if (image != null && TryParseColor(attr.Value, out Color tintColor))
                        {
                            image.color = tintColor;
                        }
                        break;
                    case "image-tint-color":
                        if (image != null && TryParseColor(attr.Value, out Color imageTintColor))
                        {
                            image.color = imageTintColor;
                        }
                        break;
                    // Process size attributes in UXML
                    case "width":
                        if (rectTransform != null)
                        {
                            var size = ParseSizeValue(attr.Value, rectTransform, true);
                            if (size.HasValue)
                            {
                                var sizeDelta = rectTransform.sizeDelta;
                                sizeDelta.x = size.Value;
                                rectTransform.sizeDelta = sizeDelta;
                            }
                        }
                        break;
                    case "height":
                        if (rectTransform != null)
                        {
                            var size = ParseSizeValue(attr.Value, rectTransform, false);
                            if (size.HasValue)
                            {
                                var sizeDelta = rectTransform.sizeDelta;
                                sizeDelta.y = size.Value;
                                rectTransform.sizeDelta = sizeDelta;
                            }
                        }
                        break;
                }
            }
        }

        private void ApplyInlineStyleString(GameObject gameObject, string styleString)
        {
            if (string.IsNullOrEmpty(styleString)) return;

            // Parse inline style string (style="width: 100px; height: 50px;")
            var properties = styleString.Split(';');
            foreach (var prop in properties)
            {
                var parts = prop.Split(':');
                if (parts.Length == 2)
                {
                    var propertyName = parts[0].Trim();
                    var propertyValue = parts[1].Trim();
                    
                    // Create temporary rule to apply styles
                    var tempRule = new USSRule();
                    tempRule.Properties[propertyName] = propertyValue;
                    ApplyStyleRule(gameObject, tempRule);
                }
            }
        }

        /// <summary>
        /// Parse inline style string and return property dictionary
        /// </summary>
        /// <param name="styleString">Inline style string, e.g. "width: 100px; height: 50px;"</param>
        /// <returns>Dictionary containing style properties</returns>
        private Dictionary<string, string> ParseInlineStyleProperties(string styleString)
        {
            var result = new Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(styleString)) return result;

            // Parse inline style string (style="width: 100px; height: 50px;")
            var properties = styleString.Split(';');
            foreach (var prop in properties)
            {
                var parts = prop.Split(':');
                if (parts.Length == 2)
                {
                    var propertyName = parts[0].Trim().ToLower();
                    var propertyValue = parts[1].Trim();
                    
                    if (!string.IsNullOrEmpty(propertyName) && !string.IsNullOrEmpty(propertyValue))
                    {
                        result[propertyName] = propertyValue;
                    }
                }
            }
            
            return result;
        }

        private float? ParseSizeValue(string value, RectTransform rectTransform, bool isWidth)
        {
            if (string.IsNullOrEmpty(value)) return null;

            value = value.Trim().ToLower();

            // Handle auto
            if (value == "auto")
            {
                return GetAutoSize(rectTransform, isWidth);
            }

            // Handle percentage
            if (value.EndsWith("%"))
            {
                if (float.TryParse(value.Substring(0, value.Length - 1), out float percentage))
                {
                    var parent = rectTransform.parent as RectTransform;
                    if (parent != null)
                    {
                        var parentSize = isWidth ? parent.rect.width : parent.rect.height;
                        return parentSize * (percentage / 100f);
                    }
                }
                return null;
            }

            // Handle various units
            if (value.EndsWith("px"))
            {
                value = value.Substring(0, value.Length - 2);
            }
            else if (value.EndsWith("em"))
            {
                // 1em = current font size
                var fontSize = GetCurrentFontSize(rectTransform);
                if (float.TryParse(value.Substring(0, value.Length - 2), out float emValue))
                {
                    return emValue * fontSize;
                }
                return null;
            }
            else if (value.EndsWith("rem"))
            {
                // 1rem = root font size, default 16px
                if (float.TryParse(value.Substring(0, value.Length - 3), out float remValue))
                {
                    return remValue * 16f;
                }
                return null;
            }
            else if (value.EndsWith("vw"))
            {
                // 1vw = 1% of viewport width
                if (float.TryParse(value.Substring(0, value.Length - 2), out float vwValue))
                {
                    var canvas = rectTransform.GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        return (canvas.pixelRect.width * vwValue) / 100f;
                    }
                }
                return null;
            }
            else if (value.EndsWith("vh"))
            {
                // 1vh = 1% of viewport height
                if (float.TryParse(value.Substring(0, value.Length - 2), out float vhValue))
                {
                    var canvas = rectTransform.GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        return (canvas.pixelRect.height * vhValue) / 100f;
                    }
                }
                return null;
            }

            // Default parse as pixel value
            if (float.TryParse(value, out float pixelValue))
            {
                return pixelValue;
            }

            return null;
        }

        private float GetAutoSize(RectTransform rectTransform, bool isWidth)
        {
            // For auto size, try to calculate the preferred size of content
            var layoutElement = rectTransform.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                return isWidth ? layoutElement.preferredWidth : layoutElement.preferredHeight;
            }

            // For Text component, calculate the preferred size of text
            var text = rectTransform.GetComponent<Text>();
            if (text != null)
            {
                var textGenerator = new TextGenerator();
                var generationSettings = text.GetGenerationSettings(rectTransform.rect.size);
                
                if (isWidth)
                {
                    generationSettings.generationExtents = new Vector2(float.MaxValue, generationSettings.generationExtents.y);
                    return textGenerator.GetPreferredWidth(text.text, generationSettings);
                }
                else
                {
                    return textGenerator.GetPreferredHeight(text.text, generationSettings);
                }
            }

            // Default return current size
            return isWidth ? rectTransform.sizeDelta.x : rectTransform.sizeDelta.y;
        }

        private float GetCurrentFontSize(RectTransform rectTransform)
        {
            var text = rectTransform.GetComponent<Text>();
            if (text != null)
            {
                return text.fontSize;
            }
            return 14f; // Default font size
        }

        private bool IsStyleMatch(string selector, string className, string elementName, string tagName)
        {
            if (string.IsNullOrEmpty(selector)) return false;

            // Handle class selector (.className)
            if (selector.StartsWith(".") && !string.IsNullOrEmpty(className))
            {
                var selectorClass = selector.Substring(1).Trim();
                return className.Split(' ').Any(c => c == selectorClass);
            }

            // Handle ID selector (#elementName)
            if (selector.StartsWith("#") && !string.IsNullOrEmpty(elementName))
            {
                var selectorId = selector.Substring(1).Trim();
                return elementName == selectorId;
            }

            // Handle tag selector (tagName)
            if (!selector.StartsWith(".") && !selector.StartsWith("#"))
            {
                return selector.Trim().Equals(tagName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void ApplyStyleRule(GameObject gameObject, USSRule rule)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            var image = gameObject.GetComponent<Image>();
            var text = gameObject.GetComponent<Text>();

            foreach (var property in rule.Properties)
            {
                // Debug.Log($"[Style Application] Element: {gameObject.name}, Property: {property.Key}, Value: {property.Value}");
                switch (property.Key.ToLower())
                {
                    case "width":
                        if (rectTransform != null)
                        {
                            var width = ParseSizeValue(property.Value, rectTransform, true);
                            if (width.HasValue)
                            {
                                var sizeDelta = rectTransform.sizeDelta;
                                sizeDelta.x = width.Value;
                                rectTransform.sizeDelta = sizeDelta;
                            }
                        }
                        break;
                    case "height":
                        if (rectTransform != null)
                        {
                            var height = ParseSizeValue(property.Value, rectTransform, false);
                            if (height.HasValue)
                            {
                                var sizeDelta = rectTransform.sizeDelta;
                                sizeDelta.y = height.Value;
                                rectTransform.sizeDelta = sizeDelta;
                            }
                        }
                        break;
                    case "min-width":
                        if (rectTransform != null)
                        {
                            var minWidth = ParseSizeValue(property.Value, rectTransform, true);
                            if (minWidth.HasValue)
                            {
                                var layoutElement = rectTransform.GetComponent<LayoutElement>();
                                if (layoutElement == null)
                                    layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
                                layoutElement.minWidth = minWidth.Value;
                            }
                        }
                        break;
                    case "min-height":
                        if (rectTransform != null)
                        {
                            var minHeight = ParseSizeValue(property.Value, rectTransform, false);
                            if (minHeight.HasValue)
                            {
                                var layoutElement = rectTransform.GetComponent<LayoutElement>();
                                if (layoutElement == null)
                                    layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
                                layoutElement.minHeight = minHeight.Value;
                            }
                        }
                        break;
                    case "max-width":
                        if (rectTransform != null)
                        {
                            var maxWidth = ParseSizeValue(property.Value, rectTransform, true);
                            if (maxWidth.HasValue)
                            {
                                var layoutElement = rectTransform.GetComponent<LayoutElement>();
                                if (layoutElement == null)
                                    layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
                                layoutElement.preferredWidth = maxWidth.Value;
                            }
                        }
                        break;
                    case "max-height":
                        if (rectTransform != null)
                        {
                            var maxHeight = ParseSizeValue(property.Value, rectTransform, false);
                            if (maxHeight.HasValue)
                            {
                                var layoutElement = rectTransform.GetComponent<LayoutElement>();
                                if (layoutElement == null)
                                    layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
                                layoutElement.preferredHeight = maxHeight.Value;
                            }
                        }
                        break;
                    case "flex-basis":
                        if (rectTransform != null)
                        {
                            var flexBasis = ParseSizeValue(property.Value, rectTransform, true);
                            if (flexBasis.HasValue)
                            {
                                var layoutElement = rectTransform.GetComponent<LayoutElement>();
                                if (layoutElement == null)
                                    layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
                                layoutElement.flexibleWidth = flexBasis.Value;
                            }
                        }
                        break;
                    case "flex-grow":
                        if (rectTransform != null && float.TryParse(property.Value, out float flexGrow))
                        {
                            var layoutElement = rectTransform.GetComponent<LayoutElement>();
                            if (layoutElement == null)
                                layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
                            layoutElement.flexibleWidth = flexGrow;
                        }
                        break;
                    case "flex-shrink":
                        if (rectTransform != null && float.TryParse(property.Value, out float flexShrink))
                        {
                            var layoutElement = rectTransform.GetComponent<LayoutElement>();
                            if (layoutElement == null)
                                layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
                            // Unity doesn't have direct shrink property, simulate with preferredWidth
                            if (flexShrink < 1f)
                            {
                                layoutElement.preferredWidth = rectTransform.sizeDelta.x * flexShrink;
                            }
                        }
                        break;
                    case "box-sizing":
                        // Handle box model, border-box vs content-box
                        if (rectTransform != null)
                        {
                            var contentSizeFitter = rectTransform.GetComponent<ContentSizeFitter>();
                            if (property.Value.ToLower() == "content-box")
                            {
                                if (contentSizeFitter == null)
                                    contentSizeFitter = rectTransform.gameObject.AddComponent<ContentSizeFitter>();
                                contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                                contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                            }
                        }
                        break;
                    case "left":
                        if (rectTransform != null && TryParseFloat(property.Value, out float left))
                        {
                            // For left property, ensure anchor and pivot are set correctly
                            rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
                            rectTransform.anchorMax = new Vector2(0, rectTransform.anchorMax.y);
                            // Set pivot.x = 0, use left edge as positioning reference
                            var pivot = rectTransform.pivot;
                            pivot.x = 0;
                            rectTransform.pivot = pivot;
                            // Set position so left edge is 'left' pixels from parent's left edge
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.x = left;
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "top":
                        if (rectTransform != null && TryParseFloat(property.Value, out float top))
                        {
                            // For top property, ensure anchor and pivot are set correctly
                            rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 1);
                            rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
                            // Set pivot.y = 1, use top edge as positioning reference
                            var pivot = rectTransform.pivot;
                            pivot.y = 1;
                            rectTransform.pivot = pivot;
                            // Set position so top edge is 'top' pixels from parent's top edge
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.y = -top; // Unity UI Y-axis is upward positive, CSS is downward positive
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "right":
                        if (rectTransform != null && TryParseFloat(property.Value, out float right))
                        {
                            // For right property, need to change anchor and pivot
                            rectTransform.anchorMin = new Vector2(1, rectTransform.anchorMin.y);
                            rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
                            // Set pivot.x = 1, use right edge as positioning reference
                            var pivot = rectTransform.pivot;
                            pivot.x = 1;
                            rectTransform.pivot = pivot;
                            // Set position so right edge is 'right' pixels from parent's right edge
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.x = -right;
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "bottom":
                        if (rectTransform != null && TryParseFloat(property.Value, out float bottom))
                        {
                            // For bottom property, need to change anchor and pivot
                            rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
                            rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 0);
                            // Set pivot.y = 0, use bottom edge as positioning reference
                            var pivot = rectTransform.pivot;
                            pivot.y = 0;
                            rectTransform.pivot = pivot;
                            // Set position so bottom edge is 'bottom' pixels from parent's bottom edge
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.y = bottom;
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "position":
                        if (rectTransform != null && property.Value.ToLower() == "absolute")
                        {
                            // Absolute positioning: set to stretch mode
                            rectTransform.anchorMin = Vector2.zero;
                            rectTransform.anchorMax = Vector2.one;
                            rectTransform.offsetMin = Vector2.zero;
                            rectTransform.offsetMax = Vector2.zero;
                        }
                        break;
                    case "margin":
                        ApplyMargin(rectTransform, property.Value);
                        break;
                    case "margin-left":
                        if (rectTransform != null && TryParseFloat(property.Value, out float marginLeft))
                        {
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.x += marginLeft;
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "margin-top":
                        if (rectTransform != null && TryParseFloat(property.Value, out float marginTop))
                        {
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.y -= marginTop;
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "margin-right":
                        if (rectTransform != null && TryParseFloat(property.Value, out float marginRight))
                        {
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.x -= marginRight;
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "margin-bottom":
                        if (rectTransform != null && TryParseFloat(property.Value, out float marginBottom))
                        {
                            var anchoredPosition = rectTransform.anchoredPosition;
                            anchoredPosition.y += marginBottom;
                            rectTransform.anchoredPosition = anchoredPosition;
                        }
                        break;
                    case "padding":
                        ApplyPadding(rectTransform, property.Value);
                        break;
                    case "flex-direction":
                        // This property is handled in EnsureLayoutGroup
                        break;
                    case "justify-content":
                        ApplyJustifyContent(rectTransform, property.Value);
                        break;
                    case "align-items":
                        ApplyAlignItems(rectTransform, property.Value);
                        break;
                    case "align-self":
                        ApplyAlignSelf(rectTransform, property.Value);
                        break;
                    case "background-color":
                        // Ensure Image component exists
                        if (image == null)
                        {
                            image = gameObject.AddComponent<Image>();
                        }
                        if (TryParseColor(property.Value, out Color bgColor))
                        {
                            image.color = bgColor;
                        }
                        break;
                    case "color":
                        if (text != null && TryParseColor(property.Value, out Color textColor))
                        {
                            text.color = textColor;
                        }
                        break;
                    case "font-size":
                        if (text != null)
                        {
                            var fontSize = ParseSizeValue(property.Value, rectTransform, true);
                            if (fontSize.HasValue)
                            {
                                text.fontSize = Mathf.Max(1, (int)fontSize.Value);
                            }
                        }
                        break;
                    case "font-weight":
                        if (text != null)
                        {
                            switch (property.Value.ToLower())
                            {
                                case "bold":
                                case "700":
                                case "800":
                                case "900":
                                    text.fontStyle = FontStyle.Bold;
                                    break;
                                case "normal":
                                case "400":
                                    text.fontStyle = FontStyle.Normal;
                                    break;
                            }
                        }
                        break;
                    case "font-style":
                        if (text != null)
                        {
                            switch (property.Value.ToLower())
                            {
                                case "italic":
                                    text.fontStyle = FontStyle.Italic;
                                    break;
                                case "normal":
                                    text.fontStyle = FontStyle.Normal;
                                    break;
                            }
                        }
                        break;
                    case "background-image":
                        // Ensure Image component exists
                        if (image == null)
                        {
                            image = gameObject.AddComponent<Image>();
                        }
                        // Try to load image resource
                        var sprite = LoadSpriteFromPath(property.Value);
                        if (sprite != null)
                        {
                            image.sprite = sprite;
                            // Ensure background image is visible
                            if (image.color.a == 0)
                            {
                                image.color = Color.white; // Set to visible white
                            }
                        }
                        else
                        {
                            _result.Warnings.Add($"Failed to load background image: {property.Value}");
                        }
                        break;
                    case "background-size":
                        // Handle background image scaling mode
                        if (image != null)
                        {
                            switch (property.Value.ToLower())
                            {
                                case "cover":
                                    image.type = Image.Type.Filled;
                                    image.preserveAspect = true;
                                    break;
                                case "contain":
                                    image.type = Image.Type.Simple;
                                    image.preserveAspect = true;
                                    break;
                                case "stretch":
                                    image.type = Image.Type.Simple;
                                    image.preserveAspect = false;
                                    break;
                            }
                        }
                        break;
                    case "background-repeat":
                        // Handle background image repeat mode
                        if (image != null)
                        {
                            switch (property.Value.ToLower())
                            {
                                case "repeat":
                                    image.type = Image.Type.Tiled;
                                    break;
                                case "no-repeat":
                                    image.type = Image.Type.Simple;
                                    break;
                            }
                        }
                        break;
                    case "image-tint-color":
                    case "tint-color":
                        if (image != null && TryParseColor(property.Value, out Color tintColor))
                        {
                            image.color = tintColor;
                        }
                        break;
                    case "border-radius":
                        // For rounded corners, consider using special images or shaders
                        break;
                    case "opacity":
                        if (TryParseFloat(property.Value, out float opacity))
                        {
                            if (image != null)
                            {
                                var color = image.color;
                                color.a = opacity;
                                image.color = color;
                            }
                            if (text != null)
                            {
                                var color = text.color;
                                color.a = opacity;
                                text.color = color;
                            }
                        }
                        break;
                    case "text-align":
                        if (text != null)
                        {
                            switch (property.Value.ToLower())
                            {
                                case "left":
                                    text.alignment = TextAnchor.MiddleLeft;
                                    break;
                                case "center":
                                    text.alignment = TextAnchor.MiddleCenter;
                                    break;
                                case "right":
                                    text.alignment = TextAnchor.MiddleRight;
                                    break;
                            }
                        }
                        break;
                    case "white-space":
                        if (text != null)
                        {
                            switch (property.Value.ToLower())
                            {
                                case "nowrap":
                                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                                    break;
                                case "pre":
                                case "pre-wrap":
                                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                                    break;
                            }
                        }
                        break;
                    case "overflow":
                        if (property.Value.ToLower() == "hidden")
                        {
                            // Add Mask component to implement overflow: hidden
                            if (image == null)
                            {
                                image = gameObject.AddComponent<Image>();
                            }
                            var mask = gameObject.AddComponent<Mask>();
                            mask.showMaskGraphic = false;
                        }
                        break;
                }
            }
        }

        private void ApplyMargin(RectTransform rectTransform, string value)
        {
            if (rectTransform == null) return;

            var margins = ParseSpacingValue(value);
            if (margins.Length > 0)
            {
                var position = rectTransform.anchoredPosition;
                position.x += margins[3]; // left
                position.y -= margins[0]; // top (CSS downward positive, Unity upward positive)
                rectTransform.anchoredPosition = position;
            }
        }

        private void ApplyPadding(RectTransform rectTransform, string value)
        {
            if (rectTransform == null) return;

            var paddings = ParseSpacingValue(value);
            if (paddings.Length > 0)
            {
                // For padding, we need to adjust the content area
                var offsetMin = rectTransform.offsetMin;
                var offsetMax = rectTransform.offsetMax;
                
                offsetMin.x += paddings[3]; // left
                offsetMin.y += paddings[2]; // bottom
                offsetMax.x -= paddings[1]; // right
                offsetMax.y -= paddings[0]; // top
                
                rectTransform.offsetMin = offsetMin;
                rectTransform.offsetMax = offsetMax;
            }
        }

        private void ApplyJustifyContent(RectTransform rectTransform, string value)
        {
            var layoutGroup = rectTransform.GetComponent<LayoutGroup>();
            if (layoutGroup == null) return;

            switch (value.ToLower())
            {
                case "flex-start":
                    if (layoutGroup is HorizontalLayoutGroup hlg)
                        hlg.childAlignment = TextAnchor.MiddleLeft;
                    else if (layoutGroup is VerticalLayoutGroup vlg)
                        vlg.childAlignment = TextAnchor.UpperCenter;
                    break;
                case "center":
                    layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                    break;
                case "flex-end":
                    if (layoutGroup is HorizontalLayoutGroup hlg2)
                        hlg2.childAlignment = TextAnchor.MiddleRight;
                    else if (layoutGroup is VerticalLayoutGroup vlg2)
                        vlg2.childAlignment = TextAnchor.LowerCenter;
                    break;
                case "space-between":
                    // Unity's layout components don't directly support space-between, need custom implementation
                    break;
            }
        }

        private void ApplyAlignItems(RectTransform rectTransform, string value)
        {
            var layoutGroup = rectTransform.GetComponent<LayoutGroup>();
            if (layoutGroup == null) return;

            switch (value.ToLower())
            {
                case "flex-start":
                    if (layoutGroup is HorizontalLayoutGroup hlg)
                        hlg.childAlignment = TextAnchor.UpperLeft;
                    else if (layoutGroup is VerticalLayoutGroup vlg)
                        vlg.childAlignment = TextAnchor.UpperLeft;
                    break;
                case "center":
                    layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                    break;
                case "flex-end":
                    if (layoutGroup is HorizontalLayoutGroup hlg2)
                        hlg2.childAlignment = TextAnchor.LowerRight;
                    else if (layoutGroup is VerticalLayoutGroup vlg2)
                        vlg2.childAlignment = TextAnchor.LowerRight;
                    break;
                case "stretch":
                    if (layoutGroup is HorizontalLayoutGroup hlg3)
                    {
                        hlg3.childControlHeight = true;
                        hlg3.childForceExpandHeight = true;
                    }
                    else if (layoutGroup is VerticalLayoutGroup vlg3)
                    {
                        vlg3.childControlWidth = true;
                        vlg3.childForceExpandWidth = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply align-self style - controls alignment of individual elements on the cross axis
        /// </summary>
        private void ApplyAlignSelf(RectTransform rectTransform, string value)
        {
            if (rectTransform == null) return;
            
            var parent = rectTransform.parent as RectTransform;
            if (parent == null) return;
            
            var parentLayoutGroup = parent.GetComponent<LayoutGroup>();
            bool isInLayoutGroup = parentLayoutGroup != null;
            bool isHorizontalLayout = parentLayoutGroup is HorizontalLayoutGroup;
            bool isVerticalLayout = parentLayoutGroup is VerticalLayoutGroup;
            
            // Ensure LayoutElement component exists to control layout behavior
            var layoutElement = rectTransform.GetComponent<LayoutElement>();
            if (layoutElement == null && isInLayoutGroup)
            {
                layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
            }

            switch (value.ToLower())
            {
                case "auto":
                    // Use parent container's align-items setting
                    if (layoutElement != null)
                    {
                        layoutElement.ignoreLayout = false;
                    }
                    break;
                    
                case "flex-start":
                    if (isInLayoutGroup)
                    {
                        if (isHorizontalLayout)
                        {
                            // In horizontal layout, flex-start means top alignment
                            SetElementAlignmentInHorizontalLayout(rectTransform, layoutElement, TextAnchor.UpperCenter);
                        }
                        else if (isVerticalLayout)
                        {
                            // In vertical layout, flex-start means left alignment
                            SetElementAlignmentInVerticalLayout(rectTransform, layoutElement, TextAnchor.MiddleLeft);
                        }
                    }
                    else
                    {
                        // Not in layout group, directly set anchor to top-left corner
                        rectTransform.anchorMin = new Vector2(0, 1);
                        rectTransform.anchorMax = new Vector2(0, 1);
                        rectTransform.pivot = new Vector2(0, 1);
                    }
                    break;
                    
                case "center":
                    if (isInLayoutGroup)
                    {
                        if (isHorizontalLayout)
                        {
                            // In horizontal layout, center means vertical centering
                            SetElementAlignmentInHorizontalLayout(rectTransform, layoutElement, TextAnchor.MiddleCenter);
                        }
                        else if (isVerticalLayout)
                        {
                            // In vertical layout, center means horizontal centering
                            SetElementAlignmentInVerticalLayout(rectTransform, layoutElement, TextAnchor.MiddleCenter);
                        }
                    }
                    else
                    {
                        // Not in layout group, set to completely centered
                        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                    break;
                    
                case "flex-end":
                    if (isInLayoutGroup)
                    {
                        if (isHorizontalLayout)
                        {
                            // In horizontal layout, flex-end means bottom alignment
                            SetElementAlignmentInHorizontalLayout(rectTransform, layoutElement, TextAnchor.LowerCenter);
                        }
                        else if (isVerticalLayout)
                        {
                            // In vertical layout, flex-end means right alignment
                            SetElementAlignmentInVerticalLayout(rectTransform, layoutElement, TextAnchor.MiddleRight);
                        }
                    }
                    else
                    {
                        // Not in layout group, set anchor to bottom-right corner
                        rectTransform.anchorMin = new Vector2(1, 0);
                        rectTransform.anchorMax = new Vector2(1, 0);
                        rectTransform.pivot = new Vector2(1, 0);
                    }
                    break;
                    
                case "stretch":
                    if (isInLayoutGroup)
                    {
                        if (isHorizontalLayout)
                        {
                            // In horizontal layout, stretch means vertical stretching
                            if (layoutElement != null)
                            {
                                layoutElement.minHeight = -1;
                                layoutElement.preferredHeight = -1;
                                layoutElement.flexibleHeight = 1;
                            }
                            // Set vertical stretching
                            rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
                            rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
                        }
                        else if (isVerticalLayout)
                        {
                            // In vertical layout, stretch means horizontal stretching
                            if (layoutElement != null)
                            {
                                layoutElement.minWidth = -1;
                                layoutElement.preferredWidth = -1;
                                layoutElement.flexibleWidth = 1;
                            }
                            // Set horizontal stretching
                            rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
                            rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
                        }
                    }
                    else
                    {
                        // Not in layout group, set to full stretch
                        SetFillParent(rectTransform);
                    }
                    break;
                    
                case "baseline":
                    // Baseline alignment is mainly for text, can be simulated as center alignment in Unity
                    ApplyAlignSelf(rectTransform, "center");
                    break;
            }
        }

        /// <summary>
        /// Set element alignment in horizontal layout group
        /// </summary>
        private void SetElementAlignmentInHorizontalLayout(RectTransform rectTransform, LayoutElement layoutElement, TextAnchor alignment)
        {
            if (layoutElement != null)
            {
                // Disable height control, allow elements to position freely
                layoutElement.ignoreLayout = false;
            }
            
            // Set vertical anchor based on alignment
            switch (alignment)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.UpperLeft:
                case TextAnchor.UpperRight:
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 1);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
                    rectTransform.pivot = new Vector2(rectTransform.pivot.x, 1);
                    break;
                case TextAnchor.MiddleCenter:
                case TextAnchor.MiddleLeft:
                case TextAnchor.MiddleRight:
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0.5f);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 0.5f);
                    rectTransform.pivot = new Vector2(rectTransform.pivot.x, 0.5f);
                    break;
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerRight:
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 0);
                    rectTransform.pivot = new Vector2(rectTransform.pivot.x, 0);
                    break;
            }
        }

        /// <summary>
        /// Set element alignment in vertical layout group
        /// </summary>
        private void SetElementAlignmentInVerticalLayout(RectTransform rectTransform, LayoutElement layoutElement, TextAnchor alignment)
        {
            if (layoutElement != null)
            {
                // Disable width control, allow elements to position freely
                layoutElement.ignoreLayout = false;
            }
            
            // Set horizontal anchor based on alignment
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.MiddleLeft:
                case TextAnchor.LowerLeft:
                    rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(0, rectTransform.anchorMax.y);
                    rectTransform.pivot = new Vector2(0, rectTransform.pivot.y);
                    break;
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    rectTransform.anchorMin = new Vector2(0.5f, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(0.5f, rectTransform.anchorMax.y);
                    rectTransform.pivot = new Vector2(0.5f, rectTransform.pivot.y);
                    break;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    rectTransform.anchorMin = new Vector2(1, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
                    rectTransform.pivot = new Vector2(1, rectTransform.pivot.y);
                    break;
            }
        }

        private float[] ParseSpacingValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return new float[0];

            var parts = value.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new float[4]; // top, right, bottom, left

            for (int i = 0; i < parts.Length && i < 4; i++)
            {
                if (TryParseFloat(parts[i], out float parsed))
                {
                    result[i] = parsed;
                }
            }

            // CSS shorthand rules
            switch (parts.Length)
            {
                case 1:
                    // All directions the same
                    result[1] = result[2] = result[3] = result[0];
                    break;
                case 2:
                    // top/bottom = parts[0], left/right = parts[1]
                    result[2] = result[0];
                    result[3] = result[1];
                    break;
                case 3:
                    // top = parts[0], left/right = parts[1], bottom = parts[2]
                    result[3] = result[1];
                    break;
                // case 4: Already filled in order
            }

            return result;
        }

        private static Sprite ConvertSVGToSprite(string path)
        {
            try
            {
                // Try to load SVG as VectorImage
                var vectorImage = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VectorImage>(path);
                if (vectorImage != null)
                {
                    Debug.Log($"[Image Loading] Successfully loaded SVG as VectorImage: {path}");
                    
                    // Use default dimensions to create texture
                    int width = 256;
                    int height = 256;
                    
                    // Create a placeholder texture indicating this is an SVG file
                    var texture = CreateSVGPlaceholderTexture(width, height);
                    
                    if (texture != null)
                    {
                        // Create Sprite
                        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        sprite.name = System.IO.Path.GetFileNameWithoutExtension(path) + "_SVG";
                        return sprite;
                    }
                }
                
                Debug.LogWarning($"[Image Loading] Failed to load SVG file: {path}");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Image Loading] SVG conversion failed: {path}, error: {ex.Message}");
                return null;
            }
        }
        
        private static Texture2D CreateSVGPlaceholderTexture(int width, int height)
        {
            try
            {
                var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                var colors = new Color[width * height];
                
                // Create a placeholder image with SVG identifier
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        
                        // Create gradient background
                        float gradient = (float)(x + y) / (width + height);
                        
                        // Border effect
                        if (x < 5 || x >= width - 5 || y < 5 || y >= height - 5)
                        {
                            colors[index] = new Color(0.3f, 0.3f, 0.3f, 1f); // Dark gray border
                        }
                        else
                        {
                            colors[index] = new Color(0.8f + gradient * 0.2f, 0.8f + gradient * 0.2f, 0.8f + gradient * 0.2f, 1f);
                        }
                    }
                }
                
                // Add "SVG" text effect at center (simple pixel art)
                DrawSimpleText(colors, width, height, "SVG");
                
                texture.SetPixels(colors);
                texture.Apply();
                
                return texture;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Image Loading] Failed to create SVG placeholder texture: {ex.Message}");
                return null;
            }
        }
        
        private static void DrawSimpleText(Color[] colors, int width, int height, string text)
        {
            // Simple placeholder: create a diagonal stripe pattern to represent SVG
            int centerX = width / 2;
            int centerY = height / 2;
            
            var patternColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            
            // Draw simple diagonal stripe pattern
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    
                    // Create diagonal stripe pattern
                    if ((x + y) % 20 < 10)
                    {
                        colors[index] = patternColor;
                    }
                }
            }
            
            // Draw a simple cross mark at the center
            int crossSize = 20;
            var crossColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            
            // Horizontal line
            for (int x = centerX - crossSize; x <= centerX + crossSize; x++)
            {
                if (x >= 0 && x < width)
                {
                    int index = centerY * width + x;
                    if (index >= 0 && index < colors.Length)
                    {
                        colors[index] = crossColor;
                    }
                }
            }
            
            // Vertical line
            for (int y = centerY - crossSize; y <= centerY + crossSize; y++)
            {
                if (y >= 0 && y < height)
                {
                    int index = y * width + centerX;
                    if (index >= 0 && index < colors.Length)
                    {
                        colors[index] = crossColor;
                    }
                }
            }
        }

        /// <summary>
        /// Sprite loading configuration options
        /// </summary>
        public class SpriteLoadingOptions
        {
            public bool EnableTextureConversion { get; set; } = true;
            public bool EnableSVGConversion { get; set; } = true;
            public bool EnableCaching { get; set; } = true;
            public bool EnableDebugLogging { get; set; } = false;
        }

        // Static cache and configuration
        private static Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private static SpriteLoadingOptions _loadingOptions = new SpriteLoadingOptions();

        /// <summary>
        /// Set sprite loading options
        /// </summary>
        public static void SetSpriteLoadingOptions(SpriteLoadingOptions options)
        {
            _loadingOptions = options ?? new SpriteLoadingOptions();
        }

        /// <summary>
        /// Clear sprite cache
        /// </summary>
        public static void ClearSpriteCache()
        {
            _spriteCache.Clear();
        }



        /// <summary>
        /// Path normalization processing
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // Remove url() wrapper
            if (path.StartsWith("url(") && path.EndsWith(")"))
                path = path[4..^1];
            
            // Remove quotes
            return path.Trim('\'', '"');
        }

        /// <summary>
        /// Safe strategy execution method
        /// </summary>
        private static Sprite SafeExecuteStrategy(Func<string, Sprite> strategy, string path, string strategyName)
        {
            try
            {
                return strategy(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Sprite Loading] Failed to load sprite from path: {path}, error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to convert Texture2D to Sprite
        /// </summary>
        private static Sprite TryConvertTexture2DToSprite(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return null;

            var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (textureImporter != null && textureImporter.textureType != TextureImporterType.Sprite)
            {
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return null;
        }

        /// <summary>
        /// Strategy 1: Direct path loading
        /// </summary>
        private static Sprite LoadFromDirectPath(string path)
        {
            if (!path.StartsWith("Assets/")) return null;

            
            // Try to load Sprite directly
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            // Try SVG conversion
            if (_loadingOptions.EnableSVGConversion && path.EndsWith(".svg"))
            {
                sprite = ConvertSVGToSprite(path);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            // Try Texture2D conversion
            if (_loadingOptions.EnableTextureConversion)
            {
                sprite = TryConvertTexture2DToSprite(path);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        /// <summary>
        /// Strategy 2: Unity project database format loading
        /// </summary>
        private static Sprite LoadFromProjectDatabase(string path)
        {
            if (!path.StartsWith("project://database/")) return null;

            
            var assetPath = path.Substring("project://database/".Length);
            
            // Handle URL parameters
            var queryIndex = assetPath.IndexOf('?');
            if (queryIndex > 0)
            {
                var queryString = assetPath.Substring(queryIndex + 1);
                assetPath = assetPath.Substring(0, queryIndex);
                
                // Parse GUID
                var guidMatch = Regex.Match(queryString, @"guid=([a-fA-F0-9]+)");
                if (guidMatch.Success)
                {
                    var guid = guidMatch.Groups[1].Value;
                    var guidPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(guidPath))
                    {
                        var sprite = LoadFromDirectPath(guidPath);
                        if (sprite != null)
                        {
                            return sprite;
                        }
                    }
                }
            }
            
            // Remove anchor
            var anchorIndex = assetPath.IndexOf('#');
            if (anchorIndex > 0)
            {
                assetPath = assetPath.Substring(0, anchorIndex);
            }
            
            // Try to use extracted path directly
            var directSprite = LoadFromDirectPath(assetPath);
            if (directSprite != null)
            {
                return directSprite;
            }

            return null;
        }

        /// <summary>
        /// Strategy 3: Assets folder loading
        /// </summary>
        private static Sprite LoadFromAssetsFolder(string path)
        {
            if (path.StartsWith("Assets/")) return null; // Already handled by strategy 1

            
            var assetsPath = "Assets/" + path;
            return LoadFromDirectPath(assetsPath);
        }

        /// <summary>
        /// Strategy 4: Resources folder loading
        /// </summary>
        private static Sprite LoadFromResources(string path)
        {
            
            var resourcesPath = path;
            if (path.StartsWith("Assets/Resources/"))
            {
                resourcesPath = path.Substring("Assets/Resources/".Length);
            }
            
            // Remove extension
            var extensionIndex = resourcesPath.LastIndexOf('.');
            if (extensionIndex > 0)
            {
                resourcesPath = resourcesPath.Substring(0, extensionIndex);
            }
            
            var sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite != null)
            {
                return sprite;
            }

            return null;
        }

        /// <summary>
        /// Strategy 5: Project search loading
        /// </summary>
        private static Sprite LoadFromAssetSearch(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(fileName)) return null;

            
            // Search Sprite
            var searchResults = AssetDatabase.FindAssets($"{fileName} t:Sprite");
            foreach (var guid in searchResults)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null && sprite.name == fileName)
                {
                    return sprite;
                }
            }

            // Search SVG
            if (_loadingOptions.EnableSVGConversion)
            {
                var svgResults = AssetDatabase.FindAssets($"{fileName} t:VectorImage");
                foreach (var guid in svgResults)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith(".svg"))
                    {
                        var sprite = ConvertSVGToSprite(assetPath);
                        if (sprite != null)
                        {
                            return sprite;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Strategy 6: Texture2D search conversion
        /// </summary>
        private static Sprite LoadFromTexture2DSearch(string path)
        {
            if (!_loadingOptions.EnableTextureConversion) return null;

            var fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(fileName)) return null;

            
            var textureResults = AssetDatabase.FindAssets($"{fileName} t:Texture2D");
            foreach (var guid in textureResults)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null && texture.name == fileName)
                {
                    var sprite = TryConvertTexture2DToSprite(assetPath);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Strategy 7: Dynamic Sprite creation
        /// </summary>
        private static Sprite LoadFromDynamicCreation(string path)
        {
            
            var texture2D = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture2D != null)
            {
                return Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
            }

            // Finally try SVG
            if (_loadingOptions.EnableSVGConversion && path.EndsWith(".svg"))
            {
                var sprite = ConvertSVGToSprite(path);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        /// <summary>
        /// Main sprite loading method
        /// </summary>
        private Sprite LoadSpriteFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Check cache
            if (_loadingOptions.EnableCaching && _spriteCache.TryGetValue(path, out var cachedSprite))
            {
                return cachedSprite;
            }

            var originalPath = path;
            var normalizedPath = NormalizePath(path);
            

            // Define loading strategies (sorted by priority)
            var strategies = new[]
            {
                new { Name = "Direct Path", Strategy = (Func<string, Sprite>)LoadFromDirectPath },
                new { Name = "Project Database", Strategy = (Func<string, Sprite>)LoadFromProjectDatabase },
                new { Name = "Assets Folder", Strategy = (Func<string, Sprite>)LoadFromAssetsFolder },
                new { Name = "Resources", Strategy = (Func<string, Sprite>)LoadFromResources },
                new { Name = "Project Search", Strategy = (Func<string, Sprite>)LoadFromAssetSearch },
                new { Name = "Texture2D Search", Strategy = (Func<string, Sprite>)LoadFromTexture2DSearch },
                new { Name = "Dynamic Creation", Strategy = (Func<string, Sprite>)LoadFromDynamicCreation }
            };

            // Try strategies in priority order
            Sprite result = null;
            foreach (var strategy in strategies)
            {
                result = SafeExecuteStrategy(strategy.Strategy, normalizedPath, strategy.Name);
                if (result != null) break;
            }

            // Cache result (including null results to avoid repeated attempts)
            if (_loadingOptions.EnableCaching)
            {
                _spriteCache[path] = result;
            }

            return result;
        }

        private bool TryParseFloat(string value, out float result)
        {
            // Remove unit suffix
            value = Regex.Replace(value, @"[^\d\.-]", "");
            return float.TryParse(value, out result);
        }

        private bool TryParseColor(string value, out Color result)
        {
            result = Color.white;
            value = value.Trim();
            
            // Handle hexadecimal colors (#RGB or #RRGGBB)
            if (value.StartsWith("#"))
            {
                return ColorUtility.TryParseHtmlString(value, out result);
            }
            // Handle rgb(r,g,b) format
            else if (value.StartsWith("rgb("))
            {
                var rgbMatch = Regex.Match(value, @"rgb\((\d+),\s*(\d+),\s*(\d+)\)");
                if (rgbMatch.Success)
                {
                    var r = int.Parse(rgbMatch.Groups[1].Value) / 255f;
                    var g = int.Parse(rgbMatch.Groups[2].Value) / 255f;
                    var b = int.Parse(rgbMatch.Groups[3].Value) / 255f;
                    result = new Color(r, g, b, 1f);
                    return true;
                }
            }
            // Handle rgba(r,g,b,a) format
            else if (value.StartsWith("rgba("))
            {
                var rgbaMatch = Regex.Match(value, @"rgba\((\d+),\s*(\d+),\s*(\d+),\s*([\d\.]+)\)");
                if (rgbaMatch.Success)
                {
                    var r = int.Parse(rgbaMatch.Groups[1].Value) / 255f;
                    var g = int.Parse(rgbaMatch.Groups[2].Value) / 255f;
                    var b = int.Parse(rgbaMatch.Groups[3].Value) / 255f;
                    var a = float.Parse(rgbaMatch.Groups[4].Value);
                    result = new Color(r, g, b, a);
                    return true;
                }
            }
            // Handle common color names
            else
            {
                switch (value.ToLower())
                {
                    case "transparent":
                        result = new Color(0, 0, 0, 0);
                        return true;
                    case "white":
                        result = Color.white;
                        return true;
                    case "black":
                        result = Color.black;
                        return true;
                    case "red":
                        result = Color.red;
                        return true;
                    case "green":
                        result = Color.green;
                        return true;
                    case "blue":
                        result = Color.blue;
                        return true;
                    case "yellow":
                        result = Color.yellow;
                        return true;
                    case "magenta":
                        result = Color.magenta;
                        return true;
                    case "cyan":
                        result = Color.cyan;
                        return true;
                    case "gray":
                    case "grey":
                        result = Color.gray;
                        return true;
                }
            }
            
            return false;
        }
    }
}
