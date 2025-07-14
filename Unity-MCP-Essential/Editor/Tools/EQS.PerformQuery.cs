#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using com.MiAO.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.MiAO.Unity.MCP.Essential.Tools
{
    public partial class Tool_EQS
    {
        [McpPluginTool
        (
            "EQS_PerformQuery",
            Title = "Perform EQS Query"
        )]
        [Description(@"EQS spatial query tool - Intelligent location selection and spatial reasoning

Executes complex spatial queries based on multi-dimensional conditions and scoring criteria, returning prioritized location candidates.

Query Process:
1. Area of Interest filtering - Narrow search scope
2. Hard condition filtering - Exclude locations that don't meet basic requirements
3. Soft scoring calculation - Multi-dimensional scoring of candidate locations
4. Weight synthesis - Calculate final scores based on weights
5. Sorted output - Return best locations sorted by score

Supported condition types: DistanceTo, Clearance, VisibilityOf, CustomProperty, ObjectProximity
Supported scoring criteria: ProximityTo, FarthestFrom, DensityOfObjects, HeightPreference, SlopeAnalysis, CoverQuality, PathComplexity, MultiPoint
Distance modes: euclidean, manhattan, horizontal, chebyshev
Scoring curves: linear, exponential, logarithmic, smoothstep, inverse")]
        public string PerformQuery
        (
            [Description("Unique identifier for the query")]
            string queryID,
            [Description("Target object type for the query (optional)")]
            string? targetObjectType = null,
            [Description("Reference points list. Format: [{\"name\":\"PlayerStart\",\"position\":[10,0,20]}]. Each point needs name and position[x,y,z] coordinates.")]
            string referencePointsJson = "[]",
            [Description("Area of interest definition. Sphere: {\"type\":\"sphere\",\"center\":[15,1,25],\"radius\":30}. Box: {\"type\":\"box\",\"center\":[15,1,25],\"size\":[20,10,20]}")]
            string? areaOfInterestJson = null,
            [Description("Query conditions array. DistanceTo: {\"conditionType\":\"DistanceTo\",\"parameters\":{\"targetPoint\":[10,0,20],\"minDistance\":5,\"maxDistance\":25,\"distanceMode\":\"euclidean\"}}. distanceMode options: euclidean|manhattan|chebyshev|horizontal|vertical|squared. Clearance: {\"conditionType\":\"Clearance\",\"parameters\":{\"requiredHeight\":2.0,\"requiredRadius\":0.5,\"obstacleLayers\":[\"Default\",\"Obstacle\",\"Terrain\"],\"obstacleLayerMask\":123}}. Optional: obstacleLayers (layer names array) or obstacleLayerMask (int mask value), defaults to common obstacle layers. VisibilityOf: {\"conditionType\":\"VisibilityOf\",\"parameters\":{\"targetPoint\":[15,1,25],\"eyeHeight\":1.7,\"maxViewAngle\":90,\"successThreshold\":0.8}}. CustomProperty: {\"conditionType\":\"CustomProperty\",\"parameters\":{\"propertyName\":\"terrainType\",\"expectedValue\":\"ground\",\"comparisonType\":\"equals\"}}. ObjectProximity: {\"conditionType\":\"ObjectProximity\",\"parameters\":{\"objectId\":\"12345\",\"proximityMode\":\"surface\",\"maxDistance\":5.0,\"minDistance\":1.0,\"colliderType\":\"any\"}}. proximityMode options: inside|outside|surface. colliderType options: any|trigger|solid")]
            string conditionsJson = "[]",
            [Description("Scoring criteria array. ProximityTo: {\"criterionType\":\"ProximityTo\",\"parameters\":{\"targetPoint\":[50,0,50],\"maxDistance\":100,\"scoringCurve\":\"linear\",\"distanceMode\":\"euclidean\"},\"weight\":0.7}. scoringCurve options: linear|exponential|logarithmic|smoothstep|inverse. distanceMode options: euclidean|manhattan|chebyshev|horizontal|vertical|squared. FarthestFrom: {\"criterionType\":\"FarthestFrom\",\"parameters\":{\"targetPoint\":[30,0,30],\"minDistance\":10,\"scoringCurve\":\"exponential\"},\"weight\":0.5}. scoringCurve options: linear|exponential|logarithmic|smoothstep|threshold. DensityOfObjects: {\"criterionType\":\"DensityOfObjects\",\"parameters\":{\"radius\":5,\"objectType\":\"Enemy\",\"densityMode\":\"inverse\",\"useDistanceWeighting\":true},\"weight\":0.6}. densityMode options: count|weighted|inverse. HeightPreference: {\"criterionType\":\"HeightPreference\",\"parameters\":{\"preferenceMode\":\"higher\",\"referenceHeight\":0,\"heightRange\":50},\"weight\":0.4}. preferenceMode options: higher|lower|specific|avoid. SlopeAnalysis: {\"criterionType\":\"SlopeAnalysis\",\"parameters\":{\"slopeMode\":\"flat\",\"tolerance\":10,\"sampleRadius\":2},\"weight\":0.3}. slopeMode options: flat|steep|specific. CoverQuality: {\"criterionType\":\"CoverQuality\",\"parameters\":{\"coverRadius\":3,\"coverMode\":\"omnidirectional\",\"minCoverHeight\":1.5},\"weight\":0.8}. coverMode options: omnidirectional|partial|majority. PathComplexity: {\"criterionType\":\"PathComplexity\",\"parameters\":{\"startPoint\":[25,0,25],\"complexityMode\":\"simple\",\"pathLength\":20},\"weight\":0.3}. complexityMode options: simple|complex. MultiPoint: {\"criterionType\":\"MultiPoint\",\"parameters\":{\"targetPoints\":[[10,0,10],[20,0,20]],\"multiMode\":\"average\",\"weights\":[0.6,0.4]},\"weight\":0.5}. multiMode options: average|weighted|minimum|maximum")]
            string scoringCriteriaJson = "[]",
            [Description("Desired number of results to return")] 
            int desiredResultCount = 10
        )
        => MainThread.Instance.Run(() =>
        {
            try
            {
                // Check if environment is initialized
                if (_currentEnvironment == null)
                {
                    return Error.EnvironmentNotInitialized();
                }

                var startTime = DateTime.Now;

                // Parse input parameters
                var query = new EQSQuery
                {
                    QueryID = queryID,
                    TargetObjectType = targetObjectType,
                    DesiredResultCount = desiredResultCount
                };

                // Parse reference point
                try
                {
                    var referencePoints = JsonUtils.Deserialize<List<Dictionary<string, object>>>(referencePointsJson);
                    foreach (var point in referencePoints)
                    {
                        var name = point.ContainsKey("name") ? point["name"].ToString() : "";
                        var positionArray = JsonUtils.Deserialize<float[]>(point["position"].ToString());
                        query.QueryContext.ReferencePoints.Add(new EQSReferencePoint
                        {
                            Name = name,
                            Position = new Vector3(positionArray[0], positionArray[1], positionArray[2])
                        });
                    }
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to parse reference point: {ex.Message}";
                }

                // Parse area of interest
                if (!string.IsNullOrEmpty(areaOfInterestJson))
                {
                    try
                                          {
                          var areaData = JsonUtils.Deserialize<Dictionary<string, object>>(areaOfInterestJson);
                          var areaOfInterest = ParseAreaOfInterest(areaData);
                          query.QueryContext.AreaOfInterest = areaOfInterest;
                    }
                    catch (Exception ex)
                    {
                        return $"[Error] Failed to parse area of interest: {ex.Message}";
                    }
                }

                // Parse query conditions
                try
                {
                    var conditions = JsonUtils.Deserialize<List<Dictionary<string, object>>>(conditionsJson);
                    foreach (var condition in conditions)
                    {
                        var eqsCondition = new EQSCondition
                        {
                            ConditionType = condition["conditionType"].ToString(),
                                            Weight = condition.ContainsKey("weight") ? ParseUtils.ParseFloat(condition["weight"]) : 1.0f,
                Invert = condition.ContainsKey("invert") && ParseUtils.ParseBool(condition["invert"])
                        };

                        if (condition.ContainsKey("parameters"))
                        {
                            var parameters = JsonUtils.Deserialize<Dictionary<string, object>>(condition["parameters"].ToString());
                            eqsCondition.Parameters = parameters;
                        }

                        query.Conditions.Add(eqsCondition);
                    }
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to parse query conditions: {ex.Message}";
                }

                // Parse scoring criteria
                try
                {
                    var scoringCriteria = JsonUtils.Deserialize<List<Dictionary<string, object>>>(scoringCriteriaJson);
                    foreach (var criterion in scoringCriteria)
                    {
                        var eqsCriterion = new EQSScoringCriterion
                        {
                            CriterionType = criterion["criterionType"].ToString(),
                            Weight = criterion.ContainsKey("weight") ? ParseUtils.ParseFloat(criterion["weight"]) : 1.0f,
                            NormalizationMethod = criterion.ContainsKey("normalizationMethod") ? criterion["normalizationMethod"].ToString() : "linear"
                        };

                        if (criterion.ContainsKey("parameters"))
                        {
                            var parameters = JsonUtils.Deserialize<Dictionary<string, object>>(criterion["parameters"].ToString());
                            eqsCriterion.Parameters = parameters;
                        }

                        query.ScoringCriteria.Add(eqsCriterion);
                    }
                }
                catch (Exception ex)
                {
                    return $"[Error] Failed to parse scoring criteria: {ex.Message}";
                }

                // Execute query
                var result = ExecuteQuery(query);
                
                // Cache results
                _queryCache[queryID] = result;

                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                result.ExecutionTimeMs = (float)executionTime;

                // Automatically create visualization (display green to red gradient based on scores)
                // Show all points that meet criteria, not just the top few
                if (result.Status == "Success" && result.Results.Count > 0)
                {
                    // Re-execute query to get all results for displaying all candidates
                    var allCandidatesResult = ExecuteQueryForVisualization(query);
                    AutoVisualizeQueryResults(allCandidatesResult);
                }

                // Create safe serialized version, avoiding Vector3 circular references
                var safeResult = new
                {
                    QueryID = result.QueryID,
                    Status = result.Status,
                    ErrorMessage = result.ErrorMessage,
                    ExecutionTimeMs = result.ExecutionTimeMs,
                    ResultsCount = result.Results.Count,
                    Results = result.Results.Take(5).Select(candidate => new
                    {
                        WorldPosition = new { x = candidate.WorldPosition.x, y = candidate.WorldPosition.y, z = candidate.WorldPosition.z },
                        Score = candidate.Score,
                        CellIndices = candidate.CellIndices.HasValue ? 
                            new { x = candidate.CellIndices.Value.x, y = candidate.CellIndices.Value.y, z = candidate.CellIndices.Value.z } : null,
                        BreakdownScores = candidate.BreakdownScores,
                        AssociatedObjectIDs = candidate.AssociatedObjectIDs
                    }).ToArray()
                };

                return @$"[Success] EQS query executed successfully.
# Query Results:
```json
{JsonUtils.Serialize(safeResult)}
```

# Result Summary:
- Query ID: {result.QueryID}
- Status: {result.Status}  
- Number of candidate locations found: {result.Results.Count}
- Execution time: {result.ExecutionTimeMs:F2}ms
- Auto visualization: {(result.Results.Count > 0 ? "Created" : "No results, not created")}

# Top 3 Best Locations:
{string.Join("\n", result.Results.Take(3).Select((candidate, index) => 
    $"#{index + 1}: Position({candidate.WorldPosition.x:F2}, {candidate.WorldPosition.y:F2}, {candidate.WorldPosition.z:F2}) Score:{candidate.Score:F3}"))}

# Visualization Legend:
- 🟢 Green = High score (0.7-1.0)
- 🟡 Yellow-green = Medium-high score (0.5-0.7)  
- 🟡 Yellow = Medium score (0.3-0.5)
- 🟠 Orange = Medium-low score (0.1-0.3)
- 🔴 Red = Low score (0.0-0.1)
- Gray = Unavailable
- All points meeting criteria will display corresponding colors
- Uniform size, no score text displayed
- Visualization persists permanently until manual cleanup or re-query";
            }
            catch (Exception ex)
            {
                return $"[Error] EQS query execution failed: {ex.Message}";
            }
        });

        /// <summary>
        /// Execute query for visualization (return all candidate points without limiting quantity)
        /// </summary>
        private static EQSQueryResult ExecuteQueryForVisualization(EQSQuery query)
        {
            if (_currentEnvironment == null)
            {
                return new EQSQueryResult
                {
                    QueryID = query.QueryID,
                    Status = "Failure",
                    ErrorMessage = "Environment not initialized"
                };
            }

            var candidates = new List<EQSLocationCandidate>();
            var grid = _currentEnvironment.Grid;

            // Filter points that meet criteria
            var validCells = FilterCells(grid.Cells, query);

            // Score each point that meets criteria
            foreach (var cell in validCells)
            {
                var candidate = new EQSLocationCandidate
                {
                    WorldPosition = cell.WorldPosition,
                    CellIndices = cell.Indices,
                    AssociatedObjectIDs = new List<string>(cell.DynamicOccupants)
                };

                var totalScore = 0f;
                var totalWeight = 0f;

                foreach (var criterion in query.ScoringCriteria)
                {
                    var score = CalculateScore(cell, criterion, query);
                    candidate.BreakdownScores[criterion.CriterionType] = score;
                    totalScore += score * criterion.Weight;
                    totalWeight += criterion.Weight;
                }

                candidate.Score = totalWeight > 0 ? totalScore / totalWeight : 0f;
                candidates.Add(candidate);
            }

            // Sort by score, but return all candidate points (no quantity limit)
            var sortedCandidates = candidates
                .OrderByDescending(c => c.Score)
                .ToList();

            return new EQSQueryResult
            {
                QueryID = query.QueryID,
                Status = sortedCandidates.Count > 0 ? "Success" : "Failure",
                Results = sortedCandidates,
                ErrorMessage = sortedCandidates.Count == 0 ? "No valid candidates found" : ""
            };
        }

        /// <summary>
        /// Core method for EQS query execution
        /// 
        /// Point selection logic explanation:
        /// 1. Filter candidate points from environment grid (FilterCells)
        /// 2. Perform multi-dimensional scoring for each candidate point (CalculateScore)
        /// 3. Calculate comprehensive score based on weights
        /// 4. Sort by score and return best points
        /// 
        /// This design allows complex spatial reasoning, such as:
        /// - Finding cover positions close to player but far from enemies
        /// - Selecting sniper points with good visibility and safety
        /// - Finding suitable locations for placing medical kits
        /// </summary>
        /// <param name="query">EQS query object containing all query parameters</param>
        /// <returns>Query result containing sorted candidate points</returns>
        private static EQSQueryResult ExecuteQuery(EQSQuery query)
        {
            if (_currentEnvironment == null)
            {
                return new EQSQueryResult
                {
                    QueryID = query.QueryID,
                    Status = "Failure",
                    ErrorMessage = "Environment not initialized"
                };
            }

            var candidates = new List<EQSLocationCandidate>();
            var grid = _currentEnvironment.Grid;

            // Phase 1: Candidate point filtering
            // Filter points that meet basic conditions from all grid cells
            // This step significantly reduces the number of points that need scoring, improving performance
            var validCells = FilterCells(grid.Cells, query);

            // Phase 2: Candidate point scoring
            // Perform multi-dimensional scoring for each point that passed filtering
            foreach (var cell in validCells)
            {
                var candidate = new EQSLocationCandidate
                {
                    WorldPosition = cell.WorldPosition,
                    CellIndices = cell.Indices,
                    AssociatedObjectIDs = new List<string>(cell.DynamicOccupants)
                };

                // Multi-dimensional scoring system:
                // Each scoring criterion calculates scores independently, then weighted average by weights
                // This allows complex decisions like "70% consider distance, 30% consider safety"
                var totalScore = 0f;
                var totalWeight = 0f;

                foreach (var criterion in query.ScoringCriteria)
                {
                    var score = CalculateScore(cell, criterion, query);
                    candidate.BreakdownScores[criterion.CriterionType] = score; // Save individual scores for debugging
                    totalScore += score * criterion.Weight; // Weighted accumulation
                    totalWeight += criterion.Weight;
                }

                // Calculate final score (weighted average)
                candidate.Score = totalWeight > 0 ? totalScore / totalWeight : 0f;
                candidates.Add(candidate);
            }

            // Phase 3: Result sorting and truncation
            // Sort by score from high to low, take top N best points
            var sortedCandidates = candidates
                .OrderByDescending(c => c.Score)
                .Take(query.DesiredResultCount)
                .ToList();

            return new EQSQueryResult
            {
                QueryID = query.QueryID,
                Status = sortedCandidates.Count > 0 ? "Success" : "Failure",
                Results = sortedCandidates,
                ErrorMessage = sortedCandidates.Count == 0 ? "No valid candidates found" : ""
            };
        }

        /// <summary>
        /// Candidate point filter - EQS's first screening mechanism
        /// 
        /// Filtering logic:
        /// 1. Area of interest filtering: Only consider points within specified area
        /// 2. Condition filtering: Each point must satisfy all specified conditions
        /// 
        /// Filter condition types:
        /// - DistanceTo: Distance constraints (e.g., 5-20 meters from player)
        /// - Clearance: Spatial clearance (e.g., requires 2 meters height space)
        /// - CustomProperty: Custom properties (e.g., terrain type is "grassland")
        /// - VisibilityOf: Line of sight visibility (e.g., can see target point)
        /// 
        /// This design ensures only truly feasible points enter the scoring phase
        /// </summary>
        /// <param name="cells">All grid cells</param>
        /// <param name="query">Query parameters</param>
        /// <returns>Array of valid cells that passed filtering</returns>
        private static EQSCell[] FilterCells(EQSCell[] cells, EQSQuery query)
        {
            var validCells = new List<EQSCell>();

            foreach (var cell in cells)
            {
                // Area of interest check: If area of interest is specified, only consider points within the area
                // This can significantly reduce computation, e.g., only search for points within 50 meters of player
                if (query.QueryContext.AreaOfInterest != null && !IsInAreaOfInterest(cell, query.QueryContext.AreaOfInterest))
                    continue;

                // Condition check: Points must satisfy all specified conditions
                // Uses "AND" logic: If any condition is not met, the point is excluded
                var passesAllConditions = true;
                foreach (var condition in query.Conditions)
                {
                    if (!EvaluateCondition(cell, condition, query))
                    {
                        passesAllConditions = false;
                        break; // Early exit optimization
                    }
                }

                if (passesAllConditions)
                    validCells.Add(cell);
            }

            return validCells.ToArray();
        }

        /// <summary>
        /// Check if point is within area of interest
        /// 
        /// Supported area types:
        /// - Sphere: Spherical area (center point + radius)
        /// - Box: Rectangular area (center point + size)
        /// 
        /// Purpose of area of interest:
        /// 1. Performance optimization: Reduce number of points to process
        /// 2. Logical constraints: Ensure results are within reasonable range
        /// Example: Find cover within 30 meters of player, not the entire map
        /// </summary>
        private static bool IsInAreaOfInterest(EQSCell cell, EQSAreaOfInterest areaOfInterest)
        {
            switch (areaOfInterest.Type.ToLower())
            {
                case "sphere":
                    return Vector3.Distance(cell.WorldPosition, areaOfInterest.Center) <= areaOfInterest.Radius;
                case "box":
                    var bounds = new Bounds(areaOfInterest.Center, areaOfInterest.Size);
                    return bounds.Contains(cell.WorldPosition);
                default:
                    return true; // Unknown type defaults to pass
            }
        }

        /// <summary>
        /// Evaluate whether a single condition is satisfied
        /// 
        /// Condition evaluation is EQS's core filtering mechanism, each condition type has different evaluation logic:
        /// 
        /// 1. DistanceTo: Distance constraints
        ///    - Purpose: Ensure points are within appropriate distance range
        ///    - Example: Medical kits should be 5-15 meters from player (too close is wasteful, too far is inconvenient)
        /// 
        /// 2. Clearance: Spatial clearance
        ///    - Purpose: Ensure points have sufficient activity space
        ///    - Example: Sniper positions need 2 meters height space to avoid hitting head
        /// 
        /// 3. CustomProperty: Custom properties
        ///    - Purpose: Filter based on terrain or environmental features
        ///    - Example: Only place picnic tables on "grassland" terrain
        /// 
        /// 4. VisibilityOf: Line of sight visibility
        ///    - Purpose: Ensure clear line of sight
        ///    - Example: Sentry positions must be able to see entrance
        /// </summary>
        /// <param name="cell">Grid cell to evaluate</param>
        /// <param name="condition">Evaluation condition</param>
        /// <param name="query">Query context</param>
        /// <returns>Whether condition is satisfied</returns>
        private static bool EvaluateCondition(EQSCell cell, EQSCondition condition, EQSQuery query)
        {
            bool result = false;

            switch (condition.ConditionType.ToLower())
            {
                case "distanceto":
                    result = EvaluateDistanceCondition(cell, condition);
                    break;
                case "clearance":
                    result = EvaluateClearanceCondition(cell, condition);
                    break;
                case "customproperty":
                    result = EvaluateCustomPropertyCondition(cell, condition);
                    break;
                case "visibilityof":
                    result = EvaluateVisibilityCondition(cell, condition);
                    break;
                case "objectproximity":
                    result = EvaluateObjectProximityCondition(cell, condition);
                    break;
                default:
                    result = true; // Unknown conditions default to pass
                    break;
            }

            // Support condition inversion: Sometimes we need points that "don't satisfy certain conditions"
            // Example: Find concealed positions that are "not within enemy line of sight"
            return condition.Invert ? !result : result;
        }

        /// <summary>
        /// Distance condition evaluation
        /// 
        /// Distance constraints are the most commonly used filtering conditions, supporting minimum and maximum distance limits:
        /// - minDistance: Minimum distance (avoid points too close)
        /// - maxDistance: Maximum distance (avoid points too far)
        /// 
        /// Application scenarios:
        /// - Cover positions: 10-30 meters from player (both safe and not too far)
        /// - Supply points: 20-50 meters from combat zone (safe resupply)
        /// - Patrol points: 50-100 meters from base (appropriate coverage range)
        /// </summary>
        private static bool EvaluateDistanceCondition(EQSCell cell, EQSCondition condition)
        {
            if (!condition.Parameters.ContainsKey("targetPoint"))
                return false;

            var targetPointArray = JsonUtils.Deserialize<float[]>(condition.Parameters["targetPoint"].ToString());
            var targetPoint = new Vector3(targetPointArray[0], targetPointArray[1], targetPointArray[2]);
            var distance = Vector3.Distance(cell.WorldPosition, targetPoint);

                                    var minDistance = condition.Parameters.ContainsKey("minDistance") ? 
                ParseUtils.ParseFloat(condition.Parameters["minDistance"]) : 0f;
            var maxDistance = condition.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(condition.Parameters["maxDistance"]) : float.MaxValue;

            return distance >= minDistance && distance <= maxDistance;
        }

        /// <summary>
        /// Spatial clearance condition evaluation - Complete implementation
        /// 
        /// Clearance check ensures points have sufficient activity space:
        /// - requiredHeight: Required vertical space (default 2 meters)
        /// - requiredRadius: Required horizontal space (default 0.5 meters)
        /// - obstacleLayerMask: Layer mask for obstacle detection (default: common obstacle layers)
        /// 
        /// Complete implementation includes:
        /// 1. Vertical space check (upward raycast)
        /// 2. Horizontal space check (multi-directional raycast)
        /// 3. Basic walkability check
        /// </summary>
        private static bool EvaluateClearanceCondition(EQSCell cell, EQSCondition condition)
        {
            var requiredHeight = condition.Parameters.ContainsKey("requiredHeight") ? 
                ParseUtils.ParseFloat(condition.Parameters["requiredHeight"]) : 2f;
            var requiredRadius = condition.Parameters.ContainsKey("requiredRadius") ? 
                ParseUtils.ParseFloat(condition.Parameters["requiredRadius"]) : 0.5f;

            // Get obstacle layer mask - support custom configuration
            var obstacleLayerMask = GetObstacleLayerMask(condition);

            // Basic check: Cannot have static occupancy and must be walkable
            if (cell.StaticOccupancy || !(bool)cell.Properties.GetValueOrDefault("isWalkable", false))
                return false;

            var position = cell.WorldPosition;

            // Vertical space check: Cast ray upward from current position
            if (Physics.Raycast(position, Vector3.up, requiredHeight, obstacleLayerMask))
            {
                return false; // Obstacle above
            }

            // Horizontal space check: Check horizontal clearance in 8 directions
            var directions = new Vector3[]
            {
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                Vector3.forward + Vector3.right, Vector3.forward + Vector3.left,
                Vector3.back + Vector3.right, Vector3.back + Vector3.left
            };

            foreach (var direction in directions)
            {
                var normalizedDir = direction.normalized;
                if (Physics.Raycast(position, normalizedDir, requiredRadius, obstacleLayerMask))
                {
                    return false; // Horizontal obstacle
                }
            }

            // Ground check: Ensure there's support underfoot
            if (!Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, 0.5f, obstacleLayerMask))
            {
                return false; // No ground underfoot
            }

            return true;
        }

        /// <summary>
        /// Get obstacle layer mask for clearance detection
        /// </summary>
        private static LayerMask GetObstacleLayerMask(EQSCondition condition)
        {
            // Check if custom layer mask is specified
            if (condition.Parameters.ContainsKey("obstacleLayerMask"))
            {
                var layerMaskValue = ParseUtils.ParseInt(condition.Parameters["obstacleLayerMask"]);
                return layerMaskValue;
            }
            
            // Check if layer names are specified
            if (condition.Parameters.ContainsKey("obstacleLayers"))
            {
                var layerNames = JsonUtils.Deserialize<string[]>(condition.Parameters["obstacleLayers"].ToString());
                if (layerNames != null && layerNames.Length > 0)
                {
                    return LayerMask.GetMask(layerNames);
                }
            }
            
            // Default: Use common obstacle layers
            // Include typical layers that might contain obstacles
            try
            {
                return LayerMask.GetMask("Default", "Obstacle", "Terrain", "Building", "Wall", "Ground");
            }
            catch
            {
                // If some layers don't exist, fall back to Default + try common ones individually
                LayerMask mask = LayerMask.GetMask("Default");
                
                var commonLayers = new[] { "Obstacle", "Terrain", "Building", "Wall", "Ground", "Static" };
                foreach (var layerName in commonLayers)
                {
                    try
                    {
                        var layerIndex = LayerMask.NameToLayer(layerName);
                        if (layerIndex >= 0)
                        {
                            mask |= (1 << layerIndex);
                        }
                    }
                    catch
                    {
                        // Ignore layers that don't exist
                    }
                }
                
                return mask;
            }
        }

        /// <summary>
        /// Custom property condition evaluation
        /// 
        /// Allows filtering based on custom properties of grid cells:
        /// - propertyName: Property name
        /// - value: Expected value
        /// - operator: Comparison operator (equals, contains, etc.)
        /// 
        /// Application scenarios:
        /// - Terrain type filtering: Only place tents on "grassland"
        /// - Height filtering: Only set watchtowers on "highland"
        /// - Security level: Only place supplies in "safe zones"
        /// 
        /// This provides high flexibility to customize various filtering logic based on game requirements
        /// </summary>
        private static bool EvaluateCustomPropertyCondition(EQSCell cell, EQSCondition condition)
        {
            if (!condition.Parameters.ContainsKey("propertyName"))
                return false;

            var propertyName = condition.Parameters["propertyName"].ToString();
            if (!cell.Properties.ContainsKey(propertyName))
                return false;

            var propertyValue = cell.Properties[propertyName];
            var expectedValue = condition.Parameters.GetValueOrDefault("value");
            var operatorType = condition.Parameters.GetValueOrDefault("operator", "equals").ToString().ToLower();

            switch (operatorType)
            {
                case "equals":
                    return propertyValue.Equals(expectedValue);
                case "contains":
                    return propertyValue.ToString().Contains(expectedValue.ToString());
                default:
                    return true;
            }
        }

        /// <summary>
        /// Line of sight visibility condition evaluation - Complete implementation
        /// 
        /// Checks if target position is visible from current location, considering visual obstacles.
        /// 
        /// Complete implementation includes:
        /// 1. Raycast line of sight obstacle detection
        /// 2. View angle limitations (optional)
        /// 3. Multi-point sampling for improved accuracy
        /// 4. Height offset (eye position)
        /// 
        /// Application scenarios:
        /// - Sentry positions: Must be able to see key entrances
        /// - Sniper points: Need clear line of sight to target area
        /// - Watchtowers: Require 360-degree view or specific directional view
        /// </summary>
        private static bool EvaluateVisibilityCondition(EQSCell cell, EQSCondition condition)
        {
            if (!condition.Parameters.ContainsKey("targetPoint"))
                return false;

            var targetPointArray = JsonUtils.Deserialize<float[]>(condition.Parameters["targetPoint"].ToString());
            var targetPoint = new Vector3(targetPointArray[0], targetPointArray[1], targetPointArray[2]);
            
            // Observer height offset (simulating eye position)
            var eyeHeight = condition.Parameters.ContainsKey("eyeHeight") ? 
                ParseUtils.ParseFloat(condition.Parameters["eyeHeight"]) : 1.7f;
            var observerPosition = cell.WorldPosition + Vector3.up * eyeHeight;
            
            // Target height offset (optional)
            var targetHeight = condition.Parameters.ContainsKey("targetHeight") ? 
                ParseUtils.ParseFloat(condition.Parameters["targetHeight"]) : 0f;
            var adjustedTargetPoint = targetPoint + Vector3.up * targetHeight;
            
            // View angle limitation (optional)
            var maxViewAngle = condition.Parameters.ContainsKey("maxViewAngle") ? 
                ParseUtils.ParseFloat(condition.Parameters["maxViewAngle"]) : 360f;
            
            // View direction (optional, used to limit view angle)
            Vector3 viewDirection = Vector3.forward;
            if (condition.Parameters.ContainsKey("viewDirection"))
            {
                var viewDirArray = JsonUtils.Deserialize<float[]>(condition.Parameters["viewDirection"].ToString());
                viewDirection = new Vector3(viewDirArray[0], viewDirArray[1], viewDirArray[2]).normalized;
            }
            
            var directionToTarget = (adjustedTargetPoint - observerPosition).normalized;
            var distance = Vector3.Distance(observerPosition, adjustedTargetPoint);
            
            // Check view angle limitation
            if (maxViewAngle < 360f)
            {
                var angle = Vector3.Angle(viewDirection, directionToTarget);
                if (angle > maxViewAngle / 2f)
                    return false; // Outside view angle
            }
            
            // Multi-point sampling for line of sight check (improved accuracy)
            var sampleCount = condition.Parameters.ContainsKey("sampleCount") ? 
                ParseUtils.ParseInt(condition.Parameters["sampleCount"]) : 3;
            
            var successfulSamples = 0;
            var requiredSuccessRate = condition.Parameters.ContainsKey("requiredSuccessRate") ? 
                ParseUtils.ParseFloat(condition.Parameters["requiredSuccessRate"]) : 0.6f;
            
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 sampleTarget = adjustedTargetPoint;
                
                // Add small random offset for multi-point sampling
                if (sampleCount > 1)
                {
                    var randomOffset = UnityEngine.Random.insideUnitSphere * 0.5f;
                    randomOffset.y = 0; // Only offset on horizontal plane
                    sampleTarget += randomOffset;
                }
                
                var sampleDirection = (sampleTarget - observerPosition).normalized;
                var sampleDistance = Vector3.Distance(observerPosition, sampleTarget);
                
                // Raycast line of sight check
                if (!Physics.Raycast(observerPosition, sampleDirection, sampleDistance, 
                    LayerMask.GetMask("Default")))
                {
                    successfulSamples++;
                }
            }
            
            // Check if success rate meets requirements
            var successRate = (float)successfulSamples / sampleCount;
            return successRate >= requiredSuccessRate;
        }

        /// <summary>
        /// Object proximity condition evaluation - Complete implementation
        /// 
        /// Checks spatial relationship between position and specified object:
        /// - inside: Whether point is inside the object
        /// - outside: Whether point is outside the object
        /// - surface: Whether point is within specified distance range from object surface
        /// 
        /// Supports multiple collider type detection, suitable for:
        /// - Building interior position queries (inside mode)
        /// - Safe zone perimeter queries (outside + maxDistance)
        /// - Object surface proximity queries (surface mode)
        /// - Avoidance zone settings (outside + minDistance)
        /// 
        /// Implementation details:
        /// 1. Find target GameObject by InstanceID or name
        /// 2. Filter colliders by colliderType
        /// 3. Use Physics queries to detect spatial relationships
        /// 4. Support distance threshold control
        /// </summary>
        private static bool EvaluateObjectProximityCondition(EQSCell cell, EQSCondition condition)
        {
            // Get target object
            GameObject targetObject = null;
            
            // Prioritize using objectId (InstanceID)
            if (condition.Parameters.ContainsKey("objectId"))
            {
                var objectIdStr = condition.Parameters["objectId"].ToString();
                if (int.TryParse(objectIdStr, out int instanceId))
                {
                    targetObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                }
            }
            
            // If not found by ID, try finding by name
            if (targetObject == null && condition.Parameters.ContainsKey("objectName"))
            {
                var objectName = condition.Parameters["objectName"].ToString();
                targetObject = GameObject.Find(objectName);
            }
            
            if (targetObject == null)
            {
                Debug.LogWarning($"[EQS] ObjectProximity condition: Cannot find target object");
                return false;
            }
            
            // Get parameters
            var proximityMode = condition.Parameters.ContainsKey("proximityMode") ? 
                condition.Parameters["proximityMode"].ToString().ToLower() : "surface";
            
            var maxDistance = condition.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(condition.Parameters["maxDistance"]) : 5f;
            
            var minDistance = condition.Parameters.ContainsKey("minDistance") ? 
                ParseUtils.ParseFloat(condition.Parameters["minDistance"]) : 0f;
            
            var colliderType = condition.Parameters.ContainsKey("colliderType") ? 
                condition.Parameters["colliderType"].ToString().ToLower() : "any";
            
            // Get target object colliders
            var colliders = GetObjectColliders(targetObject, colliderType);
            if (colliders.Length == 0)
            {
                Debug.LogWarning($"[EQS] ObjectProximity condition: Target object '{targetObject.name}' has no suitable colliders");
                return false;
            }
            
            var checkPosition = cell.WorldPosition;
            
            switch (proximityMode)
            {
                case "inside":
                    return IsPositionInsideColliders(checkPosition, colliders);
                
                case "outside":
                    var isInside = IsPositionInsideColliders(checkPosition, colliders);
                    if (isInside)
                        return false; // Inside, doesn't meet outside condition
                    
                    // Check distance limitation
                    if (maxDistance > 0)
                    {
                        var distanceToSurface = GetDistanceToCollidersSurface(checkPosition, colliders);
                        return distanceToSurface >= minDistance && distanceToSurface <= maxDistance;
                    }
                    
                    return true; // Outside and no distance limitation
                
                case "surface":
                    var surfaceDistance = GetDistanceToCollidersSurface(checkPosition, colliders);
                    return surfaceDistance >= minDistance && surfaceDistance <= maxDistance;
                
                default:
                    Debug.LogWarning($"[EQS] ObjectProximity condition: Unknown proximityMode '{proximityMode}'");
                    return false;
            }
        }
        
        /// <summary>
        /// Get object colliders by type
        /// </summary>
        private static Collider[] GetObjectColliders(GameObject targetObject, string colliderType)
        {
            var allColliders = targetObject.GetComponentsInChildren<Collider>();
            
            switch (colliderType)
            {
                case "trigger":
                    return allColliders.Where(c => c.isTrigger).ToArray();
                
                case "solid":
                    return allColliders.Where(c => !c.isTrigger).ToArray();
                
                case "any":
                default:
                    return allColliders;
            }
        }
        
        /// <summary>
        /// Check if position is inside colliders
        /// </summary>
        private static bool IsPositionInsideColliders(Vector3 position, Collider[] colliders)
        {
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;
                
                // Use Bounds.Contains for fast pre-check
                if (!collider.bounds.Contains(position))
                    continue;
                
                // Use ClosestPoint for precise check
                var closestPoint = collider.ClosestPoint(position);
                var distance = Vector3.Distance(position, closestPoint);
                
                // If distance is very small, consider it inside
                if (distance < 0.01f)
                {
                    // Further check: if closestPoint is same as position, then position is inside collider
                    if (Vector3.Distance(position, closestPoint) < 0.001f)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Calculate shortest distance from position to colliders surface
        /// </summary>
        private static float GetDistanceToCollidersSurface(Vector3 position, Collider[] colliders)
        {
            var minDistance = float.MaxValue;
            
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;
                
                var closestPoint = collider.ClosestPoint(position);
                var distance = Vector3.Distance(position, closestPoint);
                
                minDistance = Mathf.Min(minDistance, distance);
            }
            
            return minDistance == float.MaxValue ? 0f : minDistance;
        }

        /// <summary>
        /// Calculate score for a point under specific scoring criteria
        /// 
        /// Scoring system is EQS core, different from filtering (binary judgment), scoring provides continuous values:
        /// 
        /// 1. ProximityTo: Proximity scoring
        ///    - Closer to target point, higher score
        ///    - Used for: Finding nearest cover, supply points, etc.
        /// 
        /// 2. FarthestFrom: Distance scoring
        ///    - Farther from target point, higher score
        ///    - Used for: Avoiding danger zones, finding safe positions
        /// 
        /// 3. DensityOfObjects: Object density scoring
        ///    - Score based on surrounding object count
        ///    - Used for: Avoiding crowded areas or finding active areas
        /// 
        /// Score range is typically 0-1, convenient for weight calculation and comparison
        /// </summary>
        /// <param name="cell">Grid cell to score</param>
        /// <param name="criterion">Scoring criterion</param>
        /// <param name="query">Query context</param>
        /// <returns>Score in 0-1 range</returns>
        private static float CalculateScore(EQSCell cell, EQSScoringCriterion criterion, EQSQuery query)
        {
            switch (criterion.CriterionType.ToLower())
            {
                case "proximityto":
                    return CalculateProximityScore(cell, criterion);
                case "farthestfrom":
                    return CalculateFarthestScore(cell, criterion);
                case "densityofobjects":
                    return CalculateDensityScore(cell, criterion);
                case "heightpreference":
                    return CalculateHeightPreferenceScore(cell, criterion);
                case "slopeanalysis":
                    return CalculateSlopeAnalysisScore(cell, criterion);
                case "coverquality":
                    return CalculateCoverQualityScore(cell, criterion);
                case "pathcomplexity":
                    return CalculatePathComplexityScore(cell, criterion);
                case "multipoint":
                    return CalculateMultiPointScore(cell, criterion);
                default:
                    return 0.5f; // Unknown type returns medium score
            }
        }

        /// <summary>
        /// Proximity score calculation - Complete implementation
        /// 
        /// Scoring logic: Closer to target point, higher score
        /// Supports multiple distance calculation modes and scoring curves
        /// 
        /// This scoring is suitable for:
        /// - Medical kit placement: Prioritize positions near injured players
        /// - Cover selection: Choose safe points closest to current position
        /// - Resource collection: Prioritize building positions near resource points
        /// 
        /// Complete implementation includes:
        /// 1. Multiple distance calculation modes (Euclidean, Manhattan, Chebyshev)
        /// 2. Configurable scoring curves (linear, exponential, logarithmic)
        /// 3. Optimal distance range settings
        /// 4. Multi-target point support
        /// </summary>
        private static float CalculateProximityScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            if (!criterion.Parameters.ContainsKey("targetPoint"))
                return 0f;

            var targetPointArray = JsonUtils.Deserialize<float[]>(criterion.Parameters["targetPoint"].ToString());
            var targetPoint = new Vector3(targetPointArray[0], targetPointArray[1], targetPointArray[2]);
            
            // Distance calculation mode
            var distanceMode = criterion.Parameters.ContainsKey("distanceMode") ? 
                criterion.Parameters["distanceMode"].ToString().ToLower() : "euclidean";
            
            // Scoring curve type
            var scoringCurve = criterion.Parameters.ContainsKey("scoringCurve") ? 
                criterion.Parameters["scoringCurve"].ToString().ToLower() : "linear";
            
            // Maximum distance (for normalization)
            var maxDistance = criterion.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["maxDistance"]) : 100f;
            
            // Optimal distance (gets highest score at this distance)
            var optimalDistance = criterion.Parameters.ContainsKey("optimalDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["optimalDistance"]) : 0f;
            
            // Calculate distance
            var distance = MathUtils.CalculateDistance(cell.WorldPosition, targetPoint, distanceMode);
            
            // Handle optimal distance case
            if (optimalDistance > 0)
            {
                // If optimal distance is set, closer to optimal distance gets higher score
                var distanceFromOptimal = Mathf.Abs(distance - optimalDistance);
                distance = distanceFromOptimal;
                maxDistance = Mathf.Max(maxDistance - optimalDistance, optimalDistance);
            }
            
            // Normalize distance
            var normalizedDistance = Mathf.Clamp01(distance / maxDistance);
            
            // Calculate final score based on scoring curve
            float score = 0f;
            
            switch (scoringCurve)
            {
                case "linear":
                    score = 1f - normalizedDistance;
                    break;
                case "exponential":
                    // Exponential decay: Score drops rapidly as distance increases
                    var exponentialFactor = criterion.Parameters.ContainsKey("exponentialFactor") ? 
                        ParseUtils.ParseFloat(criterion.Parameters["exponentialFactor"]) : 2f;
                    score = Mathf.Pow(1f - normalizedDistance, exponentialFactor);
                    break;
                case "logarithmic":
                    // Logarithmic decay: Score drops slowly as distance increases
                    score = 1f - Mathf.Log(1f + normalizedDistance * 9f) / Mathf.Log(10f);
                    break;
                case "smoothstep":
                    // Smooth step: Changes fastest in middle range
                    score = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
                    break;
                case "inverse":
                    // Inverse decay
                    score = 1f / (1f + normalizedDistance * normalizedDistance);
                    break;
                default:
                    score = 1f - normalizedDistance;
                    break;
            }
            
            return Mathf.Clamp01(score);
        }

        /// <summary>
        /// Distance score calculation - Complete implementation
        /// 
        /// Scoring logic: Farther from target point, higher score
        /// Supports multiple distance calculation modes and scoring curves
        /// 
        /// This scoring is suitable for:
        /// - Safe position selection: Stay away from enemies or danger zones
        /// - Distributed deployment: Avoid resource over-concentration
        /// - Retreat routes: Choose paths away from combat zones
        /// 
        /// Opposite to proximity scoring, demonstrating EQS system flexibility
        /// </summary>
        private static float CalculateFarthestScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            if (!criterion.Parameters.ContainsKey("targetPoint"))
                return 0f;

            var targetPointArray = JsonUtils.Deserialize<float[]>(criterion.Parameters["targetPoint"].ToString());
            var targetPoint = new Vector3(targetPointArray[0], targetPointArray[1], targetPointArray[2]);
            
            // Distance calculation mode
            var distanceMode = criterion.Parameters.ContainsKey("distanceMode") ? 
                criterion.Parameters["distanceMode"].ToString().ToLower() : "euclidean";
            
            // Scoring curve type
            var scoringCurve = criterion.Parameters.ContainsKey("scoringCurve") ? 
                criterion.Parameters["scoringCurve"].ToString().ToLower() : "linear";
            
            // Maximum distance (for normalization)
            var maxDistance = criterion.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["maxDistance"]) : 100f;
            
            // Minimum effective distance (below this distance score is 0)
            var minDistance = criterion.Parameters.ContainsKey("minDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["minDistance"]) : 0f;
            
            // Calculate distance
            var distance = MathUtils.CalculateDistance(cell.WorldPosition, targetPoint, distanceMode);
            
            // Apply minimum distance limitation
            if (distance < minDistance)
                return 0f;
            
            // Normalize distance
            var effectiveDistance = distance - minDistance;
            var effectiveMaxDistance = maxDistance - minDistance;
            var normalizedDistance = Mathf.Clamp01(effectiveDistance / effectiveMaxDistance);
            
            // Calculate final score based on scoring curve
            float score = 0f;
            
            switch (scoringCurve)
            {
                case "linear":
                    score = normalizedDistance;
                    break;
                case "exponential":
                    var exponentialFactor = criterion.Parameters.ContainsKey("exponentialFactor") ? 
                        ParseUtils.ParseFloat(criterion.Parameters["exponentialFactor"]) : 2f;
                    score = Mathf.Pow(normalizedDistance, 1f / exponentialFactor);
                    break;
                case "logarithmic":
                    score = Mathf.Log(1f + normalizedDistance * 9f) / Mathf.Log(10f);
                    break;
                case "smoothstep":
                    score = Mathf.SmoothStep(0f, 1f, normalizedDistance);
                    break;
                case "threshold":
                    var threshold = criterion.Parameters.ContainsKey("threshold") ? 
                        ParseUtils.ParseFloat(criterion.Parameters["threshold"]) : 0.5f;
                    score = normalizedDistance >= threshold ? 1f : 0f;
                    break;
                default:
                    score = normalizedDistance;
                    break;
            }
            
            return Mathf.Clamp01(score);
        }
        


        /// <summary>
        /// Object density score calculation - Complete implementation
        /// 
        /// Scoring logic: Score based on count and type of dynamic objects within specified radius
        /// 
        /// Application scenarios:
        /// 1. High density preference:
        ///    - Shop locations: Choose areas with high foot traffic
        ///    - Gathering points: Choose positions easy to congregate
        /// 
        /// 2. Low density preference (implemented through negative weights):
        ///    - Hidden positions: Avoid crowded areas
        ///    - Quiet areas: Stay away from noisy places
        /// 
        /// Complete implementation includes:
        /// 1. 3D spatial search within specified radius
        /// 2. Object type filtering
        /// 3. Distance weight decay
        /// 4. Multiple density calculation modes
        /// </summary>
        private static float CalculateDensityScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            var radius = criterion.Parameters.ContainsKey("radius") ? 
                ParseUtils.ParseFloat(criterion.Parameters["radius"]) : 5f;
            
            var maxDensity = criterion.Parameters.ContainsKey("maxDensity") ? 
                ParseUtils.ParseFloat(criterion.Parameters["maxDensity"]) : 5f;
            
            var objectTypeFilter = criterion.Parameters.ContainsKey("objectType") ? 
                criterion.Parameters["objectType"].ToString() : null;
            
            var useDistanceWeighting = criterion.Parameters.ContainsKey("useDistanceWeighting") ? 
                ParseUtils.ParseBool(criterion.Parameters["useDistanceWeighting"]) : true;
            
            var densityMode = criterion.Parameters.ContainsKey("densityMode") ? 
                criterion.Parameters["densityMode"].ToString().ToLower() : "count";
            
            if (_currentEnvironment == null)
                return 0f;
            
            var grid = _currentEnvironment.Grid;
            var cellPosition = cell.WorldPosition;
            var totalDensity = 0f;
            
            // Calculate grid cells within search range
            var searchRadiusInCells = Mathf.CeilToInt(radius / grid.CellSize);
            var cellIndices = cell.Indices;
            
            for (int x = -searchRadiusInCells; x <= searchRadiusInCells; x++)
            {
                for (int y = -searchRadiusInCells; y <= searchRadiusInCells; y++)
                {
                    for (int z = -searchRadiusInCells; z <= searchRadiusInCells; z++)
                    {
                        var checkIndices = new Vector3Int(
                            cellIndices.x + x,
                            cellIndices.y + y,
                            cellIndices.z + z
                        );
                        
                        // Check if indices are within grid bounds
                        if (checkIndices.x < 0 || checkIndices.x >= grid.Dimensions.x ||
                            checkIndices.y < 0 || checkIndices.y >= grid.Dimensions.y ||
                            checkIndices.z < 0 || checkIndices.z >= grid.Dimensions.z)
                            continue;
                        
                        var checkCellIndex = MathUtils.CoordinateToIndex(checkIndices, grid.Dimensions);
                        if (checkCellIndex >= grid.Cells.Length)
                            continue;
                        
                        var checkCell = grid.Cells[checkCellIndex];
                        var distance = Vector3.Distance(cellPosition, checkCell.WorldPosition);
                        
                        // Check if within search radius
                        if (distance > radius)
                            continue;
                        
                        // Calculate this cell's contribution
                        var cellContribution = CalculateCellDensityContribution(
                            checkCell, distance, objectTypeFilter, useDistanceWeighting, densityMode);
                        
                        totalDensity += cellContribution;
                    }
                }
            }
            
            // Final calculation based on density mode
            float finalScore = 0f;
            
            switch (densityMode)
            {
                case "count":
                    finalScore = totalDensity / maxDensity;
                    break;
                case "weighted":
                    finalScore = totalDensity / maxDensity;
                    break;
                case "inverse":
                    // Inverse density: Lower density gives higher score
                    finalScore = 1f - (totalDensity / maxDensity);
                    break;
                default:
                    finalScore = totalDensity / maxDensity;
                    break;
            }
            
            return Mathf.Clamp01(finalScore);
        }
        
        /// <summary>
        /// Calculate single grid cell's contribution to density
        /// </summary>
        private static float CalculateCellDensityContribution(EQSCell cell, float distance, 
            string objectTypeFilter, bool useDistanceWeighting, string densityMode)
        {
            var contribution = 0f;
            
            // Calculate dynamic object contribution
            foreach (var objectId in cell.DynamicOccupants)
            {
                // Object type filtering
                if (!string.IsNullOrEmpty(objectTypeFilter))
                {
                    // Here we need to get object type based on actual situation
                    // Simplified implementation: assume object ID contains type info or lookup from environment
                    var dynamicObj = _currentEnvironment.DynamicObjects
                        .FirstOrDefault(obj => obj.Id == objectId);
                    
                    if (dynamicObj != null && dynamicObj.Type != objectTypeFilter)
                        continue; // Mismatched object type
                }
                
                var objectContribution = 1f;
                
                // Apply distance weight decay
                if (useDistanceWeighting && distance > 0)
                {
                    // Use inverse square decay
                    objectContribution = 1f / (1f + distance * distance);
                }
                
                contribution += objectContribution;
            }
            
            // Consider static geometry influence (optional)
            if (cell.StaticOccupancy)
            {
                var staticContribution = 0.1f; // Base contribution value for static objects
                
                if (useDistanceWeighting && distance > 0)
                {
                    staticContribution = staticContribution / (1f + distance * distance);
                }
                
                contribution += staticContribution;
            }
            
            return contribution;
        }
        

        
        /// <summary>
        /// Height preference score calculation
        /// 
        /// Score based on position height, supporting multiple height preference modes:
        /// - High ground preference: Higher altitude gives higher score (watchtowers, sniper positions)
        /// - Low ground preference: Lower altitude gives higher score (concealment, shelter)
        /// - Specific height: Closer to target height gives higher score
        /// </summary>
        private static float CalculateHeightPreferenceScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            var preferenceMode = criterion.Parameters.ContainsKey("preferenceMode") ? 
                criterion.Parameters["preferenceMode"].ToString().ToLower() : "higher";
            
            var referenceHeight = criterion.Parameters.ContainsKey("referenceHeight") ? 
                ParseUtils.ParseFloat(criterion.Parameters["referenceHeight"]) : 0f;
            
            var heightRange = criterion.Parameters.ContainsKey("heightRange") ? 
                ParseUtils.ParseFloat(criterion.Parameters["heightRange"]) : 100f;
            
            var cellHeight = cell.WorldPosition.y;
            
            switch (preferenceMode)
            {
                case "higher":
                    // Higher is better
                    return Mathf.Clamp01((cellHeight - referenceHeight) / heightRange);
                
                case "lower":
                    // Lower is better
                    return Mathf.Clamp01((referenceHeight - cellHeight) / heightRange);
                
                case "specific":
                    // Closer to specific height is better
                    var heightDiff = Mathf.Abs(cellHeight - referenceHeight);
                    return Mathf.Clamp01(1f - (heightDiff / heightRange));
                
                case "avoid":
                    // Avoid specific height
                    var avoidDiff = Mathf.Abs(cellHeight - referenceHeight);
                    return Mathf.Clamp01(avoidDiff / heightRange);
                
                default:
                    return 0.5f;
            }
        }
        
        /// <summary>
        /// Slope analysis score calculation
        /// 
        /// Analyze terrain slope, suitable for:
        /// - Flat terrain preference (construction, parking)
        /// - Sloped terrain preference (skiing, drainage)
        /// - Specific slope requirements
        /// </summary>
        private static float CalculateSlopeAnalysisScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            var preferredSlope = criterion.Parameters.ContainsKey("preferredSlope") ? 
                ParseUtils.ParseFloat(criterion.Parameters["preferredSlope"]) : 0f;
            
            var slopeMode = criterion.Parameters.ContainsKey("slopeMode") ? 
                criterion.Parameters["slopeMode"].ToString().ToLower() : "flat";
            
            var tolerance = criterion.Parameters.ContainsKey("tolerance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["tolerance"]) : 10f;
            
            // Simplified slope calculation: check height differences of surrounding cells
            if (_currentEnvironment == null)
                return 0.5f;
            
            var grid = _currentEnvironment.Grid;
            var cellHeight = cell.WorldPosition.y;
            var heightDifferences = new List<float>();
            
            // Check adjacent cells
            var directions = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
            };
            
            foreach (var dir in directions)
            {
                var neighborIndices = cell.Indices + dir;
                if (neighborIndices.x >= 0 && neighborIndices.x < grid.Dimensions.x &&
                    neighborIndices.z >= 0 && neighborIndices.z < grid.Dimensions.z)
                {
                    var neighborIndex = MathUtils.CoordinateToIndex(neighborIndices, grid.Dimensions);
                    if (neighborIndex < grid.Cells.Length)
                    {
                        var neighborHeight = grid.Cells[neighborIndex].WorldPosition.y;
                        heightDifferences.Add(Mathf.Abs(cellHeight - neighborHeight));
                    }
                }
            }
            
            if (heightDifferences.Count == 0)
                return 0.5f;
            
            var averageSlope = heightDifferences.Average();
            var slopeAngle = Mathf.Atan(averageSlope / grid.CellSize) * Mathf.Rad2Deg;
            
            switch (slopeMode)
            {
                case "flat":
                    // Flat terrain preference
                    return Mathf.Clamp01(1f - (slopeAngle / tolerance));
                
                case "steep":
                    // Steep terrain preference
                    return Mathf.Clamp01(slopeAngle / tolerance);
                
                case "specific":
                    // Specific slope preference
                    var slopeDiff = Mathf.Abs(slopeAngle - preferredSlope);
                    return Mathf.Clamp01(1f - (slopeDiff / tolerance));
                
                default:
                    return 0.5f;
            }
        }
        
        /// <summary>
        /// Cover quality score calculation
        /// 
        /// Evaluate position's cover value:
        /// - Surrounding obstacle density
        /// - Line of sight obstruction level
        /// - Multi-directional protection
        /// </summary>
        private static float CalculateCoverQualityScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            var coverRadius = criterion.Parameters.ContainsKey("coverRadius") ? 
                ParseUtils.ParseFloat(criterion.Parameters["coverRadius"]) : 3f;
            
            var threatDirections = criterion.Parameters.ContainsKey("threatDirections") ? 
                JsonUtils.Deserialize<float[][]>(criterion.Parameters["threatDirections"].ToString()) : null;
            
            var coverMode = criterion.Parameters.ContainsKey("coverMode") ? 
                criterion.Parameters["coverMode"].ToString().ToLower() : "omnidirectional";
            
            if (_currentEnvironment == null)
                return 0f;
            
            var coverScore = 0f;
            var position = cell.WorldPosition + Vector3.up * 1.5f; // Eye height
            
            // Check direction array
            Vector3[] checkDirections;
            
            if (threatDirections != null && threatDirections.Length > 0)
            {
                // Use specified threat directions
                checkDirections = threatDirections.Select(dir => 
                    new Vector3(dir[0], dir[1], dir[2]).normalized).ToArray();
            }
            else
            {
                // Use default 8-directional check
                checkDirections = new Vector3[]
                {
                    Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                    (Vector3.forward + Vector3.right).normalized,
                    (Vector3.forward + Vector3.left).normalized,
                    (Vector3.back + Vector3.right).normalized,
                    (Vector3.back + Vector3.left).normalized
                };
            }
            
            var protectedDirections = 0;
            
            foreach (var direction in checkDirections)
            {
                // Check if there's cover in this direction
                if (Physics.Raycast(position, direction, coverRadius, LayerMask.GetMask("Default")))
                {
                    protectedDirections++;
                }
            }
            
            switch (coverMode)
            {
                case "omnidirectional":
                    // Full directional protection
                    coverScore = (float)protectedDirections / checkDirections.Length;
                    break;
                
                case "partial":
                    // Partial protection is sufficient
                    coverScore = protectedDirections > 0 ? 1f : 0f;
                    break;
                
                case "majority":
                    // Majority directions have protection
                    coverScore = protectedDirections >= (checkDirections.Length / 2) ? 1f : 0f;
                    break;
                
                default:
                    coverScore = (float)protectedDirections / checkDirections.Length;
                    break;
            }
            
            return Mathf.Clamp01(coverScore);
        }
        
        /// <summary>
        /// Path complexity score calculation
        /// 
        /// Evaluate path complexity to reach this position:
        /// - Straight line distance vs actual path distance
        /// - Number of obstacles on path
        /// - Path tortuosity degree
        /// </summary>
        private static float CalculatePathComplexityScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            if (!criterion.Parameters.ContainsKey("startPoint"))
                return 0.5f;
            
            var startPointArray = JsonUtils.Deserialize<float[]>(criterion.Parameters["startPoint"].ToString());
            var startPoint = new Vector3(startPointArray[0], startPointArray[1], startPointArray[2]);
            
            var complexityMode = criterion.Parameters.ContainsKey("complexityMode") ? 
                criterion.Parameters["complexityMode"].ToString().ToLower() : "simple";
            
            var maxComplexity = criterion.Parameters.ContainsKey("maxComplexity") ? 
                ParseUtils.ParseFloat(criterion.Parameters["maxComplexity"]) : 2f;
            
            var directDistance = Vector3.Distance(startPoint, cell.WorldPosition);
            
            if (directDistance < 0.1f)
                return 1f; // Starting position
            
            switch (complexityMode)
            {
                case "simple":
                    // Simple straight line obstacle check
                    var direction = (cell.WorldPosition - startPoint).normalized;
                    var obstacleCount = 0;
                    var checkDistance = 0f;
                    var stepSize = 1f;
                    
                    while (checkDistance < directDistance)
                    {
                        var checkPoint = startPoint + direction * checkDistance;
                        if (Physics.CheckSphere(checkPoint, 0.5f, LayerMask.GetMask("Default")))
                        {
                            obstacleCount++;
                        }
                        checkDistance += stepSize;
                    }
                    
                    var complexity = (float)obstacleCount / (directDistance / stepSize);
                    return Mathf.Clamp01(1f - (complexity / maxComplexity));
                
                case "linecast":
                    // Raycast check
                    var hasObstacle = Physics.Linecast(startPoint, cell.WorldPosition, LayerMask.GetMask("Default"));
                    return hasObstacle ? 0f : 1f;
                
                default:
                    return 0.5f;
            }
        }
        
        /// <summary>
        /// Multi-point score calculation
        /// 
        /// Comprehensive scoring considering multiple target points simultaneously:
        /// - Average distance to multiple points
        /// - Distance to nearest point
        /// - Distance to farthest point
        /// - Custom weight combination
        /// </summary>
        private static float CalculateMultiPointScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            if (!criterion.Parameters.ContainsKey("targetPoints"))
                return 0f;
            
            var targetPointsData = JsonUtils.Deserialize<float[][]>(criterion.Parameters["targetPoints"].ToString());
            var targetPoints = targetPointsData.Select(arr => 
                new Vector3(arr[0], arr[1], arr[2])).ToArray();
            
            var multiMode = criterion.Parameters.ContainsKey("multiMode") ? 
                criterion.Parameters["multiMode"].ToString().ToLower() : "average";
            
            var weights = criterion.Parameters.ContainsKey("weights") ? 
                JsonUtils.Deserialize<float[]>(criterion.Parameters["weights"].ToString()) : null;
            
            var maxDistance = criterion.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["maxDistance"]) : 100f;
            
            if (targetPoints.Length == 0)
                return 0f;
            
            var distances = targetPoints.Select(point => 
                Vector3.Distance(cell.WorldPosition, point)).ToArray();
            
            switch (multiMode)
            {
                case "average":
                    // Average distance
                    var avgDistance = distances.Average();
                    return Mathf.Clamp01(1f - (avgDistance / maxDistance));
                
                case "closest":
                    // Nearest point distance
                    var minDistance = distances.Min();
                    return Mathf.Clamp01(1f - (minDistance / maxDistance));
                
                case "farthest":
                    // Farthest point distance
                    var maxDist = distances.Max();
                    return Mathf.Clamp01(1f - (maxDist / maxDistance));
                
                case "weighted":
                    // Weighted average
                    if (weights != null && weights.Length == distances.Length)
                    {
                        var weightedSum = 0f;
                        var totalWeight = 0f;
                        
                        for (int i = 0; i < distances.Length; i++)
                        {
                            var score = 1f - (distances[i] / maxDistance);
                            weightedSum += score * weights[i];
                            totalWeight += weights[i];
                        }
                        
                        return totalWeight > 0 ? Mathf.Clamp01(weightedSum / totalWeight) : 0f;
                    }
                    else
                    {
                        // If weights don't match, fall back to average
                        goto case "average";
                    }
                
                case "best":
                    // Best (nearest) point score
                    var bestDistance = distances.Min();
                    return Mathf.Clamp01(1f - (bestDistance / maxDistance));
                
                default:
                    goto case "average";
            }
        }

        private static EQSAreaOfInterest ParseAreaOfInterest(Dictionary<string, object> areaData)
        {
            try
            {
                if (string.IsNullOrEmpty(areaData["type"].ToString()))
                    return null;

                var type = areaData["type"].ToString();
                var areaOfInterest = new EQSAreaOfInterest { Type = type };

                if (type == "sphere" || type == "box")
                {
                    // More robust center parsing
                    float[] center;
                    try
                    {
                        center = JsonUtils.Deserialize<float[]>(areaData["center"].ToString());
                    }
                    catch
                    {
                        // If direct parsing fails, try handling integer array
                        center = ParseUtils.ParseFloatArray(areaData["center"]);
                    }

                    areaOfInterest.Center = new Vector3(center[0], center[1], center[2]);

                    if (type == "sphere")
                    {
                        // More robust radius parsing
                        float radius;
                        try
                        {
                            radius = ParseUtils.ParseFloat(areaData["radius"]);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Unable to parse radius value: {areaData["radius"]}", ex);
                        }
                        areaOfInterest.Radius = radius;
                    }
                    else if (type == "box")
                    {
                        // Handle size array
                        float[] size;
                        try
                        {
                            size = JsonUtils.Deserialize<float[]>(areaData["size"].ToString());
                        }
                        catch
                        {
                            size = ParseUtils.ParseFloatArray(areaData["size"]);
                        }
                        areaOfInterest.Size = new Vector3(size[0], size[1], size[2]);
                    }
                }

                if (areaData.ContainsKey("areaName"))
                    areaOfInterest.AreaName = areaData["areaName"].ToString();

                return areaOfInterest;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse area of interest: " + ex.Message, ex);
            }
        }



        /// <summary>
        /// Auto-visualize query results using green to red color gradient
        /// Display all points that meet criteria and have scores, not just the top few
        /// </summary>
        private static void AutoVisualizeQueryResults(EQSQueryResult queryResult)
        {
            try
            {
                // Find or create EQS Visualization Group
                var visualizationGroup = FindOrCreateVisualizationGroup(queryResult.QueryID);
                visualizationGroup.UpdateQueryResults(queryResult);

                Debug.Log($"[EQS] Auto-created visualization for query '{queryResult.QueryID}' with {queryResult.Results.Count} markers");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EQS] Auto-visualization of query results failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Find or create EQS Visualization Group
        /// </summary>
        private static EQSVisualizationGroup FindOrCreateVisualizationGroup(string queryId)
        {
            // Try to find existing visualization group
            var existingGroup = GameObject.FindObjectOfType<EQSVisualizationGroup>();
            if (existingGroup != null && existingGroup.QueryId == queryId)
            {
                return existingGroup;
            }

            // Create new visualization group
            var groupObj = new GameObject($"EQS Visualization Group [{queryId}]");
            var group = groupObj.AddComponent<EQSVisualizationGroup>();
            group.QueryId = queryId;
            
            // Mark as editor-only object
            groupObj.hideFlags = HideFlags.DontSave;
            
            return group;
        }

    }

    // EQS Visualization Group Component - Similar to Light Probe Group
    public class EQSVisualizationGroup : MonoBehaviour
    {
        [System.Serializable]
        public class MarkerData
        {
            public Vector3 position;
            public float score;
            public Color color;
            public Vector3Int? cellIndices;
            public Dictionary<string, float> breakdownScores;
            public List<string> associatedObjectIDs;
            public bool isSelected = false;

            public MarkerData(Tool_EQS.EQSLocationCandidate candidate)
            {
                position = candidate.WorldPosition;
                score = candidate.Score;
                color = CalculateScoreColor(score);
                cellIndices = candidate.CellIndices;
                breakdownScores = new Dictionary<string, float>(candidate.BreakdownScores);
                associatedObjectIDs = new List<string>(candidate.AssociatedObjectIDs);
            }

            private static Color CalculateScoreColor(float score)
            {
                score = Mathf.Clamp01(score);
                if (score <= 0.5f)
                {
                    var t = score * 2f;
                    return new Color(1f, t, 0f);
                }
                else
                {
                    var t = (score - 0.5f) * 2f;
                    return new Color(1f - t, 1f, 0f);
                }
            }
        }

        [Header("EQS Query Information")]
        public string QueryId;
        
        [Header("Visualization Settings")]
        public float markerSize = 0.3f;
        public float selectedAlpha = 1.0f;
        public float unselectedAlpha = 0.5f;
        
        [Header("Information Display")]
        public bool showScore = true;
        public bool showPosition = true;
        
        [Header("Query Results")]
        public List<MarkerData> markers = new List<MarkerData>();

        public void UpdateQueryResults(Tool_EQS.EQSQueryResult queryResult)
        {
            QueryId = queryResult.QueryID;
            markers.Clear();

            foreach (var candidate in queryResult.Results)
            {
                markers.Add(new MarkerData(candidate));
            }

            // Update object name to reflect result count
            gameObject.name = $"EQS Visualization Group [{QueryId}] ({markers.Count} results)";
        }

        private void OnDrawGizmos()
        {
            if (markers == null || markers.Count == 0) return;

            // Only draw basic markers when this GameObject is not selected
            // This prevents double rendering when the object is selected
            #if UNITY_EDITOR
            if (UnityEditor.Selection.Contains(gameObject)) return;
            #endif

            DrawBasicMarkers();
        }

        private void OnDrawGizmosSelected()
        {
            if (markers == null || markers.Count == 0) return;

            // Draw all markers when group is selected
            DrawAllMarkers();
        }

        private void DrawBasicMarkers()
        {
            // Draw simple markers when group is not selected
            foreach (var marker in markers)
            {
                var color = marker.color;
                color.a = unselectedAlpha;
                Gizmos.color = color;
                Gizmos.DrawSphere(marker.position, markerSize * 0.8f); // Slightly smaller when not selected
            }
        }

        private void DrawAllMarkers()
        {
            // Draw all markers with full detail when group is selected
            foreach (var marker in markers)
            {
                var color = marker.color;
                color.a = marker.isSelected ? selectedAlpha : unselectedAlpha;
                Gizmos.color = color;
                
                // Draw marker sphere
                Gizmos.DrawSphere(marker.position, markerSize);
            }
        }



        private Vector3 CalculateCenter()
        {
            if (markers.Count == 0) return transform.position;
            
            var sum = Vector3.zero;
            foreach (var marker in markers)
            {
                sum += marker.position;
            }
            return sum / markers.Count;
        }

        // Inspector display for debugging
        public string GetSummaryText()
        {
            if (markers.Count == 0) return "No results";
            
            var bestScore = markers.Max(m => m.score);
            var avgScore = markers.Average(m => m.score);
            var worstScore = markers.Min(m => m.score);
            
            return $"Results: {markers.Count}\nBest: {bestScore:F3}\nAvg: {avgScore:F3}\nWorst: {worstScore:F3}";
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(EQSVisualizationGroup))]
    public class EQSVisualizationGroupEditor : UnityEditor.Editor
    {
        private bool showMarkerDetails = false;
        private Vector2 scrollPosition;
        private int selectedMarkerCount = 0;

        void OnSceneGUI()
        {
            var group = (EQSVisualizationGroup)target;
            if (group.markers == null || group.markers.Count == 0) return;

            // Handle marker selection in Scene view
            HandleMarkerInteraction(group);
            
            // Draw information for selected markers
            DrawSelectedMarkerInfo(group);
            
            // Handle keyboard shortcuts
            HandleKeyboardShortcuts(group);
        }

        private void HandleMarkerInteraction(EQSVisualizationGroup group)
        {
            var currentEvent = Event.current;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            for (int i = 0; i < group.markers.Count; i++)
            {
                var marker = group.markers[i];
                var worldPos = marker.position;
                
                // Calculate handle size based on distance (uniform size)
                var handleSize = HandleUtility.GetHandleSize(worldPos) * group.markerSize * 0.5f;
                
                // Draw selection handle
                EditorGUI.BeginChangeCheck();
                
                // Use original color with different transparency
                var handleColor = marker.color;
                handleColor.a = marker.isSelected ? 0.5f : 0f;
                
                using (new Handles.DrawingScope(handleColor))
                {
                    // Create clickable sphere handle
                    var buttonPressed = Handles.Button(worldPos, Quaternion.identity, handleSize, handleSize * 2f, Handles.SphereHandleCap);
                    
                    if (buttonPressed)
                    {
                        // Handle selection logic
                        bool isMultiSelect = currentEvent.control || currentEvent.command;
                        
                        if (!isMultiSelect)
                        {
                            // Single selection - deselect all others
                            for (int j = 0; j < group.markers.Count; j++)
                            {
                                group.markers[j].isSelected = (j == i);
                            }
                        }
                        else
                        {
                            // Multi-selection - toggle this marker
                            marker.isSelected = !marker.isSelected;
                        }
                        
                        // Mark object as dirty for undo system
                        Undo.RecordObject(group, "Select EQS Marker");
                        EditorUtility.SetDirty(group);
                        Repaint();
                    }
                }
                
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }
            }
            
            // Handle rectangle selection
            HandleRectangleSelection(group, controlId);
        }

        private void HandleRectangleSelection(EQSVisualizationGroup group, int controlId)
        {
            var currentEvent = Event.current;
            
            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && !currentEvent.alt)
                    {
                        // Start potential rectangle selection
                        GUIUtility.hotControl = controlId;
                        currentEvent.Use();
                    }
                    break;
                    
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        // TODO: Implement rectangle selection if needed
                        currentEvent.Use();
                    }
                    break;
                    
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        currentEvent.Use();
                    }
                    break;
            }
        }

        private void DrawSelectedMarkerInfo(EQSVisualizationGroup group)
        {
            var selectedMarkers = group.markers.Where(m => m.isSelected).ToList();
            if (selectedMarkers.Count == 0) return;

            // Draw info for each selected marker
            foreach (var marker in selectedMarkers)
            {
                var worldPos = marker.position + Vector3.up * 0.3f;
                
                // Create info text based on options
                var infoLines = new List<string>();
                
                if (group.showScore)
                {
                    infoLines.Add($"Score: {marker.score:F3}");
                }
                
                if (group.showPosition)
                {
                    infoLines.Add($"[{marker.position.x:F2}, {marker.position.y:F2}, {marker.position.z:F2}]");
                    
                    if (marker.cellIndices.HasValue)
                    {
                        var indices = marker.cellIndices.Value;
                        infoLines.Add($"Grid: ({indices.x}, {indices.y}, {indices.z})");
                    }
                }
                
                // Only show info if at least one option is enabled
                if (infoLines.Count == 0) continue;
                
                var infoText = string.Join("\n", infoLines);
                
                // Draw background box
                var guiPos = HandleUtility.WorldToGUIPoint(worldPos);
                var content = new GUIContent(infoText);
                var style = GUI.skin.box;
                var size = style.CalcSize(content);
                
                var rect = new Rect(guiPos.x - size.x * 0.5f, guiPos.y - size.y, size.x, size.y);
                
                Handles.BeginGUI();
                GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
                GUI.Box(rect, content, style);
                GUI.backgroundColor = Color.white;
                Handles.EndGUI();
            }
            
            // Draw selection count info at the bottom of screen
            if (selectedMarkers.Count > 0)
            {
                var screenRect = SceneView.currentDrawingSceneView.camera.pixelRect;
                var infoRect = new Rect(10, screenRect.height - 60, 200, 40);
                
                Handles.BeginGUI();
                var selectionInfo = $"Selected: {selectedMarkers.Count} marker(s)\nAvg Score: {selectedMarkers.Average(m => m.score):F3}";
                GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                GUI.Box(infoRect, selectionInfo);
                GUI.backgroundColor = Color.white;
                Handles.EndGUI();
            }
        }

        private void HandleKeyboardShortcuts(EQSVisualizationGroup group)
        {
            var currentEvent = Event.current;
            if (currentEvent.type != EventType.KeyDown) return;

            switch (currentEvent.keyCode)
            {
                case KeyCode.A:
                    if (currentEvent.control || currentEvent.command)
                    {
                        // Ctrl+A: Select all markers
                        Undo.RecordObject(group, "Select All EQS Markers");
                        foreach (var marker in group.markers)
                            marker.isSelected = true;
                        EditorUtility.SetDirty(group);
                        currentEvent.Use();
                        Repaint();
                    }
                    break;
                    
                case KeyCode.D:
                    if (currentEvent.control || currentEvent.command)
                    {
                        // Ctrl+D: Deselect all markers
                        Undo.RecordObject(group, "Deselect All EQS Markers");
                        foreach (var marker in group.markers)
                            marker.isSelected = false;
                        EditorUtility.SetDirty(group);
                        currentEvent.Use();
                        Repaint();
                    }
                    break;
                    
                case KeyCode.I:
                    if (currentEvent.control || currentEvent.command)
                    {
                        // Ctrl+I: Invert selection
                        Undo.RecordObject(group, "Invert EQS Marker Selection");
                        foreach (var marker in group.markers)
                            marker.isSelected = !marker.isSelected;
                        EditorUtility.SetDirty(group);
                        currentEvent.Use();
                        Repaint();
                    }
                    break;
                    
                case KeyCode.F:
                    if (selectedMarkerCount > 0)
                    {
                        // F: Focus on selected markers
                        FocusOnSelectedMarkers(group);
                        currentEvent.Use();
                    }
                    break;
            }
        }

        public override void OnInspectorGUI()
        {
            var group = (EQSVisualizationGroup)target;
            
            // Update selected marker count
            selectedMarkerCount = group.markers?.Count(m => m.isSelected) ?? 0;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("EQS Visualization Group", EditorStyles.boldLabel);
            
            // Show keyboard shortcuts info
            if (selectedMarkerCount > 0)
            {
                EditorGUILayout.HelpBox(
                    "Scene Shortcuts:\n" +
                    "• Ctrl+A: Select All\n" +
                    "• Ctrl+D: Deselect All\n" +
                    "• Ctrl+I: Invert Selection\n" +
                    "• F: Focus on Selected\n" +
                    "• Click markers in Scene to select individually\n" +
                    "• Hold Ctrl/Cmd for multi-selection", 
                    UnityEditor.MessageType.Info);
            }
            
            EditorGUILayout.Space();

            // Query information
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Query ID", group.QueryId);
            EditorGUILayout.IntField("Marker Count", group.markers?.Count ?? 0);
            EditorGUI.EndDisabledGroup();

            if (group.markers != null && group.markers.Count > 0)
            {
                var bestScore = group.markers.Max(m => m.score);
                var avgScore = group.markers.Average(m => m.score);
                var worstScore = group.markers.Min(m => m.score);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.FloatField("Best Score", bestScore);
                EditorGUILayout.FloatField("Average Score", avgScore);
                EditorGUILayout.FloatField("Worst Score", worstScore);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space();

            // Visualization settings
            EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            var newMarkerSize = EditorGUILayout.Slider("Marker Size", group.markerSize, 0.1f, 2.0f);
            var newSelectedAlpha = EditorGUILayout.Slider("Selected Alpha", group.selectedAlpha, 0.1f, 1.0f);
            var newUnselectedAlpha = EditorGUILayout.Slider("Unselected Alpha", group.unselectedAlpha, 0.05f, 1.0f);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(group, "Change Visualization Settings");
                group.markerSize = newMarkerSize;
                group.selectedAlpha = newSelectedAlpha;
                group.unselectedAlpha = newUnselectedAlpha;
                EditorUtility.SetDirty(group);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            
            // Information Display options
            EditorGUILayout.LabelField("Information Display", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            var newShowScore = EditorGUILayout.Toggle("Show Score", group.showScore);
            var newShowPosition = EditorGUILayout.Toggle("Show Position", group.showPosition);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(group, "Change Information Display Settings");
                group.showScore = newShowScore;
                group.showPosition = newShowPosition;
                EditorUtility.SetDirty(group);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();

            // Selection info
            if (group.markers != null && group.markers.Count > 0)
            {
                var selectedMarkers = group.markers.Where(m => m.isSelected).ToList();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Selection: {selectedMarkers.Count} of {group.markers.Count} markers selected", EditorStyles.boldLabel);
                
                // Selection buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All"))
                {
                    foreach (var marker in group.markers)
                        marker.isSelected = true;
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Deselect All"))
                {
                    foreach (var marker in group.markers)
                        marker.isSelected = false;
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Invert Selection"))
                {
                    foreach (var marker in group.markers)
                        marker.isSelected = !marker.isSelected;
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // Marker details toggle
                showMarkerDetails = EditorGUILayout.Foldout(showMarkerDetails, 
                    $"Marker Details ({group.markers.Count} markers)");

                if (showMarkerDetails)
                {
                    EditorGUILayout.Space();
                    
                    // Scroll view for markers
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, 
                        GUILayout.Height(Mathf.Min(300, group.markers.Count * 60)));

                    var sortedMarkers = group.markers.OrderByDescending(m => m.score).ToList();
                    
                    for (int i = 0; i < sortedMarkers.Count; i++)
                    {
                        var marker = sortedMarkers[i];
                        
                        EditorGUILayout.BeginVertical(marker.isSelected ? "box" : GUI.skin.box);
                        
                        EditorGUILayout.BeginHorizontal();
                        
                        // Selection toggle
                        EditorGUI.BeginChangeCheck();
                        var newSelected = EditorGUILayout.Toggle(marker.isSelected, GUILayout.Width(20));
                        if (EditorGUI.EndChangeCheck())
                        {
                            marker.isSelected = newSelected;
                            SceneView.RepaintAll();
                        }
                        
                        // Color indicator
                        var rect = EditorGUILayout.GetControlRect(GUILayout.Width(20), GUILayout.Height(20));
                        EditorGUI.DrawRect(rect, marker.color);
                        
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField($"Rank #{i + 1} {(marker.isSelected ? "[Selected]" : "")}", 
                            marker.isSelected ? EditorStyles.boldLabel : EditorStyles.label);
                        EditorGUILayout.LabelField($"Score: {marker.score:F3}");
                        EditorGUILayout.LabelField($"Position: ({marker.position.x:F1}, {marker.position.y:F1}, {marker.position.z:F1})");
                        
                        if (marker.cellIndices.HasValue)
                        {
                            var indices = marker.cellIndices.Value;
                            EditorGUILayout.LabelField($"Grid: ({indices.x}, {indices.y}, {indices.z})");
                        }
                        EditorGUILayout.EndVertical();
                        
                        // Locate button
                        if (GUILayout.Button("Locate", GUILayout.Width(60)))
                        {
                            LocateMarker(marker);
                        }
                        
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        
                        if (i < sortedMarkers.Count - 1)
                            EditorGUILayout.Space();
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
            }

            EditorGUILayout.Space();

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Focus on Best Result"))
            {
                FocusOnBestResult(group);
            }
            
            var currentSelectedMarkers = group.markers?.Where(m => m.isSelected).ToList();
            EditorGUI.BeginDisabledGroup(currentSelectedMarkers == null || currentSelectedMarkers.Count == 0);
            if (GUILayout.Button($"Focus on Selected ({currentSelectedMarkers?.Count ?? 0})"))
            {
                FocusOnSelectedMarkers(group);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Focus on Group"))
            {
                FocusOnGroup(group);
            }

            if (GUILayout.Button("Clear Visualization"))
            {
                if (EditorUtility.DisplayDialog("Clear Visualization", 
                    "Are you sure you want to remove this EQS visualization?", "Yes", "Cancel"))
                {
                    DestroyImmediate(group.gameObject);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void LocateMarker(EQSVisualizationGroup.MarkerData marker)
        {
            SceneView.lastActiveSceneView.pivot = marker.position;
            SceneView.lastActiveSceneView.Repaint();
        }

        private void FocusOnBestResult(EQSVisualizationGroup group)
        {
            if (group.markers == null || group.markers.Count == 0) return;
            
            var bestMarker = group.markers.OrderByDescending(m => m.score).First();
            SceneView.lastActiveSceneView.pivot = bestMarker.position;
            SceneView.lastActiveSceneView.size = 10f;
            SceneView.lastActiveSceneView.Repaint();
        }

        private void FocusOnSelectedMarkers(EQSVisualizationGroup group)
        {
            if (group.markers == null || group.markers.Count == 0) return;
            
            var selectedMarkers = group.markers.Where(m => m.isSelected).ToList();
            if (selectedMarkers.Count == 0) return;
            
            if (selectedMarkers.Count == 1)
            {
                // Single selected marker - focus closely
                SceneView.lastActiveSceneView.pivot = selectedMarkers[0].position;
                SceneView.lastActiveSceneView.size = 5f;
            }
            else
            {
                // Multiple selected markers - calculate bounds
                var bounds = new Bounds(selectedMarkers[0].position, Vector3.zero);
                foreach (var marker in selectedMarkers)
                {
                    bounds.Encapsulate(marker.position);
                }
                
                SceneView.lastActiveSceneView.pivot = bounds.center;
                SceneView.lastActiveSceneView.size = Mathf.Max(bounds.size.x, bounds.size.z) * 1.2f;
            }
            
            SceneView.lastActiveSceneView.Repaint();
        }

        private void FocusOnGroup(EQSVisualizationGroup group)
        {
            if (group.markers == null || group.markers.Count == 0) return;
            
            // Calculate bounds of all markers
            var bounds = new Bounds(group.markers[0].position, Vector3.zero);
            foreach (var marker in group.markers)
            {
                bounds.Encapsulate(marker.position);
            }
            
            SceneView.lastActiveSceneView.pivot = bounds.center;
            SceneView.lastActiveSceneView.size = Mathf.Max(bounds.size.x, bounds.size.z) * 1.2f;
            SceneView.lastActiveSceneView.Repaint();
        }
    }
    #endif
} 