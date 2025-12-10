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
        
        /// <summary>Detection range multiplier when player is sneaking</summary>
        public const float EnemySneakDetectionMultiplier = 0.5f;
        
        // ===== PATHFINDING =====
        /// <summary>Grid cell width for pathfinding (pixels)</summary>
        public const float PathfindingGridCellWidth = 64.0f;
        
        /// <summary>Grid cell height for pathfinding (pixels)</summary>
        public const float PathfindingGridCellHeight = 32.0f;
        
        /// <summary>Maximum pathfinding search distance (pixels)</summary>
        public const float PathfindingMaxSearchDistance = 800.0f;
        
        /// <summary>Maximum A* iterations to prevent infinite loops</summary>
        public const int PathfindingMaxIterations = 500;
        
        // ===== COLLISION =====
        /// <summary>Spatial hash grid size for collision detection (pixels)</summary>
        public const float CollisionGridSize = 128.0f;
        
        /// <summary>Collision cell half-width (pixels)</summary>
        public const float CollisionCellHalfWidth = 32.0f;
        
        /// <summary>Collision cell half-height (pixels)</summary>
        public const float CollisionCellHalfHeight = 16.0f;
    }
}

