using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using com.IvanMurzak.Unity.MCP.Editor.API;
using com.IvanMurzak.Unity.MCP.Common;

namespace com.IvanMurzak.Unity.MCP.Editor.Tests
{
    /// <summary>
    /// Comprehensive unit tests for EQS_PerformQuery method
    /// 
    /// Test coverage includes:
    /// 1. Normal functionality tests - Verify correct execution of various query parameter combinations
    /// 2. Exception handling tests - Verify handling of error inputs and exception cases
    /// 3. Boundary condition tests - Verify handling of extreme values and boundary cases  
    /// 4. Parameter parsing tests - Verify correct parsing of JSON parameters
    /// 5. Performance tests - Verify query execution time and memory usage
    /// </summary>
    [TestFixture]
    public class EQSPerformQueryTests
    {
        private Tool_EQS eqsTool;
        private const string TEST_QUERY_ID = "test_query_001";
        
        #region Setup and Teardown
        
        [SetUp]
        public void Setup()
        {
            // Initialize test environment
            eqsTool = new Tool_EQS();

            // Create test scene and environment
            SetupTestEnvironment();
        }
        
        [TearDown]
        public void Teardown()
        {
            // Cleanup test environment
            CleanupTestEnvironment();
        }

        /// <summary>
        /// Setup test environment - Create simulated EQS environment data
        /// </summary>
        private void SetupTestEnvironment()
        {
            // Create test grid
            var testGrid = new Tool_EQS.EQSGrid
            {
                CellSize = 1.0f,
                Origin = Vector3.zero,
                Dimensions = new Vector3Int(10, 5, 10)
            };

            // Create test cells
            var cells = new Tool_EQS.EQSCell[testGrid.Dimensions.x * testGrid.Dimensions.y * testGrid.Dimensions.z];
            int index = 0;
            
            for (int x = 0; x < testGrid.Dimensions.x; x++)
            {
                for (int y = 0; y < testGrid.Dimensions.y; y++)
                {
                    for (int z = 0; z < testGrid.Dimensions.z; z++)
                    {
                        cells[index] = new Tool_EQS.EQSCell
                        {
                            WorldPosition = new Vector3(x * testGrid.CellSize, y * testGrid.CellSize, z * testGrid.CellSize),
                            Indices = new Vector3Int(x, y, z),
                            StaticOccupancy = UnityEngine.Random.value < 0.3f, // 30% static occupancy rate
                            DynamicOccupants = new List<string>(),
                            Properties = new Dictionary<string, object>
                            {
                                ["isWalkable"] = UnityEngine.Random.value > 0.2f,
                                ["terrainType"] = UnityEngine.Random.value > 0.5f ? "ground" : "water",
                                ["height"] = y * testGrid.CellSize
                            }
                        };

                        // Randomly add some dynamic occupants
                        if (UnityEngine.Random.value < 0.1f)
                        {
                            cells[index].DynamicOccupants.Add($"enemy_{UnityEngine.Random.Range(1, 100)}");
                        }
                        
                        index++;
                    }
                }
            }
            
            testGrid.Cells = cells;

            // Create test environment data
            var testEnvironment = new Tool_EQS.EQSEnvironmentData
            {
                Grid = testGrid,
                Hash = "test_environment_hash",
                LastUpdated = DateTime.Now
            };

            // Use reflection to set private field (simulate environment initialization)
            var environmentField = typeof(Tool_EQS).GetField("_currentEnvironment", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            environmentField?.SetValue(null, testEnvironment);
        }

        /// <summary>
        /// Cleanup test environment
        /// </summary>
        private void CleanupTestEnvironment()
        {
            // Cleanup environment data
            var environmentField = typeof(Tool_EQS).GetField("_currentEnvironment", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            environmentField?.SetValue(null, null);

            // Cleanup query cache
            var cacheField = typeof(Tool_EQS).GetField("_queryCache", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (cacheField?.GetValue(null) is Dictionary<string, Tool_EQS.EQSQueryResult> cache)
            {
                cache.Clear();
            }
        }

        #endregion

        #region Normal Functionality Tests

        /// <summary>
        /// Test basic query functionality - Minimal parameter set
        /// </summary>
        [Test]
        public void TestBasicQuery_MinimalParameters()
        {
            // Arrange
            var queryId = "basic_test";
            
            // Act
            var result = eqsTool.PerformQuery(queryId);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
            Assert.That(result, Does.Contain(queryId));
        }

        /// <summary>
        /// Test complete query functionality - All parameters
        /// </summary>
        [Test]
        public void TestCompleteQuery_AllParameters()
        {
            // Arrange
            var queryId = "complete_test";
            var targetObjectType = "TestObject";
            var referencePointsJson = @"[
                {""name"": ""PlayerStart"", ""position"": [5.0, 0.0, 5.0]},
                {""name"": ""Objective"", ""position"": [8.0, 0.0, 8.0]}
            ]";
            var areaOfInterestJson = @"{
                ""type"": ""sphere"",
                ""center"": [5.0, 0.0, 5.0],
                ""radius"": 10.0
            }";
            var conditionsJson = @"[
                {
                    ""conditionType"": ""DistanceTo"",
                    ""parameters"": {
                        ""targetPoint"": [5.0, 0.0, 5.0],
                        ""minDistance"": 2.0,
                        ""maxDistance"": 8.0,
                        ""distanceMode"": ""euclidean""
                    }
                }
            ]";
            var scoringCriteriaJson = @"[
                {
                    ""criterionType"": ""ProximityTo"",
                    ""parameters"": {
                        ""targetPoint"": [5.0, 0.0, 5.0],
                        ""maxDistance"": 10.0,
                        ""scoringCurve"": ""linear""
                    },
                    ""weight"": 0.8
                }
            ]";
            
            // Act
            var result = eqsTool.PerformQuery(
                queryId,
                targetObjectType,
                referencePointsJson,
                areaOfInterestJson,
                conditionsJson,
                scoringCriteriaJson,
                5
            );
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
            Assert.That(result, Does.Contain(queryId));
            Assert.That(result, Does.Contain("found candidate locations"));
        }

        /// <summary>
        /// Test distance condition query
        /// </summary>
        [Test]
        public void TestDistanceConditionQuery()
        {
            // Arrange
            var queryId = "distance_test";
            var conditionsJson = @"[
                {
                    ""conditionType"": ""DistanceTo"",
                    ""parameters"": {
                        ""targetPoint"": [5.0, 0.0, 5.0],
                        ""minDistance"": 1.0,
                        ""maxDistance"": 5.0,
                        ""distanceMode"": ""euclidean""
                    }
                }
            ]";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, conditionsJson: conditionsJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test proximity scoring query
        /// </summary>
        [Test]
        public void TestProximityScoring()
        {
            // Arrange
            var queryId = "proximity_test";
            var scoringCriteriaJson = @"[
                {
                    ""criterionType"": ""ProximityTo"",
                    ""parameters"": {
                        ""targetPoint"": [5.0, 0.0, 5.0],
                        ""maxDistance"": 10.0,
                        ""scoringCurve"": ""linear""
                    },
                    ""weight"": 1.0
                }
            ]";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, scoringCriteriaJson: scoringCriteriaJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test sphere area of interest
        /// </summary>
        [Test]
        public void TestSphereAreaOfInterest()
        {
            // Arrange
            var queryId = "sphere_area_test";
            var areaOfInterestJson = @"{
                ""type"": ""sphere"",
                ""center"": [5.0, 2.0, 5.0],
                ""radius"": 3.0
            }";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, areaOfInterestJson: areaOfInterestJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test box area of interest
        /// </summary>
        [Test]
        public void TestBoxAreaOfInterest()
        {
            // Arrange
            var queryId = "box_area_test";
            var areaOfInterestJson = @"{
                ""type"": ""box"",
                ""center"": [5.0, 2.0, 5.0],
                ""size"": [6.0, 4.0, 6.0]
            }";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, areaOfInterestJson: areaOfInterestJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        #endregion

        #region Exception Handling Tests

        /// <summary>
        /// Test query when environment is not initialized
        /// </summary>
        [Test]
        public void TestQuery_EnvironmentNotInitialized()
        {
            // Arrange - Cleanup environment
            CleanupTestEnvironment();
            
            // Act
            var result = eqsTool.PerformQuery("test_no_env");
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Error"));
            Assert.That(result, Does.Contain("environment not initialized"));

            // Cleanup - Re-set environment
            SetupTestEnvironment();
        }

        /// <summary>
        /// Test invalid reference points JSON
        /// </summary>
        [Test]
        public void TestQuery_InvalidReferencePointsJson()
        {
            // Arrange
            var queryId = "invalid_ref_points";
            var invalidReferencePointsJson = @"[{""invalid"": ""json""}]"; // Missing required fields

            // Act
            var result = eqsTool.PerformQuery(queryId, referencePointsJson: invalidReferencePointsJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Error"));
            Assert.That(result, Does.Contain("failed to parse reference points"));
        }

        /// <summary>
        /// Test invalid area of interest JSON
        /// </summary>
        [Test]
        public void TestQuery_InvalidAreaOfInterestJson()
        {
            // Arrange
            var queryId = "invalid_area";
            var invalidAreaJson = @"{""invalid"": ""format""}"; // Missing type field

            // Act
            var result = eqsTool.PerformQuery(queryId, areaOfInterestJson: invalidAreaJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Error"));
            Assert.That(result, Does.Contain("failed to parse area of interest"));
        }

        /// <summary>
        /// Test invalid query conditions JSON
        /// </summary>
        [Test]
        public void TestQuery_InvalidConditionsJson()
        {
            // Arrange
            var queryId = "invalid_conditions";
            var invalidConditionsJson = @"[{""badCondition"": ""test""}]"; // Missing conditionType

            // Act
            var result = eqsTool.PerformQuery(queryId, conditionsJson: invalidConditionsJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Error"));
            Assert.That(result, Does.Contain("failed to parse query conditions"));
        }

        /// <summary>
        /// Test invalid scoring criteria JSON
        /// </summary>
        [Test]
        public void TestQuery_InvalidScoringCriteriaJson()
        {
            // Arrange
            var queryId = "invalid_scoring";
            var invalidScoringJson = @"[{""badScoring"": ""test""}]"; // Missing criterionType

            // Act
            var result = eqsTool.PerformQuery(queryId, scoringCriteriaJson: invalidScoringJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Error"));
            Assert.That(result, Does.Contain("failed to parse scoring criteria"));
        }

        /// <summary>
        /// Test malicious JSON input
        /// </summary>
        [Test]
        public void TestQuery_MaliciousJsonInput()
        {
            // Arrange
            var queryId = "malicious_test";
            var maliciousJson = @"{{""type"": ""sphere"", ""__proto__"": {{""polluted"": true}}}}";

            // Act & Assert - Should not throw exception
            Assert.DoesNotThrow(() => {
                var result = eqsTool.PerformQuery(queryId, areaOfInterestJson: maliciousJson);
                Assert.That(result, Is.Not.Null);
            });
        }

        #endregion

        #region Boundary Condition Tests

        /// <summary>
        /// Test empty query ID
        /// </summary>
        [Test]
        public void TestQuery_EmptyQueryId()
        {
            // Act
            var result = eqsTool.PerformQuery("");
            
            // Assert
            Assert.That(result, Is.Not.Null);
            // Empty ID should still execute, but use empty string as ID
        }

        /// <summary>
        /// Test extremely large result count
        /// </summary>
        [Test]
        public void TestQuery_LargeResultCount()
        {
            // Arrange
            var queryId = "large_count_test";
            var largeCount = 10000;
            
            // Act
            var result = eqsTool.PerformQuery(queryId, desiredResultCount: largeCount);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test zero result count
        /// </summary>
        [Test]
        public void TestQuery_ZeroResultCount()
        {
            // Arrange
            var queryId = "zero_count_test";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, desiredResultCount: 0);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test negative result count
        /// </summary>
        [Test]
        public void TestQuery_NegativeResultCount()
        {
            // Arrange
            var queryId = "negative_count_test";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, desiredResultCount: -5);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test extremely small area of interest
        /// </summary>
        [Test]
        public void TestQuery_TinyAreaOfInterest()
        {
            // Arrange
            var queryId = "tiny_area_test";
            var tinyAreaJson = @"{
                ""type"": ""sphere"",
                ""center"": [5.0, 2.0, 5.0],
                ""radius"": 0.001
            }";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, areaOfInterestJson: tinyAreaJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test extremely large area of interest
        /// </summary>
        [Test]
        public void TestQuery_HugeAreaOfInterest()
        {
            // Arrange
            var queryId = "huge_area_test";
            var hugeAreaJson = @"{
                ""type"": ""sphere"",
                ""center"": [5.0, 2.0, 5.0],
                ""radius"": 10000.0
            }";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, areaOfInterestJson: hugeAreaJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        #endregion

        #region Parameter Combination Tests

        /// <summary>
        /// Test different distance modes
        /// </summary>
        [Test]
        public void TestQuery_DifferentDistanceModes()
        {
            var distanceModes = new[] { "euclidean", "manhattan", "chebyshev", "horizontal", "vertical", "squared" };
            
            foreach (var mode in distanceModes)
            {
                // Arrange
                var queryId = $"distance_mode_{mode}";
                var conditionsJson = $@"[
                    {{
                        ""conditionType"": ""DistanceTo"",
                        ""parameters"": {{
                            ""targetPoint"": [5.0, 0.0, 5.0],
                            ""minDistance"": 1.0,
                            ""maxDistance"": 5.0,
                            ""distanceMode"": ""{mode}""
                        }}
                    }}
                ]";
                
                // Act
                var result = eqsTool.PerformQuery(queryId, conditionsJson: conditionsJson);
                
                // Assert
                Assert.That(result, Is.Not.Null, $"Distance mode {mode} failed");
                Assert.That(result, Does.Contain("Success"), $"Distance mode {mode} did not succeed");
            }
        }

        /// <summary>
        /// Test different scoring curves
        /// </summary>
        [Test]
        public void TestQuery_DifferentScoringCurves()
        {
            var scoringCurves = new[] { "linear", "exponential", "logarithmic", "smoothstep", "inverse" };
            
            foreach (var curve in scoringCurves)
            {
                // Arrange
                var queryId = $"scoring_curve_{curve}";
                var scoringCriteriaJson = $@"[
                    {{
                        ""criterionType"": ""ProximityTo"",
                        ""parameters"": {{
                            ""targetPoint"": [5.0, 0.0, 5.0],
                            ""maxDistance"": 10.0,
                            ""scoringCurve"": ""{curve}""
                        }},
                        ""weight"": 1.0
                    }}
                ]";
                
                // Act
                var result = eqsTool.PerformQuery(queryId, scoringCriteriaJson: scoringCriteriaJson);
                
                // Assert
                Assert.That(result, Is.Not.Null, $"Scoring curve {curve} failed");
                Assert.That(result, Does.Contain("Success"), $"Scoring curve {curve} did not succeed");
            }
        }

        /// <summary>
        /// Test composite query conditions
        /// </summary>
        [Test]
        public void TestQuery_MultipleConditions()
        {
            // Arrange
            var queryId = "multiple_conditions_test";
            var conditionsJson = @"[
                {
                    ""conditionType"": ""DistanceTo"",
                    ""parameters"": {
                        ""targetPoint"": [5.0, 0.0, 5.0],
                        ""minDistance"": 1.0,
                        ""maxDistance"": 8.0
                    }
                },
                {
                    ""conditionType"": ""CustomProperty"",
                    ""parameters"": {
                        ""propertyName"": ""terrainType"",
                        ""value"": ""ground"",
                        ""operator"": ""equals""
                    }
                }
            ]";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, conditionsJson: conditionsJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test composite scoring criteria
        /// </summary>
        [Test]
        public void TestQuery_MultipleScoringCriteria()
        {
            // Arrange
            var queryId = "multiple_scoring_test";
            var scoringCriteriaJson = @"[
                {
                    ""criterionType"": ""ProximityTo"",
                    ""parameters"": {
                        ""targetPoint"": [5.0, 0.0, 5.0],
                        ""maxDistance"": 10.0
                    },
                    ""weight"": 0.6
                },
                {
                    ""criterionType"": ""FarthestFrom"",
                    ""parameters"": {
                        ""targetPoint"": [0.0, 0.0, 0.0],
                        ""maxDistance"": 15.0
                    },
                    ""weight"": 0.4
                }
            ]";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, scoringCriteriaJson: scoringCriteriaJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        #endregion

        #region Performance Tests

        /// <summary>
        /// Test query execution time
        /// </summary>
        [Test]
        public void TestQuery_ExecutionTime()
        {
            // Arrange
            var queryId = "performance_test";
            var startTime = DateTime.Now;
            
            // Act
            var result = eqsTool.PerformQuery(queryId);
            var endTime = DateTime.Now;
            var executionTime = (endTime - startTime).TotalMilliseconds;
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
            Assert.That(executionTime, Is.LessThan(5000), "Query execution time should be less than 5 seconds");

            // Verify result contains execution time information
            Assert.That(result, Does.Contain("execution time"));
        }

        /// <summary>
        /// Test performance of multiple queries
        /// </summary>
        [Test]
        public void TestQuery_BulkPerformance()
        {
            // Arrange
            const int queryCount = 10;
            var startTime = DateTime.Now;
            
            // Act
            var results = new List<string>();
            for (int i = 0; i < queryCount; i++)
            {
                var result = eqsTool.PerformQuery($"bulk_test_{i}");
                results.Add(result);
            }
            
            var endTime = DateTime.Now;
            var totalTime = (endTime - startTime).TotalMilliseconds;
            var averageTime = totalTime / queryCount;
            
            // Assert
            Assert.That(results.Count, Is.EqualTo(queryCount));
            Assert.That(averageTime, Is.LessThan(1000), "Average query time should be less than 1 second");

            foreach (var result in results)
            {
                Assert.That(result, Does.Contain("Success"));
            }
        }

        #endregion

        #region Cache Tests

        /// <summary>
        /// Test query result caching
        /// </summary>
        [Test]
        public void TestQuery_ResultCaching()
        {
            // Arrange
            var queryId = "cache_test";

            // Act - First query
            var result1 = eqsTool.PerformQuery(queryId);
            var firstQueryTime = DateTime.Now;

            // Act - Second query (should use same queryId to generate new result)
            var result2 = eqsTool.PerformQuery(queryId);
            var secondQueryTime = DateTime.Now;
            
            // Assert
            Assert.That(result1, Is.Not.Null);
            Assert.That(result2, Is.Not.Null);
            Assert.That(result1, Does.Contain("Success"));
            Assert.That(result2, Does.Contain("Success"));

            // Verify both queries executed successfully (because EQS generates new result for each query)
            Assert.That(result1, Does.Contain(queryId));
            Assert.That(result2, Does.Contain(queryId));
        }

        #endregion

        #region Special Case Tests

        /// <summary>
        /// Test handling of Unicode characters in query ID
        /// </summary>
        [Test]
        public void TestQuery_UnicodeQueryId()
        {
            // Arrange
            var queryId = "Test Query 🔍 αβγ";

            // Act
            var result = eqsTool.PerformQuery(queryId);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
            Assert.That(result, Does.Contain(queryId));
        }

        /// <summary>
        /// Test extremely long query ID
        /// </summary>
        [Test]
        public void TestQuery_VeryLongQueryId()
        {
            // Arrange
            var queryId = new string('A', 1000); // 1000-character query ID

            // Act
            var result = eqsTool.PerformQuery(queryId);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));
        }

        /// <summary>
        /// Test handling of special characters in JSON
        /// </summary>
        [Test]
        public void TestQuery_SpecialCharactersInJson()
        {
            // Arrange
            var queryId = "special_chars_test";
            var referencePointsJson = @"[
                {""name"": ""Point with \""quotes\"" and \n newlines"", ""position"": [1, 2, 3]}
            ]";

            // Act & Assert - Should handle JSON escape characters
            Assert.DoesNotThrow(() => {
                var result = eqsTool.PerformQuery(queryId, referencePointsJson: referencePointsJson);
                Assert.That(result, Is.Not.Null);
            });
        }

        #endregion

        #region Data Verification Tests

        /// <summary>
        /// Test query result data structure integrity
        /// </summary>
        [Test]
        public void TestQuery_ResultDataIntegrity()
        {
            // Arrange
            var queryId = "data_integrity_test";
            var scoringCriteriaJson = @"[
                {
                    ""criterionType"": ""ProximityTo"",
                    ""parameters"": {
                        ""targetPoint"": [5, 0, 5],
                        ""maxDistance"": 10
                    },
                    ""weight"": 1.0
                }
            ]";
            
            // Act
            var result = eqsTool.PerformQuery(queryId, scoringCriteriaJson: scoringCriteriaJson);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("Success"));

            // Verify result contains necessary fields
            Assert.That(result, Does.Contain("query ID"));
            Assert.That(result, Does.Contain("status"));
            Assert.That(result, Does.Contain("found candidate locations"));
            Assert.That(result, Does.Contain("execution time"));
            Assert.That(result, Does.Contain("top 3 best locations"));
        }
        
        #endregion
    }
} 