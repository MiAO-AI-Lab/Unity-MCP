#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using com.IvanMurzak.Unity.MCP.Common;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
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
            [Description("Query conditions array. DistanceTo: {\"conditionType\":\"DistanceTo\",\"parameters\":{\"targetPoint\":[10,0,20],\"minDistance\":5,\"maxDistance\":25,\"distanceMode\":\"euclidean\"}}. distanceMode options: euclidean|manhattan|chebyshev|horizontal|vertical|squared. Clearance: {\"conditionType\":\"Clearance\",\"parameters\":{\"requiredHeight\":2.0,\"requiredRadius\":0.5,\"checkDirections\":8}}. VisibilityOf: {\"conditionType\":\"VisibilityOf\",\"parameters\":{\"targetPoint\":[15,1,25],\"eyeHeight\":1.7,\"maxViewAngle\":90,\"successThreshold\":0.8}}. CustomProperty: {\"conditionType\":\"CustomProperty\",\"parameters\":{\"propertyName\":\"terrainType\",\"expectedValue\":\"ground\",\"comparisonType\":\"equals\"}}. ObjectProximity: {\"conditionType\":\"ObjectProximity\",\"parameters\":{\"objectId\":\"12345\",\"proximityMode\":\"surface\",\"maxDistance\":5.0,\"minDistance\":1.0,\"colliderType\":\"any\"}}. proximityMode options: inside|outside|surface. colliderType options: any|trigger|solid")]
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
                // 检查环境是否已初始化
                if (_currentEnvironment == null)
                {
                    return Error.EnvironmentNotInitialized();
                }

                var startTime = DateTime.Now;

                // 解析输入参数
                var query = new EQSQuery
                {
                    QueryID = queryID,
                    TargetObjectType = targetObjectType,
                    DesiredResultCount = desiredResultCount
                };

                // 解析参考点
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
                    return $"[Error] 解析参考点失败: {ex.Message}";
                }

                // 解析兴趣区域
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
                        return $"[Error] 解析兴趣区域失败: {ex.Message}";
                    }
                }

                // 解析查询条件
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
                    return $"[Error] 解析查询条件失败: {ex.Message}";
                }

                // 解析评分标准
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
                    return $"[Error] 解析评分标准失败: {ex.Message}";
                }

                // 执行查询
                var result = ExecuteQuery(query);
                
                // 缓存结果
                _queryCache[queryID] = result;

                var executionTime = (DateTime.Now - startTime).TotalMilliseconds;
                result.ExecutionTimeMs = (float)executionTime;

                // 自动创建可视化（根据评分显示绿到红的渐变色）
                // 显示所有满足条件的点，而不只是前几名
                if (result.Status == "Success" && result.Results.Count > 0)
                {
                    // 为了显示所有候选点，重新执行查询获取所有结果
                    var allCandidatesResult = ExecuteQueryForVisualization(query);
                    AutoVisualizeQueryResults(allCandidatesResult);
                }

                // 创建安全的序列化版本，避免Vector3循环引用
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

                return @$"[Success] EQS查询执行成功。
# 查询结果:
```json
{JsonUtils.Serialize(safeResult)}
```

# 结果摘要:
- 查询ID: {result.QueryID}
- 状态: {result.Status}  
- 找到的候选位置数: {result.Results.Count}
- 执行时间: {result.ExecutionTimeMs:F2}毫秒
- 自动可视化: {(result.Results.Count > 0 ? "已创建" : "无结果，未创建")}

# 前3个最佳位置:
{string.Join("\n", result.Results.Take(3).Select((candidate, index) => 
    $"#{index + 1}: 位置({candidate.WorldPosition.x:F2}, {candidate.WorldPosition.y:F2}, {candidate.WorldPosition.z:F2}) 分数:{candidate.Score:F3}"))}

# 可视化说明:
- 🟢 绿色 = 高评分 (0.7-1.0)
- 🟡 黄绿色 = 中高评分 (0.5-0.7)  
- 🟡 黄色 = 中等评分 (0.3-0.5)
- 🟠 橙色 = 中低评分 (0.1-0.3)
- 🔴 红色 = 低评分 (0.0-0.1)
- 灰色 = 不可用
- 所有满足条件的点都会显示对应颜色
- 统一大小，不显示分数文本
- 可视化永久保留，直到手动清除或重新查询";
            }
            catch (Exception ex)
            {
                return $"[Error] EQS查询执行失败: {ex.Message}";
            }
        });

        /// <summary>
        /// 执行查询用于可视化（返回所有候选点，不限制数量）
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

            // 筛选符合条件的点位
            var validCells = FilterCells(grid.Cells, query);

            // 对每个符合条件的点位进行评分
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

            // 按分数排序，但返回所有候选点（不限制数量）
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
        /// EQS查询执行的核心方法
        /// 
        /// 点位选择逻辑说明：
        /// 1. 从环境网格中筛选候选点位（FilterCells）
        /// 2. 对每个候选点位进行多维度评分（CalculateScore）
        /// 3. 根据权重计算综合得分
        /// 4. 按得分排序并返回最佳点位
        /// 
        /// 这种设计允许复杂的空间推理，如：
        /// - 找到离玩家近但远离敌人的掩体位置
        /// - 选择视野好且安全的狙击点
        /// - 寻找适合放置医疗包的位置
        /// </summary>
        /// <param name="query">包含所有查询参数的EQS查询对象</param>
        /// <returns>包含排序后候选点位的查询结果</returns>
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

            // 第一阶段：候选点过滤
            // 从所有网格单元中筛选出符合基本条件的点位
            // 这一步大幅减少需要评分的点位数量，提高性能
            var validCells = FilterCells(grid.Cells, query);

            // 第二阶段：候选点评分
            // 对每个通过过滤的点位进行多维度评分
            foreach (var cell in validCells)
            {
                var candidate = new EQSLocationCandidate
                {
                    WorldPosition = cell.WorldPosition,
                    CellIndices = cell.Indices,
                    AssociatedObjectIDs = new List<string>(cell.DynamicOccupants)
                };

                // 多维度评分系统：
                // 每个评分标准独立计算分数，然后按权重加权平均
                // 这允许复杂的决策，如"70%考虑距离，30%考虑安全性"
                var totalScore = 0f;
                var totalWeight = 0f;

                foreach (var criterion in query.ScoringCriteria)
                {
                    var score = CalculateScore(cell, criterion, query);
                    candidate.BreakdownScores[criterion.CriterionType] = score; // 保存各项得分用于调试
                    totalScore += score * criterion.Weight; // 加权累加
                    totalWeight += criterion.Weight;
                }

                // 计算最终得分（加权平均）
                candidate.Score = totalWeight > 0 ? totalScore / totalWeight : 0f;
                candidates.Add(candidate);
            }

            // 第三阶段：结果排序和截取
            // 按分数从高到低排序，取前N个最佳点位
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
        /// 候选点过滤器 - EQS的第一道筛选机制
        /// 
        /// 过滤逻辑：
        /// 1. 兴趣区域过滤：只考虑指定区域内的点位
        /// 2. 条件过滤：每个点位必须满足所有指定条件
        /// 
        /// 过滤条件类型：
        /// - DistanceTo: 距离约束（如：距离玩家5-20米）
        /// - Clearance: 空间间隙（如：需要2米高度空间）
        /// - CustomProperty: 自定义属性（如：地形类型为"草地"）
        /// - VisibilityOf: 视线可见性（如：能看到目标点）
        /// 
        /// 这种设计确保只有真正可行的点位进入评分阶段
        /// </summary>
        /// <param name="cells">所有网格单元</param>
        /// <param name="query">查询参数</param>
        /// <returns>通过过滤的有效单元数组</returns>
        private static EQSCell[] FilterCells(EQSCell[] cells, EQSQuery query)
        {
            var validCells = new List<EQSCell>();

            foreach (var cell in cells)
            {
                // 兴趣区域检查：如果指定了兴趣区域，只考虑区域内的点位
                // 这可以显著减少计算量，例如只在玩家周围50米内寻找点位
                if (query.QueryContext.AreaOfInterest != null && !IsInAreaOfInterest(cell, query.QueryContext.AreaOfInterest))
                    continue;

                // 条件检查：点位必须满足所有指定条件
                // 采用"与"逻辑：任何一个条件不满足，该点位就被排除
                var passesAllConditions = true;
                foreach (var condition in query.Conditions)
                {
                    if (!EvaluateCondition(cell, condition, query))
                    {
                        passesAllConditions = false;
                        break; // 早期退出优化
                    }
                }

                if (passesAllConditions)
                    validCells.Add(cell);
            }

            return validCells.ToArray();
        }

        /// <summary>
        /// 检查点位是否在兴趣区域内
        /// 
        /// 支持的区域类型：
        /// - Sphere: 球形区域（中心点+半径）
        /// - Box: 矩形区域（中心点+尺寸）
        /// 
        /// 兴趣区域的作用：
        /// 1. 性能优化：减少需要处理的点位数量
        /// 2. 逻辑约束：确保结果在合理范围内
        /// 例如：在玩家周围30米内寻找掩体，而不是整个地图
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
                    return true; // 未知类型默认通过
            }
        }

        /// <summary>
        /// 评估单个条件是否满足
        /// 
        /// 条件评估是EQS的核心过滤机制，每种条件类型有不同的评估逻辑：
        /// 
        /// 1. DistanceTo: 距离约束
        ///    - 用途：确保点位在合适的距离范围内
        ///    - 示例：医疗包应该距离玩家5-15米（太近浪费，太远不便）
        /// 
        /// 2. Clearance: 空间间隙
        ///    - 用途：确保点位有足够的活动空间
        ///    - 示例：狙击位置需要2米高度空间，避免撞头
        /// 
        /// 3. CustomProperty: 自定义属性
        ///    - 用途：基于地形或环境特征过滤
        ///    - 示例：只在"草地"地形上放置野餐桌
        /// 
        /// 4. VisibilityOf: 视线可见性
        ///    - 用途：确保视线通畅
        ///    - 示例：哨兵位置必须能看到入口
        /// </summary>
        /// <param name="cell">要评估的网格单元</param>
        /// <param name="condition">评估条件</param>
        /// <param name="query">查询上下文</param>
        /// <returns>是否满足条件</returns>
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
                    result = true; // 未知条件默认通过
                    break;
            }

            // 支持条件反转：有时我们需要"不满足某条件"的点位
            // 例如：寻找"不在敌人视线范围内"的隐蔽位置
            return condition.Invert ? !result : result;
        }

        /// <summary>
        /// 距离条件评估
        /// 
        /// 距离约束是最常用的过滤条件，支持最小和最大距离限制：
        /// - minDistance: 最小距离（避免太近的点位）
        /// - maxDistance: 最大距离（避免太远的点位）
        /// 
        /// 应用场景：
        /// - 掩体位置：距离玩家10-30米（既安全又不会太远）
        /// - 补给点：距离战斗区域20-50米（安全补给）
        /// - 巡逻点：距离基地50-100米（覆盖范围合适）
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
        /// 空间间隙条件评估 - 完整实现
        /// 
        /// 间隙检查确保点位有足够的活动空间：
        /// - requiredHeight: 所需垂直空间（默认2米）
        /// - requiredRadius: 所需水平空间（默认0.5米）
        /// 
        /// 完整实现包括：
        /// 1. 垂直空间检查（向上射线投射）
        /// 2. 水平空间检查（多方向射线投射）
        /// 3. 基础可行走性检查
        /// </summary>
        private static bool EvaluateClearanceCondition(EQSCell cell, EQSCondition condition)
        {
            var requiredHeight = condition.Parameters.ContainsKey("requiredHeight") ? 
                ParseUtils.ParseFloat(condition.Parameters["requiredHeight"]) : 2f;
            var requiredRadius = condition.Parameters.ContainsKey("requiredRadius") ? 
                ParseUtils.ParseFloat(condition.Parameters["requiredRadius"]) : 0.5f;

            // 基础检查：不能有静态占用且必须可行走
            if (cell.StaticOccupancy || !(bool)cell.Properties.GetValueOrDefault("isWalkable", false))
                return false;

            var position = cell.WorldPosition;

            // 垂直空间检查：从当前位置向上发射射线
            if (Physics.Raycast(position, Vector3.up, requiredHeight, LayerMask.GetMask("Default")))
            {
                return false; // 上方有障碍物
            }

            // 水平空间检查：8个方向检查水平间隙
            var directions = new Vector3[]
            {
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                Vector3.forward + Vector3.right, Vector3.forward + Vector3.left,
                Vector3.back + Vector3.right, Vector3.back + Vector3.left
            };

            foreach (var direction in directions)
            {
                var normalizedDir = direction.normalized;
                if (Physics.Raycast(position, normalizedDir, requiredRadius, LayerMask.GetMask("Default")))
                {
                    return false; // 水平方向有障碍物
                }
            }

            // 地面检查：确保脚下有支撑
            if (!Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, 0.5f, LayerMask.GetMask("Default")))
            {
                return false; // 脚下没有地面
            }

            return true;
        }

        /// <summary>
        /// 自定义属性条件评估
        /// 
        /// 允许基于网格单元的自定义属性进行过滤：
        /// - propertyName: 属性名称
        /// - value: 期望值
        /// - operator: 比较操作符（equals, contains等）
        /// 
        /// 应用场景：
        /// - 地形类型过滤：只在"草地"上放置帐篷
        /// - 高度过滤：只在"高地"上设置瞭望台
        /// - 安全级别：只在"安全区域"放置补给
        /// 
        /// 这提供了高度的灵活性，可以根据游戏需求定制各种过滤逻辑
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
        /// 视线可见性条件评估 - 完整实现
        /// 
        /// 检查从当前点位是否能看到目标位置，考虑视觉障碍物。
        /// 
        /// 完整实现包括：
        /// 1. 射线投射检查视线障碍
        /// 2. 视野角度限制（可选）
        /// 3. 多点采样提高准确性
        /// 4. 高度偏移（眼睛位置）
        /// 
        /// 应用场景：
        /// - 哨兵位置：必须能看到关键入口
        /// - 狙击点：需要清晰视线到目标区域
        /// - 观察哨：要求360度视野或特定方向视野
        /// </summary>
        private static bool EvaluateVisibilityCondition(EQSCell cell, EQSCondition condition)
        {
            if (!condition.Parameters.ContainsKey("targetPoint"))
                return false;

            var targetPointArray = JsonUtils.Deserialize<float[]>(condition.Parameters["targetPoint"].ToString());
            var targetPoint = new Vector3(targetPointArray[0], targetPointArray[1], targetPointArray[2]);
            
            // 观察者高度偏移（模拟眼睛位置）
            var eyeHeight = condition.Parameters.ContainsKey("eyeHeight") ? 
                ParseUtils.ParseFloat(condition.Parameters["eyeHeight"]) : 1.7f;
            var observerPosition = cell.WorldPosition + Vector3.up * eyeHeight;
            
            // 目标高度偏移（可选）
            var targetHeight = condition.Parameters.ContainsKey("targetHeight") ? 
                ParseUtils.ParseFloat(condition.Parameters["targetHeight"]) : 0f;
            var adjustedTargetPoint = targetPoint + Vector3.up * targetHeight;
            
            // 视野角度限制（可选）
            var maxViewAngle = condition.Parameters.ContainsKey("maxViewAngle") ? 
                ParseUtils.ParseFloat(condition.Parameters["maxViewAngle"]) : 360f;
            
            // 观察方向（可选，用于限制视野角度）
            Vector3 viewDirection = Vector3.forward;
            if (condition.Parameters.ContainsKey("viewDirection"))
            {
                var viewDirArray = JsonUtils.Deserialize<float[]>(condition.Parameters["viewDirection"].ToString());
                viewDirection = new Vector3(viewDirArray[0], viewDirArray[1], viewDirArray[2]).normalized;
            }
            
            var directionToTarget = (adjustedTargetPoint - observerPosition).normalized;
            var distance = Vector3.Distance(observerPosition, adjustedTargetPoint);
            
            // 检查视野角度限制
            if (maxViewAngle < 360f)
            {
                var angle = Vector3.Angle(viewDirection, directionToTarget);
                if (angle > maxViewAngle / 2f)
                    return false; // 超出视野角度
            }
            
            // 多点采样检查视线（提高准确性）
            var sampleCount = condition.Parameters.ContainsKey("sampleCount") ? 
                ParseUtils.ParseInt(condition.Parameters["sampleCount"]) : 3;
            
            var successfulSamples = 0;
            var requiredSuccessRate = condition.Parameters.ContainsKey("requiredSuccessRate") ? 
                ParseUtils.ParseFloat(condition.Parameters["requiredSuccessRate"]) : 0.6f;
            
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 sampleTarget = adjustedTargetPoint;
                
                // 为多点采样添加小的随机偏移
                if (sampleCount > 1)
                {
                    var randomOffset = UnityEngine.Random.insideUnitSphere * 0.5f;
                    randomOffset.y = 0; // 只在水平面偏移
                    sampleTarget += randomOffset;
                }
                
                var sampleDirection = (sampleTarget - observerPosition).normalized;
                var sampleDistance = Vector3.Distance(observerPosition, sampleTarget);
                
                // 射线投射检查视线
                if (!Physics.Raycast(observerPosition, sampleDirection, sampleDistance, 
                    LayerMask.GetMask("Default")))
                {
                    successfulSamples++;
                }
            }
            
            // 检查成功率是否满足要求
            var successRate = (float)successfulSamples / sampleCount;
            return successRate >= requiredSuccessRate;
        }

        /// <summary>
        /// 物体接近度条件评估 - 完整实现
        /// 
        /// 检查位置相对于指定物体的空间关系：
        /// - inside: 点位是否在物体内部
        /// - outside: 点位是否在物体外部
        /// - surface: 点位是否在距离物体表面指定距离范围内
        /// 
        /// 支持多种碰撞器类型检测，适用于：
        /// - 建筑物内部位置查询（inside模式）
        /// - 安全区域外围查询（outside + maxDistance）
        /// - 物体表面附近查询（surface模式）
        /// - 避让区域设置（outside + minDistance）
        /// 
        /// 实现细节：
        /// 1. 通过InstanceID或名称查找目标GameObject
        /// 2. 根据colliderType过滤碰撞器
        /// 3. 使用Physics查询检测空间关系
        /// 4. 支持距离阈值控制
        /// </summary>
        private static bool EvaluateObjectProximityCondition(EQSCell cell, EQSCondition condition)
        {
            // 获取目标对象
            GameObject targetObject = null;
            
            // 优先使用objectId（InstanceID）
            if (condition.Parameters.ContainsKey("objectId"))
            {
                var objectIdStr = condition.Parameters["objectId"].ToString();
                if (int.TryParse(objectIdStr, out int instanceId))
                {
                    targetObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                }
            }
            
            // 如果通过ID没找到，尝试使用名称查找
            if (targetObject == null && condition.Parameters.ContainsKey("objectName"))
            {
                var objectName = condition.Parameters["objectName"].ToString();
                targetObject = GameObject.Find(objectName);
            }
            
            if (targetObject == null)
            {
                Debug.LogWarning($"[EQS] ObjectProximity条件：找不到目标对象");
                return false;
            }
            
            // 获取参数
            var proximityMode = condition.Parameters.ContainsKey("proximityMode") ? 
                condition.Parameters["proximityMode"].ToString().ToLower() : "surface";
            
            var maxDistance = condition.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(condition.Parameters["maxDistance"]) : 5f;
            
            var minDistance = condition.Parameters.ContainsKey("minDistance") ? 
                ParseUtils.ParseFloat(condition.Parameters["minDistance"]) : 0f;
            
            var colliderType = condition.Parameters.ContainsKey("colliderType") ? 
                condition.Parameters["colliderType"].ToString().ToLower() : "any";
            
            // 获取目标对象的碰撞器
            var colliders = GetObjectColliders(targetObject, colliderType);
            if (colliders.Length == 0)
            {
                Debug.LogWarning($"[EQS] ObjectProximity条件：目标对象 '{targetObject.name}' 没有找到合适的碰撞器");
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
                        return false; // 在内部，不满足outside条件
                    
                    // 检查距离限制
                    if (maxDistance > 0)
                    {
                        var distanceToSurface = GetDistanceToCollidersSurface(checkPosition, colliders);
                        return distanceToSurface >= minDistance && distanceToSurface <= maxDistance;
                    }
                    
                    return true; // 在外部且无距离限制
                
                case "surface":
                    var surfaceDistance = GetDistanceToCollidersSurface(checkPosition, colliders);
                    return surfaceDistance >= minDistance && surfaceDistance <= maxDistance;
                
                default:
                    Debug.LogWarning($"[EQS] ObjectProximity条件：未知的proximityMode '{proximityMode}'");
                    return false;
            }
        }
        
        /// <summary>
        /// 根据类型获取对象的碰撞器
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
        /// 检查位置是否在碰撞器内部
        /// </summary>
        private static bool IsPositionInsideColliders(Vector3 position, Collider[] colliders)
        {
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;
                
                // 使用 Bounds.Contains 进行快速预检查
                if (!collider.bounds.Contains(position))
                    continue;
                
                // 使用 ClosestPoint 进行精确检查
                var closestPoint = collider.ClosestPoint(position);
                var distance = Vector3.Distance(position, closestPoint);
                
                // 如果距离很小，认为在内部
                if (distance < 0.01f)
                {
                    // 进一步检查：如果closestPoint与position相同，则position在collider内部
                    if (Vector3.Distance(position, closestPoint) < 0.001f)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 计算位置到碰撞器表面的最短距离
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
        /// 计算点位在特定评分标准下的得分
        /// 
        /// 评分系统是EQS的核心，不同于过滤（二元判断），评分提供连续值：
        /// 
        /// 1. ProximityTo: 接近度评分
        ///    - 越接近目标点，得分越高
        ///    - 用于：寻找最近的掩体、补给点等
        /// 
        /// 2. FarthestFrom: 远离度评分
        ///    - 越远离目标点，得分越高
        ///    - 用于：避开危险区域、寻找安全位置
        /// 
        /// 3. DensityOfObjects: 对象密度评分
        ///    - 根据周围对象数量评分
        ///    - 用于：避开拥挤区域或寻找活跃区域
        /// 
        /// 评分范围通常是0-1，便于权重计算和比较
        /// </summary>
        /// <param name="cell">要评分的网格单元</param>
        /// <param name="criterion">评分标准</param>
        /// <param name="query">查询上下文</param>
        /// <returns>0-1范围内的得分</returns>
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
                    return 0.5f; // 未知类型返回中等分数
            }
        }

        /// <summary>
        /// 接近度评分计算 - 完整实现
        /// 
        /// 评分逻辑：距离目标点越近，得分越高
        /// 支持多种距离计算模式和评分曲线
        /// 
        /// 这种评分适用于：
        /// - 医疗包放置：优先选择离受伤玩家近的位置
        /// - 掩体选择：选择离当前位置最近的安全点
        /// - 资源收集：优先选择离资源点近的建筑位置
        /// 
        /// 完整实现包括：
        /// 1. 多种距离计算模式（欧几里得、曼哈顿、切比雪夫）
        /// 2. 可配置的评分曲线（线性、指数、对数）
        /// 3. 最优距离范围设置
        /// 4. 多目标点支持
        /// </summary>
        private static float CalculateProximityScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            if (!criterion.Parameters.ContainsKey("targetPoint"))
                return 0f;

            var targetPointArray = JsonUtils.Deserialize<float[]>(criterion.Parameters["targetPoint"].ToString());
            var targetPoint = new Vector3(targetPointArray[0], targetPointArray[1], targetPointArray[2]);
            
            // 距离计算模式
            var distanceMode = criterion.Parameters.ContainsKey("distanceMode") ? 
                criterion.Parameters["distanceMode"].ToString().ToLower() : "euclidean";
            
            // 评分曲线类型
            var scoringCurve = criterion.Parameters.ContainsKey("scoringCurve") ? 
                criterion.Parameters["scoringCurve"].ToString().ToLower() : "linear";
            
            // 最大距离（用于归一化）
            var maxDistance = criterion.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["maxDistance"]) : 100f;
            
            // 最优距离（在此距离获得最高分）
            var optimalDistance = criterion.Parameters.ContainsKey("optimalDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["optimalDistance"]) : 0f;
            
            // 计算距离
            var distance = MathUtils.CalculateDistance(cell.WorldPosition, targetPoint, distanceMode);
            
            // 处理最优距离情况
            if (optimalDistance > 0)
            {
                // 如果设置了最优距离，距离最优距离越近分数越高
                var distanceFromOptimal = Mathf.Abs(distance - optimalDistance);
                distance = distanceFromOptimal;
                maxDistance = Mathf.Max(maxDistance - optimalDistance, optimalDistance);
            }
            
            // 归一化距离
            var normalizedDistance = Mathf.Clamp01(distance / maxDistance);
            
            // 根据评分曲线计算最终分数
            float score = 0f;
            
            switch (scoringCurve)
            {
                case "linear":
                    score = 1f - normalizedDistance;
                    break;
                case "exponential":
                    // 指数衰减：距离增加时分数快速下降
                    var exponentialFactor = criterion.Parameters.ContainsKey("exponentialFactor") ? 
                        ParseUtils.ParseFloat(criterion.Parameters["exponentialFactor"]) : 2f;
                    score = Mathf.Pow(1f - normalizedDistance, exponentialFactor);
                    break;
                case "logarithmic":
                    // 对数衰减：距离增加时分数缓慢下降
                    score = 1f - Mathf.Log(1f + normalizedDistance * 9f) / Mathf.Log(10f);
                    break;
                case "smoothstep":
                    // 平滑步进：在中间范围变化最快
                    score = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
                    break;
                case "inverse":
                    // 反比衰减
                    score = 1f / (1f + normalizedDistance * normalizedDistance);
                    break;
                default:
                    score = 1f - normalizedDistance;
                    break;
            }
            
            return Mathf.Clamp01(score);
        }

        /// <summary>
        /// 远离度评分计算 - 完整实现
        /// 
        /// 评分逻辑：距离目标点越远，得分越高
        /// 支持多种距离计算模式和评分曲线
        /// 
        /// 这种评分适用于：
        /// - 安全位置选择：远离敌人或危险区域
        /// - 分散部署：避免资源过于集中
        /// - 撤退路线：选择远离战斗区域的路径
        /// 
        /// 与接近度评分相反，体现了EQS系统的灵活性
        /// </summary>
        private static float CalculateFarthestScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            if (!criterion.Parameters.ContainsKey("targetPoint"))
                return 0f;

            var targetPointArray = JsonUtils.Deserialize<float[]>(criterion.Parameters["targetPoint"].ToString());
            var targetPoint = new Vector3(targetPointArray[0], targetPointArray[1], targetPointArray[2]);
            
            // 距离计算模式
            var distanceMode = criterion.Parameters.ContainsKey("distanceMode") ? 
                criterion.Parameters["distanceMode"].ToString().ToLower() : "euclidean";
            
            // 评分曲线类型
            var scoringCurve = criterion.Parameters.ContainsKey("scoringCurve") ? 
                criterion.Parameters["scoringCurve"].ToString().ToLower() : "linear";
            
            // 最大距离（用于归一化）
            var maxDistance = criterion.Parameters.ContainsKey("maxDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["maxDistance"]) : 100f;
            
            // 最小有效距离（低于此距离得分为0）
            var minDistance = criterion.Parameters.ContainsKey("minDistance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["minDistance"]) : 0f;
            
            // 计算距离
            var distance = MathUtils.CalculateDistance(cell.WorldPosition, targetPoint, distanceMode);
            
            // 应用最小距离限制
            if (distance < minDistance)
                return 0f;
            
            // 归一化距离
            var effectiveDistance = distance - minDistance;
            var effectiveMaxDistance = maxDistance - minDistance;
            var normalizedDistance = Mathf.Clamp01(effectiveDistance / effectiveMaxDistance);
            
            // 根据评分曲线计算最终分数
            float score = 0f;
            
            switch (scoringCurve)
            {
                case "linear":
                    score = normalizedDistance;
                    break;
                case "exponential":
                    // 指数增长：距离增加时分数快速增长
                    var exponentialFactor = criterion.Parameters.ContainsKey("exponentialFactor") ? 
                        ParseUtils.ParseFloat(criterion.Parameters["exponentialFactor"]) : 2f;
                    score = Mathf.Pow(normalizedDistance, 1f / exponentialFactor);
                    break;
                case "logarithmic":
                    // 对数增长：距离增加时分数缓慢增长
                    score = Mathf.Log(1f + normalizedDistance * 9f) / Mathf.Log(10f);
                    break;
                case "smoothstep":
                    // 平滑步进
                    score = Mathf.SmoothStep(0f, 1f, normalizedDistance);
                    break;
                case "threshold":
                    // 阈值模式：超过阈值距离就给满分
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
        /// 对象密度评分计算 - 完整实现
        /// 
        /// 评分逻辑：根据指定半径内动态对象的数量和类型进行评分
        /// 
        /// 应用场景：
        /// 1. 高密度偏好：
        ///    - 商店位置：选择人流量大的区域
        ///    - 集会点：选择容易聚集的位置
        /// 
        /// 2. 低密度偏好（通过权重为负实现）：
        ///    - 隐蔽位置：避开人群密集区域
        ///    - 安静区域：远离喧嚣的地方
        /// 
        /// 完整实现包括：
        /// 1. 指定半径内的3D空间搜索
        /// 2. 对象类型过滤
        /// 3. 距离权重衰减
        /// 4. 多种密度计算模式
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
            
            // 计算搜索范围内的网格单元
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
                        
                        // 检查索引是否在网格范围内
                        if (checkIndices.x < 0 || checkIndices.x >= grid.Dimensions.x ||
                            checkIndices.y < 0 || checkIndices.y >= grid.Dimensions.y ||
                            checkIndices.z < 0 || checkIndices.z >= grid.Dimensions.z)
                            continue;
                        
                        var checkCellIndex = MathUtils.CoordinateToIndex(checkIndices, grid.Dimensions);
                        if (checkCellIndex >= grid.Cells.Length)
                            continue;
                        
                        var checkCell = grid.Cells[checkCellIndex];
                        var distance = Vector3.Distance(cellPosition, checkCell.WorldPosition);
                        
                        // 检查是否在搜索半径内
                        if (distance > radius)
                            continue;
                        
                        // 计算该单元格的贡献
                        var cellContribution = CalculateCellDensityContribution(
                            checkCell, distance, objectTypeFilter, useDistanceWeighting, densityMode);
                        
                        totalDensity += cellContribution;
                    }
                }
            }
            
            // 根据密度模式进行最终计算
            float finalScore = 0f;
            
            switch (densityMode)
            {
                case "count":
                    finalScore = totalDensity / maxDensity;
                    break;
                case "weighted":
                    // 已经在计算过程中应用了距离权重
                    finalScore = totalDensity / maxDensity;
                    break;
                case "inverse":
                    // 反向密度：密度越低分数越高
                    finalScore = 1f - (totalDensity / maxDensity);
                    break;
                default:
                    finalScore = totalDensity / maxDensity;
                    break;
            }
            
            return Mathf.Clamp01(finalScore);
        }
        
        /// <summary>
        /// 计算单个网格单元对密度的贡献
        /// </summary>
        private static float CalculateCellDensityContribution(EQSCell cell, float distance, 
            string objectTypeFilter, bool useDistanceWeighting, string densityMode)
        {
            var contribution = 0f;
            
            // 计算动态对象贡献
            foreach (var objectId in cell.DynamicOccupants)
            {
                // 对象类型过滤
                if (!string.IsNullOrEmpty(objectTypeFilter))
                {
                    // 这里需要根据实际情况获取对象类型
                    // 简化实现：假设对象ID包含类型信息或从环境中查找
                    var dynamicObj = _currentEnvironment.DynamicObjects
                        .FirstOrDefault(obj => obj.Id == objectId);
                    
                    if (dynamicObj != null && dynamicObj.Type != objectTypeFilter)
                        continue; // 不匹配的对象类型
                }
                
                var objectContribution = 1f;
                
                // 应用距离权重衰减
                if (useDistanceWeighting && distance > 0)
                {
                    // 使用平方反比衰减
                    objectContribution = 1f / (1f + distance * distance);
                }
                
                contribution += objectContribution;
            }
            
            // 考虑静态几何体的影响（可选）
            if (cell.StaticOccupancy)
            {
                var staticContribution = 0.1f; // 静态对象的基础贡献值
                
                if (useDistanceWeighting && distance > 0)
                {
                    staticContribution = staticContribution / (1f + distance * distance);
                }
                
                contribution += staticContribution;
            }
            
            return contribution;
        }
        

        
        /// <summary>
        /// 高度偏好评分计算
        /// 
        /// 根据点位的高度进行评分，支持多种高度偏好模式：
        /// - 高地偏好：越高分数越高（瞭望台、狙击点）
        /// - 低地偏好：越低分数越高（隐蔽、避风）
        /// - 特定高度：接近目标高度分数越高
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
                    // 越高越好
                    return Mathf.Clamp01((cellHeight - referenceHeight) / heightRange);
                
                case "lower":
                    // 越低越好
                    return Mathf.Clamp01((referenceHeight - cellHeight) / heightRange);
                
                case "specific":
                    // 接近特定高度越好
                    var heightDiff = Mathf.Abs(cellHeight - referenceHeight);
                    return Mathf.Clamp01(1f - (heightDiff / heightRange));
                
                case "avoid":
                    // 避开特定高度
                    var avoidDiff = Mathf.Abs(cellHeight - referenceHeight);
                    return Mathf.Clamp01(avoidDiff / heightRange);
                
                default:
                    return 0.5f;
            }
        }
        
        /// <summary>
        /// 坡度分析评分计算
        /// 
        /// 分析地形坡度，适用于：
        /// - 平坦地形偏好（建筑、停车）
        /// - 坡度地形偏好（滑雪、排水）
        /// - 特定坡度要求
        /// </summary>
        private static float CalculateSlopeAnalysisScore(EQSCell cell, EQSScoringCriterion criterion)
        {
            var preferredSlope = criterion.Parameters.ContainsKey("preferredSlope") ? 
                ParseUtils.ParseFloat(criterion.Parameters["preferredSlope"]) : 0f;
            
            var slopeMode = criterion.Parameters.ContainsKey("slopeMode") ? 
                criterion.Parameters["slopeMode"].ToString().ToLower() : "flat";
            
            var tolerance = criterion.Parameters.ContainsKey("tolerance") ? 
                ParseUtils.ParseFloat(criterion.Parameters["tolerance"]) : 10f;
            
            // 简化的坡度计算：检查周围单元格的高度差
            if (_currentEnvironment == null)
                return 0.5f;
            
            var grid = _currentEnvironment.Grid;
            var cellHeight = cell.WorldPosition.y;
            var heightDifferences = new List<float>();
            
            // 检查相邻单元格
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
                    // 平坦地形偏好
                    return Mathf.Clamp01(1f - (slopeAngle / tolerance));
                
                case "steep":
                    // 陡峭地形偏好
                    return Mathf.Clamp01(slopeAngle / tolerance);
                
                case "specific":
                    // 特定坡度偏好
                    var slopeDiff = Mathf.Abs(slopeAngle - preferredSlope);
                    return Mathf.Clamp01(1f - (slopeDiff / tolerance));
                
                default:
                    return 0.5f;
            }
        }
        
        /// <summary>
        /// 掩体质量评分计算
        /// 
        /// 评估位置的掩体价值：
        /// - 周围障碍物密度
        /// - 视线遮挡程度
        /// - 多方向保护
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
            var position = cell.WorldPosition + Vector3.up * 1.5f; // 眼睛高度
            
            // 检查方向数组
            Vector3[] checkDirections;
            
            if (threatDirections != null && threatDirections.Length > 0)
            {
                // 使用指定的威胁方向
                checkDirections = threatDirections.Select(dir => 
                    new Vector3(dir[0], dir[1], dir[2]).normalized).ToArray();
            }
            else
            {
                // 使用默认的8方向检查
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
                // 检查该方向是否有掩体
                if (Physics.Raycast(position, direction, coverRadius, LayerMask.GetMask("Default")))
                {
                    protectedDirections++;
                }
            }
            
            switch (coverMode)
            {
                case "omnidirectional":
                    // 全方向保护
                    coverScore = (float)protectedDirections / checkDirections.Length;
                    break;
                
                case "partial":
                    // 部分保护即可
                    coverScore = protectedDirections > 0 ? 1f : 0f;
                    break;
                
                case "majority":
                    // 大部分方向有保护
                    coverScore = protectedDirections >= (checkDirections.Length / 2) ? 1f : 0f;
                    break;
                
                default:
                    coverScore = (float)protectedDirections / checkDirections.Length;
                    break;
            }
            
            return Mathf.Clamp01(coverScore);
        }
        
        /// <summary>
        /// 路径复杂度评分计算
        /// 
        /// 评估到达该位置的路径复杂度：
        /// - 直线距离vs实际路径距离
        /// - 路径上的障碍物数量
        /// - 路径的曲折程度
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
                return 1f; // 起点位置
            
            switch (complexityMode)
            {
                case "simple":
                    // 简单的直线障碍检查
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
                    // 射线检查
                    var hasObstacle = Physics.Linecast(startPoint, cell.WorldPosition, LayerMask.GetMask("Default"));
                    return hasObstacle ? 0f : 1f;
                
                default:
                    return 0.5f;
            }
        }
        
        /// <summary>
        /// 多点评分计算
        /// 
        /// 同时考虑多个目标点的综合评分：
        /// - 到多个点的平均距离
        /// - 到最近点的距离
        /// - 到最远点的距离
        /// - 自定义权重组合
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
                    // 平均距离
                    var avgDistance = distances.Average();
                    return Mathf.Clamp01(1f - (avgDistance / maxDistance));
                
                case "closest":
                    // 最近点距离
                    var minDistance = distances.Min();
                    return Mathf.Clamp01(1f - (minDistance / maxDistance));
                
                case "farthest":
                    // 最远点距离
                    var maxDist = distances.Max();
                    return Mathf.Clamp01(1f - (maxDist / maxDistance));
                
                case "weighted":
                    // 加权平均
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
                        // 如果权重不匹配，回退到平均值
                        goto case "average";
                    }
                
                case "best":
                    // 最优（最近）点的分数
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
                    // 更健壮的center解析
                    float[] center;
                    try
                    {
                        center = JsonUtils.Deserialize<float[]>(areaData["center"].ToString());
                    }
                    catch
                    {
                        // 如果直接解析失败，尝试处理整数数组
                        center = ParseUtils.ParseFloatArray(areaData["center"]);
                    }

                    areaOfInterest.Center = new Vector3(center[0], center[1], center[2]);

                    if (type == "sphere")
                    {
                        // 更健壮的radius解析
                        float radius;
                        try
                        {
                            radius = ParseUtils.ParseFloat(areaData["radius"]);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"无法解析radius值: {areaData["radius"]}", ex);
                        }
                        areaOfInterest.Radius = radius;
                    }
                    else if (type == "box")
                    {
                        // 处理size数组
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
                throw new InvalidOperationException("解析兴趣区域失败: " + ex.Message, ex);
            }
        }



        /// <summary>
        /// 自动可视化查询结果，使用绿到红的颜色渐变
        /// 显示所有满足条件且有评分的点，不只是前几名
        /// </summary>
        private static void AutoVisualizeQueryResults(EQSQueryResult queryResult)
        {
            try
            {
                // if (_activeVisualizations.ContainsKey(queryResult.QueryID))
                // {
                //     CleanupVisualization(queryResult.QueryID);
                // }

                var visualization = new EQSVisualization
                {
                    QueryId = queryResult.QueryID,
                    DebugObjects = new List<GameObject>(),
                    ExpirationTime = DateTime.MaxValue // 永久保留，不自动清除
                };

                // 显示所有满足条件的点，不只是前几名
                foreach (var candidate in queryResult.Results.Select((c, index) => new { Candidate = c, Index = index }))
                {
                    // 根据评分计算颜色（绿到红渐变）
                    var color = CalculateScoreColor(candidate.Candidate.Score);
                    var debugObj = CreateScoredDebugMarker(candidate.Candidate, color, candidate.Index); // 不显示分数
                    visualization.DebugObjects.Add(debugObj);
                }

                _activeVisualizations[queryResult.QueryID] = visualization;
                Debug.Log($"[EQS] 自动创建查询 '{queryResult.QueryID}' 的可视化，共 {visualization.DebugObjects.Count} 个标记");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EQS] 自动可视化查询结果失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据评分计算颜色（0.0=红色，1.0=绿色）
        /// </summary>
        private static Color CalculateScoreColor(float score)
        {
            // 确保评分在0-1范围内
            score = Mathf.Clamp01(score);
            
            // 创建从红色到绿色的渐变
            // 红色 (1,0,0) -> 黄色 (1,1,0) -> 绿色 (0,1,0)
            if (score <= 0.5f)
            {
                // 从红色到黄色
                var t = score * 2f; // 0-0.5 映射到 0-1
                return new Color(1f, t, 0f);
            }
            else
            {
                // 从黄色到绿色
                var t = (score - 0.5f) * 2f; // 0.5-1 映射到 0-1
                return new Color(1f - t, 1f, 0f);
            }
        }

        /// <summary>
        /// 创建带评分的调试标记
        /// </summary>
        private static GameObject CreateScoredDebugMarker(EQSLocationCandidate candidate, Color color, int index)
        {
            // 创建调试标记GameObject
            var markerName = $"EQS_QueryResult_#{index}_Score{candidate.Score:F2}";
            var debugObj = new GameObject(markerName);
            debugObj.transform.position = candidate.WorldPosition;

            // 添加可视化组件
            var sphereRenderer = debugObj.AddComponent<MeshRenderer>();
            var meshFilter = debugObj.AddComponent<MeshFilter>();
            
            // 使用Unity内置的球体网格
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            
            // 创建兼容的材质
            var material = CreateCompatibleMaterial(color, true); // 查询结果需要发光
            sphereRenderer.material = material;

            // 设置统一大小（不根据排名或分数改变大小）
            var baseScale = 0.2f;
            debugObj.transform.localScale = Vector3.one * baseScale;

            // 添加EQS调试组件
            var debugComponent = debugObj.AddComponent<EQSDebugMarker>();
            debugComponent.Initialize(candidate);

            // 标记为编辑器专用对象
            debugObj.hideFlags = HideFlags.DontSave;

            return debugObj;
        }


        /// <summary>
        /// 创建兼容的材质（用于查询结果可视化）
        /// </summary>
        private static Material CreateCompatibleMaterial(Color color, bool enableEmission)
        {
            return MaterialUtils.CreateMaterial(color, enableEmission);
        }
    }

    // EQS调试标记组件
    public class EQSDebugMarker : MonoBehaviour
    {
        public Tool_EQS.EQSLocationCandidate Candidate { get; private set; }

        public void Initialize(Tool_EQS.EQSLocationCandidate candidate)
        {
            Candidate = candidate;
        }

        private void OnDrawGizmos()
        {
            if (Candidate == null) return;

            // 绘制Gizmos
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            // 绘制到关联对象的连线
            if (Candidate.AssociatedObjectIDs != null && Candidate.AssociatedObjectIDs.Count > 0)
            {
                Gizmos.color = Color.cyan;
                foreach (var objId in Candidate.AssociatedObjectIDs)
                {
                    // 这里可以添加查找对象并绘制连线的逻辑
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Candidate == null) return;
 
            // 选中时显示更多信息
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position, 0.3f);

            // 显示网格索引信息
            if (Candidate.CellIndices.HasValue)
            {
                var indices = Candidate.CellIndices.Value;
                var labelText = $"Grid: ({indices.x}, {indices.y}, {indices.z})\nScore: {Candidate.Score:F3}";
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, labelText);
                #endif
            }
        }
    }
} 