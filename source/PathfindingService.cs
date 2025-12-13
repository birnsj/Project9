using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// Shared pathfinding service using A* algorithm with object pooling for performance.
    /// Optimized for single-threaded usage (lock removed for better performance).
    /// </summary>
    public class PathfindingService
    {
        // Shared data structures to avoid allocations
        // Since usage is single-threaded, no lock is needed
        private static readonly PriorityQueue<(int x, int y), float> _sharedOpenSet = new();
        private static readonly Dictionary<(int x, int y), float> _sharedGScore = new();
        private static readonly Dictionary<(int x, int y), float> _sharedFScore = new();
        private static readonly HashSet<(int x, int y)> _sharedClosedSet = new();
        private static readonly Dictionary<(int x, int y), (int x, int y)> _sharedCameFrom = new();
        
        // Object pool for path lists to reduce GC allocations
        private static readonly Stack<List<Vector2>> _pathPool = new Stack<List<Vector2>>();
        
        // Pathfinding cache to avoid recalculating same paths
        private static readonly Dictionary<(int, int, int, int), (List<Vector2>? path, float timestamp)> _pathCache = new();
        private static float _lastCacheCleanup = 0.0f;
        
        /// <summary>
        /// Rent a path list from the pool (or create new if pool is empty)
        /// </summary>
        private static List<Vector2> RentPath()
        {
            if (_pathPool.Count > 0)
            {
                var path = _pathPool.Pop();
                path.Clear();
                return path;
            }
            return new List<Vector2>();
        }
        
        /// <summary>
        /// Return a path list to the pool for reuse
        /// </summary>
        public static void ReturnPath(List<Vector2>? path)
        {
            if (path != null && _pathPool.Count < GameConfig.PathfindingMaxPoolSize)
            {
                path.Clear();
                _pathPool.Push(path);
            }
        }
        
        /// <summary>
        /// Round vector to grid for cache key
        /// </summary>
        private static (int, int) RoundToGrid(Vector2 pos, float gridSize)
        {
            return ((int)(pos.X / gridSize), (int)(pos.Y / gridSize));
        }
        
        /// <summary>
        /// Clean old cache entries
        /// </summary>
        private static void CleanPathCache(float currentTime)
        {
            if (currentTime - _lastCacheCleanup < GameConfig.PathfindingCacheCleanupInterval)
                return;
            
            _lastCacheCleanup = currentTime;
            
            var keysToRemove = new List<(int, int, int, int)>();
            foreach (var kvp in _pathCache)
            {
                if (currentTime - kvp.Value.timestamp > GameConfig.PathfindingCacheDuration * 2)
                {
                    keysToRemove.Add(kvp.Key);
                    // Return cached path to pool if it exists
                    if (kvp.Value.path != null)
                        ReturnPath(kvp.Value.path);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _pathCache.Remove(key);
            }
        }
        
        /// <summary>
        /// Find path from start to end using A* algorithm with shared data structures
        /// Includes object pooling and caching for performance
        /// </summary>
        public static List<Vector2>? FindPath(
            Vector2 start, 
            Vector2 end, 
            Func<Vector2, bool> checkCollision,
            float gridCellWidth,
            float gridCellHeight)
        {
            // Check cache first (round to grid for cache key)
            float currentTime = (float)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
            var cacheKey = (
                RoundToGrid(start, gridCellWidth).Item1,
                RoundToGrid(start, gridCellWidth).Item2,
                RoundToGrid(end, gridCellWidth).Item1,
                RoundToGrid(end, gridCellWidth).Item2
            );
            
            // Check cache
            if (_pathCache.TryGetValue(cacheKey, out var cached))
            {
                // Return cached path if still valid
                if (currentTime - cached.timestamp < GameConfig.PathfindingCacheDuration)
                {
                    // Return a copy from pool (don't return the cached one)
                    if (cached.path != null && cached.path.Count > 0)
                    {
                        var cachedPath = RentPath();
                        cachedPath.AddRange(cached.path);
                        return cachedPath;
                    }
                    return null;
                }
                
                // Throttle: don't recalculate too frequently
                if (currentTime - cached.timestamp < GameConfig.PathfindingMinRequestInterval)
                {
                    // Return stale cached path
                    if (cached.path != null && cached.path.Count > 0)
                    {
                        var stalePath = RentPath();
                        stalePath.AddRange(cached.path);
                        return stalePath;
                    }
                    return null;
                }
            }
            
            // Clean cache periodically
            CleanPathCache(currentTime);
            
            // Log pathfinding attempt (only in debug builds to reduce overhead)
            #if DEBUG
            LogOverlay.Log($"[Pathfinding] Attempting path from ({start.X:F1}, {start.Y:F1}) to ({end.X:F1}, {end.Y:F1})", LogLevel.Debug);
            #endif
            // Clear shared data structures
            _sharedOpenSet.Clear();
            _sharedGScore.Clear();
            _sharedFScore.Clear();
            _sharedClosedSet.Clear();
            _sharedCameFrom.Clear();
                
                // Corner offset for collision checking (check corners to avoid clipping obstacles)
                float cornerOffsetX = gridCellWidth * 0.4f;
                float cornerOffsetY = gridCellHeight * 0.4f;
                
                // Convert world coordinates to grid coordinates
                (int x, int y) startCell = (
                    (int)Math.Floor(start.X / gridCellWidth),
                    (int)Math.Floor(start.Y / gridCellHeight)
                );
                
                (int x, int y) endCell = (
                    (int)Math.Floor(end.X / gridCellWidth),
                    (int)Math.Floor(end.Y / gridCellHeight)
                );
                
                // Early exit if start equals goal
                if (startCell == endCell)
                {
                    // But check if the actual position is valid
                    Vector2 endWorldPos = new Vector2(
                        endCell.x * gridCellWidth + gridCellWidth / 2,
                        endCell.y * gridCellHeight + gridCellHeight / 2
                    );
                    if (!checkCollision(endWorldPos))
                    {
                        return new List<Vector2> { end };
                    }
                    // If end is blocked, we'll need to find a path to a nearby valid cell
                }
                
                // Check if end position is in collision - if so, try to find a nearby valid end
                Vector2 endWorldPosCheck = new Vector2(
                    endCell.x * gridCellWidth + gridCellWidth / 2,
                    endCell.y * gridCellHeight + gridCellHeight / 2
                );
                
                // Check center and corners for better accuracy (using offsets declared at method start)
                bool endBlocked = checkCollision(endWorldPosCheck) ||
                    checkCollision(new Vector2(endWorldPosCheck.X - cornerOffsetX, endWorldPosCheck.Y - cornerOffsetY)) ||
                    checkCollision(new Vector2(endWorldPosCheck.X + cornerOffsetX, endWorldPosCheck.Y - cornerOffsetY)) ||
                    checkCollision(new Vector2(endWorldPosCheck.X - cornerOffsetX, endWorldPosCheck.Y + cornerOffsetY)) ||
                    checkCollision(new Vector2(endWorldPosCheck.X + cornerOffsetX, endWorldPosCheck.Y + cornerOffsetY));
                
                if (endBlocked)
                {
                    // End is blocked - try to find a nearby valid cell
                    bool foundValidEnd = false;
                    (int x, int y) originalEndCell = endCell;
                    
                    // Try nearby cells in expanding radius (increased for better pathfinding)
                    for (int radius = 1; radius <= 8 && !foundValidEnd; radius++)
                    {
                        for (int dx = -radius; dx <= radius && !foundValidEnd; dx++)
                        {
                            for (int dy = -radius; dy <= radius && !foundValidEnd; dy++)
                            {
                                if (Math.Abs(dx) < radius && Math.Abs(dy) < radius) continue; // Only check perimeter
                                
                                (int x, int y) testCell = (endCell.x + dx, endCell.y + dy);
                                Vector2 testWorldPos = new Vector2(
                                    testCell.x * gridCellWidth + gridCellWidth / 2,
                                    testCell.y * gridCellHeight + gridCellHeight / 2
                                );
                                
                                // Check center and corners
                                bool testBlocked = checkCollision(testWorldPos) ||
                                    checkCollision(new Vector2(testWorldPos.X - cornerOffsetX, testWorldPos.Y - cornerOffsetY)) ||
                                    checkCollision(new Vector2(testWorldPos.X + cornerOffsetX, testWorldPos.Y - cornerOffsetY)) ||
                                    checkCollision(new Vector2(testWorldPos.X - cornerOffsetX, testWorldPos.Y + cornerOffsetY)) ||
                                    checkCollision(new Vector2(testWorldPos.X + cornerOffsetX, testWorldPos.Y + cornerOffsetY));
                                
                                if (!testBlocked)
                                {
                                    endCell = testCell;
                                    foundValidEnd = true;
                                }
                            }
                        }
                    }
                    
                    if (!foundValidEnd)
                    {
                        LogOverlay.Log($"[Pathfinding] End position blocked and no nearby valid cells found at ({end.X:F1}, {end.Y:F1})", LogLevel.Warning);
                    }
                    else
                    {
                        LogOverlay.Log($"[Pathfinding] Found alternative end cell ({endCell.x}, {endCell.y}) near blocked position", LogLevel.Info);
                    }
                }
                
                // Check if start position is in collision - if so, try to find a nearby valid start
                Vector2 startWorldPos = new Vector2(
                    startCell.x * gridCellWidth + gridCellWidth / 2,
                    startCell.y * gridCellHeight + gridCellHeight / 2
                );
                
                // Check center and corners for better accuracy
                bool startBlocked = checkCollision(startWorldPos) ||
                    checkCollision(new Vector2(startWorldPos.X - cornerOffsetX, startWorldPos.Y - cornerOffsetY)) ||
                    checkCollision(new Vector2(startWorldPos.X + cornerOffsetX, startWorldPos.Y - cornerOffsetY)) ||
                    checkCollision(new Vector2(startWorldPos.X - cornerOffsetX, startWorldPos.Y + cornerOffsetY)) ||
                    checkCollision(new Vector2(startWorldPos.X + cornerOffsetX, startWorldPos.Y + cornerOffsetY));
                
                if (startBlocked)
                {
                    // Start is blocked - try to find a nearby valid cell
                    bool foundValidStart = false;
                    for (int radius = 1; radius <= 3 && !foundValidStart; radius++)
                    {
                        for (int dx = -radius; dx <= radius && !foundValidStart; dx++)
                        {
                            for (int dy = -radius; dy <= radius && !foundValidStart; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                if (Math.Abs(dx) < radius && Math.Abs(dy) < radius) continue; // Only check perimeter at each radius
                                
                                (int x, int y) testCell = (startCell.x + dx, startCell.y + dy);
                                Vector2 testWorldPos = new Vector2(
                                    testCell.x * gridCellWidth + gridCellWidth / 2,
                                    testCell.y * gridCellHeight + gridCellHeight / 2
                                );
                                
                                // Check center and corners
                                bool testBlocked = checkCollision(testWorldPos) ||
                                    checkCollision(new Vector2(testWorldPos.X - cornerOffsetX, testWorldPos.Y - cornerOffsetY)) ||
                                    checkCollision(new Vector2(testWorldPos.X + cornerOffsetX, testWorldPos.Y - cornerOffsetY)) ||
                                    checkCollision(new Vector2(testWorldPos.X - cornerOffsetX, testWorldPos.Y + cornerOffsetY)) ||
                                    checkCollision(new Vector2(testWorldPos.X + cornerOffsetX, testWorldPos.Y + cornerOffsetY));
                                
                                if (!testBlocked)
                                {
                                    startCell = testCell;
                                    foundValidStart = true;
                                }
                            }
                        }
                    }
                    
                    // If still no valid start found, pathfinding will likely fail
                    if (!foundValidStart)
                    {
                        LogOverlay.Log($"[Pathfinding] Start position blocked and no nearby valid cells found at ({start.X:F1}, {start.Y:F1})", LogLevel.Warning);
                    }
                    else
                    {
                        LogOverlay.Log($"[Pathfinding] Found alternative start cell ({startCell.x}, {startCell.y}) near blocked position", LogLevel.Info);
                    }
                }
                
                // Initialize A* data structures
                _sharedGScore[startCell] = 0;
                _sharedFScore[startCell] = Heuristic(startCell, endCell);
                _sharedOpenSet.Enqueue(startCell, _sharedFScore[startCell]);
                
                int iterations = 0;
                const int MAX_ITERATIONS = GameConfig.PathfindingMaxIterations;
                
                while (_sharedOpenSet.Count > 0 && iterations < MAX_ITERATIONS)
                {
                    iterations++;
                    
                    // Get cell with lowest f score
                    (int x, int y) current = _sharedOpenSet.Dequeue();
                    
                    // Check if we reached the goal
                    if (current == endCell)
                    {
                        var path = ReconstructPath(_sharedCameFrom, current, start, end, gridCellWidth, gridCellHeight);
                        #if DEBUG
                        LogOverlay.Log($"[Pathfinding] Path found successfully in {iterations} iterations, {path.Count} waypoints", LogLevel.Debug);
                        #endif
                        
                        // Cache the path
                        var pathCopy = RentPath();
                        pathCopy.AddRange(path);
                        _pathCache[cacheKey] = (pathCopy, currentTime);
                        
                        return path;
                    }
                    
                    // Add to closed set
                    _sharedClosedSet.Add(current);
                    
                    // Check all neighbors (8-directional)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            
                            (int x, int y) neighbor = (current.x + dx, current.y + dy);
                            
                            // Skip if already evaluated
                            if (_sharedClosedSet.Contains(neighbor))
                                continue;
                            
                            // Check collision for this neighbor
                            // Check center and also check corners to ensure we don't clip obstacles
                            Vector2 neighborWorldPos = new Vector2(
                                neighbor.x * gridCellWidth + gridCellWidth / 2,
                                neighbor.y * gridCellHeight + gridCellHeight / 2
                            );
                            
                            // Check center and corners for better accuracy
                            bool neighborBlocked = checkCollision(neighborWorldPos) ||
                                checkCollision(new Vector2(neighborWorldPos.X - cornerOffsetX, neighborWorldPos.Y - cornerOffsetY)) ||
                                checkCollision(new Vector2(neighborWorldPos.X + cornerOffsetX, neighborWorldPos.Y - cornerOffsetY)) ||
                                checkCollision(new Vector2(neighborWorldPos.X - cornerOffsetX, neighborWorldPos.Y + cornerOffsetY)) ||
                                checkCollision(new Vector2(neighborWorldPos.X + cornerOffsetX, neighborWorldPos.Y + cornerOffsetY));
                            
                            if (neighborBlocked)
                                continue;
                            
                            // Calculate movement cost (diagonal = 1.414, orthogonal = 1.0)
                            float moveCost = (dx != 0 && dy != 0) ? 1.414f : 1.0f;
                            float tentativeGScore = _sharedGScore[current] + moveCost;
                            
                            // Check if this path to neighbor is better
                            if (!_sharedGScore.ContainsKey(neighbor) || tentativeGScore < _sharedGScore[neighbor])
                            {
                                _sharedCameFrom[neighbor] = current;
                                _sharedGScore[neighbor] = tentativeGScore;
                                _sharedFScore[neighbor] = tentativeGScore + Heuristic(neighbor, endCell);
                                
                                _sharedOpenSet.Enqueue(neighbor, _sharedFScore[neighbor]);
                            }
                        }
                    }
                }
                
                // No path found - log reason
                if (iterations >= MAX_ITERATIONS)
                {
                    LogOverlay.Log($"[Pathfinding] Failed: Hit MAX_ITERATIONS limit ({MAX_ITERATIONS}) - path too long or complex", LogLevel.Error);
                    LogOverlay.Log($"[Pathfinding] Start: ({start.X:F1}, {start.Y:F1}) -> End: ({end.X:F1}, {end.Y:F1})", LogLevel.Debug);
                    LogOverlay.Log($"[Pathfinding] Grid cells: Start({startCell.x}, {startCell.y}) -> End({endCell.x}, {endCell.y})", LogLevel.Debug);
                }
                else if (_sharedOpenSet.Count == 0)
                {
                    LogOverlay.Log($"[Pathfinding] Failed: No valid path exists - destination unreachable", LogLevel.Error);
                    LogOverlay.Log($"[Pathfinding] Start: ({start.X:F1}, {start.Y:F1}) -> End: ({end.X:F1}, {end.Y:F1})", LogLevel.Debug);
                    LogOverlay.Log($"[Pathfinding] Grid cells: Start({startCell.x}, {startCell.y}) -> End({endCell.x}, {endCell.y})", LogLevel.Debug);
                    LogOverlay.Log($"[Pathfinding] Explored {iterations} cells before giving up", LogLevel.Debug);
                }
                
                // Cache null result (no path found) to avoid repeated failed attempts
                _pathCache[cacheKey] = (null, currentTime);
                return null;
        }
        
        /// <summary>
        /// Smooth path by removing unnecessary waypoints (uses pooled list)
        /// </summary>
        public static List<Vector2>? SmoothPath(List<Vector2>? path, Func<Vector2, Vector2, bool>? checkLineOfSight = null)
        {
            if (path == null || path.Count <= 2)
                return path;
            
            List<Vector2> smoothedPath = RentPath(); // Use pooled list
            smoothedPath.Add(path[0]); // Always keep start
            
            int currentIndex = 0;
            
            while (currentIndex < path.Count - 1)
            {
                int farthestVisible = currentIndex + 1;
                
                // Find the farthest point we can see from current point
                for (int i = currentIndex + 2; i < path.Count; i++)
                {
                    // If we can see point i from current, skip intermediate points
                    if (checkLineOfSight == null || !checkLineOfSight(path[currentIndex], path[i]))
                    {
                        farthestVisible = i;
                    }
                    else
                    {
                        break; // Line of sight blocked
                    }
                }
                
                // Add the farthest visible point
                if (farthestVisible != currentIndex + 1 || farthestVisible == path.Count - 1)
                {
                    smoothedPath.Add(path[farthestVisible]);
                }
                else
                {
                    smoothedPath.Add(path[currentIndex + 1]);
                }
                
                currentIndex = farthestVisible;
            }
            
            return smoothedPath;
        }
        
        /// <summary>
        /// Simplify path by removing collinear points (uses pooled list)
        /// </summary>
        public static List<Vector2>? SimplifyPath(List<Vector2>? path, float threshold = 0.1f)
        {
            if (path == null || path.Count <= 2)
                return path;
            
            List<Vector2> simplified = RentPath(); // Use pooled list
            simplified.Add(path[0]); // Always keep start
            
            for (int i = 1; i < path.Count - 1; i++)
            {
                Vector2 prev = path[i - 1];
                Vector2 current = path[i];
                Vector2 next = path[i + 1];
                
                // Calculate angle between segments
                Vector2 dir1 = Vector2.Normalize(current - prev);
                Vector2 dir2 = Vector2.Normalize(next - current);
                
                float dot = Vector2.Dot(dir1, dir2);
                
                // If not moving in roughly same direction, keep this point
                if (dot < 1.0f - threshold)
                {
                    simplified.Add(current);
                }
            }
            
            simplified.Add(path[path.Count - 1]); // Always keep end
            
            return simplified;
        }
        
        /// <summary>
        /// Heuristic for A* (Euclidean distance)
        /// </summary>
        private static float Heuristic((int x, int y) a, (int x, int y) b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        
        /// <summary>
        /// Reconstruct path from A* results (uses pooled list)
        /// </summary>
        private static List<Vector2> ReconstructPath(
            Dictionary<(int x, int y), (int x, int y)> cameFrom,
            (int x, int y) current,
            Vector2 startWorld,
            Vector2 endWorld,
            float gridCellWidth,
            float gridCellHeight)
        {
            List<Vector2> path = RentPath(); // Use pooled list
            
            // Walk backwards through the path
            while (cameFrom.ContainsKey(current))
            {
                Vector2 worldPos = new Vector2(
                    current.x * gridCellWidth + gridCellWidth / 2,
                    current.y * gridCellHeight + gridCellHeight / 2
                );
                path.Add(worldPos);
                current = cameFrom[current];
            }
            
            // Reverse to get start-to-end order
            path.Reverse();
            
            // Replace first waypoint with actual start position
            if (path.Count > 0)
                path[0] = startWorld;
            
            // Add final destination
            path.Add(endWorld);
            
            return path;
        }
        
        /// <summary>
        /// Get estimated path length without computing full path
        /// </summary>
        public static float EstimatePathLength(Vector2 start, Vector2 end, float gridCellWidth, float gridCellHeight)
        {
            (int x, int y) startCell = (
                (int)Math.Floor(start.X / gridCellWidth),
                (int)Math.Floor(start.Y / gridCellHeight)
            );
            
            (int x, int y) endCell = (
                (int)Math.Floor(end.X / gridCellWidth),
                (int)Math.Floor(end.Y / gridCellHeight)
            );
            
            return Heuristic(startCell, endCell) * Math.Max(gridCellWidth, gridCellHeight);
        }
    }
}

