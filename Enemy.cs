using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

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

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public float AttackRange => _attackRange;
        public float DetectionRange => _detectionRange;
        public bool IsAttacking => _isAttacking;
        public bool HasDetectedPlayer => _hasDetectedPlayer;

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
            _chaseSpeed = 100.0f; // pixels per second
            _attackRange = 50.0f; // pixels - distance to trigger attack
            _detectionRange = 200.0f; // pixels - how far enemy can detect player (aggro radius)
            _color = Color.DarkRed;
            _size = 32;
            _attackCooldown = 1.0f; // seconds between attacks
            _currentAttackCooldown = 0.0f;
            _isAttacking = false;
            _flashDuration = 0.5f; // Total flash duration in seconds
            _flashTimer = 0.0f;
            _flashInterval = 0.1f; // Time between flash on/off
            _flashTime = 0.0f;
            _isFlashing = false;
            _hasDetectedPlayer = false;
            
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

        public void Update(Vector2 playerPosition, float deltaTime, bool playerIsSneaking = false)
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
                effectiveDetectionRange = playerIsSneaking ? _detectionRange * 0.5f : _detectionRange;
            }

            // Check if player is within detection range
            bool playerInRange = distanceToPlayer <= effectiveDetectionRange;
            
            if (playerInRange)
            {
                // If player is sneaking and in the reduced range, also check sight cone
                if (playerIsSneaking && distanceToPlayer <= effectiveDetectionRange && !_hasDetectedPlayer)
                {
                    // Check if player is in sight cone
                    if (IsPointInSightCone(playerPosition))
                    {
                        _hasDetectedPlayer = true;
                        _exclamationTimer = _exclamationDuration;
                    }
                }
                else
                {
                    // Normal detection (not sneaking or already detected)
                    if (!_hasDetectedPlayer)
                    {
                        _exclamationTimer = _exclamationDuration;
                    }
                    _hasDetectedPlayer = true;
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
                    // Chase the player
                    _isAttacking = false;
                    directionToPlayer.Normalize();
                    float moveDistance = _chaseSpeed * deltaTime;
                    
                    // Don't overshoot the player - stop at attack range
                    if (moveDistance > distanceToPlayer - _attackRange)
                    {
                        moveDistance = MathHelper.Max(0, distanceToPlayer - _attackRange);
                    }

                    if (moveDistance > 0)
                    {
                        _position += directionToPlayer * moveDistance;
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
                
                // Move back to original position
                if (distanceToOriginal > 5.0f) // Stop threshold
                {
                    directionToOriginal.Normalize();
                    float moveDistance = _chaseSpeed * deltaTime;
                    
                    // Don't overshoot the original position
                    if (moveDistance > distanceToOriginal)
                    {
                        moveDistance = distanceToOriginal;
                    }
                    
                    _position += directionToOriginal * moveDistance;
                    
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
                    
                    _position += directionToOriginal * moveDistance;
                    
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
            
            // Draw exclamation mark if just detected player
            if (_exclamationTimer > 0.0f)
            {
                if (_exclamationTexture == null)
                {
                    CreateExclamationTexture(spriteBatch.GraphicsDevice);
                }
                
                if (_exclamationTexture != null)
                {
                    // Draw exclamation above enemy head
                    Vector2 exclamationPos = _position - new Vector2(_exclamationTexture.Width / 2.0f, _size + 10);
                    // Make it flash/pulse
                    float alpha = MathHelper.Clamp(_exclamationTimer / _exclamationDuration, 0.0f, 1.0f);
                    Color exclamationColor = new Color((byte)255, (byte)255, (byte)0, (byte)(255 * alpha));
                    spriteBatch.Draw(_exclamationTexture, exclamationPos, exclamationColor);
                }
            }
        }
    }
}

