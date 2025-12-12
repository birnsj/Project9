using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// Manages all game entities (player and enemies)
    /// </summary>
    public class EntityManager
    {
        private Player _player;
        private List<Enemy> _enemies = new List<Enemy>();
        private CollisionManager? _collisionManager;
        
        // Performance tracking
        private System.Diagnostics.Stopwatch _pathfindingStopwatch = new System.Diagnostics.Stopwatch();
        private float _lastPathfindingTimeMs = 0.0f;
        private int _activePathfindingCount = 0;
        
        // Track if player is following cursor (for UI purposes)
        private bool _isFollowingCursor = false;

        public Player Player => _player;
        public List<Enemy> Enemies => _enemies;
        public float LastPathfindingTimeMs => _lastPathfindingTimeMs;
        public int ActivePathfindingCount => _activePathfindingCount;
        public bool IsFollowingCursor => _isFollowingCursor;

        /// <summary>
        /// Create EntityManager with player (CollisionManager can be set later)
        /// </summary>
        public EntityManager(Player player)
        {
            _player = player;
        }
        
        /// <summary>
        /// Set the CollisionManager (must be called before Update)
        /// </summary>
        public void SetCollisionManager(CollisionManager collisionManager)
        {
            _collisionManager = collisionManager;
        }

        /// <summary>
        /// Load enemies from map data
        /// </summary>
        public void LoadEnemies(Project9.Shared.MapData? mapData)
        {
            _enemies.Clear();
            
            if (mapData?.Enemies != null)
            {
                foreach (var enemyData in mapData.Enemies)
                {
                    Vector2 enemyPosition = new Vector2(enemyData.X, enemyData.Y);
                    _enemies.Add(new Enemy(enemyPosition));
                }
                Console.WriteLine($"[EntityManager] Loaded {_enemies.Count} enemies");
            }
        }

        /// <summary>
        /// Get the enemy the player is currently in combat with (if any)
        /// An enemy is considered "in combat" if it has detected the player and is within combat range
        /// </summary>
        private Enemy? GetEnemyInCombat()
        {
            const float combatRange = 200.0f; // Range within which enemy is considered "in combat"
            
            Enemy? closestCombatEnemy = null;
            float closestDistance = float.MaxValue;
            
            foreach (var enemy in _enemies)
            {
                if (enemy.HasDetectedPlayer)
                {
                    float distanceToPlayer = Vector2.Distance(_player.Position, enemy.Position);
                    if (distanceToPlayer <= combatRange && distanceToPlayer < closestDistance)
                    {
                        closestCombatEnemy = enemy;
                        closestDistance = distanceToPlayer;
                    }
                }
            }
            
            return closestCombatEnemy;
        }

        /// <summary>
        /// Update all entities
        /// </summary>
        public void Update(float deltaTime, Vector2? followPosition)
        {
            if (_collisionManager == null)
                throw new InvalidOperationException("CollisionManager must be set before calling Update");
            
            _pathfindingStopwatch.Restart();
            _activePathfindingCount = 0;
            
            // Track if player is following cursor
            _isFollowingCursor = followPosition.HasValue;
            
            // Determine which enemy the player is in combat with (if any)
            Enemy? combatEnemy = GetEnemyInCombat();
            
            // Create collision check function that only checks the combat enemy (or no enemies if not in combat)
            // This allows the player to move away from non-combat enemies without collision blocking
            Func<Vector2, bool> playerCollisionCheck;
            System.Collections.Generic.IEnumerable<Enemy>? combatEnemyList = null;
            
            if (combatEnemy != null)
            {
                // Only check collision with the enemy in combat
                combatEnemyList = new List<Enemy> { combatEnemy };
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, combatEnemyList);
            }
            else
            {
                // Not in combat - only check terrain collision (no enemy collision)
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, false);
            }
            
            // Update player movement with CollisionManager for perfect collision resolution
            _player.Update(
                followPosition, 
                deltaTime, 
                playerCollisionCheck, 
                (from, to) => _collisionManager.IsLineOfSightBlocked(from, to),
                _collisionManager,
                combatEnemyList
            );
            
            // Count active pathfinding
            if (_player.TargetPosition.HasValue)
            {
                _activePathfindingCount++;
            }

            // Update all enemies
            foreach (var enemy in _enemies)
            {
                // Capture enemy position for collision checking (to exclude self from collision)
                Vector2 enemyCurrentPos = enemy.Position;
                enemy.Update(
                    _player.Position, 
                    deltaTime, 
                    _player.IsSneaking, 
                    (pos) => _collisionManager.CheckCollision(pos, true, enemyCurrentPos), 
                    (from, to) => _collisionManager.IsLineOfSightBlocked(from, to, enemyCurrentPos),
                    _collisionManager
                );
                
                // Count active pathfinding
                if (enemy.TargetPosition.HasValue)
                {
                    _activePathfindingCount++;
                }

                // Check if enemy hits player
                float distanceToPlayer = Vector2.Distance(_player.Position, enemy.Position);
                if (enemy.IsAttacking && distanceToPlayer <= enemy.AttackRange)
                {
                    _player.TakeHit();
                    break; // Only take one hit per frame
                }
            }
            
            _pathfindingStopwatch.Stop();
            _lastPathfindingTimeMs = (float)_pathfindingStopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Handle player movement command
        /// </summary>
        public void MovePlayerTo(Vector2 target)
        {
            if (_collisionManager == null)
                throw new InvalidOperationException("CollisionManager must be set before calling MovePlayerTo");
            
            // Determine which enemy the player is in combat with (if any)
            Enemy? combatEnemy = GetEnemyInCombat();
            
            // Create collision check function that only checks the combat enemy (or no enemies if not in combat)
            // This allows the player to move away from non-combat enemies without collision blocking
            Func<Vector2, bool> playerCollisionCheck;
            if (combatEnemy != null)
            {
                // Only check collision with the enemy in combat
                var combatEnemyList = new List<Enemy> { combatEnemy };
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, combatEnemyList);
            }
            else
            {
                // Not in combat - only check terrain collision (no enemy collision)
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, false);
            }
            
            // Check if enemies are blocking the path (they move, so timing matters)
            bool enemyNearTarget = false;
            foreach (var enemy in _enemies)
            {
                float distToTarget = Vector2.Distance(enemy.Position, target);
                if (distToTarget < GameConfig.EnemyNearTargetThreshold)
                {
                    enemyNearTarget = true;
                    LogOverlay.Log($"[EntityManager] Enemy near target at ({enemy.Position.X:F1}, {enemy.Position.Y:F1}), dist={distToTarget:F1}px", LogLevel.Warning);
                }
            }
            
            Console.WriteLine($"[EntityManager] Move player to ({target.X:F0}, {target.Y:F0}), EnemyNear={enemyNearTarget}, CombatEnemy={(combatEnemy != null ? "Yes" : "No")}");
            _player.SetTarget(
                target, 
                playerCollisionCheck,
                (pos) => _collisionManager.CheckCollision(pos, false) // Terrain-only for target validation
            );
        }

        /// <summary>
        /// Handle player attack enemy
        /// </summary>
        public void AttackEnemy(Enemy enemy)
        {
            Console.WriteLine("[EntityManager] Attacked enemy");
            enemy.TakeHit();
        }

        /// <summary>
        /// Clear player movement target
        /// </summary>
        public void ClearPlayerTarget()
        {
            _player.ClearTarget();
        }

        /// <summary>
        /// Toggle player sneak mode
        /// </summary>
        public void TogglePlayerSneak()
        {
            _player.ToggleSneak();
        }

        /// <summary>
        /// Reset player position
        /// </summary>
        public void ResetPlayerPosition(Vector2 position)
        {
            _player.Position = position;
            _player.ClearTarget();
        }
    }
}

