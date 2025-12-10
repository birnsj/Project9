using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    public class Player
    {
        private Vector2 _position;
        private Vector2? _targetPosition;
        private float _walkSpeed;
        private float _runSpeed;
        private float _sneakSpeed;
        private float _distanceThreshold;
        private float _currentSpeed;
        private Texture2D? _texture;
        private Color _color;
        private Color _normalColor;
        private Color _sneakColor;
        private int _size;
        private bool _isSneaking;
        private Texture2D? _diamondTexture;
        private float _flashDuration;
        private float _flashTimer;
        private float _flashInterval;
        private float _flashTime;
        private bool _isFlashing;
        private Vector2? _waypoint; // Intermediate waypoint for pathfinding around obstacles
        private List<Vector2> _path; // Path of waypoints to follow
        private float _stuckTimer; // Timer to detect if player is stuck
        private int _preferredSlideDirection; // -1 for left, 1 for right, 0 for no preference
        private const float STUCK_THRESHOLD = 0.5f; // Seconds before considering stuck

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = value;
        }

        public float RunSpeed
        {
            get => _runSpeed;
            set => _runSpeed = value;
        }

        public float CurrentSpeed => _currentSpeed;

        public bool IsSneaking => _isSneaking;

        public void TakeHit()
        {
            _isFlashing = true;
            _flashTimer = _flashDuration;
            _flashTime = 0.0f;
        }

        public void ToggleSneak()
        {
            _isSneaking = !_isSneaking;
            _color = _isSneaking ? _sneakColor : _normalColor;
        }

        public Player(Vector2 startPosition)
        {
            _position = startPosition;
            _targetPosition = null;
            _walkSpeed = GameConfig.PlayerWalkSpeed;
            _runSpeed = GameConfig.PlayerRunSpeed;
            _sneakSpeed = _walkSpeed * GameConfig.PlayerSneakSpeedMultiplier;
            _distanceThreshold = 100.0f; // pixels
            _currentSpeed = 0.0f;
            _normalColor = Color.Red;
            _sneakColor = Color.Purple;
            _color = _normalColor;
            _size = 32;
            _isSneaking = false;
            _flashDuration = 0.5f; // Total flash duration in seconds
            _flashTimer = 0.0f;
            _flashInterval = 0.1f; // Time between flash on/off
            _flashTime = 0.0f;
            _isFlashing = false;
            _waypoint = null;
            _path = new List<Vector2>();
            _stuckTimer = 0.0f;
            _preferredSlideDirection = 0; // No preference initially
        }

        public void SetTarget(Vector2 target, Func<Vector2, bool>? checkCollision = null, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            // Check if target location itself is blocked by TERRAIN collision only
            // (Allow clicking near enemies for attack or movement)
            if (checkTerrainOnly != null && checkTerrainOnly(target))
            {
                Console.WriteLine("[Player] Target location is blocked by terrain - click ignored");
                return; // Don't set target if it's inside a wall
            }
            else if (checkCollision != null && checkTerrainOnly == null && checkCollision(target))
            {
                // Fallback if no terrain-only check provided
                Console.WriteLine("[Player] Target location is blocked by collision - click ignored");
                return;
            }
            
            _targetPosition = target;
            _waypoint = null; // Clear waypoint when setting new target
            _path?.Clear(); // Reuse existing list to avoid allocation
            _stuckTimer = 0.0f;
            _preferredSlideDirection = 0; // Reset slide preference on new target
            
            // First check if direct path is clear - only use grid-based pathfinding if blocked
            if (checkCollision != null)
            {
                Vector2 direction = target - _position;
                float distance = direction.Length();
                
                // Check if direct path is clear
                bool pathClear = true;
                int samples = (int)(distance / 16.0f) + 1;
                for (int i = 0; i <= samples; i++)
                {
                    float t = (float)i / samples;
                    Vector2 samplePoint = _position + (target - _position) * t;
                    if (checkCollision(samplePoint))
                    {
                        pathClear = false;
                        break;
                    }
                }
                
                // Only use pathfinding if direct path is blocked
                if (!pathClear)
                {
                    _path = FindPathUsingGridCells(_position, target, checkCollision);
                    if (_path == null || _path.Count == 0)
                    {
                        Console.WriteLine("[Player] Pathfinding failed - no path found to target");
                    }
                }
                // If path is clear, _path will remain empty and player will move directly
            }
        }

        public void ClearTarget()
        {
            _targetPosition = null;
            _waypoint = null;
            _path.Clear(); // Reuse existing list to avoid allocation
            _currentSpeed = 0.0f;
            _stuckTimer = 0.0f;
        }

        public void Update(Vector2? followPosition, float deltaTime, Func<Vector2, bool>? checkCollision = null, Func<Vector2, Vector2, bool>? checkLineOfSight = null)
        {
            // Update flash timer
            if (_isFlashing)
            {
                _flashTimer -= deltaTime;
                _flashTime += deltaTime;
                
                if (_flashTimer <= 0.0f)
                {
                    _isFlashing = false;
                    _flashTimer = 0.0f;
                    _flashTime = 0.0f;
                }
            }

            Vector2? moveTarget = null;

            // Priority: follow position (mouse held) > target position (click)
            if (followPosition.HasValue)
            {
                // Add dead zone to prevent jitter when mouse is very close
                // Larger dead zone when sneaking due to slower movement
                float deadZone = _isSneaking ? 8.0f : 2.0f;
                Vector2 direction = followPosition.Value - _position;
                float distance = direction.Length();
                
                // Only update target if mouse moved significantly (dead zone)
                if (distance > deadZone)
                {
                    moveTarget = followPosition.Value;
                }
                else
                {
                    // Mouse is very close, keep current target or stop
                    if (_targetPosition.HasValue)
                    {
                        moveTarget = _targetPosition.Value;
                    }
                }
            }
            else if (_targetPosition.HasValue)
            {
                // Use pathfinding path if available
                if (_path != null && _path.Count > 0)
                {
                    // Remove waypoints we've passed
                    while (_path.Count > 0)
                    {
                        float distToNext = Vector2.Distance(_position, _path[0]);
                        if (distToNext < 10.0f)
                        {
                            _path.RemoveAt(0);
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    // Use next waypoint in path if available
                    if (_path.Count > 0)
                    {
                        moveTarget = _path[0];
                    }
                    else
                    {
                        // Path complete - move directly to exact target
                        moveTarget = _targetPosition.Value;
                    }
                }
                else
                {
                    // Use waypoint if available, otherwise use target
                    moveTarget = _waypoint ?? _targetPosition.Value;
                }
            }

            if (moveTarget.HasValue)
            {
                Vector2 direction = moveTarget.Value - _position;
                float distance = direction.Length();
                
                // Calculate distance to final target for speed determination (not waypoint distance)
                float distanceToFinalTarget = _targetPosition.HasValue 
                    ? Vector2.Distance(_position, _targetPosition.Value) 
                    : distance;

                // Check if this is the final target (for smooth deceleration)
                bool isFinalTarget = !followPosition.HasValue && 
                                    _targetPosition.HasValue && 
                                    (moveTarget == _targetPosition.Value || (_path != null && _path.Count == 0));

                // Determine speed with smooth deceleration near final target
                float baseSpeed = _isSneaking ? _sneakSpeed : _runSpeed;
                
                if (isFinalTarget && distanceToFinalTarget < GameConfig.PlayerSlowdownRadius)
                {
                    // Smooth deceleration - slow down proportionally as we approach target
                    float slowdownFactor = distanceToFinalTarget / GameConfig.PlayerSlowdownRadius;
                    // Ensure minimum speed to prevent getting stuck (20% of base speed)
                    slowdownFactor = MathHelper.Max(slowdownFactor, 0.2f);
                    _currentSpeed = baseSpeed * slowdownFactor;
                }
                else
                {
                    // Full speed when far from target or following cursor
                    _currentSpeed = baseSpeed;
                }

                // Use precise stop threshold for final target
                float stopThreshold = isFinalTarget ? GameConfig.PlayerStopThreshold : (_isSneaking ? 10.0f : 5.0f);
                
                // Check if we've reached the target
                if (distance <= stopThreshold && isFinalTarget && _targetPosition.HasValue)
                {
                    // Reached target - snap immediately and stop all movement
                    _position = _targetPosition.Value;
                    _targetPosition = null;
                    _waypoint = null;
                    _path?.Clear();
                    _currentSpeed = 0.0f;
                }
                else if (distance > stopThreshold)
                {
                    direction.Normalize();
                    float moveDistance = _currentSpeed * deltaTime;
                    
                    // Don't overshoot the target
                    if (moveDistance > distance)
                    {
                        moveDistance = distance;
                    }

                    // Calculate next position
                    Vector2 nextPosition = _position + direction * moveDistance;
                    
                    // If we have a pathfinding path, use it
                    if (_path != null && _path.Count > 0)
                    {
                        // Check if direct path to target is now clear - if so, clear pathfinding
                        // But only check if we're close to the target to avoid premature clearing
                        if (_targetPosition.HasValue && checkCollision != null)
                        {
                            Vector2 directDirection = _targetPosition.Value - _position;
                            float directDistance = directDirection.Length();
                            
                            // Only check if we're reasonably close to the target (within 3 grid cells)
                            if (directDistance < GameConfig.PathfindingGridCellWidth * 3)
                            {
                                bool directPathClear = true;
                                int samples = (int)(directDistance / 16.0f) + 1;
                                for (int i = 0; i <= samples; i++)
                                {
                                    float t = (float)i / samples;
                                    Vector2 samplePoint = _position + directDirection * t;
                                    if (checkCollision(samplePoint))
                                    {
                                        directPathClear = false;
                                        break;
                                    }
                                }
                                
                                // If direct path is clear and we're close, abandon pathfinding
                                if (directPathClear)
                                {
                                    _path.Clear();
                                }
                            }
                        }
                        
                        // If pathfinding path still exists, use it
                        if (_path != null && _path.Count > 0)
                        {
                            // Check for collision at next position
                            bool hasCollision = checkCollision != null && checkCollision(nextPosition);
                            
                            if (!hasCollision)
                            {
                                // Path is clear - move to next position
                                _position = nextPosition;
                                _stuckTimer = 0.0f;
                            }
                            else
                            {
                                // Pathfinding waypoint blocked - try sliding first
                                Vector2 slidePosition = TrySlideAlongCollision(_position, nextPosition, direction, moveDistance, checkCollision);
                                if (slidePosition != _position)
                                {
                                    _position = slidePosition;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                    // Can't slide - recalculate path
                                    if (checkCollision != null && _targetPosition.HasValue)
                                    {
                                        _path = FindPathUsingGridCells(_position, _targetPosition.Value, checkCollision);
                                    }
                                    _stuckTimer += deltaTime;
                                }
                            }
                            return; // Exit early - pathfinding handles movement
                        }
                    }
                    
                    // No pathfinding path - check for collision
                    bool hasCollision2 = checkCollision != null && checkCollision(nextPosition);
                    
                    if (hasCollision2)
                    {
                        // Collision detected - always use A* pathfinding when we have a target
                        if (_targetPosition.HasValue && !followPosition.HasValue && checkCollision != null)
                        {
                            // Always try pathfinding when collision is detected and we have a target
                            // This ensures we can navigate around obstacles and through holes
                            if (_path == null || _path.Count == 0)
                            {
                                _path = FindPathUsingGridCells(_position, _targetPosition.Value, checkCollision);
                            }
                            
                            if (_path != null && _path.Count > 0)
                            {
                                _preferredSlideDirection = 0;
                                _stuckTimer = 0.0f;
                                return; // Will use pathfinding next frame
                            }
                        }
                        
                        // No pathfinding available or following mouse - try sliding for smooth edge movement
                        Vector2 slidePosition = TrySlideAlongCollision(_position, nextPosition, direction, moveDistance, checkCollision);
                        if (slidePosition != _position)
                        {
                            _position = slidePosition;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            // Can't slide - stuck
                            _preferredSlideDirection = 0;
                            _stuckTimer += deltaTime;
                            if (_stuckTimer > STUCK_THRESHOLD && !followPosition.HasValue)
                            {
                                ClearTarget();
                            }
                        }
                    }
                    else
                    {
                        // Path is clear - move normally
                        _position = nextPosition;
                        _stuckTimer = 0.0f; // Reset stuck timer if we moved
                        _preferredSlideDirection = 0; // Reset preference when moving freely
                    }
                }
                else
                {
                    // Reached intermediate waypoint
                    if (_waypoint.HasValue && moveTarget == _waypoint.Value)
                    {
                        // Reached waypoint - clear it and continue to target
                        _waypoint = null;
                        _stuckTimer = 0.0f;
                    }
                    // Note: Final target stop is now handled above for smoother stopping
                }
            }
            else
            {
                _currentSpeed = 0.0f;
            }
        }

        private Vector2? FindWaypointAroundObstacle(Vector2 from, Vector2 to, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return null;
            
            // Use a simple pathfinding algorithm to find openings
            // Search in a grid pattern around obstacles to find clear paths
            
            Vector2 direction = to - from;
            float distance = direction.Length();
            if (distance < 1.0f) return null;
            direction.Normalize();
            
            // Search in multiple directions to find openings
            // Check 8 directions: forward, forward-left, forward-right, left, right, back-left, back-right, back
            float[] angles = { 0f, MathHelper.PiOver4, -MathHelper.PiOver4, MathHelper.PiOver2, -MathHelper.PiOver2, 
                               3 * MathHelper.PiOver4, -3 * MathHelper.PiOver4, MathHelper.Pi };
            
            float baseAngle = (float)Math.Atan2(direction.Y, direction.X);
            
            // Try different distances
            float[] searchDistances = { 64.0f, 96.0f, 128.0f, 160.0f, 192.0f };
            
            foreach (float searchDist in searchDistances)
            {
                foreach (float angleOffset in angles)
                {
                    float angle = baseAngle + angleOffset;
                    Vector2 searchDir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    Vector2 candidate = from + searchDir * searchDist;
                    
                    // Check if candidate position is clear
                    if (!checkCollision(candidate))
                    {
                        // Check if we can make progress toward target from this position
                        Vector2 toTarget = to - candidate;
                        float toTargetDist = toTarget.Length();
                        
                        // Candidate should get us closer to target (or at least not much further)
                        if (toTargetDist < distance * 1.3f)
                        {
                            // Check if there's a clearer path from candidate to target
                            // Sample a few points along the path
                            bool pathMostlyClear = true;
                            int samples = 5;
                            for (int i = 1; i <= samples; i++)
                            {
                                float t = (float)i / (samples + 1);
                                Vector2 samplePoint = candidate + (to - candidate) * t;
                                if (checkCollision(samplePoint))
                                {
                                    pathMostlyClear = false;
                                    break;
                                }
                            }
                            
                            if (pathMostlyClear || toTargetDist < distance * 0.9f)
                            {
                                return candidate;
                            }
                        }
                    }
                }
            }
            
            // If no good waypoint found, try simple perpendicular avoidance
            Vector2 perpLeft = new Vector2(-direction.Y, direction.X);
            Vector2 perpRight = new Vector2(direction.Y, -direction.X);
            
            foreach (float searchDist in searchDistances)
            {
                Vector2 waypointLeft = from + perpLeft * searchDist;
                if (!checkCollision(waypointLeft))
                {
                    Vector2 toTarget = to - waypointLeft;
                    if (toTarget.Length() < distance * 1.5f)
                    {
                        return waypointLeft;
                    }
                }
                
                Vector2 waypointRight = from + perpRight * searchDist;
                if (!checkCollision(waypointRight))
                {
                    Vector2 toTarget = to - waypointRight;
                    if (toTarget.Length() < distance * 1.5f)
                    {
                        return waypointRight;
                    }
                }
            }
            
            return null; // Couldn't find a waypoint
        }
        
        private List<Vector2> FindPathUsingGridCells(Vector2 start, Vector2 end, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return new List<Vector2>();
            
            List<Vector2> path = new List<Vector2>();
            
            // Convert positions to grid cell coordinates
            // Grid cells are centered at their positions
            int startGridX = (int)Math.Round(start.X / GameConfig.PathfindingGridCellWidth);
            int startGridY = (int)Math.Round(start.Y / GameConfig.PathfindingGridCellHeight);
            int endGridX = (int)Math.Round(end.X / GameConfig.PathfindingGridCellWidth);
            int endGridY = (int)Math.Round(end.Y / GameConfig.PathfindingGridCellHeight);
            
            // If start and end are in the same or adjacent cells, just return direct path
            if (Math.Abs(startGridX - endGridX) <= 1 && Math.Abs(startGridY - endGridY) <= 1)
            {
                return new List<Vector2> { end };
            }
            
            // Simple A* pathfinding on 64x32 grid cells using PriorityQueue for efficiency
            var openSet = new PriorityQueue<(int x, int y), float>();
            var openSetLookup = new HashSet<(int x, int y)>(); // For fast Contains checks
            var closedSet = new HashSet<(int x, int y)>();
            var cameFrom = new Dictionary<(int x, int y), (int x, int y)>();
            var gScore = new Dictionary<(int x, int y), float>();
            
            (int x, int y) startNode = (startGridX, startGridY);
            (int x, int y) endNode = (endGridX, endGridY);
            
            float startF = Heuristic(startNode, endNode);
            openSet.Enqueue(startNode, startF);
            openSetLookup.Add(startNode);
            gScore[startNode] = 0;
            
            // Limit search to reasonable area
            int maxSearchRadius = (int)(GameConfig.PathfindingMaxSearchDistance / GameConfig.PathfindingGridCellWidth);
            int maxIterations = GameConfig.PathfindingMaxIterations;
            int iterations = 0;
            
            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                
                // Get node with lowest fScore (O(log n) instead of O(n))
                (int x, int y) current = openSet.Dequeue();
                openSetLookup.Remove(current);
                
                if (current.x == endNode.x && current.y == endNode.y)
                {
                    // Reconstruct path using grid cell centers
                    path = ReconstructPath(cameFrom, current, start, end, GameConfig.PathfindingGridCellWidth, GameConfig.PathfindingGridCellHeight);
                    break;
                }
                
                closedSet.Add(current);
                
                // Check neighbors (8 directions for isometric grid)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        (int x, int y) neighbor = (current.x + dx, current.y + dy);
                        
                        // Skip if out of search radius
                        if (Math.Abs(neighbor.x - startGridX) > maxSearchRadius ||
                            Math.Abs(neighbor.y - startGridY) > maxSearchRadius)
                            continue;
                        
                        if (closedSet.Contains(neighbor))
                            continue;
                        
                        // Check if neighbor grid cell is walkable (check center of cell)
                        Vector2 neighborCellCenter = new Vector2(neighbor.x * GameConfig.PathfindingGridCellWidth, neighbor.y * GameConfig.PathfindingGridCellHeight);
                        if (checkCollision(neighborCellCenter))
                            continue;
                        
                        float tentativeGScore = gScore.GetValueOrDefault(current, float.MaxValue) + 
                                               (dx != 0 && dy != 0 ? 1.414f : 1.0f); // Diagonal cost
                        
                        if (tentativeGScore >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                            continue;
                        
                        // This path to neighbor is better than any previous one
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        float fScoreNeighbor = tentativeGScore + Heuristic(neighbor, endNode);
                        
                        if (!openSetLookup.Contains(neighbor))
                        {
                            openSet.Enqueue(neighbor, fScoreNeighbor);
                            openSetLookup.Add(neighbor);
                        }
                    }
                }
            }
            
            // If no path found, return empty list (will use wall sliding)
            return path;
        }
        
        private float Heuristic((int x, int y) a, (int x, int y) b)
        {
            // Euclidean distance for per-pixel pathfinding
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
        
        private List<Vector2> ReconstructPath(Dictionary<(int x, int y), (int x, int y)> cameFrom, 
                                               (int x, int y) current, 
                                               Vector2 start, 
                                               Vector2 end, 
                                               float gridSizeX, 
                                               float gridSizeY)
        {
            List<Vector2> path = new List<Vector2>();
            path.Add(end); // Add final destination
            
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                Vector2 worldPos = new Vector2(current.x * gridSizeX, current.y * gridSizeY);
                path.Insert(0, worldPos);
            }
            
            return path;
        }

        private bool IsPathClear(Vector2 from, Vector2 to, Func<Vector2, bool>? checkCollision)
        {
            // Check if path is clear by sampling points along the line
            Vector2 direction = to - from;
            float distance = direction.Length();
            direction.Normalize();
            
            if (checkCollision != null)
            {
                int samples = (int)(distance / 16.0f) + 1;
                for (int i = 0; i <= samples; i++)
                {
                    float t = (float)i / samples;
                    Vector2 samplePoint = from + direction * (distance * t);
                    
                    if (checkCollision(samplePoint))
                    {
                        return false; // Path is blocked
                    }
                }
            }
            
            return true; // Path is clear
        }
        
        private Vector2 TrySlideAlongCollision(Vector2 currentPos, Vector2 blockedPos, Vector2 direction, float moveDistance, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return currentPos;
            
            // Normalize direction if needed
            if (direction.LengthSquared() > 0.001f)
            {
                direction.Normalize();
            }
            else
            {
                return currentPos; // No direction to slide
            }
            
            // Define 8 isometric directions aligned with the 64x32 grid
            // For a 64x32 diamond: halfWidth = 32, halfHeight = 16
            // Isometric directions in screen space:
            Vector2[] isometricDirections = new Vector2[]
            {
                new Vector2(0, -1),           // North (up)
                new Vector2(0.707f, -0.354f), // Northeast (normalized from (32, -16))
                new Vector2(1, 0),            // East (right)
                new Vector2(0.707f, 0.354f),  // Southeast (normalized from (32, 16))
                new Vector2(0, 1),            // South (down)
                new Vector2(-0.707f, 0.354f), // Southwest (normalized from (-32, 16))
                new Vector2(-1, 0),           // West (left)
                new Vector2(-0.707f, -0.354f) // Northwest (normalized from (-32, -16))
            };
            
            // Find the closest isometric direction to the current movement direction
            int closestDir = 0;
            float closestDot = float.MinValue;
            for (int i = 0; i < isometricDirections.Length; i++)
            {
                float dot = Vector2.Dot(direction, isometricDirections[i]);
                if (dot > closestDot)
                {
                    closestDot = dot;
                    closestDir = i;
                }
            }
            
            // Get perpendicular directions in isometric space (90 degrees in isometric)
            // For isometric, perpendicular means the adjacent directions
            int leftDir = (closestDir + 6) % 8;  // 2 positions counter-clockwise
            int rightDir = (closestDir + 2) % 8; // 2 positions clockwise
            
            // Use preferred slide direction to avoid bouncing
            int[] directionsToTry;
            if (_preferredSlideDirection != 0)
            {
                // Try preferred direction first
                directionsToTry = _preferredSlideDirection > 0 ? new int[] { rightDir, leftDir } : new int[] { leftDir, rightDir };
            }
            else
            {
                // No preference - try both equally
                directionsToTry = new int[] { leftDir, rightDir };
            }
            
            // Try sliding along isometric directions
            float[] slideScales = { 1.0f, 0.8f, 0.6f, 0.4f }; // Distance scaling
            
            Vector2 bestSlide = currentPos;
            float bestDistance = 0.0f;
            int bestDirIndex = -1;
            
            foreach (int dirIndex in directionsToTry)
            {
                Vector2 slideDir = isometricDirections[dirIndex];
                
                foreach (float scale in slideScales)
                {
                    Vector2 slidePos = currentPos + slideDir * (moveDistance * scale);
                    
                    if (!checkCollision(slidePos))
                    {
                        // Check how far we can slide - prefer longer slides
                        float slideDistance = Vector2.Distance(currentPos, slidePos);
                        if (slideDistance > bestDistance)
                        {
                            bestSlide = slidePos;
                            bestDistance = slideDistance;
                            bestDirIndex = dirIndex;
                        }
                    }
                }
            }
            
            // Also try blending forward direction with isometric slide directions
            if (bestDistance < moveDistance * 0.5f)
            {
                foreach (int dirIndex in directionsToTry)
                {
                    Vector2 slideDir = isometricDirections[dirIndex];
                    
                    // Blend forward and perpendicular (isometric) movement
                    foreach (float blend in new float[] { 0.5f, 0.7f, 0.3f })
                    {
                        Vector2 blendedDir = direction * (1.0f - blend) + slideDir * blend;
                        blendedDir.Normalize();
                        
                        foreach (float scale in slideScales)
                        {
                            Vector2 slidePos = currentPos + blendedDir * (moveDistance * scale);
                            
                            if (!checkCollision(slidePos))
                            {
                                float slideDistance = Vector2.Distance(currentPos, slidePos);
                                if (slideDistance > bestDistance)
                                {
                                    bestSlide = slidePos;
                                    bestDistance = slideDistance;
                                    bestDirIndex = dirIndex;
                                }
                            }
                        }
                    }
                }
            }
            
            // If we found a good slide, use it
            if (bestDistance > 0.1f)
            {
                // Remember preferred direction (left = -1, right = 1)
                if (bestDirIndex == leftDir)
                {
                    _preferredSlideDirection = -1;
                }
                else if (bestDirIndex == rightDir)
                {
                    _preferredSlideDirection = 1;
                }
                return bestSlide;
            }
            
            // Can't slide - reset preference and return original position
            _preferredSlideDirection = 0;
            return currentPos;
        }

        private void CreateDiamondTexture(GraphicsDevice graphicsDevice)
        {
            int halfWidth = 32;  // 64/2 = 32
            int halfHeight = 16; // 32/2 = 16
            int width = halfWidth * 2;
            int height = halfHeight * 2;
            
            _diamondTexture = new Texture2D(graphicsDevice, width, height);
            Color[] colorData = new Color[width * height];
            
            Vector2 center = new Vector2(halfWidth, halfHeight);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    
                    // Check if point is inside the diamond shape
                    // Diamond formula: |x - cx|/hw + |y - cy|/hh <= 1
                    float dx = Math.Abs(x - center.X);
                    float dy = Math.Abs(y - center.Y);
                    float normalizedX = dx / halfWidth;
                    float normalizedY = dy / halfHeight;
                    
                    if (normalizedX + normalizedY <= 1.0f)
                    {
                        colorData[y * width + x] = Color.White; // Will be tinted when drawing
                    }
                    else
                    {
                        colorData[y * width + x] = Color.Transparent;
                    }
                }
            }
            
            _diamondTexture.SetData(colorData);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Create diamond texture if needed
            if (_diamondTexture == null)
            {
                CreateDiamondTexture(spriteBatch.GraphicsDevice);
            }

            // Flash effect: alternate visibility when hit
            bool visible = true;
            if (_isFlashing)
            {
                // Flash on/off based on interval
                int flashCycle = (int)(_flashTime / _flashInterval);
                visible = (flashCycle % 2 == 0);
            }

            if (visible)
            {
                // Draw isometric diamond centered at position
                // Diamond is 64x32 (halfWidth=32, halfHeight=16)
                Vector2 drawPosition = _position - new Vector2(32, 16);
                Color drawColor = _isSneaking ? _sneakColor : _normalColor;
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
        }
    }
}

