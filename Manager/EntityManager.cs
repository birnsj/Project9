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
        private CollisionManager _collisionManager;
        
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

        public EntityManager(Player player, CollisionManager collisionManager)
        {
            _player = player;
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
        /// Update all entities
        /// </summary>
        public void Update(float deltaTime, Vector2? followPosition)
        {
            _pathfindingStopwatch.Restart();
            _activePathfindingCount = 0;
            
            // Track if player is following cursor
            _isFollowingCursor = followPosition.HasValue;
            
            // Update player movement with CollisionManager for perfect collision resolution
            _player.Update(
                followPosition, 
                deltaTime, 
                (pos) => _collisionManager.CheckCollision(pos), 
                (from, to) => _collisionManager.IsLineOfSightBlocked(from, to),
                _collisionManager
            );
            
            // Count active pathfinding
            if (_player.TargetPosition.HasValue)
            {
                _activePathfindingCount++;
            }

            // Update all enemies
            foreach (var enemy in _enemies)
            {
                enemy.Update(
                    _player.Position, 
                    deltaTime, 
                    _player.IsSneaking, 
                    (pos) => _collisionManager.CheckCollision(pos), 
                    (from, to) => _collisionManager.IsLineOfSightBlocked(from, to),
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
            // Check if enemies are blocking the path (they move, so timing matters)
            bool enemyNearTarget = false;
            foreach (var enemy in _enemies)
            {
                float distToTarget = Vector2.Distance(enemy.Position, target);
                if (distToTarget < 50.0f) // Within enemy collision radius
                {
                    enemyNearTarget = true;
                    LogOverlay.Log($"[EntityManager] Enemy near target at ({enemy.Position.X:F1}, {enemy.Position.Y:F1}), dist={distToTarget:F1}px", LogLevel.Warning);
                }
            }
            
            Console.WriteLine($"[EntityManager] Move player to ({target.X:F0}, {target.Y:F0}), EnemyNear={enemyNearTarget}");
            _player.SetTarget(
                target, 
                (pos) => _collisionManager.CheckCollision(pos),
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

