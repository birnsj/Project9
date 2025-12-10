using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Base class for all game entities with position, movement, health, and rendering
    /// </summary>
    public abstract class Entity
    {
        // ===== POSITION =====
        protected Vector2 _position;
        
        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        // ===== MOVEMENT =====
        protected float _walkSpeed;
        protected float _runSpeed;
        protected float _currentSpeed;
        protected Vector2? _targetPosition;
        protected List<Vector2>? _path; // Nullable to allow complete clearing
        protected Vector2? _waypoint;
        protected float _stuckTimer;
        protected const float STUCK_THRESHOLD = 0.5f;

        public float CurrentSpeed => _currentSpeed;
        public Vector2? TargetPosition => _targetPosition;
        public List<Vector2>? Path => _path; // Expose path for debug visualization

        // ===== HEALTH =====
        protected float _maxHealth;
        protected float _currentHealth;
        protected bool _isFlashing;
        protected float _flashTimer;
        protected float _flashDuration = 0.5f;
        protected float _flashInterval = 0.1f;
        protected float _flashTime;

        public bool IsFlashing => _isFlashing;

        // ===== RENDERING =====
        protected Texture2D? _diamondTexture;
        protected Texture2D? _collisionSphereTexture;
        protected Texture2D? _collisionBufferTexture;
        protected Color _color;
        protected Color _normalColor;
        protected int _size = 32;

        protected Entity(Vector2 startPosition, Color normalColor, float walkSpeed, float runSpeed, float maxHealth = 100f)
        {
            _position = startPosition;
            _normalColor = normalColor;
            _color = normalColor;
            _walkSpeed = walkSpeed;
            _runSpeed = runSpeed;
            _maxHealth = maxHealth;
            _currentHealth = maxHealth;
            _currentSpeed = 0f;
        }

        /// <summary>
        /// Take a hit - damage and start flashing
        /// </summary>
        public virtual void TakeHit()
        {
            _isFlashing = true;
            _flashTimer = _flashDuration;
            _flashTime = 0f;
        }

        /// <summary>
        /// Update flash state
        /// </summary>
        protected void UpdateFlashing(float deltaTime)
        {
            if (_isFlashing)
            {
                _flashTimer -= deltaTime;
                _flashTime += deltaTime;
                
                if (_flashTimer <= 0f)
                {
                    _isFlashing = false;
                    _flashTimer = 0f;
                    _flashTime = 0f;
                }
            }
        }

        /// <summary>
        /// Clear movement target
        /// </summary>
        public virtual void ClearTarget()
        {
            _targetPosition = null;
            _path = null; // Clear path completely
            _waypoint = null;
            _stuckTimer = 0.0f;
            _path?.Clear();
            _waypoint = null;
            _currentSpeed = 0f;
            _stuckTimer = 0f;
        }

        /// <summary>
        /// Create diamond texture for isometric rendering
        /// </summary>
        protected void CreateDiamondTexture(GraphicsDevice graphicsDevice)
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
                    float dx = Math.Abs(x - center.X);
                    float dy = Math.Abs(y - center.Y);
                    float normalizedX = dx / halfWidth;
                    float normalizedY = dy / halfHeight;
                    
                    if (normalizedX + normalizedY <= 1.0f)
                    {
                        colorData[y * width + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * width + x] = Color.Transparent;
                    }
                }
            }
            
            _diamondTexture.SetData(colorData);
        }
        
        /// <summary>
        /// Create collision sphere texture for debug visualization
        /// Shows both the entity core and the collision buffer zone
        /// </summary>
        protected void CreateCollisionSphereTexture(GraphicsDevice graphicsDevice)
        {
            // Inner circle (entity core)
            int innerRadius = (int)GameConfig.EntityCollisionRadius;
            int innerDiameter = innerRadius * 2;
            
            _collisionSphereTexture = new Texture2D(graphicsDevice, innerDiameter, innerDiameter);
            Color[] innerData = new Color[innerDiameter * innerDiameter];
            
            Vector2 innerCenter = new Vector2(innerRadius, innerRadius);
            
            for (int x = 0; x < innerDiameter; x++)
            {
                for (int y = 0; y < innerDiameter; y++)
                {
                    float dx = x - innerCenter.X;
                    float dy = y - innerCenter.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    
                    // Draw inner circle outline (entity core)
                    if (distance >= innerRadius - 2 && distance <= innerRadius)
                    {
                        innerData[y * innerDiameter + x] = new Color(0, 255, 0, 180); // Green outline
                    }
                    else
                    {
                        innerData[y * innerDiameter + x] = Color.Transparent;
                    }
                }
            }
            
            _collisionSphereTexture.SetData(innerData);
            
            // Outer circle (collision buffer zone)
            float outerRadius = GameConfig.EntityCollisionRadius + GameConfig.CollisionBuffer;
            int outerDiameter = (int)(outerRadius * 2);
            
            _collisionBufferTexture = new Texture2D(graphicsDevice, outerDiameter, outerDiameter);
            Color[] outerData = new Color[outerDiameter * outerDiameter];
            
            Vector2 outerCenter = new Vector2(outerRadius, outerRadius);
            
            for (int x = 0; x < outerDiameter; x++)
            {
                for (int y = 0; y < outerDiameter; y++)
                {
                    float dx = x - outerCenter.X;
                    float dy = y - outerCenter.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    
                    // Draw outer circle outline (collision buffer)
                    if (distance >= outerRadius - 2 && distance <= outerRadius)
                    {
                        outerData[y * outerDiameter + x] = new Color(255, 255, 0, 120); // Yellow outline
                    }
                    // Fill buffer zone with semi-transparent yellow
                    else if (distance > innerRadius && distance < outerRadius)
                    {
                        outerData[y * outerDiameter + x] = new Color(255, 255, 0, 30); // Very transparent yellow
                    }
                    else
                    {
                        outerData[y * outerDiameter + x] = Color.Transparent;
                    }
                }
            }
            
            _collisionBufferTexture.SetData(outerData);
        }
        
        /// <summary>
        /// Draw collision sphere for debug visualization
        /// Shows both the entity core (green) and collision buffer zone (yellow)
        /// </summary>
        public virtual void DrawCollisionSphere(SpriteBatch spriteBatch)
        {
            if (_collisionSphereTexture == null || _collisionBufferTexture == null)
            {
                CreateCollisionSphereTexture(spriteBatch.GraphicsDevice);
            }
            
            // Draw outer collision buffer (yellow)
            if (_collisionBufferTexture != null)
            {
                float outerRadius = GameConfig.EntityCollisionRadius + GameConfig.CollisionBuffer;
                Vector2 outerDrawPos = _position - new Vector2(outerRadius, outerRadius);
                spriteBatch.Draw(_collisionBufferTexture, outerDrawPos, Color.White);
            }
            
            // Draw inner entity core (green)
            if (_collisionSphereTexture != null)
            {
                Vector2 innerDrawPos = _position - new Vector2(GameConfig.EntityCollisionRadius, GameConfig.EntityCollisionRadius);
                spriteBatch.Draw(_collisionSphereTexture, innerDrawPos, Color.White);
            }
        }

        /// <summary>
        /// Draw entity with flash effect
        /// </summary>
        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (_diamondTexture == null)
            {
                CreateDiamondTexture(spriteBatch.GraphicsDevice);
            }

            // Flash effect
            bool visible = true;
            if (_isFlashing)
            {
                int flashCycle = (int)(_flashTime / _flashInterval);
                visible = (flashCycle % 2 == 0);
            }

            if (visible && _diamondTexture != null)
            {
                Vector2 drawPosition = _position - new Vector2(32, 16);
                spriteBatch.Draw(_diamondTexture, drawPosition, _color);
            }
        }

        /// <summary>
        /// Update entity - must be implemented by derived classes
        /// </summary>
        public abstract void Update(float deltaTime);
    }
}

