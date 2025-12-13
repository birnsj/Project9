namespace Project9
{
    /// <summary>
    /// Centralized game configuration for easy balancing and tuning
    /// </summary>
    public static class GameConfig
    {
        // ===== PLAYER MOVEMENT =====
        /// <summary>Player walking speed in pixels per second</summary>
        public const float PlayerWalkSpeed = 75.0f;
        
        /// <summary>Player running speed in pixels per second</summary>
        public const float PlayerRunSpeed = 150.0f;
        
        /// <summary>Sneak speed multiplier (applied to walk speed)</summary>
        public const float PlayerSneakSpeedMultiplier = 0.5f;
        
        /// <summary>Distance threshold for reaching target (pixels)</summary>
        public const float PlayerStopThreshold = 1.0f;
        
        /// <summary>Distance at which player starts slowing down (pixels)</summary>
        public const float PlayerSlowdownRadius = 50.0f;
        
        // ===== ENEMY AI =====
        /// <summary>Enemy chase speed in pixels per second</summary>
        public const float EnemyChaseSpeed = 100.0f;
        
        /// <summary>Enemy attack range in pixels</summary>
        public const float EnemyAttackRange = 50.0f;
        
        /// <summary>Enemy detection range (aggro radius) in pixels</summary>
        public const float EnemyDetectionRange = 200.0f;
        
        /// <summary>Enemy attack cooldown in seconds</summary>
        public const float EnemyAttackCooldown = 1.0f;
        
        /// <summary>Damage dealt by enemy per attack</summary>
        public const float EnemyAttackDamage = 10.0f;
        
        /// <summary>Damage dealt by player per attack</summary>
        public const float PlayerAttackDamage = 10.0f;
        
        /// <summary>Detection range multiplier when player is sneaking</summary>
        public const float EnemySneakDetectionMultiplier = 0.5f;
        
        /// <summary>Distance threshold for considering an enemy "near" a target position (pixels)</summary>
        public const float EnemyNearTargetThreshold = 50.0f;
        
        // ===== PATHFINDING =====
        /// <summary>Grid cell width for pathfinding (pixels) - smaller for more nodes and better path accuracy</summary>
        public const float PathfindingGridCellWidth = 16.0f;
        
        /// <summary>Grid cell height for pathfinding (pixels) - smaller for more nodes and better path accuracy</summary>
        public const float PathfindingGridCellHeight = 8.0f;
        
        /// <summary>Maximum pathfinding search distance (pixels)</summary>
        public const float PathfindingMaxSearchDistance = 800.0f;
        
        /// <summary>Maximum A* iterations to prevent infinite loops (increased for finer grid and more nodes)</summary>
        public const int PathfindingMaxIterations = 3000;
        
        // ===== COLLISION =====
        /// <summary>Spatial hash grid size for collision detection (pixels)</summary>
        public const float CollisionGridSize = 128.0f;
        
        /// <summary>Collision cell half-width (pixels)</summary>
        public const float CollisionCellHalfWidth = 32.0f;
        
        /// <summary>Collision cell half-height (pixels)</summary>
        public const float CollisionCellHalfHeight = 16.0f;
        
        /// <summary>Entity collision radius (pixels)</summary>
        public const float EntityCollisionRadius = 22.0f;
        
        /// <summary>Collision buffer to keep entities away from walls (pixels)</summary>
        public const float CollisionBuffer = 6.0f;
        
        /// <summary>Enemy search duration when player goes out of view (seconds)</summary>
        public const float EnemySearchDuration = 5.0f;
        
        /// <summary>Enemy search radius around last known player position (pixels)</summary>
        public const float EnemySearchRadius = 200.0f;
        
        /// <summary>Maximum range enemy will chase player (pixels)</summary>
        public const float EnemyMaxChaseRange = 1024.0f;
        
        /// <summary>Range within which enemy is considered "in combat" (pixels)</summary>
        public const float EnemyCombatRange = 200.0f;
        
        /// <summary>Range within which enemies are updated each frame (pixels)</summary>
        public const float EnemyUpdateRange = 500.0f;
        
        /// <summary>Enemy exclamation mark display duration (seconds)</summary>
        public const float EnemyExclamationDuration = 1.0f;
        
        // ===== PATHFINDING CACHE =====
        /// <summary>Pathfinding cache duration (seconds)</summary>
        public const float PathfindingCacheDuration = 0.5f;
        
        /// <summary>Minimum interval between pathfinding requests for same path (seconds)</summary>
        public const float PathfindingMinRequestInterval = 0.1f;
        
        /// <summary>Pathfinding cache cleanup interval (seconds)</summary>
        public const float PathfindingCacheCleanupInterval = 5.0f;
        
        /// <summary>Maximum number of pooled paths</summary>
        public const int PathfindingMaxPoolSize = 50;
        
        // ===== SPATIAL HASH =====
        /// <summary>Enemy spatial hash grid cell size (pixels)</summary>
        public const float EnemyGridSize = 256.0f;
        
        /// <summary>Collision cache grid granularity (pixels)</summary>
        public const float CollisionCacheGridSize = 16.0f;
        
        // ===== RENDERING =====
        /// <summary>Frustum culling margin for entities (pixels, divided by zoom)</summary>
        public const float FrustumCullingMargin = 200.0f;
        
        /// <summary>Damage number lifetime (seconds)</summary>
        public const float DamageNumberLifetime = 1.5f;
        
        /// <summary>Damage number vertical offset above entity (pixels)</summary>
        public const float DamageNumberOffsetY = -50.0f;
        
        /// <summary>Click effect duration (seconds)</summary>
        public const float ClickEffectDuration = 0.5f;
        
        /// <summary>Maximum number of damage numbers displayed simultaneously</summary>
        public const int MaxDamageNumbers = 50;
        
        // ===== PLAYER RESPAWN =====
        /// <summary>Player respawn countdown duration (seconds)</summary>
        public const float PlayerRespawnCountdown = 10.0f;
        
        // ===== WAYPOINT THRESHOLDS =====
        /// <summary>Distance threshold for removing waypoints (pixels)</summary>
        public const float WaypointRemoveThreshold = 10.0f;
        
        /// <summary>Distance threshold for reaching waypoint (pixels)</summary>
        public const float WaypointReachThreshold = 5.0f;
        
        /// <summary>Distance threshold for final target when sneaking (pixels)</summary>
        public const float PlayerSneakStopThreshold = 10.0f;
        
        /// <summary>Distance threshold for final target when running (pixels)</summary>
        public const float PlayerRunStopThreshold = 5.0f;
    }
}

