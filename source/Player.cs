using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    public class Player : Entity
    {
        // Player-specific fields
        private float _sneakSpeed;
        private float _distanceThreshold;
        private Color _sneakColor;
        private bool _isSneaking;

        public bool IsSneaking => _isSneaking;

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

        public Player(Vector2 startPosition) 
            : base(startPosition, Color.Red, GameConfig.PlayerWalkSpeed, GameConfig.PlayerRunSpeed)
        {
            _sneakSpeed = _walkSpeed * GameConfig.PlayerSneakSpeedMultiplier;
            _distanceThreshold = 100.0f;
            _sneakColor = Color.Purple;
            _isSneaking = false;
        }

        public void ToggleSneak()
        {
            _isSneaking = !_isSneaking;
            _color = _isSneaking ? _sneakColor : _normalColor;
        }

        public void SetTarget(Vector2 target, Func<Vector2, bool>? checkCollision = null, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            // Always set target immediately, even if the exact location is blocked
            // This ensures clicks are never ignored - we'll try to get as close as possible
            // IMPORTANT: Completely reset all movement state to prevent progressive issues
            
            // Log the current state before setting new target (to detect timing issues)
            string pathInfo = _path != null && _path.Count > 0 ? $"{_path.Count} waypoints" : "none";
            string movingState = _currentSpeed > 0.1f ? $"moving at {_currentSpeed:F1}px/s" : "stationary";
            string oldTargetInfo = _targetPosition.HasValue ? $"({_targetPosition.Value.X:F1}, {_targetPosition.Value.Y:F1})" : "none";
            
            LogOverlay.Log($"[Player] NEW TARGET SET - Old target: {oldTargetInfo}, Path: {pathInfo}, State: {movingState}", LogLevel.Info);
            LogOverlay.Log($"[Player] Current pos: ({_position.X:F1}, {_position.Y:F1}) -> New target: ({target.X:F1}, {target.Y:F1})", LogLevel.Info);
            
            _targetPosition = target;
            _waypoint = null;
            _path = null; // Use null instead of Clear() to ensure it's truly cleared
            _stuckTimer = 0.0f;
            _currentSpeed = 0.0f; // Reset speed to ensure fresh start
            
            // Check if target location itself is blocked by TERRAIN collision
            bool targetBlockedByTerrain = checkTerrainOnly != null && checkTerrainOnly(target);
            bool targetBlockedByCollision = checkCollision != null && checkCollision(target);
            
            if (targetBlockedByTerrain)
            {
                LogOverlay.Log("[Player] Target location is blocked by terrain - will attempt to get as close as possible", LogLevel.Warning);
            }
            else if (targetBlockedByCollision && checkTerrainOnly == null)
            {
                LogOverlay.Log("[Player] Target location is blocked by collision - will attempt to get as close as possible", LogLevel.Warning);
            }
            
            // Check if direct path is clear (quick check first)
            if (checkCollision != null)
            {
                Vector2 direction = target - _position;
                float distance = direction.Length();
                
                // CRITICAL: Check if player's CURRENT position is in collision while moving
                // This can happen if player is mid-movement through collision buffer zone
                bool playerPosInCollision = checkCollision(_position);
                if (playerPosInCollision)
                {
                    LogOverlay.Log($"[Player] WARNING: Player position ({_position.X:F1}, {_position.Y:F1}) is IN COLLISION while moving! This affects path checks.", LogLevel.Error);
                }
                
                // Skip direct path check if target itself is blocked by terrain
                // (we'll need pathfinding anyway)
                bool pathClear = !targetBlockedByTerrain;
                
                if (pathClear && distance > 0.1f)
                {
                    // More thorough direct path check with denser sampling
                    // Check every 16 pixels for better obstacle detection
                    int samples = Math.Max(2, (int)(distance / 16.0f) + 1); // Denser sampling
                    LogOverlay.Log($"[Player] Checking direct path with {samples} samples over {distance:F1} pixels (player in collision: {playerPosInCollision})", LogLevel.Info);
                    
                    // If player is currently in collision, force pathfinding
                    if (playerPosInCollision)
                    {
                        LogOverlay.Log($"[Player] FORCING PATHFINDING - player position in collision", LogLevel.Warning);
                        pathClear = false;
                    }
                    else
                    {
                        // Check all points including start and end (but skip exact start/end positions)
                        for (int i = 0; i <= samples; i++)
                        {
                            float t = (float)i / samples;
                            // Skip exact start (t=0) and exact end (t=1) as they're checked separately
                            if (t < 0.01f || t > 0.99f)
                                continue;
                                
                            Vector2 samplePoint = _position + (target - _position) * t;
                            bool hasCollision = checkCollision(samplePoint);
                            
                            if (hasCollision)
                            {
                                LogOverlay.Log($"[Player] Direct path BLOCKED at sample {i}/{samples} at ({samplePoint.X:F1}, {samplePoint.Y:F1})", LogLevel.Warning);
                                pathClear = false;
                                break;
                            }
                        }
                    }
                }
                
                if (pathClear && !targetBlockedByTerrain)
                {
                    LogOverlay.Log("[Player] Direct path check PASSED - all samples clear", LogLevel.Info);
                }
                else if (!pathClear)
                {
                    LogOverlay.Log("[Player] Direct path check FAILED - obstacle detected", LogLevel.Warning);
                }
                
                // Use pathfinding if direct path is blocked OR if target is blocked by terrain
                if (!pathClear || targetBlockedByTerrain)
                {
                    LogOverlay.Log($"[Player] Starting pathfinding from ({_position.X:F1}, {_position.Y:F1}) to ({target.X:F1}, {target.Y:F1})", LogLevel.Info);
                    
                    _path = PathfindingService.FindPath(
                        _position, 
                        target, 
                        checkCollision,
                        GameConfig.PathfindingGridCellWidth,
                        GameConfig.PathfindingGridCellHeight
                    );
                    
                    if (_path == null || _path.Count == 0)
                    {
                        LogOverlay.Log("[Player] Pathfinding FAILED - will attempt direct movement with collision sliding", LogLevel.Error);
                        // Clear path to ensure we try direct movement
                        _path = null;
                        // Reset stuck timer so we can immediately try to move
                        _stuckTimer = 0.0f;
                    }
                    else
                    {
                        // Smooth the path to remove unnecessary waypoints
                        _path = PathfindingService.SimplifyPath(_path);
                        LogOverlay.Log($"[Player] Pathfinding SUCCEEDED - {_path.Count} waypoints", LogLevel.Info);
                    }
                }
                else
                {
                    // Direct path is clear - ensure path is cleared and stuck timer is reset
                    _path = null;
                    _stuckTimer = 0.0f;
                    LogOverlay.Log($"[Player] Direct path clear - no pathfinding needed. Target: ({target.X:F1}, {target.Y:F1})", LogLevel.Info);
                }
            }
            else
            {
                // No collision checking - clear path and reset stuck timer
                _path = null;
                _stuckTimer = 0.0f;
            }
        }

        public override void Update(float deltaTime)
        {
            Update(null, deltaTime, null, null);
        }

        public void Update(Vector2? followPosition, float deltaTime, Func<Vector2, bool>? checkCollision = null, Func<Vector2, Vector2, bool>? checkLineOfSight = null, CollisionManager? collisionManager = null)
        {
            UpdateFlashing(deltaTime);

            Vector2? moveTarget = null;

            if (followPosition.HasValue)
            {
                float deadZone = _isSneaking ? 8.0f : 2.0f;
                Vector2 direction = followPosition.Value - _position;
                float distance = direction.Length();
                
                if (distance > deadZone)
                {
                    moveTarget = followPosition.Value;
                }
                else if (_targetPosition.HasValue)
                {
                    moveTarget = _targetPosition.Value;
                }
            }
            else if (_targetPosition.HasValue)
            {
                if (_path != null && _path.Count > 0)
                {
                    int initialPathCount = _path.Count;
                    while (_path.Count > 0)
                    {
                        float distToNext = Vector2.Distance(_position, _path[0]);
                        if (distToNext < 10.0f)
                        {
                            LogOverlay.Log($"[Player] Removing waypoint at ({_path[0].X:F1}, {_path[0].Y:F1}) - within 10px (dist={distToNext:F1})", LogLevel.Debug);
                            _path.RemoveAt(0);
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (_path.Count < initialPathCount)
                    {
                        LogOverlay.Log($"[Player] Removed {initialPathCount - _path.Count} waypoints, {_path.Count} remaining", LogLevel.Info);
                    }
                    
                    if (_path.Count > 0)
                    {
                        moveTarget = _path[0];
                        // Ensure we have a valid target - if path waypoint is too close, use next one or target
                        float distToWaypoint = Vector2.Distance(_position, _path[0]);
                        if (distToWaypoint < 5.0f && _path.Count > 1)
                        {
                            // Skip to next waypoint if current one is too close
                            _path.RemoveAt(0);
                            moveTarget = _path[0];
                        }
                        else if (distToWaypoint < 5.0f && _path.Count == 1)
                        {
                            // Last waypoint is too close, go directly to target
                            LogOverlay.Log($"[Player] Clearing path - last waypoint too close ({distToWaypoint:F1}px), going to target", LogLevel.Info);
                            _path.Clear();
                            moveTarget = _targetPosition.Value;
                        }
                    }
                    else
                    {
                        moveTarget = _targetPosition.Value;
                    }
                }
                else
                {
                    moveTarget = _waypoint ?? _targetPosition.Value;
                }
            }

            if (moveTarget.HasValue)
            {
                Vector2 direction = moveTarget.Value - _position;
                float distance = direction.Length();
                
                float distanceToFinalTarget = _targetPosition.HasValue 
                    ? Vector2.Distance(_position, _targetPosition.Value) 
                    : distance;

                bool isFinalTarget = !followPosition.HasValue && 
                                    _targetPosition.HasValue && 
                                    (moveTarget == _targetPosition.Value || (_path != null && _path.Count == 0));

                // Determine speed with smooth deceleration
                float baseSpeed = _isSneaking ? _sneakSpeed : _runSpeed;
                
                if (isFinalTarget && distanceToFinalTarget < GameConfig.PlayerSlowdownRadius)
                {
                    float slowdownFactor = distanceToFinalTarget / GameConfig.PlayerSlowdownRadius;
                    slowdownFactor = MathHelper.Max(slowdownFactor, 0.2f);
                    _currentSpeed = baseSpeed * slowdownFactor;
                }
                else
                {
                    _currentSpeed = baseSpeed;
                }

                float stopThreshold = isFinalTarget ? GameConfig.PlayerStopThreshold : (_isSneaking ? 10.0f : 5.0f);
                
                if (distance <= stopThreshold && isFinalTarget && _targetPosition.HasValue)
                {
                    LogOverlay.Log($"[Player] Reached target! Distance={distance:F2}px, Threshold={stopThreshold:F2}px", LogLevel.Info);
                    _position = _targetPosition.Value;
                    _targetPosition = null;
                    _waypoint = null;
                    _path = null; // Use null instead of Clear() to ensure it's truly cleared
                    _stuckTimer = 0.0f;
                    _currentSpeed = 0.0f;
                }
                else if (distance > stopThreshold)
                {
                    direction.Normalize();
                    float moveDistance = _currentSpeed * deltaTime;
                    
                    if (moveDistance > distance)
                    {
                        moveDistance = distance;
                    }

                    Vector2 nextPosition = _position + direction * moveDistance;
                    
                    // Use CollisionManager's advanced collision resolution if available
                    if (collisionManager != null)
                    {
                        // Move with swept collision and sliding
                        Vector2 newPos = collisionManager.MoveWithCollision(_position, nextPosition, true);
                        
                        // Accept any movement, even small ones, to prevent getting stuck when path exists
                        // This ensures the player continues moving along the path even with small movements
                        if (newPos != _position)
                        {
                            _position = newPos;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            _stuckTimer += deltaTime;
                            
                            // If stuck and have target, try pathfinding
                            if (_stuckTimer > STUCK_THRESHOLD && _targetPosition.HasValue && checkCollision != null)
                            {
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = PathfindingService.FindPath(
                                        _position, _targetPosition.Value, checkCollision,
                                        GameConfig.PathfindingGridCellWidth, GameConfig.PathfindingGridCellHeight
                                    );
                                    if (_path != null && _path.Count > 0)
                                    {
                                        _path = PathfindingService.SimplifyPath(_path);
                                    }
                                    _stuckTimer = 0.0f;
                                }
                            }
                        }
                    }
                    // Fallback: Handle pathfinding and collision (old method)
                    else if (_path != null && _path.Count > 0)
                    {
                        if (_targetPosition.HasValue && checkCollision != null)
                        {
                            Vector2 directDirection = _targetPosition.Value - _position;
                            float directDistance = directDirection.Length();
                            
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
                                
                                if (directPathClear)
                                {
                                    _path.Clear();
                                }
                            }
                        }
                        
                        if (_path != null && _path.Count > 0)
                        {
                            bool hasCollision = checkCollision != null && checkCollision(nextPosition);
                            
                            if (!hasCollision)
                            {
                                _position = nextPosition;
                                _stuckTimer = 0.0f;
                            }
                            else
                            {
                                Vector2 slidePosition = TrySlideAlongCollision(_position, nextPosition, direction, moveDistance, checkCollision);
                                if (slidePosition != _position)
                                {
                                    _position = slidePosition;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                if (checkCollision != null && _targetPosition.HasValue)
                                {
                                    _path = PathfindingService.FindPath(
                                        _position, 
                                        _targetPosition.Value, 
                                        checkCollision,
                                        GameConfig.PathfindingGridCellWidth,
                                        GameConfig.PathfindingGridCellHeight
                                    );
                                    if (_path != null && _path.Count > 0)
                                    {
                                        _path = PathfindingService.SimplifyPath(_path);
                                    }
                                }
                                    _stuckTimer += deltaTime;
                                }
                            }
                            return;
                        }
                    }
                    
                    bool hasCollision2 = checkCollision != null && checkCollision(nextPosition);
                    
                    if (hasCollision2)
                    {
                        if (_targetPosition.HasValue && !followPosition.HasValue && checkCollision != null)
                        {
                            if (_path == null || _path.Count == 0)
                            {
                                _path = PathfindingService.FindPath(
                                    _position, 
                                    _targetPosition.Value, 
                                    checkCollision,
                                    GameConfig.PathfindingGridCellWidth,
                                    GameConfig.PathfindingGridCellHeight
                                );
                                if (_path != null && _path.Count > 0)
                                {
                                    _path = PathfindingService.SimplifyPath(_path);
                                }
                            }
                            
                            if (_path != null && _path.Count > 0)
                            {
                                _stuckTimer = 0.0f;
                                return;
                            }
                        }
                        
                        Vector2 slidePosition = TrySlideAlongCollision(_position, nextPosition, direction, moveDistance, checkCollision);
                        if (slidePosition != _position)
                        {
                            _position = slidePosition;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            _stuckTimer += deltaTime;
                            if (_stuckTimer > STUCK_THRESHOLD && !followPosition.HasValue)
                            {
                                ClearTarget();
                            }
                        }
                    }
                    else
                    {
                        _position = nextPosition;
                        _stuckTimer = 0.0f;
                    }
                }
                else
                {
                    if (_waypoint.HasValue && moveTarget == _waypoint.Value)
                    {
                        _waypoint = null;
                        _stuckTimer = 0.0f;
                    }
                }
            }
            else
            {
                _currentSpeed = 0.0f;
            }
        }

        private Vector2 TrySlideAlongCollision(Vector2 currentPos, Vector2 targetPos, Vector2 direction, float moveDistance, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return currentPos;
            
            Vector2 movement = targetPos - currentPos;
            
            if (movement.LengthSquared() < 0.001f)
                return currentPos;
            
            // Isometric-aware slide directions (aligned with diamond collision cells)
            // These match the 2:1 aspect ratio of isometric tiles
            Vector2[] isometricAxes = new Vector2[]
            {
                new Vector2(1, 0),            // East
                new Vector2(0, 1),            // South
                new Vector2(-1, 0),           // West
                new Vector2(0, -1),           // North
                new Vector2(0.894f, 0.447f),  // Southeast (isometric diagonal)
                new Vector2(0.894f, -0.447f), // Northeast (isometric diagonal)
                new Vector2(-0.894f, 0.447f), // Southwest (isometric diagonal)
                new Vector2(-0.894f, -0.447f) // Northwest (isometric diagonal)
            };
            
            direction.Normalize();
            
            // Find which isometric axis is most aligned with our movement
            int bestAxis = 0;
            float bestDot = float.MinValue;
            for (int i = 0; i < isometricAxes.Length; i++)
            {
                float dot = Vector2.Dot(direction, isometricAxes[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestAxis = i;
                }
            }
            
            // Try perpendicular isometric directions (left and right of movement)
            int[] perpAxes = new int[]
            {
                (bestAxis + 2) % 8,  // 90 degrees right
                (bestAxis + 6) % 8   // 90 degrees left (counter-clockwise)
            };
            
            // Try sliding along isometric axes at various scales
            float[] scales = { 1.0f, 0.8f, 0.6f, 0.4f, 0.3f };
            
            foreach (int axisIndex in perpAxes)
            {
                Vector2 slideDir = isometricAxes[axisIndex];
                
                foreach (float scale in scales)
                {
                    // Pure slide along isometric axis
                    Vector2 testPos = currentPos + slideDir * (moveDistance * scale);
                    if (!checkCollision(testPos))
                    {
                        return testPos;
                    }
                    
                    // Blended slide (original direction + isometric axis)
                    Vector2 blendedDir = (direction * 0.5f + slideDir * 0.5f);
                    blendedDir.Normalize();
                    testPos = currentPos + blendedDir * (moveDistance * scale);
                    if (!checkCollision(testPos))
                    {
                        return testPos;
                    }
                }
            }
            
            return currentPos;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (_diamondTexture == null)
            {
                CreateDiamondTexture(spriteBatch.GraphicsDevice);
            }

            bool visible = true;
            if (_isFlashing)
            {
                int flashCycle = (int)(_flashTime / _flashInterval);
                visible = (flashCycle % 2 == 0);
            }

            if (visible && _diamondTexture != null)
            {
                Vector2 drawPosition = _position - new Vector2(32, 16);
                Color drawColor = _isSneaking ? _sneakColor : _normalColor;
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
        }
    }
}
