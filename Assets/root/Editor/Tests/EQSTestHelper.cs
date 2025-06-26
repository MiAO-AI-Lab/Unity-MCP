using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using com.IvanMurzak.Unity.MCP.Editor.API;
using com.IvanMurzak.Unity.MCP.Common;

namespace com.IvanMurzak.Unity.MCP.Editor.Tests
{
    /// <summary>
    /// Simple JsonUtils replacement class for testing
    /// </summary>
    internal static class JsonUtils
    {
        public static string Serialize(object obj)
        {
            return JsonUtility.ToJson(obj);
        }

        public static T Deserialize<T>(string json)
        {
            try
            {
                // Handle basic array types
                if (typeof(T) == typeof(float[]))
                {
                    // Parse JSON arrays like "[5, 0, 5]"
                    if (json.StartsWith("[") && json.EndsWith("]"))
                    {
                        var arrayContent = json.Substring(1, json.Length - 2);
                        var elements = arrayContent.Split(',');
                        var floatArray = new float[elements.Length];
                        for (int i = 0; i < elements.Length; i++)
                        {
                            floatArray[i] = float.Parse(elements[i].Trim());
                        }
                        return (T)(object)floatArray;
                    }
                }

                // Handle 2D array types
                if (typeof(T) == typeof(float[][]))
                {
                    if (json.StartsWith("[[") && json.EndsWith("]]"))
                    {
                        // Simple 2D array parsing
                        var arrayContent = json.Substring(2, json.Length - 4);
                        var subArrays = arrayContent.Split(new string[] { "],[" }, StringSplitOptions.None);
                        var result = new float[subArrays.Length][];
                        for (int i = 0; i < subArrays.Length; i++)
                        {
                            var elements = subArrays[i].Split(',');
                            result[i] = new float[elements.Length];
                            for (int j = 0; j < elements.Length; j++)
                            {
                                result[i][j] = float.Parse(elements[j].Trim());
                            }
                        }
                        return (T)(object)result;
                    }
                }

                // Handle Dictionary types
                if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var keyType = typeof(T).GetGenericArguments()[0];
                    var valueType = typeof(T).GetGenericArguments()[1];
                    
                    if (keyType == typeof(string) && valueType == typeof(object))
                    {
                        return ParseDictionary<T>(json);
                    }
                }

                // Handle List types
                if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = typeof(T).GetGenericArguments()[0];
                    if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        return ParseListOfDictionaries<T>(json);
                    }
                }

                // Default use JsonUtility
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Failed to deserialize JSON to {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        private static T ParseDictionary<T>(string json)
        {
            var dict = new Dictionary<string, object>();
            
            if (json.StartsWith("{") && json.EndsWith("}"))
            {
                var content = json.Substring(1, json.Length - 2);
                var pairs = SplitJsonPairs(content);
                
                foreach (var pair in pairs)
                {
                    var colonIndex = pair.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = pair.Substring(0, colonIndex).Trim().Trim('"');
                        var valueStr = pair.Substring(colonIndex + 1).Trim();
                        
                        object value = ParseValue(valueStr);
                        dict[key] = value;
                    }
                }
            }
            
            return (T)(object)dict;
        }

        private static T ParseListOfDictionaries<T>(string json)
        {
            var list = new List<Dictionary<string, object>>();
            
            if (json.StartsWith("[") && json.EndsWith("]"))
            {
                var content = json.Substring(1, json.Length - 2);
                var objects = SplitJsonObjects(content);
                
                foreach (var objStr in objects)
                {
                    var dict = ParseDictionary<Dictionary<string, object>>(objStr);
                    list.Add(dict);
                }
            }
            
            return (T)(object)list;
        }

        private static object ParseValue(string valueStr)
        {
            valueStr = valueStr.Trim();
            
            // String
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
            {
                return valueStr.Substring(1, valueStr.Length - 2);
            }
            
            // Array
            if (valueStr.StartsWith("[") && valueStr.EndsWith("]"))
            {
                var arrayContent = valueStr.Substring(1, valueStr.Length - 2);
                var elements = arrayContent.Split(',');
                var result = new object[elements.Length];
                for (int i = 0; i < elements.Length; i++)
                {
                    result[i] = ParseValue(elements[i].Trim());
                }
                return result;
            }
            
            // Number
            if (float.TryParse(valueStr, out float floatValue))
            {
                return floatValue;
            }
            
            // Boolean
            if (bool.TryParse(valueStr, out bool boolValue))
            {
                return boolValue;
            }
            
            // Object
            if (valueStr.StartsWith("{") && valueStr.EndsWith("}"))
            {
                return ParseDictionary<Dictionary<string, object>>(valueStr);
            }
            
            return valueStr;
        }

        private static List<string> SplitJsonPairs(string content)
        {
            var pairs = new List<string>();
            var current = "";
            var depth = 0;
            var inString = false;
            var escaped = false;
            
            for (int i = 0; i < content.Length; i++)
            {
                var ch = content[i];
                
                if (escaped)
                {
                    escaped = false;
                    current += ch;
                    continue;
                }
                
                if (ch == '\\')
                {
                    escaped = true;
                    current += ch;
                    continue;
                }
                
                if (ch == '"')
                {
                    inString = !inString;
                    current += ch;
                    continue;
                }
                
                if (!inString)
                {
                    if (ch == '{' || ch == '[')
                    {
                        depth++;
                    }
                    else if (ch == '}' || ch == ']')
                    {
                        depth--;
                    }
                    else if (ch == ',' && depth == 0)
                    {
                        pairs.Add(current.Trim());
                        current = "";
                        continue;
                    }
                }
                
                current += ch;
            }
            
            if (!string.IsNullOrEmpty(current.Trim()))
            {
                pairs.Add(current.Trim());
            }
            
            return pairs;
        }

        private static List<string> SplitJsonObjects(string content)
        {
            var objects = new List<string>();
            var current = "";
            var depth = 0;
            var inString = false;
            var escaped = false;
            
            for (int i = 0; i < content.Length; i++)
            {
                var ch = content[i];
                
                if (escaped)
                {
                    escaped = false;
                    current += ch;
                    continue;
                }
                
                if (ch == '\\')
                {
                    escaped = true;
                    current += ch;
                    continue;
                }
                
                if (ch == '"')
                {
                    inString = !inString;
                    current += ch;
                    continue;
                }
                
                if (!inString)
                {
                    if (ch == '{' || ch == '[')
                    {
                        depth++;
                    }
                    else if (ch == '}' || ch == ']')
                    {
                        depth--;
                    }
                    else if (ch == ',' && depth == 0)
                    {
                        objects.Add(current.Trim());
                        current = "";
                        continue;
                    }
                }
                
                current += ch;
            }
            
            if (!string.IsNullOrEmpty(current.Trim()))
            {
                objects.Add(current.Trim());
            }
            
            return objects;
        }
    }

    /// <summary>
    /// EQS Test Helper Class
    /// Provides test data generation, environment setup and common assertion methods
    /// </summary>
    public static class EQSTestHelper
    {
        #region Test Data Generators

        /// <summary>
        /// Generate standard reference points JSON data
        /// </summary>
        public static string GenerateReferencePointsJson(params (string name, Vector3 position)[] points)
        {
            var pointList = new List<object>();
            foreach (var point in points)
            {
                pointList.Add(new
                {
                    name = point.name,
                    position = new float[] { point.position.x, point.position.y, point.position.z }
                });
            }
            return JsonUtils.Serialize(pointList.ToArray());
        }

        /// <summary>
        /// Generate sphere area of interest JSON
        /// </summary>
        public static string GenerateSphereAreaJson(Vector3 center, float radius)
        {
            return JsonUtils.Serialize(new
            {
                type = "sphere",
                center = new float[] { center.x, center.y, center.z },
                radius = radius
            });
        }

        /// <summary>
        /// Generate rectangular area of interest JSON
        /// </summary>
        public static string GenerateBoxAreaJson(Vector3 center, Vector3 size)
        {
            return JsonUtils.Serialize(new
            {
                type = "box",
                center = new float[] { center.x, center.y, center.z },
                size = new float[] { size.x, size.y, size.z }
            });
        }

        /// <summary>
        /// Generate distance condition JSON
        /// </summary>
        public static string GenerateDistanceConditionJson(Vector3 targetPoint, float minDistance = 0f, float maxDistance = float.MaxValue, string distanceMode = "euclidean")
        {
            var condition = new
            {
                conditionType = "DistanceTo",
                parameters = new
                {
                    targetPoint = new float[] { targetPoint.x, targetPoint.y, targetPoint.z },
                    minDistance = minDistance,
                    maxDistance = maxDistance,
                    distanceMode = distanceMode
                }
            };
            return JsonUtils.Serialize(new[] { condition });
        }

        /// <summary>
        /// Generate custom property condition JSON
        /// </summary>
        public static string GenerateCustomPropertyConditionJson(string propertyName, object expectedValue, string comparisonType = "equals")
        {
            var condition = new
            {
                conditionType = "CustomProperty",
                parameters = new
                {
                    propertyName = propertyName,
                    value = expectedValue,
                    comparisonType = comparisonType
                }
            };
            return JsonUtils.Serialize(new[] { condition });
        }

        /// <summary>
        /// Generate proximity scoring criteria JSON
        /// </summary>
        public static string GenerateProximityScoringJson(Vector3 targetPoint, float maxDistance = 100f, float weight = 1.0f, string scoringCurve = "linear")
        {
            var criterion = new
            {
                criterionType = "ProximityTo",
                parameters = new
                {
                    targetPoint = new float[] { targetPoint.x, targetPoint.y, targetPoint.z },
                    maxDistance = maxDistance,
                    scoringCurve = scoringCurve
                },
                weight = weight
            };
            return JsonUtils.Serialize(new[] { criterion });
        }

        /// <summary>
        /// Generate distance scoring criteria JSON
        /// </summary>
        public static string GenerateFarthestFromScoringJson(Vector3 targetPoint, float maxDistance = 100f, float weight = 1.0f, string scoringCurve = "linear")
        {
            var criterion = new
            {
                criterionType = "FarthestFrom",
                parameters = new
                {
                    targetPoint = new float[] { targetPoint.x, targetPoint.y, targetPoint.z },
                    maxDistance = maxDistance,
                    scoringCurve = scoringCurve
                },
                weight = weight
            };
            return JsonUtils.Serialize(new[] { criterion });
        }

        /// <summary>
        /// Generate density scoring criteria JSON
        /// </summary>
        public static string GenerateDensityScoringJson(float radius = 5f, string objectType = null, float weight = 1.0f, string densityMode = "count")
        {
            var parameters = new Dictionary<string, object>
            {
                ["radius"] = radius,
                ["densityMode"] = densityMode,
                ["useDistanceWeighting"] = true
            };
            
            if (!string.IsNullOrEmpty(objectType))
                parameters["objectType"] = objectType;
            
            var criterion = new
            {
                criterionType = "DensityOfObjects",
                parameters = parameters,
                weight = weight
            };
            return JsonUtils.Serialize(new[] { criterion });
        }

        #endregion

        #region Environment Setup Tools

        /// <summary>
        /// Create custom test environment
        /// </summary>
        public static Tool_EQS.EQSEnvironmentData CreateCustomTestEnvironment(
            Vector3Int dimensions,
            float cellSize = 1.0f,
            float staticOccupancyRate = 0.3f,
            float dynamicOccupancyRate = 0.1f)
        {
            var testGrid = new Tool_EQS.EQSGrid
            {
                CellSize = cellSize,
                Origin = Vector3.zero,
                Dimensions = dimensions
            };
            
            var totalCells = dimensions.x * dimensions.y * dimensions.z;
            var cells = new Tool_EQS.EQSCell[totalCells];
            int index = 0;
            
            for (int x = 0; x < dimensions.x; x++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int z = 0; z < dimensions.z; z++)
                    {
                        cells[index] = new Tool_EQS.EQSCell
                        {
                            WorldPosition = new Vector3(x * cellSize, y * cellSize, z * cellSize),
                            Indices = new Vector3Int(x, y, z),
                            StaticOccupancy = UnityEngine.Random.value < staticOccupancyRate,
                            DynamicOccupants = new List<string>(),
                            Properties = new Dictionary<string, object>
                            {
                                ["isWalkable"] = UnityEngine.Random.value > 0.2f,
                                ["terrainType"] = GetRandomTerrainType(),
                                ["height"] = y * cellSize,
                                ["temperature"] = UnityEngine.Random.Range(-10f, 40f),
                                ["visibility"] = UnityEngine.Random.Range(0f, 1f)
                            }
                        };

                        // Add dynamic occupants
                        if (UnityEngine.Random.value < dynamicOccupancyRate)
                        {
                            var objectType = GetRandomObjectType();
                            cells[index].DynamicOccupants.Add($"{objectType}_{UnityEngine.Random.Range(1, 1000)}");
                        }
                        
                        index++;
                    }
                }
            }
            
            testGrid.Cells = cells;
            
            return new Tool_EQS.EQSEnvironmentData
            {
                Grid = testGrid,
                Hash = $"test_env_{Guid.NewGuid()}",
                LastUpdated = DateTime.Now,
                StaticGeometry = GenerateStaticGeometry(dimensions, cellSize),
                DynamicObjects = GenerateDynamicObjects(50)
            };
        }

        /// <summary>
        /// Generate static geometry data
        /// </summary>
        private static List<Tool_EQS.EQSStaticGeometry> GenerateStaticGeometry(Vector3Int dimensions, float cellSize)
        {
            var staticGeometry = new List<Tool_EQS.EQSStaticGeometry>();
            var count = UnityEngine.Random.Range(5, 15);
            
            for (int i = 0; i < count; i++)
            {
                var center = new Vector3(
                    UnityEngine.Random.Range(0, dimensions.x * cellSize),
                    UnityEngine.Random.Range(0, dimensions.y * cellSize),
                    UnityEngine.Random.Range(0, dimensions.z * cellSize)
                );
                
                var size = new Vector3(
                    UnityEngine.Random.Range(1f, 5f),
                    UnityEngine.Random.Range(1f, 3f),
                    UnityEngine.Random.Range(1f, 5f)
                );
                
                staticGeometry.Add(new Tool_EQS.EQSStaticGeometry
                {
                    Id = $"static_{i}",
                    Name = $"Building_{i}",
                    Bounds = new Bounds(center, size),
                    Type = GetRandomBuildingType()
                });
            }
            
            return staticGeometry;
        }

        /// <summary>
        /// Generate dynamic object data
        /// </summary>
        private static List<Tool_EQS.EQSDynamicObject> GenerateDynamicObjects(int count)
        {
            var dynamicObjects = new List<Tool_EQS.EQSDynamicObject>();
            
            for (int i = 0; i < count; i++)
            {
                var objectType = GetRandomObjectType();
                dynamicObjects.Add(new Tool_EQS.EQSDynamicObject
                {
                    Id = $"{objectType}_{i}",
                    Name = $"{objectType}_{i}",
                    Position = new Vector3(
                        UnityEngine.Random.Range(-50f, 50f),
                        UnityEngine.Random.Range(0f, 10f),
                        UnityEngine.Random.Range(-50f, 50f)
                    ),
                    Type = objectType,
                    Properties = new Dictionary<string, object>
                    {
                        ["health"] = UnityEngine.Random.Range(0f, 100f),
                        ["speed"] = UnityEngine.Random.Range(1f, 10f),
                        ["aggression"] = UnityEngine.Random.Range(0f, 1f)
                    }
                });
            }
            
            return dynamicObjects;
        }

        #endregion

        #region Random Data Generators

        private static string GetRandomTerrainType()
        {
            var terrainTypes = new[] { "ground", "water", "rock", "sand", "grass", "mud" };
            return terrainTypes[UnityEngine.Random.Range(0, terrainTypes.Length)];
        }
        
        private static string GetRandomObjectType()
        {
            var objectTypes = new[] { "enemy", "player", "npc", "item", "vehicle", "animal" };
            return objectTypes[UnityEngine.Random.Range(0, objectTypes.Length)];
        }
        
        private static string GetRandomBuildingType()
        {
            var buildingTypes = new[] { "house", "tower", "wall", "gate", "bridge", "monument" };
            return buildingTypes[UnityEngine.Random.Range(0, buildingTypes.Length)];
        }

        #endregion

        #region Assertion Tools

        /// <summary>
        /// Verify if query result is successful
        /// </summary>
        public static bool IsQueryResultSuccessful(string result)
        {
            return !string.IsNullOrEmpty(result) && result.Contains("Success");
        }

        /// <summary>
        /// Verify if query result contains errors
        /// </summary>
        public static bool IsQueryResultError(string result)
        {
            return !string.IsNullOrEmpty(result) && result.Contains("Error");
        }

        /// <summary>
        /// Extract execution time from query result
        /// </summary>
        public static float? ExtractExecutionTime(string result)
        {
            if (string.IsNullOrEmpty(result))
                return null;

            // Simple regex pattern matching execution time
            var pattern = @"执行时间:\s*(\d+\.?\d*)\s*毫秒";
            var match = System.Text.RegularExpressions.Regex.Match(result, pattern);
            
            if (match.Success && float.TryParse(match.Groups[1].Value, out float time))
            {
                return time;
            }
            
            return null;
        }

        /// <summary>
        /// Extract candidate location count from query result
        /// </summary>
        public static int? ExtractCandidateCount(string result)
        {
            if (string.IsNullOrEmpty(result))
                return null;
            
            var pattern = @"找到的候选位置数:\s*(\d+)";
            var match = System.Text.RegularExpressions.Regex.Match(result, pattern);
            
            if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
            {
                return count;
            }
            
            return null;
        }

        #endregion

        #region Performance Testing Tools

        /// <summary>
        /// Execute performance test and return results
        /// </summary>
        public static PerformanceTestResult ExecutePerformanceTest(Tool_EQS eqsTool, string queryId, int iterations = 1)
        {
            var results = new List<string>();
            var executionTimes = new List<double>();
            
            for (int i = 0; i < iterations; i++)
            {
                var startTime = DateTime.Now;
                var result = eqsTool.PerformQuery($"{queryId}_{i}");
                var endTime = DateTime.Now;
                
                results.Add(result);
                executionTimes.Add((endTime - startTime).TotalMilliseconds);
            }
            
            return new PerformanceTestResult
            {
                Results = results,
                ExecutionTimes = executionTimes,
                AverageExecutionTime = executionTimes.Average(),
                MinExecutionTime = executionTimes.Min(),
                MaxExecutionTime = executionTimes.Max(),
                TotalExecutionTime = executionTimes.Sum()
            };
        }
        
        #endregion
    }

    /// <summary>
    /// Performance test result
    /// </summary>
    public class PerformanceTestResult
    {
        public List<string> Results { get; set; } = new();
        public List<double> ExecutionTimes { get; set; } = new();
        public double AverageExecutionTime { get; set; }
        public double MinExecutionTime { get; set; }
        public double MaxExecutionTime { get; set; }
        public double TotalExecutionTime { get; set; }
    }
} 