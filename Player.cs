using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Project9
{
    public class Player
    {
        private Vector2 _position;
        private Vector2? _targetPosition;
        private float _walkSpeed;
        private float _runSpeed;
        private float _sneakSpeed;
        private float _distanceThreshold;
        private float _currentSpeed;
        private Texture2D? _texture;
        private Color _color;
        private Color _normalColor;
        private Color _sneakColor;
        private int _size;
        private bool _isSneaking;
        private Texture2D? _diamondTexture;
        private float _flashDuration;
        private float _flashTimer;
        private float _flashInterval;
        private float _flashTime;
        private bool _isFlashing;

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

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

        public float CurrentSpeed => _currentSpeed;

        public bool IsSneaking => _isSneaking;

        public void TakeHit()
        {
            _isFlashing = true;
            _flashTimer = _flashDuration;
            _flashTime = 0.0f;
        }

        public void ToggleSneak()
        {
            _isSneaking = !_isSneaking;
            _color = _isSneaking ? _sneakColor : _normalColor;
        }

        public Player(Vector2 startPosition)
        {
            _position = startPosition;
            _targetPosition = null;
            _walkSpeed = 75.0f; // pixels per second
            _runSpeed = 150.0f; // pixels per second
            _sneakSpeed = _walkSpeed / 2.0f; // half of walk speed
            _distanceThreshold = 100.0f; // pixels
            _currentSpeed = 0.0f;
            _normalColor = Color.Red;
            _sneakColor = Color.Purple;
            _color = _normalColor;
            _size = 32;
            _isSneaking = false;
            _flashDuration = 0.5f; // Total flash duration in seconds
            _flashTimer = 0.0f;
            _flashInterval = 0.1f; // Time between flash on/off
            _flashTime = 0.0f;
            _isFlashing = false;
        }

        public void SetTarget(Vector2 target)
        {
            _targetPosition = target;
        }

        public void ClearTarget()
        {
            _targetPosition = null;
            _currentSpeed = 0.0f;
        }

        public void Update(Vector2? followPosition, float deltaTime)
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

            Vector2? moveTarget = null;

            // Priority: follow position (mouse held) > target position (click)
            if (followPosition.HasValue)
            {
                // Add dead zone to prevent jitter when mouse is very close
                // Larger dead zone when sneaking due to slower movement
                float deadZone = _isSneaking ? 8.0f : 2.0f;
                Vector2 direction = followPosition.Value - _position;
                float distance = direction.Length();
                
                // Only update target if mouse moved significantly (dead zone)
                if (distance > deadZone)
                {
                    moveTarget = followPosition.Value;
                }
                else
                {
                    // Mouse is very close, keep current target or stop
                    if (_targetPosition.HasValue)
                    {
                        moveTarget = _targetPosition.Value;
                    }
                }
            }
            else if (_targetPosition.HasValue)
            {
                moveTarget = _targetPosition.Value;
            }

            if (moveTarget.HasValue)
            {
                Vector2 direction = moveTarget.Value - _position;
                float distance = direction.Length();

                // Determine speed based on sneak mode or distance
                if (_isSneaking)
                {
                    // Sneak mode: always use sneak speed, not affected by distance
                    _currentSpeed = _sneakSpeed;
                }
                else
                {
                    // Normal mode: use walk/run based on distance
                    if (distance < _distanceThreshold)
                    {
                        _currentSpeed = _walkSpeed;
                    }
                    else
                    {
                        _currentSpeed = _runSpeed;
                    }
                }

                // Move towards target
                // Larger stop threshold when sneaking to prevent jitter
                float stopThreshold = _isSneaking ? 10.0f : 5.0f;
                if (distance > stopThreshold)
                {
                    direction.Normalize();
                    float moveDistance = _currentSpeed * deltaTime;
                    
                    // Don't overshoot the target
                    if (moveDistance > distance)
                    {
                        moveDistance = distance;
                    }

                    _position += direction * moveDistance;
                }
                else
                {
                    // Reached target - use smoother approach, especially when sneaking
                    if (distance > 1.0f)
                    {
                        // Smooth approach - more gradual when sneaking
                        float approachFactor = _isSneaking ? 0.3f : 0.5f;
                        direction.Normalize();
                        _position += direction * distance * approachFactor;
                    }
                    else
                    {
                        // Very close, snap to target
                        _position = moveTarget.Value;
                    }
                    
                    if (!followPosition.HasValue && distance < stopThreshold)
                    {
                        // Only clear target if not following mouse and close enough
                        _targetPosition = null;
                        _currentSpeed = 0.0f;
                    }
                }
            }
            else
            {
                _currentSpeed = 0.0f;
            }
        }

        private void CreateDiamondTexture(GraphicsDevice graphicsDevice)
        {
            int halfWidth = 40;
            int halfHeight = 20;
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

        public void Draw(SpriteBatch spriteBatch)
        {
            // Create diamond texture if needed
            if (_diamondTexture == null)
            {
                CreateDiamondTexture(spriteBatch.GraphicsDevice);
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
                // Draw isometric diamond centered at position
                // Diamond is 80x40 (halfWidth=40, halfHeight=20)
                Vector2 drawPosition = _position - new Vector2(40, 20);
                Color drawColor = _isSneaking ? _sneakColor : _normalColor;
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
        }
    }
}

