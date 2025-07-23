#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.ReflectorNet.Model;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace com.MiAO.Unity.MCP.Common
{
    public class McpRunner : IMcpRunner
    {
        static readonly JsonElement EmptyInputSchema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{},\"required\":[]}").RootElement;

        protected readonly ILogger<McpRunner> _logger;
        readonly ToolRunnerCollection _tools;
        readonly ResourceRunnerCollection _resources;

        public McpRunner(ILogger<McpRunner> logger, ToolRunnerCollection tools, ResourceRunnerCollection resources)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogTrace("Ctor.");
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Registered tools [{0}]:", tools.Count);
                foreach (var kvp in tools)
                    _logger.LogTrace("Tool: {0}", kvp.Key);
            }

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Registered resources [{0}]:", resources.Count);
                foreach (var kvp in resources)
                    _logger.LogTrace("Resource: {0}", kvp.Key);
            }
        }

        public bool HasTool(string name) => _tools.ContainsKey(name);
        public bool HasResource(string name) => _resources.ContainsKey(name);

        public async Task<IResponseData<ResponseCallTool>> RunCallTool(IRequestCallTool data, CancellationToken cancellationToken = default)
        {
            if (data == null)
                return ResponseData<ResponseCallTool>.Error(Consts.Guid.Zero, "Tool data is null.")
                    .Log(_logger);

            if (string.IsNullOrEmpty(data.Name))
                return ResponseData<ResponseCallTool>.Error(data.RequestID, "Tool.Name is null.")
                    .Log(_logger);

            if (!_tools.TryGetValue(data.Name, out var runner))
                return ResponseData<ResponseCallTool>.Error(data.RequestID, $"Tool with Name '{data.Name}' not found.")
                    .Log(_logger);
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var message = data.Arguments == null
                        ? $"Run tool '{data.Name}' with no parameters."
                        : $"Run tool '{data.Name}' with parameters[{data.Arguments.Count}]:\n{string.Join(",\n", data.Arguments)}\n";
                    _logger.LogInformation(message);
                }

                // Convert JsonElement values based on target method parameter types
                var convertedArguments = ConvertArgumentsForMethod(runner, data.Arguments);

                var result = await runner.Run(convertedArguments);
                if (result == null)
                    return ResponseData<ResponseCallTool>.Error(data.RequestID, $"Tool '{data.Name}' returned null result.")
                        .Log(_logger);

                return result.Log(_logger).Pack(data.RequestID);
            }
            catch (Exception ex)
            {
                // Handle or log the exception as needed
                return ResponseData<ResponseCallTool>.Error(data.RequestID, $"Failed to run tool '{data.Name}'. Exception: {ex}")
                    .Log(_logger, ex);
            }
        }

        /// <summary>
        /// Converts JsonElement values in arguments based on target method parameter types
        /// </summary>
        /// <param name="runner">The tool runner containing method information</param>
        /// <param name="arguments">The arguments to convert</param>
        /// <returns>Dictionary with converted JsonElement values</returns>
        private IReadOnlyDictionary<string, JsonElement>? ConvertArgumentsForMethod(IRunTool runner, IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
                return arguments;

            try
            {
                // Get method info from runner if it's a RunTool
                if (runner is RunTool runTool)
                {
                    var methodInfo = GetMethodInfoFromRunTool(runTool);
                    if (methodInfo != null)
                    {
                        return ConvertArgumentsBasedOnMethodParameters(methodInfo, arguments);
                    }
                }

                // If we can't get method info, return original arguments
                return arguments;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert arguments for method. Using original arguments.");
                return arguments;
            }
        }

        /// <summary>
        /// Gets MethodInfo from RunTool using reflection
        /// </summary>
        /// <param name="runTool">The RunTool instance</param>
        /// <returns>MethodInfo if found, null otherwise</returns>
        private MethodInfo? GetMethodInfoFromRunTool(RunTool runTool)
        {
            try
            {
                // Access the protected/private MethodInfo field through reflection
                var type = runTool.GetType();
                var baseType = type.BaseType; // MethodWrapper
                
                if (baseType != null)
                {
                    var methodInfoField = baseType.GetField("_methodInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (methodInfoField != null)
                    {
                        return methodInfoField.GetValue(runTool) as MethodInfo;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to get MethodInfo from RunTool via reflection");
                return null;
            }
        }

        /// <summary>
        /// Converts arguments based on method parameter types
        /// </summary>
        /// <param name="methodInfo">The target method info</param>
        /// <param name="arguments">The arguments to convert</param>
        /// <returns>Dictionary with converted JsonElement values</returns>
        private IReadOnlyDictionary<string, JsonElement> ConvertArgumentsBasedOnMethodParameters(MethodInfo methodInfo, IReadOnlyDictionary<string, JsonElement> arguments)
        {
            var parameters = methodInfo.GetParameters();
            var convertedArguments = new Dictionary<string, JsonElement>();

            foreach (var kvp in arguments)
            {
                var parameterName = kvp.Key;
                var jsonElement = kvp.Value;
                
                // Find matching parameter
                var parameter = parameters.FirstOrDefault(p => p.Name == parameterName);
                if (parameter != null)
                {
                    var convertedElement = ConvertJsonElementToTargetType(jsonElement, parameter.ParameterType);
                    convertedArguments[parameterName] = convertedElement;
                }
                else
                {
                    // Keep original if no matching parameter found
                    convertedArguments[parameterName] = jsonElement;
                }
            }

            return convertedArguments;
        }

        // Type converter delegates
        private delegate JsonElement TypeConverter(JsonElement element);
        
        // Static dictionary for type converters
        private static readonly Dictionary<Type, TypeConverter> _typeConverters = new()
        {
            { typeof(string), ConvertToString },
            { typeof(int), ConvertToNumber<int> },
            { typeof(int?), ConvertToNumber<int> },
            { typeof(long), ConvertToNumber<long> },
            { typeof(long?), ConvertToNumber<long> },
            { typeof(float), ConvertToNumber<float> },
            { typeof(float?), ConvertToNumber<float> },
            { typeof(double), ConvertToNumber<double> },
            { typeof(double?), ConvertToNumber<double> },
            { typeof(decimal), ConvertToNumber<decimal> },
            { typeof(decimal?), ConvertToNumber<decimal> },
            { typeof(bool), ConvertToBool },
            { typeof(bool?), ConvertToBool },
        };

        /// <summary>
        /// Converts JsonElement to match target type using generic approach
        /// </summary>
        /// <param name="element">The JsonElement to convert</param>
        /// <param name="targetType">The target parameter type</param>
        /// <returns>Converted JsonElement</returns>
        private JsonElement ConvertJsonElementToTargetType(JsonElement element, Type targetType)
        {
            try
            {
                // Handle null values
                if (element.ValueKind == JsonValueKind.Null)
                    return element;

                // Handle nullable types
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                
                // Handle arrays
                if (underlyingType.IsArray && element.ValueKind == JsonValueKind.String)
                    return ParseStringAsJsonElement(element, JsonValueKind.Array);
                
                // Handle objects
                if (element.ValueKind == JsonValueKind.String && underlyingType == typeof(object))
                    return ParseStringAsJsonElement(element, JsonValueKind.Object);

                // Handle enums
                if (underlyingType.IsEnum && element.ValueKind == JsonValueKind.String)
                    return ConvertToEnum(element, underlyingType);

                // Try to find converter in dictionary
                if (_typeConverters.TryGetValue(underlyingType, out var converter))
                    return converter(element);

                // For unknown types, return as-is
                return element;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to convert JsonElement to target type {TargetType}. Using original element.", targetType.Name);
                return element;
            }
        }

        // Generic type conversion helpers
        private static JsonElement ConvertToString(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return element;
            
            var stringValue = element.ToString();
            return JsonSerializer.Deserialize<JsonElement>($"\"{stringValue}\"");
        }

        private static JsonElement ConvertToNumber<T>(JsonElement element) where T : struct
        {
            if (element.ValueKind == JsonValueKind.Number)
                return element;
            
            if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString();
                if (TryParseNumber<T>(stringValue, out var numericValue))
                {
                    return JsonSerializer.Deserialize<JsonElement>(numericValue.ToString());
                }
            }
            
            return element;
        }

        private static bool TryParseNumber<T>(string? input, out T result) where T : struct
        {
            result = default;
            if (string.IsNullOrEmpty(input))
                return false;

            try
            {
                var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(T));
                if (converter.CanConvertFrom(typeof(string)))
                {
                    result = (T)converter.ConvertFromString(input);
                    return true;
                }
            }
            catch
            {
                // Fallback to specific type parsing
            }

            // Use switch statement for cleaner logic
            switch (typeof(T).Name)
            {
                case nameof(Int32):
                    if (int.TryParse(input, out var intVal))
                    {
                        result = (T)(object)intVal;
                        return true;
                    }
                    break;
                case nameof(Int64):
                    if (long.TryParse(input, out var longVal))
                    {
                        result = (T)(object)longVal;
                        return true;
                    }
                    break;
                case nameof(Single):
                    if (float.TryParse(input, out var floatVal))
                    {
                        result = (T)(object)floatVal;
                        return true;
                    }
                    break;
                case nameof(Double):
                    if (double.TryParse(input, out var doubleVal))
                    {
                        result = (T)(object)doubleVal;
                        return true;
                    }
                    break;
                case nameof(Decimal):
                    if (decimal.TryParse(input, out var decimalVal))
                    {
                        result = (T)(object)decimalVal;
                        return true;
                    }
                    break;
            }
            
            return false;
        }

        private static JsonElement ConvertToBool(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                return element;
            
            if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString()?.ToLowerInvariant();
                return stringValue switch
                {
                    "true" => JsonSerializer.Deserialize<JsonElement>("true"),
                    "false" => JsonSerializer.Deserialize<JsonElement>("false"),
                    _ => element
                };
            }
            
            return element;
        }

        private JsonElement ParseStringAsJsonElement(JsonElement element, JsonValueKind valueKind)
        {
            try
            {
                if (element.ValueKind != JsonValueKind.String)
                    return element;

                var stringValue = element.GetString();
                if (string.IsNullOrEmpty(stringValue))
                    return element;
                    
                try
                {
                    var parsedElement = JsonSerializer.Deserialize<JsonElement>(stringValue);

                    if (parsedElement.ValueKind == valueKind)
                        return parsedElement;
                    else
                    {
                        _logger.LogTrace("The parsed element of string '{0}' is not of the expected value kind '{1}'. Using original element.", stringValue, valueKind);
                        return element;
                    }
                }
                catch
                {
                    return element;
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to convert JsonElement to array type. Using original element.");
                return element;
            }
        }

        private static JsonElement ConvertToEnum(JsonElement element, Type enumType)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString();
                if (Enum.TryParse(enumType, stringValue, true, out var enumValue))
                {
                    return JsonSerializer.Deserialize<JsonElement>($"\"{enumValue}\"");
                }
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                // Numbers are often used for enum values
                return element;
            }
            
            return element;
        }

        public Task<IResponseData<ResponseListTool[]>> RunListTool(IRequestListTool data, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Listing tools.");
                var result = _tools
                    .Select(kvp => new ResponseListTool()
                    {
                        Name = kvp.Key,
                        Title = kvp.Value.Title,
                        Description = kvp.Value.Description,
                        InputSchema = kvp.Value.InputSchema?.ToJsonElement() ?? EmptyInputSchema,
                    })
                    .ToArray();

                return result
                    .Log(_logger)
                    .Pack(data.RequestID)
                    .TaskFromResult();
            }
            catch (Exception ex)
            {
                // Handle or log the exception as needed
                return ResponseData<ResponseListTool[]>.Error(data.RequestID, $"Failed to list tools. Exception: {ex}")
                    .Log(_logger, ex)
                    .TaskFromResult();
            }
        }

        public async Task<IResponseData<ResponseResourceContent[]>> RunResourceContent(IRequestResourceContent data, CancellationToken cancellationToken = default)
        {
            if (data == null)
                throw new ArgumentException("Resource data is null.");

            if (data.Uri == null)
                throw new ArgumentException("Resource.Uri is null.");

            var runner = FindResourceContentRunner(data.Uri, _resources, out var uriTemplate)?.RunGetContent;
            if (runner == null || uriTemplate == null)
                throw new ArgumentException($"No route matches the URI: {data.Uri}");

            _logger.LogInformation("Executing resource '{0}'.", data.Uri);

            var parameters = ParseUriParameters(uriTemplate!, data.Uri);
            PrintParameters(parameters);

            // Execute the resource with the parameters from Uri
            var result = await runner.Run(parameters);
            return result.Pack(data.RequestID);
        }

        public async Task<IResponseData<ResponseListResource[]>> RunListResources(IRequestListResources data, CancellationToken cancellationToken = default)
        {
            var tasks = _resources.Values
                .Select(resource => resource.RunListContext.Run());

            await Task.WhenAll(tasks);

            return tasks
                .SelectMany(x => x.Result)
                .ToArray()
                .Pack(data.RequestID);
        }

        public Task<IResponseData<ResponseResourceTemplate[]>> RunResourceTemplates(IRequestListResourceTemplates data, CancellationToken cancellationToken = default)
            => _resources.Values
                .Select(resource => new ResponseResourceTemplate(resource.Route, resource.Name, resource.Description, resource.MimeType))
                .ToArray()
                .Pack(data.RequestID)
                .TaskFromResult();

        IRunResource? FindResourceContentRunner(string uri, IDictionary<string, IRunResource> resources, out string? uriTemplate)
        {
            foreach (var route in resources)
            {
                if (IsMatch(route.Key, uri))
                {
                    uriTemplate = route.Key;
                    return route.Value;
                }
            }
            uriTemplate = null;
            return null;
        }

        bool IsMatch(string uriTemplate, string uri)
        {
            // Convert pattern to regex
            var regexPattern = "^" + Regex.Replace(uriTemplate, @"\{(\w+)\}", @"(?<$1>[^/]+)") + "(?:/.*)?$";

            return Regex.IsMatch(uri, regexPattern);
        }

        IDictionary<string, object?> ParseUriParameters(string pattern, string uri)
        {
            var parameters = new Dictionary<string, object?>()
            {
                { "uri", uri }
            };

            // Convert pattern to regex
            var regexPattern = "^" + Regex.Replace(pattern, @"\{(\w+)\}", @"(?<$1>.+)") + "(?:/.*)?$";

            var regex = new Regex(regexPattern);
            var match = regex.Match(uri);

            if (match.Success)
            {
                foreach (var groupName in regex.GetGroupNames())
                {
                    if (groupName != "0") // Skip the entire match group
                    {
                        parameters[groupName] = match.Groups[groupName].Value;
                    }
                }
            }

            return parameters;
        }

        void PrintParameters(IDictionary<string, object?> parameters)
        {
            if (!_logger.IsEnabled(LogLevel.Debug))
                return;

            var parameterLogs = string.Join(Environment.NewLine, parameters.Select(kvp => $"{kvp.Key} = {kvp.Value ?? "null"}"));
            _logger.LogDebug("Parsed Parameters [{0}]:\n{1}", parameters.Count, parameterLogs);
        }

        public void Dispose()
        {
            _resources.Clear();
            _tools.Clear();
        }
    }
}