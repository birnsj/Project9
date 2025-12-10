using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    public class Enemy
    {
        private Vector2 _position;
        private Vector2 _originalPosition;
        private float _chaseSpeed;
        private float _attackRange;
        private float _detectionRange;
        private Texture2D? _texture;
        private Color _color;
        private int _size;
        private float _attackCooldown;
        private float _currentAttackCooldown;
        private bool _isAttacking;
        private float _flashDuration;
        private float _flashTimer;
        private float _flashInterval;
        private float _flashTime;
        private bool _isFlashing;
        private Texture2D? _circleTexture;
        private Texture2D? _sightConeTexture;
        private Texture2D? _exclamationTexture;
        private Texture2D? _diamondTexture;
        private bool _hasDetectedPlayer;
        private float _exclamationTimer; // Timer for exclamation mark display
        private float _exclamationDuration; // How long to show exclamation
        
        // Rotation and sight cone
        private float _rotation; // Current rotation angle in radians
        private float _sightConeAngle; // Field of view angle (in radians)
        private float _sightConeLength; // Length of sight cone
        private float _rotationSpeed; // Speed of rotation
        private float _behaviorTimer; // Timer for changing behavior
        private float _behaviorChangeInterval; // How often to change behavior
        private bool _isRotating; // Whether currently rotating
        private Random _random;
        private Vector2? _waypoint; // Intermediate waypoint for pathfinding around obstacles
        private List<Vector2> _path; // Path of waypoints to follow
        private float _stuckTimer; // Timer to detect if enemy is stuck
        private int _preferredSlideDirection; // -1 for left, 1 for right, 0 for no preference
        private const float STUCK_THRESHOLD = 0.5f; // Seconds before considering stuck

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public float AttackRange => _attackRange;
        public float DetectionRange => _detectionRange;
        public bool IsAttacking => _isAttacking;
        public bool HasDetectedPlayer => _hasDetectedPlayer;
        public bool IsFlashing => _isFlashing;

        public void TakeHit()
        {
            _isFlashing = true;
            _flashTimer = _flashDuration;
            _flashTime = 0.0f;
        }

        public Enemy(Vector2 startPosition)
        {
            _position = startPosition;
            _originalPosition = startPosition; // Store original spawn position
            _chaseSpeed = GameConfig.EnemyChaseSpeed;
            _attackRange = GameConfig.EnemyAttackRange;
            _detectionRange = GameConfig.EnemyDetectionRange;
            _color = Color.DarkRed;
            _size = 32;
            _attackCooldown = GameConfig.EnemyAttackCooldown;
            _currentAttackCooldown = 0.0f;
            _isAttacking = false;
            _flashDuration = 0.5f; // Total flash duration in seconds
            _flashTimer = 0.0f;
            _flashInterval = 0.1f; // Time between flash on/off
            _flashTime = 0.0f;
            _isFlashing = false;
            _hasDetectedPlayer = false;
            _path = new List<Vector2>();
            
            // Initialize rotation and sight cone
            _random = new Random();
            _rotation = (float)(_random.NextDouble() * Math.PI * 2); // Random starting rotation
            _sightConeAngle = MathHelper.ToRadians(60); // 60 degree field of view
            _sightConeLength = _detectionRange * 0.8f; // Sight cone is 80% of detection range
            _rotationSpeed = MathHelper.ToRadians(45); // Rotate 45 degrees per second
            _behaviorTimer = 0.0f;
            _behaviorChangeInterval = 2.0f + (float)(_random.NextDouble() * 3.0f); // Change behavior every 2-5 seconds
            _isRotating = _random.Next(2) == 0; // Randomly start rotating or still
            _exclamationTimer = 0.0f;
            _exclamationDuration = 1.0f; // Show exclamation for 1 second
            _waypoint = null;
            _stuckTimer = 0.0f;
            
            // Create sight cone texture
            // Note: GraphicsDevice will be available when Draw is first called
        }
        
        private bool IsPointInSightCone(Vector2 point)
        {
            Vector2 directionToPoint = point - _position;
            float distance = directionToPoint.Length();
            
            // Check if point is within sight cone length
            if (distance > _sightConeLength || distance == 0)
                return false;
            
            directionToPoint.Normalize();
            
            // Calculate angle of point relative to enemy's facing direction
            float pointAngle = (float)Math.Atan2(directionToPoint.Y, directionToPoint.X);
            
            // Normalize angles to 0-2PI range
            float enemyAngle = _rotation;
            while (enemyAngle < 0) enemyAngle += (float)(Math.PI * 2);
            while (enemyAngle >= Math.PI * 2) enemyAngle -= (float)(Math.PI * 2);
            while (pointAngle < 0) pointAngle += (float)(Math.PI * 2);
            while (pointAngle >= Math.PI * 2) pointAngle -= (float)(Math.PI * 2);
            
            // Calculate angular difference
            float angleDiff = Math.Abs(pointAngle - enemyAngle);
            if (angleDiff > Math.PI)
                angleDiff = (float)(Math.PI * 2 - angleDiff);
            
            // Check if within half the sight cone angle
            return angleDiff <= _sightConeAngle / 2.0f;
        }

        public void Update(Vector2 playerPosition, float deltaTime, bool playerIsSneaking = false, Func<Vector2, bool>? checkCollision = null, Func<Vector2, Vector2, bool>? checkLineOfSight = null)
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

            // Update attack cooldown
            if (_currentAttackCooldown > 0.0f)
            {
                _currentAttackCooldown -= deltaTime;
            }

            // Update exclamation timer
            if (_exclamationTimer > 0.0f)
            {
                _exclamationTimer -= deltaTime;
            }

            // Calculate distance to player
            Vector2 directionToPlayer = playerPosition - _position;
            float distanceToPlayer = directionToPlayer.Length();

            // Adjust detection range based on whether player is sneaking
            // Once detected, ignore sneak mode and use full range
            float effectiveDetectionRange;
            if (_hasDetectedPlayer)
            {
                // Already detected - always use full range regardless of sneak
                effectiveDetectionRange = _detectionRange;
            }
            else
            {
                // Not detected yet - sneak mode reduces detection range
                effectiveDetectionRange = playerIsSneaking ? _detectionRange * GameConfig.EnemySneakDetectionMultiplier : _detectionRange;
            }

            // Check if player is within detection range
            bool playerInRange = distanceToPlayer <= effectiveDetectionRange;
            
            if (playerInRange)
            {
                // Check if line of sight is blocked by collision
                bool lineOfSightBlocked = checkLineOfSight != null && checkLineOfSight(_position, playerPosition);
                
                // If player is sneaking and in the reduced range, also check sight cone
                if (playerIsSneaking && distanceToPlayer <= effectiveDetectionRange && !_hasDetectedPlayer)
                {
                    // Check if player is in sight cone and line of sight is not blocked
                    if (IsPointInSightCone(playerPosition) && !lineOfSightBlocked)
                    {
                        _hasDetectedPlayer = true;
                        _exclamationTimer = _exclamationDuration;
                    }
                }
                else
                {
                    // Normal detection (not sneaking or already detected)
                    // Only detect if line of sight is not blocked
                    if (!lineOfSightBlocked)
                    {
                        if (!_hasDetectedPlayer)
                        {
                            _exclamationTimer = _exclamationDuration;
                        }
                        _hasDetectedPlayer = true;
                    }
                }
            }

            // If player has been detected and is still in range, chase/attack
            if (_hasDetectedPlayer && playerInRange)
            {
                // Check if player is within attack range
                if (distanceToPlayer <= _attackRange)
                {
                    // Attack the player
                    if (_currentAttackCooldown <= 0.0f)
                    {
                        _isAttacking = true;
                        _currentAttackCooldown = _attackCooldown;
                        // Attack logic could be extended here (damage player, etc.)
                    }
                    else
                    {
                        _isAttacking = false;
                    }
                    // Stop moving when in attack range
                }
                else
                {
                    // Chase the player - check if direct path is clear first
                    _isAttacking = false;
                    
                    // Calculate target position (player position, but stop at attack range)
                    Vector2 chaseTarget = playerPosition;
                    if (distanceToPlayer > _attackRange)
                    {
                        // Target is player position, but we'll stop at attack range
                        chaseTarget = playerPosition;
                    }
                    else
                    {
                        // Already in attack range, don't move
                        return;
                    }
                    
                    // First check if direct path is clear - only use pathfinding if blocked
                    bool pathClear = true;
                    if (checkCollision != null)
                    {
                        Vector2 direction = chaseTarget - _position;
                        float distance = direction.Length();
                        int samples = (int)(distance / 16.0f) + 1;
                        for (int i = 0; i <= samples; i++)
                        {
                            float t = (float)i / samples;
                            Vector2 samplePoint = _position + (chaseTarget - _position) * t;
                            if (checkCollision(samplePoint))
                            {
                                pathClear = false;
                                break;
                            }
                        }
                    }
                    
                    // Only use pathfinding if direct path is blocked
                    if (!pathClear && (_path == null || _path.Count == 0) && checkCollision != null)
                    {
                        // Find path using A*
                        _path = FindPath(_position, chaseTarget, checkCollision);
                    }
                    else if (pathClear)
                    {
                        // Clear path - clear any existing pathfinding path
                        _path?.Clear(); // Reuse existing list to avoid allocation
                    }
                    
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
                            Vector2 directionToWaypoint = _path[0] - _position;
                            float distanceToWaypoint = directionToWaypoint.Length();
                            
                            if (distanceToWaypoint > 5.0f)
                            {
                                directionToWaypoint.Normalize();
                                float waypointMoveDistance = _chaseSpeed * deltaTime;
                                if (waypointMoveDistance > distanceToWaypoint)
                                {
                                    waypointMoveDistance = distanceToWaypoint;
                                }
                                
                                Vector2 waypointNewPosition = _position + directionToWaypoint * waypointMoveDistance;
                                if (checkCollision == null || !checkCollision(waypointNewPosition))
                                {
                                    _position = waypointNewPosition;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                    // Waypoint path blocked - recalculate path
                                    _path = FindPath(_position, chaseTarget, checkCollision);
                                    _stuckTimer += deltaTime;
                                }
                            }
                            else
                            {
                                // Reached waypoint - remove it
                                _path.RemoveAt(0);
                            }
                            return; // Exit early after pathfinding movement
                        }
                    }
                    
                    // Direct movement (when path is clear or pathfinding not needed)
                    directionToPlayer.Normalize();
                    float moveDistance = _chaseSpeed * deltaTime;
                    
                    // Don't overshoot the player - stop at attack range
                    if (moveDistance > distanceToPlayer - _attackRange)
                    {
                        moveDistance = MathHelper.Max(0, distanceToPlayer - _attackRange);
                    }

                    if (moveDistance > 0)
                    {
                        Vector2 newPosition = _position + directionToPlayer * moveDistance;
                        if (checkCollision == null || !checkCollision(newPosition))
                        {
                            _position = newPosition;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            // Hit collision - try sliding along the edge
                            Vector2 slidePosition = TrySlideAlongCollision(_position, newPosition, directionToPlayer, moveDistance, checkCollision);
                            if (slidePosition != _position)
                            {
                                _position = slidePosition;
                                _stuckTimer = 0.0f;
                            }
                            else
                            {
                                // Can't slide - start pathfinding
                                if ((_path == null || _path.Count == 0) && checkCollision != null)
                                {
                                    _path = FindPath(_position, chaseTarget, checkCollision);
                                }
                                
                                // Fallback: try to find a waypoint
                                if ((_path == null || _path.Count == 0) && !_waypoint.HasValue)
                                {
                                    _waypoint = FindWaypointAroundObstacle(_position, playerPosition, checkCollision);
                                }
                            }
                            
                            if (_waypoint.HasValue)
                            {
                                // Move toward waypoint instead
                                Vector2 directionToWaypoint = _waypoint.Value - _position;
                                float distanceToWaypoint = directionToWaypoint.Length();
                                
                                if (distanceToWaypoint > 5.0f)
                                {
                                    directionToWaypoint.Normalize();
                                    float waypointMoveDistance = _chaseSpeed * deltaTime;
                                    if (waypointMoveDistance > distanceToWaypoint)
                                    {
                                        waypointMoveDistance = distanceToWaypoint;
                                    }
                                    
                                    Vector2 waypointNewPosition = _position + directionToWaypoint * waypointMoveDistance;
                                    if (checkCollision == null || !checkCollision(waypointNewPosition))
                                    {
                                        _position = waypointNewPosition;
                                        _stuckTimer = 0.0f;
                                    }
                                    else
                                    {
                                        // Waypoint path also blocked - clear it and try again next frame
                                        _waypoint = null;
                                        _stuckTimer += deltaTime;
                                    }
                                }
                                else
                                {
                                    // Reached waypoint - clear it
                                    _waypoint = null;
                                }
                            }
                            else
                            {
                                // Can't find a path - try immediate obstacle avoidance
                                Vector2 perpLeft = new Vector2(-directionToPlayer.Y, directionToPlayer.X);
                                Vector2 perpRight = new Vector2(directionToPlayer.Y, -directionToPlayer.X);
                                
                                // Try left
                                Vector2 avoidLeft = _position + perpLeft * (_chaseSpeed * deltaTime);
                                if (checkCollision == null || !checkCollision(avoidLeft))
                                {
                                    _position = avoidLeft;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                    // Try right
                                    Vector2 avoidRight = _position + perpRight * (_chaseSpeed * deltaTime);
                                    if (checkCollision == null || !checkCollision(avoidRight))
                                    {
                                        _position = avoidRight;
                                        _stuckTimer = 0.0f;
                                    }
                                    else
                                    {
                                        // Can't move - stuck
                                        _stuckTimer += deltaTime;
                                        
                                        // If stuck for too long, force pathfinding recalculation
                                        if (_stuckTimer > STUCK_THRESHOLD && checkCollision != null)
                                        {
                                            Console.WriteLine("[Enemy] Stuck for too long, recalculating path");
                                            _path = FindPath(_position, chaseTarget, checkCollision);
                                            _waypoint = null;
                                            _stuckTimer = 0.0f;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // When chasing, face the player
                if (distanceToPlayer > _attackRange)
                {
                    directionToPlayer.Normalize();
                    _rotation = (float)Math.Atan2(directionToPlayer.Y, directionToPlayer.X);
                }
            }
            else if (_hasDetectedPlayer && !playerInRange)
            {
                // Player was detected but is now outside aggro range - return to original position
                _isAttacking = false;
                Vector2 directionToOriginal = _originalPosition - _position;
                float distanceToOriginal = directionToOriginal.Length();
                
                // Move back to original position - check if direct path is clear first
                if (distanceToOriginal > 5.0f) // Stop threshold
                {
                    // First check if direct path is clear
                    bool pathClear = true;
                    if (checkCollision != null)
                    {
                        Vector2 direction = _originalPosition - _position;
                        float distance = direction.Length();
                        int samples = (int)(distance / 16.0f) + 1;
                        for (int i = 0; i <= samples; i++)
                        {
                            float t = (float)i / samples;
                            Vector2 samplePoint = _position + (_originalPosition - _position) * t;
                            if (checkCollision(samplePoint))
                            {
                                pathClear = false;
                                break;
                            }
                        }
                    }
                    
                    // Only use pathfinding if direct path is blocked
                    if (!pathClear && (_path == null || _path.Count == 0) && checkCollision != null)
                    {
                        // Find path using A*
                        _path = FindPath(_position, _originalPosition, checkCollision);
                    }
                    else if (pathClear)
                    {
                        // Clear path - clear any existing pathfinding path
                        _path?.Clear(); // Reuse existing list to avoid allocation
                    }
                    
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
                            Vector2 directionToWaypoint = _path[0] - _position;
                            float distanceToWaypoint = directionToWaypoint.Length();
                            
                            if (distanceToWaypoint > 5.0f)
                            {
                                directionToWaypoint.Normalize();
                                float waypointMoveDistance = _chaseSpeed * deltaTime;
                                if (waypointMoveDistance > distanceToWaypoint)
                                {
                                    waypointMoveDistance = distanceToWaypoint;
                                }
                                
                                Vector2 waypointNewPosition = _position + directionToWaypoint * waypointMoveDistance;
                                if (checkCollision == null || !checkCollision(waypointNewPosition))
                                {
                                    _position = waypointNewPosition;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                    // Waypoint path blocked - recalculate path
                                    _path = FindPath(_position, _originalPosition, checkCollision);
                                    _stuckTimer += deltaTime;
                                }
                            }
                            else
                            {
                                // Reached waypoint - remove it
                                _path.RemoveAt(0);
                            }
                            return; // Exit early after pathfinding movement
                        }
                    }
                    
                    // Fallback: if pathfinding failed, try direct movement
                    directionToOriginal.Normalize();
                    float moveDistance = _chaseSpeed * deltaTime;
                    
                    // Don't overshoot the original position
                    if (moveDistance > distanceToOriginal)
                    {
                        moveDistance = distanceToOriginal;
                    }
                    
                    Vector2 newPosition = _position + directionToOriginal * moveDistance;
                    if (checkCollision == null || !checkCollision(newPosition))
                    {
                        _position = newPosition;
                        _stuckTimer = 0.0f;
                        _preferredSlideDirection = 0; // Reset preference when moving freely
                    }
                    else
                    {
                        // Hit collision - try sliding along the edge
                        Vector2 slidePosition = TrySlideAlongCollision(_position, newPosition, directionToOriginal, moveDistance, checkCollision);
                        if (slidePosition != _position)
                        {
                            _position = slidePosition;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            // Can't slide - reset preference and try to find a waypoint
                            _preferredSlideDirection = 0;
                            if (!_waypoint.HasValue)
                            {
                                _waypoint = FindWaypointAroundObstacle(_position, _originalPosition, checkCollision);
                            }
                        }
                        
                        if (_waypoint.HasValue)
                        {
                            Vector2 directionToWaypoint = _waypoint.Value - _position;
                            float distanceToWaypoint = directionToWaypoint.Length();
                            
                            if (distanceToWaypoint > 5.0f)
                            {
                                directionToWaypoint.Normalize();
                                float waypointMoveDistance = _chaseSpeed * deltaTime;
                                if (waypointMoveDistance > distanceToWaypoint)
                                {
                                    waypointMoveDistance = distanceToWaypoint;
                                }
                                
                                Vector2 waypointNewPosition = _position + directionToWaypoint * waypointMoveDistance;
                                if (checkCollision == null || !checkCollision(waypointNewPosition))
                                {
                                    _position = waypointNewPosition;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                    _waypoint = null;
                                    _stuckTimer += deltaTime;
                                }
                            }
                            else
                            {
                                _waypoint = null;
                            }
                        }
                        else
                        {
                            _stuckTimer += deltaTime;
                            
                            // If stuck for too long returning home, force pathfinding
                            if (_stuckTimer > STUCK_THRESHOLD && checkCollision != null)
                            {
                                Console.WriteLine("[Enemy] Stuck returning home, recalculating path");
                                _path = FindPath(_position, _originalPosition, checkCollision);
                                _waypoint = null;
                                _stuckTimer = 0.0f;
                            }
                        }
                    }
                    
                    // Face the direction of movement
                    _rotation = (float)Math.Atan2(directionToOriginal.Y, directionToOriginal.X);
                }
                else
                {
                    // Snap to original position if very close
                    _position = _originalPosition;
                    // Reset detection state once back at original position
                    _hasDetectedPlayer = false;
                    
                    // Update behavior timer for rotation/idle behavior
                    _behaviorTimer += deltaTime;
                    if (_behaviorTimer >= _behaviorChangeInterval)
                    {
                        // Change behavior: randomly rotate or stay still
                        _isRotating = _random.Next(2) == 0;
                        _behaviorTimer = 0.0f;
                        _behaviorChangeInterval = 2.0f + (float)(_random.NextDouble() * 3.0f); // 2-5 seconds
                    }
                    
                    // Rotate if in rotating mode
                    if (_isRotating)
                    {
                        _rotation += _rotationSpeed * deltaTime;
                        // Keep rotation in 0-2PI range
                        if (_rotation > Math.PI * 2)
                            _rotation -= (float)(Math.PI * 2);
                        if (_rotation < 0)
                            _rotation += (float)(Math.PI * 2);
                    }
                }
            }
            else
            {
                // Player is outside detection range and hasn't been detected - patrol/idle
                _isAttacking = false;
                Vector2 directionToOriginal = _originalPosition - _position;
                float distanceToOriginal = directionToOriginal.Length();
                
                // If not at original position, return to it
                if (distanceToOriginal > 5.0f) // Stop threshold
                {
                    directionToOriginal.Normalize();
                    float moveDistance = _chaseSpeed * deltaTime;
                    
                    // Don't overshoot the original position
                    if (moveDistance > distanceToOriginal)
                    {
                        moveDistance = distanceToOriginal;
                    }
                    
                    Vector2 newPosition = _position + directionToOriginal * moveDistance;
                    
                    // Check collision before moving - use pathfinding if blocked
                    if (checkCollision == null || !checkCollision(newPosition))
                    {
                        _position = newPosition;
                        _stuckTimer = 0.0f;
                        _waypoint = null;
                    }
                    else
                    {
                        // Path is blocked - try to find a way around
                        if (!_waypoint.HasValue)
                        {
                            _waypoint = FindWaypointAroundObstacle(_position, _originalPosition, checkCollision);
                        }
                        
                        if (_waypoint.HasValue)
                        {
                            // Move toward waypoint instead
                            Vector2 directionToWaypoint = _waypoint.Value - _position;
                            float distanceToWaypoint = directionToWaypoint.Length();
                            
                            if (distanceToWaypoint > 5.0f)
                            {
                                directionToWaypoint.Normalize();
                                float waypointMoveDistance = _chaseSpeed * deltaTime;
                                if (waypointMoveDistance > distanceToWaypoint)
                                {
                                    waypointMoveDistance = distanceToWaypoint;
                                }
                                
                                Vector2 waypointNewPosition = _position + directionToWaypoint * waypointMoveDistance;
                                if (checkCollision == null || !checkCollision(waypointNewPosition))
                                {
                                    _position = waypointNewPosition;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                    _waypoint = null;
                                    _stuckTimer += deltaTime;
                                }
                            }
                            else
                            {
                                _waypoint = null;
                            }
                        }
                        else
                        {
                            _stuckTimer += deltaTime;
                            
                            // If stuck for too long patrolling, try pathfinding
                            if (_stuckTimer > STUCK_THRESHOLD && checkCollision != null)
                            {
                                Console.WriteLine("[Enemy] Stuck patrolling, attempting pathfinding");
                                _path = FindPath(_position, _originalPosition, checkCollision);
                                _stuckTimer = 0.0f;
                            }
                        }
                    }
                    
                    // Face the direction of movement
                    _rotation = (float)Math.Atan2(directionToOriginal.Y, directionToOriginal.X);
                }
                else
                {
                    // At original position - patrol/idle behavior
                    _position = _originalPosition;
                    
                    // Update behavior timer for rotation/idle behavior
                    _behaviorTimer += deltaTime;
                    if (_behaviorTimer >= _behaviorChangeInterval)
                    {
                        // Change behavior: randomly rotate or stay still
                        _isRotating = _random.Next(2) == 0;
                        _behaviorTimer = 0.0f;
                        _behaviorChangeInterval = 2.0f + (float)(_random.NextDouble() * 3.0f); // 2-5 seconds
                    }
                    
                    // Rotate if in rotating mode
                    if (_isRotating)
                    {
                        _rotation += _rotationSpeed * deltaTime;
                        // Keep rotation in 0-2PI range
                        if (_rotation > Math.PI * 2)
                            _rotation -= (float)(Math.PI * 2);
                        if (_rotation < 0)
                            _rotation += (float)(Math.PI * 2);
                    }
                }
            }
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
                    
                    // Draw circle outline (2 pixel thick)
                    if (distance >= radius - 2 && distance <= radius)
                    {
                        colorData[y * diameter + x] = new Color(255, 0, 0, 100); // Semi-transparent red
                    }
                    else
                    {
                        colorData[y * diameter + x] = Color.Transparent;
                    }
                }
            }
            
            _circleTexture.SetData(colorData);
        }

        public void DrawAggroRadius(SpriteBatch spriteBatch, float effectiveRange)
        {
            // Create or recreate texture if needed for the effective range
            int radius = (int)effectiveRange;
            if (_circleTexture == null || _circleTexture.Width != radius * 2)
            {
                CreateCircleTexture(spriteBatch.GraphicsDevice, radius);
            }

            // Draw aggro radius circle centered on enemy
            Vector2 drawPosition = _position - new Vector2(effectiveRange, effectiveRange);
            spriteBatch.Draw(_circleTexture, drawPosition, Color.White);
        }

        private void CreateSightConeTexture(GraphicsDevice graphicsDevice)
        {
            int size = (int)_sightConeLength * 2;
            _sightConeTexture = new Texture2D(graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
            float halfAngle = _sightConeAngle / 2.0f;
            
            // Create a generic cone texture that can be rotated smoothly
            // The cone will be drawn pointing right (0 degrees) and rotated when drawn
            Vector2 forwardDir = new Vector2(1, 0); // Pointing right (0 degrees)
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
                        
                        // Check if point is within the cone using cross product
                        // Point is inside if it's to the right of left edge and left of right edge
                        float crossLeft = Vector2.Dot(new Vector2(-leftEdge.Y, leftEdge.X), dir);
                        float crossRight = Vector2.Dot(new Vector2(rightEdge.Y, -rightEdge.X), dir);
                        
                        if (crossLeft >= 0 && crossRight >= 0)
                        {
                            // Fade out towards edges
                            float alpha = 1.0f - (distance / _sightConeLength) * 0.5f;
                            byte alphaByte = (byte)(80 * alpha);
                            colorData[y * size + x] = new Color((byte)255, (byte)255, (byte)0, alphaByte); // Yellow, semi-transparent
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

        public void DrawSightCone(SpriteBatch spriteBatch)
        {
            if (!_hasDetectedPlayer)
            {
                // Only draw sight cone when not chasing
                if (_sightConeTexture == null)
                {
                    CreateSightConeTexture(spriteBatch.GraphicsDevice);
                }
                
                if (_sightConeTexture != null)
                {
                    // Draw sight cone with smooth rotation (no snapping)
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

        private void CreateDiamondTexture(GraphicsDevice graphicsDevice)
        {
            int halfWidth = 32;
            int halfHeight = 16;
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

        private void CreateExclamationTexture(GraphicsDevice graphicsDevice)
        {
            int size = 32;
            _exclamationTexture = new Texture2D(graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            // Draw a simple "!" shape
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    // Draw vertical line for exclamation
                    if (x >= size / 2 - 2 && x < size / 2 + 2)
                    {
                        // Top part (exclamation line)
                        if (y >= size / 4 && y < size * 3 / 4)
                        {
                            colorData[y * size + x] = Color.Yellow;
                        }
                        // Bottom dot
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

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_texture == null)
            {
                // Create a simple colored rectangle texture if not loaded
                _texture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
                _texture.SetData(new[] { _color });
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
                // Create diamond texture if needed
                if (_diamondTexture == null)
                {
                    CreateDiamondTexture(spriteBatch.GraphicsDevice);
                }
                
                // Change color when attacking
                Color drawColor = _isAttacking ? Color.OrangeRed : _color;

                // Draw isometric diamond centered at position
                // Diamond is 64x32 (halfWidth=32, halfHeight=16)
                Vector2 drawPosition = _position - new Vector2(32, 16);
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
            
            // Draw exclamation mark if player is detected (show the whole time)
            if (_hasDetectedPlayer)
            {
                if (_exclamationTexture == null)
                {
                    CreateExclamationTexture(spriteBatch.GraphicsDevice);
                }
                
                if (_exclamationTexture != null)
                {
                    // Draw exclamation above enemy head
                    Vector2 exclamationPos = _position - new Vector2(_exclamationTexture.Width / 2.0f, _size + 10);
                    // Always show at full opacity when detected
                    Color exclamationColor = Color.Yellow;
                    spriteBatch.Draw(_exclamationTexture, exclamationPos, exclamationColor);
                }
            }
        }

        private Vector2? FindWaypointAroundObstacle(Vector2 from, Vector2 to, Func<Vector2, bool>? checkCollision)
        {
            // Try to find a waypoint around obstacles
            Vector2 direction = to - from;
            float distance = direction.Length();
            if (distance < 1.0f) return null;
            direction.Normalize();
            
            // Perpendicular directions (left and right)
            Vector2 perpLeft = new Vector2(-direction.Y, direction.X);
            Vector2 perpRight = new Vector2(direction.Y, -direction.X);
            
            // Try moving perpendicular to find a clear path
            // Search in increasing distances
            float[] searchDistances = { 64.0f, 96.0f, 128.0f, 160.0f };
            
            if (checkCollision != null)
            {
                foreach (float searchDist in searchDistances)
                {
                    // Try left side
                    Vector2 waypointLeft = from + perpLeft * searchDist;
                    if (!checkCollision(waypointLeft))
                    {
                        // Check if path from waypoint to target is mostly clear
                        Vector2 toTarget = to - waypointLeft;
                        float toTargetDist = toTarget.Length();
                        if (toTargetDist < distance * 1.5f) // Waypoint should get us closer or at least not much further
                        {
                            return waypointLeft;
                        }
                    }
                    
                    // Try right side
                    Vector2 waypointRight = from + perpRight * searchDist;
                    if (!checkCollision(waypointRight))
                    {
                        Vector2 toTarget = to - waypointRight;
                        float toTargetDist = toTarget.Length();
                        if (toTargetDist < distance * 1.5f)
                        {
                            return waypointRight;
                        }
                    }
                }
            }
            
            return null; // Couldn't find a waypoint
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
        
        private List<Vector2> FindPath(Vector2 start, Vector2 end, Func<Vector2, bool> checkCollision)
        {
            List<Vector2> path = new List<Vector2>();
            
            // Simple A* pathfinding (matches Player)
            // Convert positions to grid coordinates
            int startGridX = (int)Math.Round(start.X / GameConfig.PathfindingGridCellWidth);
            int startGridY = (int)Math.Round(start.Y / GameConfig.PathfindingGridCellHeight);
            int endGridX = (int)Math.Round(end.X / GameConfig.PathfindingGridCellWidth);
            int endGridY = (int)Math.Round(end.Y / GameConfig.PathfindingGridCellHeight);
            
            // If start and end are in the same or adjacent cells, just return direct path
            if (Math.Abs(startGridX - endGridX) <= 1 && Math.Abs(startGridY - endGridY) <= 1)
            {
                return new List<Vector2> { end };
            }
            
            // If start and end are very close, just return direct path
            float directDistance = Vector2.Distance(start, end);
            if (directDistance < GameConfig.PathfindingGridCellWidth * 2)
            {
                // Check if direct path is clear
                bool pathClear = true;
                int samples = (int)(directDistance / 16.0f) + 1;
                for (int i = 0; i <= samples; i++)
                {
                    float t = (float)i / samples;
                    Vector2 samplePoint = start + (end - start) * t;
                    if (checkCollision(samplePoint))
                    {
                        pathClear = false;
                        break;
                    }
                }
                
                if (pathClear)
                {
                    return new List<Vector2> { end };
                }
            }
            
            // A* pathfinding using PriorityQueue for efficiency
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
            
            // Limit iterations to prevent performance issues
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
                
                // Check neighbors (8 directions)
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
    }
}

