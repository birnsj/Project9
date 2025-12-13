using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    public class Enemy : Entity
    {
        // Enemy-specific fields (AI behavior)
        private Vector2 _originalPosition;
        private float _attackRange;
        private float _detectionRange;
        private float _attackCooldown;
        private float _currentAttackCooldown;
        private bool _isAttacking;
        private bool _hasDetectedPlayer;
        private float _exclamationTimer;
        private float _exclamationDuration = GameConfig.EnemyExclamationDuration;
        
        // Search behavior (when player goes out of view during alarm)
        private bool _isSearching = false;
        private Vector2 _lastKnownPlayerPosition;
        private Vector2 _searchTarget;
        private float _searchTimer = 0.0f;
        private bool _previouslyHadLineOfSight = false; // Track if we had line of sight previously
        private const float SEARCH_DURATION = GameConfig.EnemySearchDuration;
        private const float SEARCH_RADIUS = GameConfig.EnemySearchRadius;
        
        // Death animation
        private float _deathPulseTimer = 0.0f;
        private float _deathPulseSpeed = 2.0f; // Pulses per second
        private bool _isDead = false;
        
        // Sight cone and rotation
        private float _rotation;
        
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
        
        private float _sightConeAngle;
        private float _sightConeLength;
        private float _rotationSpeed;
        private float _behaviorTimer;
        private float _behaviorChangeInterval;
        private bool _isRotating;
        private Random _random;
        
        // Rendering textures
        private Texture2D? _circleTexture;
        private Texture2D? _sightConeTexture;
        private Texture2D? _exclamationTexture;

        public float AttackRange => _attackRange;
        public float DetectionRange => _detectionRange;
        public bool IsAttacking => _isAttacking;
        public bool HasDetectedPlayer => _hasDetectedPlayer;
        
        /// <summary>
        /// Force the enemy to detect the player (called by cameras when they detect the player)
        /// </summary>
        public void ForceDetectPlayer(Vector2 playerPosition)
        {
            // Always set detection, even if already detected (refreshes the alert)
            Console.WriteLine($"[Enemy] ForceDetectPlayer called at ({_position.X:F1}, {_position.Y:F1}). Was detected: {_hasDetectedPlayer}");
            _hasDetectedPlayer = true;
            _exclamationTimer = _exclamationDuration;
            _lastKnownPlayerPosition = playerPosition; // Set last known position so enemy knows where to go
            _isSearching = false; // Stop searching if we're alerted
            _searchTimer = 0.0f; // Reset search timer
            Console.WriteLine($"[Enemy] Now HasDetectedPlayer = {_hasDetectedPlayer}, lastKnownPos = ({_lastKnownPlayerPosition.X:F1}, {_lastKnownPlayerPosition.Y:F1})");
        }
        
        /// <summary>
        /// Reset enemy detection (called when alarm expires and enemy doesn't have direct detection)
        /// </summary>
        public void ResetDetection()
        {
            _hasDetectedPlayer = false;
            _exclamationTimer = 0.0f;
            _path?.Clear(); // Clear path so they can return to original position
        }

        public Enemy(Vector2 startPosition) 
            : base(startPosition, Color.DarkRed, GameConfig.EnemyChaseSpeed, GameConfig.EnemyChaseSpeed, maxHealth: 50f)
        {
            _originalPosition = startPosition;
            _attackRange = GameConfig.EnemyAttackRange;
            _detectionRange = GameConfig.EnemyDetectionRange;
            _attackCooldown = GameConfig.EnemyAttackCooldown;
            _currentAttackCooldown = 0.0f;
            _isAttacking = false;
            _hasDetectedPlayer = false;
            
            // Initialize rotation and sight cone
            _random = new Random();
            _rotation = (float)(_random.NextDouble() * Math.PI * 2);
            _sightConeAngle = MathHelper.ToRadians(60);
            _sightConeLength = _detectionRange * 0.8f;
            _rotationSpeed = MathHelper.ToRadians(45);
            _behaviorTimer = 0.0f;
            _behaviorChangeInterval = 2.0f + (float)(_random.NextDouble() * 3.0f);
            _isRotating = _random.Next(2) == 0;
            _exclamationTimer = 0.0f;
        }
        
        /// <summary>
        /// Initialize all textures for this enemy (call during LoadContent, not Draw)
        /// </summary>
        public override void InitializeTextures(GraphicsDevice graphicsDevice)
        {
            base.InitializeTextures(graphicsDevice);
            // Sight cone and exclamation textures are created on-demand when needed
        }
        
        private bool IsPointInSightCone(Vector2 point)
        {
            Vector2 directionToPoint = point - _position;
            float distance = directionToPoint.Length();
            
            if (distance > _sightConeLength || distance == 0)
                return false;
            
            directionToPoint.Normalize();
            
            float pointAngle = (float)Math.Atan2(directionToPoint.Y, directionToPoint.X);
            
            float enemyAngle = _rotation;
            while (enemyAngle < 0) enemyAngle += (float)(Math.PI * 2);
            while (enemyAngle >= Math.PI * 2) enemyAngle -= (float)(Math.PI * 2);
            while (pointAngle < 0) pointAngle += (float)(Math.PI * 2);
            while (pointAngle >= Math.PI * 2) pointAngle -= (float)(Math.PI * 2);
            
            float angleDiff = Math.Abs(pointAngle - enemyAngle);
            if (angleDiff > Math.PI)
                angleDiff = (float)(Math.PI * 2 - angleDiff);
            
            return angleDiff <= _sightConeAngle / 2.0f;
        }

        public override void Update(float deltaTime)
        {
            // This signature for base compatibility - use full Update below
        }
        
        /// <summary>
        /// Update death animation (pulsing effect)
        /// </summary>
        public void UpdateDeathAnimation(float deltaTime)
        {
            if (!_isDead && !IsAlive)
            {
                _isDead = true;
            }
            
            if (_isDead)
            {
                _deathPulseTimer += deltaTime * _deathPulseSpeed;
            }
        }
        
        public bool IsDead => _isDead;

        public void Update(Vector2 playerPosition, float deltaTime, bool playerIsSneaking = false, Func<Vector2, bool>? checkCollision = null, Func<Vector2, Vector2, bool>? checkLineOfSight = null, CollisionManager? collisionManager = null, Func<Vector2, bool>? checkTerrainOnly = null, bool alarmActive = false, bool playerIsAlive = true)
        {
            UpdateFlashing(deltaTime);

            if (_currentAttackCooldown > 0.0f)
            {
                _currentAttackCooldown -= deltaTime;
            }

            if (_exclamationTimer > 0.0f)
            {
                _exclamationTimer -= deltaTime;
            }

            // If player is dead, return to original position and clear detection
            if (!playerIsAlive)
            {
                if (_hasDetectedPlayer)
                {
                    _hasDetectedPlayer = false;
                    _isAttacking = false;
                }
                ReturnToOriginal(deltaTime, checkCollision, collisionManager);
                return;
            }

            Vector2 directionToPlayer = playerPosition - _position;
            float distanceToPlayer = directionToPlayer.Length();

            float effectiveDetectionRange;
            if (_hasDetectedPlayer)
            {
                effectiveDetectionRange = _detectionRange;
            }
            else
            {
                effectiveDetectionRange = playerIsSneaking ? _detectionRange * GameConfig.EnemySneakDetectionMultiplier : _detectionRange;
            }

            bool playerInRange = distanceToPlayer <= effectiveDetectionRange;
            bool hasLineOfSight = false;
            
            if (playerInRange)
            {
                bool lineOfSightBlocked = checkLineOfSight != null && checkLineOfSight(_position, playerPosition);
                hasLineOfSight = !lineOfSightBlocked;
                
                if (playerIsSneaking && distanceToPlayer <= effectiveDetectionRange && !_hasDetectedPlayer)
                {
                    if (IsPointInSightCone(playerPosition) && hasLineOfSight)
                    {
                        _hasDetectedPlayer = true;
                        _exclamationTimer = _exclamationDuration;
                        _lastKnownPlayerPosition = playerPosition;
                        _isSearching = false; // Stop searching if we see player
                    }
                }
                else
                {
                    if (hasLineOfSight)
                    {
                        if (!_hasDetectedPlayer)
                        {
                            _exclamationTimer = _exclamationDuration;
                        }
                        _hasDetectedPlayer = true;
                        _lastKnownPlayerPosition = playerPosition;
                        _isSearching = false; // Stop searching if we see player
                    }
                }
            }
            
            // Handle search behavior during alarm
            // Only start searching if we previously had line of sight and then lost it
            // Don't search immediately if we were alerted by camera without line of sight
            if (alarmActive && _hasDetectedPlayer && !hasLineOfSight && !_isSearching && _previouslyHadLineOfSight)
            {
                // Player lost - start searching (we had line of sight before, now we don't)
                _isSearching = true;
                _searchTimer = SEARCH_DURATION;
                _lastKnownPlayerPosition = playerPosition;
                // Set first search target near last known position
                float randomAngle = (float)(_random.NextDouble() * Math.PI * 2);
                float randomDistance = (float)(_random.NextDouble() * SEARCH_RADIUS);
                _searchTarget = _lastKnownPlayerPosition + new Vector2(
                    (float)Math.Cos(randomAngle) * randomDistance,
                    (float)Math.Sin(randomAngle) * randomDistance
                );
            }
            
            // Track line of sight state for next frame
            _previouslyHadLineOfSight = hasLineOfSight;
            
            // Update search timer
            if (_isSearching)
            {
                _searchTimer -= deltaTime;
                if (_searchTimer <= 0.0f)
                {
                    // Search time expired - return to original position
                    _isSearching = false;
                    _hasDetectedPlayer = false;
                    _path?.Clear();
                }
            }

            // If enemy has detected player (either directly or via camera alert), chase them
            // Use a large chase range when alerted (1024 pixels, same as camera alert radius)
            // During alarm, enemies should chase even without direct line of sight
            const float maxChaseRange = GameConfig.EnemyMaxChaseRange;
            
            // If alarm is active, enemies chase even without line of sight
            // Otherwise, they need line of sight to chase
            bool shouldChase = _hasDetectedPlayer && !_isSearching && distanceToPlayer <= maxChaseRange;
            if (shouldChase && !alarmActive)
            {
                // Only require line of sight if alarm is not active
                shouldChase = hasLineOfSight;
            }
            
            // Debug logging
            if (_hasDetectedPlayer && !shouldChase)
            {
                Console.WriteLine($"[Enemy] HasDetectedPlayer=true but not chasing. isSearching={_isSearching}, distance={distanceToPlayer:F1}, maxRange={maxChaseRange}, hasLoS={hasLineOfSight}, alarmActive={alarmActive}");
            }
            
            if (shouldChase)
            {
                if (distanceToPlayer <= _attackRange)
                {
                    // Face the player when in attack range
                    directionToPlayer.Normalize();
                    _rotation = (float)Math.Atan2(directionToPlayer.Y, directionToPlayer.X);
                    
                    if (_currentAttackCooldown <= 0.0f)
                    {
                        _isAttacking = true;
                        _currentAttackCooldown = _attackCooldown;
                    }
                    else
                    {
                        _isAttacking = false;
                    }
                }
                else
                {
                    _isAttacking = false;
                    ChaseTarget(playerPosition, distanceToPlayer, deltaTime, checkCollision, collisionManager, checkTerrainOnly);
                }
            }
            else if (_isSearching)
            {
                // Searching for player during alarm
                _isAttacking = false;
                SearchBehavior(deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
            else if (_hasDetectedPlayer && distanceToPlayer > maxChaseRange)
            {
                // Player too far away, return to original position
                _isAttacking = false;
                ReturnToOriginal(deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
            else
            {
                _isAttacking = false;
                PatrolBehavior(deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
        }

        private void ChaseTarget(Vector2 target, float distanceToTarget, float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            if (distanceToTarget <= _attackRange)
                return;

            // Face the player while chasing
            FaceTarget(target);

            // Use terrain-only check for direct path validation (like player)
            Func<Vector2, bool> terrainCheck = checkTerrainOnly ?? ((pos) => checkCollision != null ? checkCollision(pos) : false);
            bool pathClear = CheckDirectPath(target, terrainCheck);
            
            // Recalculate path if blocked or if path is empty/invalid
            // Use terrain-only collision for pathfinding - enemy collision handled during movement
            if (!pathClear && (_path == null || _path.Count == 0) && checkTerrainOnly != null)
            {
                _path = PathfindingService.FindPath(
                    _position, 
                    target, 
                    checkTerrainOnly,
                    GameConfig.PathfindingGridCellWidth,
                    GameConfig.PathfindingGridCellHeight
                );
                if (_path != null && _path.Count > 0)
                {
                    // Use much less aggressive simplification to keep most waypoints needed for obstacles
                    List<Vector2> originalPath = new List<Vector2>(_path);
                    _path = PathfindingService.SimplifyPath(_path, 0.4f);
                    if (_path == null || _path.Count == 0)
                    {
                        _path = originalPath; // Restore if simplification removed everything
                    }
                }
            }
            else if (pathClear)
            {
                _path?.Clear();
            }
            
            if (_path != null && _path.Count > 0)
            {
                FollowPath(target, deltaTime, checkCollision, collisionManager, checkTerrainOnly, faceFinalTarget: true);
            }
            else
            {
                MoveDirectly(target, distanceToTarget, deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
        }

        private void ReturnToOriginal(float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            Vector2 directionToOriginal = _originalPosition - _position;
            float distanceToOriginal = directionToOriginal.Length();
            
            if (distanceToOriginal > 5.0f)
            {
                // Use terrain-only check for pathfinding
                Func<Vector2, bool> terrainCheck = checkTerrainOnly ?? ((pos) => checkCollision != null ? checkCollision(pos) : false);
                bool pathClear = CheckDirectPath(_originalPosition, terrainCheck);
                
                if (!pathClear && (_path == null || _path.Count == 0) && checkTerrainOnly != null)
                {
                    _path = PathfindingService.FindPath(
                        _position, 
                        _originalPosition, 
                        checkTerrainOnly,
                        GameConfig.PathfindingGridCellWidth,
                        GameConfig.PathfindingGridCellHeight
                    );
                    if (_path != null && _path.Count > 0)
                    {
                        List<Vector2> originalPath = new List<Vector2>(_path);
                        _path = PathfindingService.SimplifyPath(_path, 0.4f);
                        if (_path == null || _path.Count == 0)
                        {
                            _path = originalPath; // Restore if simplification removed everything
                        }
                    }
                }
                else if (pathClear)
                {
                    _path?.Clear();
                }
                
                if (_path != null && _path.Count > 0)
                {
                    FollowPath(_originalPosition, deltaTime, checkCollision, collisionManager, checkTerrainOnly, faceFinalTarget: false);
                }
                else
                {
                    MoveDirectly(_originalPosition, distanceToOriginal, deltaTime, checkCollision, collisionManager, checkTerrainOnly, faceTarget: false);
                }
            }
            else
            {
                _position = _originalPosition;
                _hasDetectedPlayer = false;
                IdleBehavior(deltaTime);
            }
        }

        private void SearchBehavior(float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            // Move towards search target
            float distanceToTarget = Vector2.Distance(_position, _searchTarget);
            
            if (distanceToTarget < 30.0f)
            {
                // Reached search target - pick a new random search point
                float randomAngle = (float)(_random.NextDouble() * Math.PI * 2);
                float randomDistance = (float)(_random.NextDouble() * SEARCH_RADIUS);
                _searchTarget = _lastKnownPlayerPosition + new Vector2(
                    (float)Math.Cos(randomAngle) * randomDistance,
                    (float)Math.Sin(randomAngle) * randomDistance
                );
            }
            else
            {
                // Move towards search target
                Vector2 directionToTarget = _searchTarget - _position;
                float distance = directionToTarget.Length();
                
                if (distance > 0)
                {
                    directionToTarget.Normalize();
                    Vector2 desiredPosition = _position + directionToTarget * _currentSpeed * deltaTime;
                    
                    if (collisionManager != null)
                    {
                        Vector2 finalPos = collisionManager.MoveWithCollision(_position, desiredPosition, true, 3, _position);
                        if (Vector2.DistanceSquared(_position, finalPos) > 0.01f)
                        {
                            _position = finalPos;
                        }
                    }
                    else if (checkCollision == null || !checkCollision(desiredPosition))
                    {
                        _position = desiredPosition;
                    }
                    
                    // Update rotation to face search target
                    _rotation = (float)Math.Atan2(directionToTarget.Y, directionToTarget.X);
                }
            }
        }

        private void PatrolBehavior(float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            Vector2 directionToOriginal = _originalPosition - _position;
            float distanceToOriginal = directionToOriginal.Length();
            
            if (distanceToOriginal > 5.0f)
            {
                MoveDirectly(_originalPosition, distanceToOriginal, deltaTime, checkCollision, collisionManager, checkTerrainOnly, faceTarget: false);
            }
            else
            {
                _position = _originalPosition;
                IdleBehavior(deltaTime);
            }
        }

        private void IdleBehavior(float deltaTime)
        {
            _behaviorTimer += deltaTime;
            if (_behaviorTimer >= _behaviorChangeInterval)
            {
                _isRotating = _random.Next(2) == 0;
                _behaviorTimer = 0.0f;
                _behaviorChangeInterval = 2.0f + (float)(_random.NextDouble() * 3.0f);
            }
            
            if (_isRotating)
            {
                _rotation += _rotationSpeed * deltaTime;
                if (_rotation > Math.PI * 2)
                    _rotation -= (float)(Math.PI * 2);
                if (_rotation < 0)
                    _rotation += (float)(Math.PI * 2);
            }
        }

        private bool CheckDirectPath(Vector2 target, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return true;
            
            Vector2 direction = target - _position;
            float distance = direction.Length();
            // Use denser sampling (every 8 pixels) like player to catch more obstacles
            int samples = Math.Max(3, (int)(distance / 8.0f) + 1);
            
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                // Skip exact start and end to avoid checking current position
                if (t < 0.001f || t > 0.999f)
                    continue;
                    
                Vector2 samplePoint = _position + (target - _position) * t;
                if (checkCollision(samplePoint))
                {
                    return false;
                }
            }
            return true;
        }

        private void FollowPath(Vector2 finalTarget, float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null, bool faceFinalTarget = false)
        {
            if (_path == null)
                return;
            
            // Only face the final target (player) if alerted/chasing
            if (faceFinalTarget)
            {
                FaceTarget(finalTarget);
            }
            
            while (_path.Count > 0 && Vector2.Distance(_position, _path[0]) < 10.0f)
            {
                _path.RemoveAt(0);
            }
            
            if (_path.Count > 0)
            {
                Vector2 waypoint = _path[0];
                Vector2 direction = waypoint - _position;
                float distance = direction.Length();
                
                if (distance > 5.0f)
                {
                    direction.Normalize();
                    
                    // If not facing final target, face the movement direction (waypoint)
                    if (!faceFinalTarget)
                    {
                        _rotation = (float)Math.Atan2(direction.Y, direction.X);
                    }
                    
                    float moveDistance = _runSpeed * deltaTime;
                    if (moveDistance > distance) moveDistance = distance;
                    
                    Vector2 newPosition = _position + direction * moveDistance;
                    
                    // Use MoveWithCollision if available, otherwise fall back to simple check
                    if (collisionManager != null)
                    {
                        Vector2 finalPos = collisionManager.MoveWithCollision(_position, newPosition, true, 3, _position);
                        if (Vector2.DistanceSquared(_position, finalPos) > 0.01f)
                        {
                            _position = finalPos;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            // Stuck - recalculate path using terrain-only
                            if (checkTerrainOnly != null)
                            {
                                _path = PathfindingService.FindPath(
                                    _position, 
                                    finalTarget, 
                                    checkTerrainOnly,
                                    GameConfig.PathfindingGridCellWidth,
                                    GameConfig.PathfindingGridCellHeight
                                );
                                if (_path != null && _path.Count > 0)
                                {
                                    List<Vector2> originalPath = new List<Vector2>(_path);
                                    _path = PathfindingService.SimplifyPath(_path, 0.4f);
                                    if (_path == null || _path.Count == 0)
                                    {
                                        _path = originalPath;
                                    }
                                }
                            }
                            _stuckTimer += deltaTime;
                        }
                    }
                    else if (checkCollision == null || !checkCollision(newPosition))
                    {
                        _position = newPosition;
                        _stuckTimer = 0.0f;
                    }
                    else
                    {
                        // Recalculate path using terrain-only
                        if (checkTerrainOnly != null)
                        {
                            _path = PathfindingService.FindPath(
                                _position, 
                                finalTarget, 
                                checkTerrainOnly,
                                GameConfig.PathfindingGridCellWidth,
                                GameConfig.PathfindingGridCellHeight
                            );
                            if (_path != null && _path.Count > 0)
                            {
                                List<Vector2> originalPath = new List<Vector2>(_path);
                                _path = PathfindingService.SimplifyPath(_path, 0.4f);
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = originalPath;
                                }
                            }
                        }
                        _stuckTimer += deltaTime;
                    }
                }
            }
        }

        private void MoveDirectly(Vector2 target, float distanceToTarget, float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null, bool faceTarget = true)
        {
            Vector2 direction = (target - _position);
            direction.Normalize();
            
            // Face the target while moving (only if faceTarget is true)
            if (faceTarget)
            {
                FaceTarget(target);
            }
            else
            {
                // Face the direction of movement
                _rotation = (float)Math.Atan2(direction.Y, direction.X);
            }
            
            float moveDistance = _runSpeed * deltaTime;
            if (_isAttacking && distanceToTarget > _attackRange)
            {
                if (moveDistance > distanceToTarget - _attackRange)
                {
                    moveDistance = MathHelper.Max(0, distanceToTarget - _attackRange);
                }
            }
            else if (moveDistance > distanceToTarget)
            {
                moveDistance = distanceToTarget;
            }

            if (moveDistance > 0)
            {
                Vector2 newPosition = _position + direction * moveDistance;
                
                // Use MoveWithCollision if available for smooth sliding
                if (collisionManager != null)
                {
                    Vector2 finalPos = collisionManager.MoveWithCollision(_position, newPosition, true, 3, _position);
                    if (Vector2.DistanceSquared(_position, finalPos) > 0.01f)
                    {
                        _position = finalPos;
                        _stuckTimer = 0.0f;
                    }
                    else
                    {
                        _stuckTimer += deltaTime;
                        
                        if (_stuckTimer > STUCK_THRESHOLD && checkTerrainOnly != null)
                        {
                            Console.WriteLine("[Enemy] Stuck for too long, recalculating path");
                            _path = PathfindingService.FindPath(
                                _position, 
                                target, 
                                checkTerrainOnly,
                                GameConfig.PathfindingGridCellWidth,
                                GameConfig.PathfindingGridCellHeight
                            );
                            if (_path != null && _path.Count > 0)
                            {
                                List<Vector2> originalPath = new List<Vector2>(_path);
                                _path = PathfindingService.SimplifyPath(_path, 0.4f);
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = originalPath;
                                }
                            }
                            _stuckTimer = 0.0f;
                        }
                    }
                }
                else if (checkCollision == null || !checkCollision(newPosition))
                {
                    _position = newPosition;
                    _stuckTimer = 0.0f;
                }
                else
                {
                    Vector2 slidePosition = TrySlideAlongCollision(_position, newPosition, direction, moveDistance, checkCollision);
                    if (slidePosition != _position)
                    {
                        _position = slidePosition;
                        _stuckTimer = 0.0f;
                    }
                    else
                    {
                        _stuckTimer += deltaTime;
                        
                        if (_stuckTimer > STUCK_THRESHOLD && checkTerrainOnly != null)
                        {
                            Console.WriteLine("[Enemy] Stuck for too long, recalculating path");
                            _path = PathfindingService.FindPath(
                                _position, 
                                target, 
                                checkTerrainOnly,
                                GameConfig.PathfindingGridCellWidth,
                                GameConfig.PathfindingGridCellHeight
                            );
                            if (_path != null && _path.Count > 0)
                            {
                                List<Vector2> originalPath = new List<Vector2>(_path);
                                _path = PathfindingService.SimplifyPath(_path, 0.4f);
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = originalPath;
                                }
                            }
                            _stuckTimer = 0.0f;
                        }
                    }
                }
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

        public void DrawAggroRadius(SpriteBatch spriteBatch, float effectiveRange)
        {
            int radius = (int)effectiveRange;
            if (_circleTexture == null || _circleTexture.Width != radius * 2)
            {
                CreateCircleTexture(spriteBatch.GraphicsDevice, radius);
            }

            Vector2 drawPosition = _position - new Vector2(effectiveRange, effectiveRange);
            spriteBatch.Draw(_circleTexture, drawPosition, Color.White);
        }

        private void CreateCircleTexture(GraphicsDevice graphicsDevice, int radius)
        {
            int diameter = radius * 2;
            _circleTexture = new Texture2D(graphicsDevice, diameter, diameter);
            Color[] colorData = new Color[diameter * diameter];
            
            Vector2 center = new Vector2(radius, radius);
            
            for (int x = 0; x < diameter; x++)
            {
                for (int y = 0; y < diameter; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    
                    if (distance >= radius - 2 && distance <= radius)
                    {
                        colorData[y * diameter + x] = new Color(255, 0, 0, 100);
                    }
                    else
                    {
                        colorData[y * diameter + x] = Color.Transparent;
                    }
                }
            }
            
            _circleTexture.SetData(colorData);
        }

        public void DrawSightCone(SpriteBatch spriteBatch)
        {
            if (!_hasDetectedPlayer)
            {
                if (_sightConeTexture == null)
                {
                    CreateSightConeTexture(spriteBatch.GraphicsDevice);
                }
                
                if (_sightConeTexture != null)
                {
                    Vector2 origin = new Vector2(_sightConeTexture.Width / 2.0f, _sightConeTexture.Height / 2.0f);
                    
                    spriteBatch.Draw(
                        _sightConeTexture,
                        _position,
                        null,
                        Color.White,
                        _rotation,
                        origin,
                        1.0f,
                        SpriteEffects.None,
                        0.0f
                    );
                }
            }
        }

        private void CreateSightConeTexture(GraphicsDevice graphicsDevice)
        {
            int size = (int)_sightConeLength * 2;
            _sightConeTexture = new Texture2D(graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
            float halfAngle = _sightConeAngle / 2.0f;
            
            Vector2 forwardDir = new Vector2(1, 0);
            Vector2 leftEdge = new Vector2(
                (float)Math.Cos(-halfAngle),
                (float)Math.Sin(-halfAngle)
            );
            Vector2 rightEdge = new Vector2(
                (float)Math.Cos(halfAngle),
                (float)Math.Sin(halfAngle)
            );
            
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    Vector2 dir = pos - center;
                    float distance = dir.Length();
                    
                    if (distance > 0 && distance <= _sightConeLength)
                    {
                        dir.Normalize();
                        
                        float crossLeft = Vector2.Dot(new Vector2(-leftEdge.Y, leftEdge.X), dir);
                        float crossRight = Vector2.Dot(new Vector2(rightEdge.Y, -rightEdge.X), dir);
                        
                        if (crossLeft >= 0 && crossRight >= 0)
                        {
                            float alpha = 1.0f - (distance / _sightConeLength) * 0.5f;
                            byte alphaByte = (byte)(80 * alpha);
                            colorData[y * size + x] = new Color((byte)255, (byte)255, (byte)0, alphaByte);
                        }
                        else
                        {
                            colorData[y * size + x] = Color.Transparent;
                        }
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }
            
            _sightConeTexture.SetData(colorData);
        }

        private void CreateExclamationTexture(GraphicsDevice graphicsDevice)
        {
            int size = 32;
            _exclamationTexture = new Texture2D(graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (x >= size / 2 - 2 && x < size / 2 + 2)
                    {
                        if (y >= size / 4 && y < size * 3 / 4)
                        {
                            colorData[y * size + x] = Color.Yellow;
                        }
                        else if (y >= size * 3 / 4 && y < size * 7 / 8)
                        {
                            colorData[y * size + x] = Color.Yellow;
                        }
                        else
                        {
                            colorData[y * size + x] = Color.Transparent;
                        }
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }
            
            _exclamationTexture.SetData(colorData);
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
                Color drawColor = _isAttacking ? Color.OrangeRed : _color;
                Vector2 drawPosition = _position - new Vector2(32, 16);
                
                // Apply pulsing effect for dead enemies
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
            
            if (_hasDetectedPlayer && !_isDead)
            {
                if (_exclamationTexture == null)
                {
                    CreateExclamationTexture(spriteBatch.GraphicsDevice);
                }
                
                if (_exclamationTexture != null)
                {
                    Vector2 exclamationPos = _position - new Vector2(_exclamationTexture.Width / 2.0f, _size + 10);
                    spriteBatch.Draw(_exclamationTexture, exclamationPos, Color.Yellow);
                }
            }
        }
    }
}
