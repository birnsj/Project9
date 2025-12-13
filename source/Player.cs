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
        private Color _sneakColor;
        private bool _isSneaking;
        private float _rotation; // Facing direction in radians
        
        // Death animation
        private float _deathPulseTimer = 0.0f;
        private float _deathPulseSpeed = 2.0f; // Pulses per second
        private bool _isDead = false;
        
        // Respawn system
        private Vector2 _spawnPosition;
        private float _respawnTimer = 0.0f;
        private const float RESPAWN_COUNTDOWN = 10.0f; // 10 seconds

        public bool IsSneaking => _isSneaking;
        public bool IsDead => _isDead;
        public float RespawnTimer => _respawnTimer;
        public bool IsRespawning => _isDead && _respawnTimer > 0.0f;

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
            : base(startPosition, Color.Red, GameConfig.PlayerWalkSpeed, GameConfig.PlayerRunSpeed, maxHealth: 100f)
        {
            _sneakSpeed = _walkSpeed * GameConfig.PlayerSneakSpeedMultiplier;
            _sneakColor = Color.Purple;
            _isSneaking = false;
            _rotation = 0.0f; // Initialize facing direction
            _spawnPosition = startPosition; // Store spawn position for respawn
        }
        
        public float Rotation => _rotation;
        
        /// <summary>
        /// Set rotation to face a target position
        /// </summary>
        public void FaceTarget(Vector2 targetPosition)
        {
            Vector2 direction = targetPosition - _position;
            if (direction.LengthSquared() > 0.01f)
            {
                direction.Normalize();
                _rotation = (float)Math.Atan2(direction.Y, direction.X);
            }
        }

        public void ToggleSneak()
        {
            _isSneaking = !_isSneaking;
            _color = _isSneaking ? _sneakColor : _normalColor;
        }

        public void SetTarget(Vector2 target, Func<Vector2, bool>? checkCollision = null, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            // Don't allow setting target when dead
            if (_isDead || !IsAlive)
                return;
                
            // Always set target immediately, even if the exact location is blocked
            // This ensures clicks are never ignored - we'll try to get as close as possible
            // IMPORTANT: Completely reset all movement state to prevent progressive issues
            
            // Log the current state before setting new target (to detect timing issues)
            string pathInfo = _path != null && _path.Count > 0 ? $"{_path.Count} waypoints" : "none";
            string movingState = _currentSpeed > 0.1f ? $"moving at {_currentSpeed:F1}px/s" : "stationary";
            string oldTargetInfo = _targetPosition.HasValue ? $"({_targetPosition.Value.X:F1}, {_targetPosition.Value.Y:F1})" : "none";
            
            LogOverlay.Log($"[Player] NEW TARGET SET - Old target: {oldTargetInfo}, Path: {pathInfo}, State: {movingState}", LogLevel.Info);
            LogOverlay.Log($"[Player] Current pos: ({_position.X:F1}, {_position.Y:F1}) -> New target: ({target.X:F1}, {target.Y:F1})", LogLevel.Info);
            
            // FIX: Always set target first, before any pathfinding calculations
            // This ensures the target is set even if pathfinding fails or takes time
            // CRITICAL: Set target immediately to ensure clicks always register
            // Even if called while player is moving, this will take effect on the next Update call
            _targetPosition = target;
            _waypoint = null;
            _path = null; // Use null instead of Clear() to ensure it's truly cleared
            _stuckTimer = 0.0f;
            // FIX Bug #2: Don't reset speed to 0 - preserve momentum for smoother transitions
            // The Update method will recalculate speed based on current movement state
            // _currentSpeed = 0.0f; // REMOVED - caused 1-frame stutter when re-clicking while moving
            
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
                // IMPORTANT: Only check TERRAIN collision for direct path, not enemy collision
                // Enemy collision will be handled during movement via MoveWithCollision sliding
                // This allows direct paths (green) even when enemies are present - we'll slide around them
                if (checkCollision != null)
                {
                    Vector2 direction = target - _position;
                    float distance = direction.Length();
                    
                    // Use terrain-only check for direct path validation
                    Func<Vector2, bool> terrainCheck = checkTerrainOnly ?? ((pos) => checkCollision(pos));
                    
                    // FIX Bug #3: Check if player's position is in collision, but don't force pathfinding
                    // Just log a warning - let the direct path check happen first
                    // This prevents unnecessary pathfinding when player is near obstacles
                    bool playerPosInCollision = terrainCheck(_position);
                    if (playerPosInCollision)
                    {
                        LogOverlay.Log($"[Player] NOTE: Player position ({_position.X:F1}, {_position.Y:F1}) is in terrain collision zone - will check direct path anyway", LogLevel.Warning);
                    }
                    
                    // Skip direct path check if target itself is blocked by terrain
                    // (we'll need pathfinding anyway)
                    bool pathClear = !targetBlockedByTerrain;
                    
                    if (pathClear && distance > 0.1f)
                    {
                        // More thorough direct path check with denser sampling
                        // Check every 8 pixels for better obstacle detection (was 16, too sparse)
                        // ONLY CHECK TERRAIN - enemies will be handled during movement
                        int samples = Math.Max(3, (int)(distance / 8.0f) + 1); // Denser sampling - every 8 pixels
                        LogOverlay.Log($"[Player] Checking direct TERRAIN path with {samples} samples over {distance:F1} pixels (player in collision: {playerPosInCollision})", LogLevel.Info);
                        
                        // FIX: Check terrain only for direct path - enemy collision handled during movement
                        // This allows green direct paths even when enemies are in the way (we'll slide around them)
                        {
                            // Check all points along the path (including positions very close to start/end)
                            for (int i = 0; i <= samples; i++)
                            {
                                float t = (float)i / samples;
                                // Include all points, even near start/end, but avoid exact positions
                                if (t < 0.001f || t > 0.999f)
                                    continue;
                                
                                Vector2 samplePoint = _position + (target - _position) * t;
                                // Only check terrain collision for direct path decision
                                // Enemy collision will be handled smoothly during movement
                                bool hasTerrainCollision = terrainCheck(samplePoint);
                                
                                if (hasTerrainCollision)
                                {
                                    LogOverlay.Log($"[Player] Direct TERRAIN path BLOCKED at sample {i}/{samples} (t={t:F3}) at ({samplePoint.X:F1}, {samplePoint.Y:F1})", LogLevel.Warning);
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
                // Pathfinding should also use terrain-only collision to avoid going around enemies unnecessarily
                // Enemy collision is handled during movement via MoveWithCollision sliding
                if (!pathClear || targetBlockedByTerrain)
                {
                    LogOverlay.Log($"[Player] Starting pathfinding (terrain-only) from ({_position.X:F1}, {_position.Y:F1}) to ({target.X:F1}, {target.Y:F1})", LogLevel.Info);
                    
                    // Use terrain-only collision for pathfinding - enemies will be handled during movement
                    Func<Vector2, bool> pathfindingCheck = checkTerrainOnly ?? ((pos) => checkCollision(pos));
                    
                    _path = PathfindingService.FindPath(
                        _position, 
                        target, 
                        pathfindingCheck,
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
                        LogOverlay.Log($"[Player] Pathfinding SUCCEEDED - {_path.Count} waypoints (before simplification)", LogLevel.Info);
                        
                        // Smooth the path to remove unnecessary waypoints
                        // But don't simplify too aggressively - we need waypoints to avoid obstacles
                        // Use much larger threshold to preserve most waypoints for navigation around obstacles
                        List<Vector2> originalPath = new List<Vector2>(_path);
                        _path = PathfindingService.SimplifyPath(_path, 0.4f); // Much larger threshold to keep most waypoints
                        
                        if (_path != null && _path.Count > 0)
                        {
                            LogOverlay.Log($"[Player] Path simplified from {originalPath.Count} to {_path.Count} waypoints", LogLevel.Info);
                        }
                        else
                        {
                            LogOverlay.Log("[Player] WARNING: Path simplification removed all waypoints - using original path", LogLevel.Warning);
                            // Restore original path if simplification removed everything
                            _path = originalPath;
                        }
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
            // Check if player just died
            if (!_isDead && !IsAlive)
            {
                _isDead = true;
                _respawnTimer = RESPAWN_COUNTDOWN; // Start countdown
                ClearTarget(); // Stop movement when dead
            }
            
            // Update death animation and respawn timer if dead
            if (_isDead)
            {
                _deathPulseTimer += deltaTime * _deathPulseSpeed;
                
                // Update respawn countdown
                if (_respawnTimer > 0.0f)
                {
                    _respawnTimer -= deltaTime;
                    if (_respawnTimer <= 0.0f)
                    {
                        Respawn();
                    }
                }
                return; // Don't update movement when dead
            }
            
            Update(null, deltaTime, null, null);
        }
        
        /// <summary>
        /// Update death animation (pulsing effect)
        /// </summary>
        public void UpdateDeathAnimation(float deltaTime)
        {
            if (!_isDead && !IsAlive)
            {
                _isDead = true;
                _respawnTimer = RESPAWN_COUNTDOWN; // Start countdown
            }
            
            if (_isDead)
            {
                _deathPulseTimer += deltaTime * _deathPulseSpeed;
                
                // Update respawn countdown
                if (_respawnTimer > 0.0f)
                {
                    _respawnTimer -= deltaTime;
                    if (_respawnTimer <= 0.0f)
                    {
                        Respawn();
                    }
                }
            }
        }
        
        /// <summary>
        /// Respawn the player at spawn position
        /// </summary>
        public void Respawn()
        {
            _isDead = false;
            _respawnTimer = 0.0f;
            _deathPulseTimer = 0.0f;
            _currentHealth = _maxHealth; // Restore full health
            _position = _spawnPosition; // Return to spawn position
            ClearTarget(); // Clear any movement targets
            _isSneaking = false;
            _color = _normalColor; // Reset color
        }
        
        /// <summary>
        /// Set the spawn position (for respawning)
        /// </summary>
        public void SetSpawnPosition(Vector2 spawnPosition)
        {
            _spawnPosition = spawnPosition;
        }

        public void Update(Vector2? followPosition, float deltaTime, Func<Vector2, bool>? checkCollision = null, Func<Vector2, Vector2, bool>? checkLineOfSight = null, CollisionManager? collisionManager = null, System.Collections.Generic.IEnumerable<Enemy>? specificEnemies = null)
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
                    // Update rotation based on movement direction
                    _rotation = (float)Math.Atan2(direction.Y, direction.X);
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
                        // IMPORTANT: During combat, don't check enemy collision - allow player to move freely
                        // Enemy collision would lock the player to the enemy, preventing disengagement
                        // Terrain collision is still checked, and enemies will naturally slide/avoid during their movement
                        Vector2 newPos;
                        if (specificEnemies != null)
                        {
                            // During combat: Only check terrain collision, ignore enemy collision
                            // This allows the player to move away from enemies freely
                            newPos = collisionManager.MoveWithCollision(_position, nextPosition, false, 3, _position);
                        }
                        else
                        {
                            // Move with swept collision and sliding - MOVEMENT ONLY (no enemy collision)
                            newPos = collisionManager.MoveWithCollisionMovement(_position, nextPosition);
                        }
                        
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
                                    // FIX Bug #1: Update moveTarget immediately since path is now cleared
                                    // This prevents moving toward stale waypoint for one frame
                                    moveTarget = _targetPosition.Value;
                                    direction = _targetPosition.Value - _position;
                                    distance = direction.Length();
                                    if (distance > 0.1f)
                                    {
                                        direction.Normalize();
                                    }
                                    nextPosition = _position + direction * moveDistance;
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
                
                // Apply pulsing effect for dead player
                if (_isDead)
                {
                    float pulse = (float)(Math.Sin(_deathPulseTimer * Math.PI * 2) * 0.3f + 0.7f); // Pulse between 0.7 and 1.0
                    drawColor = new Color(
                        (byte)(drawColor.R * pulse),
                        (byte)(drawColor.G * pulse),
                        (byte)(drawColor.B * pulse),
                        (byte)(drawColor.A * pulse)
                    );
                }
                
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
        }
    }
}
