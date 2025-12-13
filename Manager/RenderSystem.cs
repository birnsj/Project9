using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;

namespace Project9
{
    /// <summary>
    /// Manages all rendering including map, entities, and UI
    /// </summary>
    public class RenderSystem
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteBatch _spriteBatch;
        private IsometricMap _map;
        private ViewportCamera _camera;
        private SpriteFont? _uiFont;
        
        private bool _showGrid64x32 = true;
        private bool _showCollision = true;
        private bool _showCollisionSpheres = true; // Show collision spheres for entities
        private bool _showPath = false; // Show path debug visualization
        private Texture2D? _gridLineTexture;
        private Texture2D? _collisionDiamondTexture;
        private Texture2D? _clickEffectTexture;
        private Texture2D? _pathLineTexture;
        private Texture2D? _healthBarBackgroundTexture;
        private Texture2D? _healthBarForegroundTexture;
        private Texture2D? _bloodSplatTexture;
        
        // Click effect
        private Vector2? _clickEffectPosition;
        private float _clickEffectTimer = 0.0f;
        
        // Damage numbers (using array for better performance, no allocations)
        private DamageNumber[] _damageNumbers = new DamageNumber[GameConfig.MaxDamageNumbers];
        private int _damageNumberCount = 0;
        
        // Performance tracking
        private int _lastDrawCallCount = 0;

        public bool ShowGrid64x32
        {
            get => _showGrid64x32;
            set => _showGrid64x32 = value;
        }

        public bool ShowCollision
        {
            get => _showCollision;
            set => _showCollision = value;
        }
        
        public bool ShowCollisionSpheres
        {
            get => _showCollisionSpheres;
            set => _showCollisionSpheres = value;
        }
        
        public bool ShowPath
        {
            get => _showPath;
            set => _showPath = value;
        }
        
        public int LastDrawCallCount => _lastDrawCallCount;

        public RenderSystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, IsometricMap map, ViewportCamera camera, SpriteFont? uiFont)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _map = map;
            _camera = camera;
            _uiFont = uiFont;
        }

        /// <summary>
        /// Show click effect at position
        /// </summary>
        public void ShowClickEffect(Vector2 worldPosition)
        {
            _clickEffectPosition = worldPosition;
            _clickEffectTimer = GameConfig.ClickEffectDuration;
        }

        /// <summary>
        /// Update click effect
        /// </summary>
        public void UpdateClickEffect(float deltaTime)
        {
            if (_clickEffectTimer > 0.0f)
            {
                _clickEffectTimer -= deltaTime;
                if (_clickEffectTimer <= 0.0f)
                {
                    _clickEffectPosition = null;
                    _clickEffectTimer = 0.0f;
                }
            }
        }
        
        /// <summary>
        /// Add a damage number to display
        /// </summary>
        public void ShowDamageNumber(Vector2 worldPosition, float damage)
        {
            // Use array instead of List to avoid allocations
            if (_damageNumberCount < _damageNumbers.Length)
            {
                _damageNumbers[_damageNumberCount] = new DamageNumber(worldPosition, damage);
                _damageNumberCount++;
            }
            // If array is full, ignore new damage numbers (or overwrite oldest)
        }
        
        /// <summary>
        /// Update all damage numbers
        /// </summary>
        public void UpdateDamageNumbers(float deltaTime)
        {
            // Update in reverse order so we can remove expired ones efficiently
            for (int i = _damageNumberCount - 1; i >= 0; i--)
            {
                _damageNumbers[i].Update(deltaTime);
                if (_damageNumbers[i].IsExpired)
                {
                    // Swap with last element and decrement count (more efficient than shifting)
                    if (i < _damageNumberCount - 1)
                    {
                        _damageNumbers[i] = _damageNumbers[_damageNumberCount - 1];
                    }
                    _damageNumberCount--;
                }
            }
        }

        /// <summary>
        /// Render everything
        /// </summary>
        public void Render(EntityManager entityManager, CollisionManager collisionManager, Desktop desktop)
        {
            _lastDrawCallCount = 0;
            _graphicsDevice.Clear(Color.DarkSlateGray);

            // World space rendering
            _spriteBatch.Begin(
                transformMatrix: _camera.GetTransform(),
                samplerState: SamplerState.PointClamp
            );

            // Use frustum-culled version for better performance
            _map.Draw(_spriteBatch, _camera, _graphicsDevice);
            _lastDrawCallCount++;

            // Draw click effect
            if (_clickEffectPosition.HasValue && _clickEffectTimer > 0.0f)
            {
                DrawClickEffect(_spriteBatch);
                _lastDrawCallCount++;
            }

            if (_showGrid64x32)
            {
                DrawGrid64x32(_spriteBatch);
                _lastDrawCallCount++;
            }

            // Calculate visible bounds for frustum culling
            Vector2 screenTopLeft = ScreenToWorld(Vector2.Zero);
            Vector2 screenBottomRight = ScreenToWorld(new Vector2(
                _graphicsDevice.Viewport.Width,
                _graphicsDevice.Viewport.Height
            ));
            
            // Expand bounds with margin for safety (account for entity size and zoom)
            float margin = GameConfig.FrustumCullingMargin / _camera.Zoom; // Larger margin when zoomed out
            float minX = screenTopLeft.X - margin;
            float maxX = screenBottomRight.X + margin;
            float minY = screenTopLeft.Y - margin;
            float maxY = screenBottomRight.Y + margin;

            // Draw cameras (before enemies so they appear behind)
            foreach (var camera in entityManager.Cameras)
            {
                // Frustum culling for cameras
                if (camera.Position.X + 100 >= minX && camera.Position.X - 100 <= maxX &&
                    camera.Position.Y + 100 >= minY && camera.Position.Y - 100 <= maxY)
                {
                    camera.DrawSightCone(_spriteBatch);
                    camera.Draw(_spriteBatch);
                    _lastDrawCallCount += 2; // Sight cone, sprite
                }
            }

            // Draw enemies with frustum culling
            foreach (var enemy in entityManager.Enemies)
            {
                // Quick AABB culling check - skip enemies outside viewport
                if (enemy.Position.X + 50 < minX || enemy.Position.X - 50 > maxX ||
                    enemy.Position.Y + 50 < minY || enemy.Position.Y - 50 > maxY)
                {
                    continue; // Skip drawing this enemy
                }
                
                // Draw blood splat under dead enemies first (so it appears below)
                if (enemy.IsDead)
                {
                    DrawBloodSplat(_spriteBatch, enemy.Position);
                }
                
                // Skip drawing aggro/sight cone for dead enemies
                if (!enemy.IsAlive && !enemy.IsDead)
                    continue;
                
                if (!enemy.IsDead)
                {
                    float effectiveRange = enemy.HasDetectedPlayer 
                        ? enemy.DetectionRange 
                        : (entityManager.Player.IsSneaking ? enemy.DetectionRange * 0.5f : enemy.DetectionRange);
                        
                    enemy.DrawAggroRadius(_spriteBatch, effectiveRange);
                    enemy.DrawSightCone(_spriteBatch);
                    
                    // Draw collision sphere if enabled
                    if (_showCollisionSpheres)
                    {
                        enemy.DrawCollisionSphere(_spriteBatch);
                    }
                    
                    // Draw direction indicator
                    enemy.DrawDirectionIndicator(_spriteBatch, enemy.Rotation);
                }
                
                // Always draw enemy sprite (alive or dead with pulse)
                enemy.Draw(_spriteBatch);
                
                // Draw health bar above enemy (only if alive and player is within detection range)
                if (enemy.IsAlive)
                {
                    float distanceSquared = Vector2.DistanceSquared(entityManager.Player.Position, enemy.Position);
                    float effectiveDetectionRange = enemy.HasDetectedPlayer 
                        ? enemy.DetectionRange 
                        : (entityManager.Player.IsSneaking ? enemy.DetectionRange * GameConfig.EnemySneakDetectionMultiplier : enemy.DetectionRange);
                    float effectiveRangeSquared = effectiveDetectionRange * effectiveDetectionRange;
                    
                    // Only show health bar if player is within detection range
                    if (distanceSquared <= effectiveRangeSquared)
                    {
                        DrawEnemyHealthBar(_spriteBatch, enemy);
                    }
                }
                
                int drawCount = enemy.IsDead ? 2 : (_showCollisionSpheres ? 6 : 5); // Blood splat + sprite for dead, full set for alive
                _lastDrawCallCount += drawCount;
            }

            // Draw blood splat under dead player first (so it appears below)
            if (entityManager.Player.IsDead)
            {
                DrawBloodSplat(_spriteBatch, entityManager.Player.Position);
            }
            
            // Draw player
            if (!entityManager.Player.IsDead && _showCollisionSpheres)
            {
                entityManager.Player.DrawCollisionSphere(_spriteBatch);
            }
            entityManager.Player.Draw(_spriteBatch);
            
            // Draw direction indicator (only if alive)
            if (!entityManager.Player.IsDead)
            {
                entityManager.Player.DrawDirectionIndicator(_spriteBatch, entityManager.Player.Rotation);
            }
            
            int playerDrawCount = entityManager.Player.IsDead ? 2 : (_showCollisionSpheres ? 3 : 2); // Blood splat + sprite for dead, full set for alive
            _lastDrawCallCount += playerDrawCount;
            
            // Draw debug path for player (only if not dragging/following cursor and path debug is enabled)
            if (_showPath && !entityManager.IsFollowingCursor)
            {
                DrawDebugPath(_spriteBatch, entityManager.Player);
            }

            // Draw collision cells
            if (_showCollision)
            {
                DrawCollisionCells(_spriteBatch, collisionManager);
                _lastDrawCallCount++;
            }

            // Draw damage numbers in world space (before ending world space rendering)
            DrawDamageNumbersWorldSpace(_spriteBatch);

            _spriteBatch.End();

            // Screen space rendering (UI)
            if (_uiFont != null)
            {
                _spriteBatch.Begin();

                // Health bar in lower left corner (only if alive)
                if (entityManager.Player.IsAlive)
                {
                    DrawHealthBar(_spriteBatch, entityManager.Player);
                }

                // Death screen (if player is dead)
                if (entityManager.Player.IsDead)
                {
                    DrawDeathScreen(_spriteBatch, entityManager.Player);
                }

                // Version number
                string versionText = "V002";
                Vector2 textSize = _uiFont.MeasureString(versionText);
                Vector2 position = new Vector2(
                    _graphicsDevice.Viewport.Width - textSize.X - 10,
                    _graphicsDevice.Viewport.Height - textSize.Y - 10
                );
                _spriteBatch.DrawString(_uiFont, versionText, position, Color.White);

                // Sneak indicator
                if (entityManager.Player.IsSneaking && entityManager.Player.IsAlive)
                {
                    string sneakText = "SNEAK";
                    Vector2 sneakTextSize = _uiFont.MeasureString(sneakText);
                    Vector2 sneakPosition = new Vector2(
                        _graphicsDevice.Viewport.Width / 2.0f - sneakTextSize.X / 2.0f,
                        50.0f
                    );
                    _spriteBatch.DrawString(_uiFont, sneakText, sneakPosition, Color.Purple);
                }
                
                // Alarm countdown
                if (entityManager.AlarmActive)
                {
                    int secondsRemaining = (int)Math.Ceiling(entityManager.AlarmTimer);
                    string alarmText = $"ALARM: {secondsRemaining}";
                    Vector2 alarmTextSize = _uiFont.MeasureString(alarmText);
                    Vector2 alarmPosition = new Vector2(
                        _graphicsDevice.Viewport.Width / 2.0f - alarmTextSize.X / 2.0f,
                        100.0f
                    );
                    
                    // Flash red when time is running out
                    Color alarmColor = secondsRemaining <= 10 ? Color.Red : Color.OrangeRed;
                    
                    // Draw shadow for better visibility
                    _spriteBatch.DrawString(_uiFont, alarmText, alarmPosition + new Vector2(2, 2), Color.Black);
                    _spriteBatch.DrawString(_uiFont, alarmText, alarmPosition, alarmColor);
                }

                _spriteBatch.End();
            }

            // Myra UI
            desktop.Render();
        }

        private void DrawGrid64x32(SpriteBatch spriteBatch)
        {
            const float gridX = 64.0f;
            
            if (_gridLineTexture == null)
            {
                _gridLineTexture = new Texture2D(_graphicsDevice, 1, 1);
                _gridLineTexture.SetData(new[] { new Color(100, 80, 80, 80) });
            }

            Vector2 topLeft = ScreenToWorld(Vector2.Zero);
            Vector2 bottomRight = ScreenToWorld(new Vector2(
                _graphicsDevice.Viewport.Width,
                _graphicsDevice.Viewport.Height
            ));

            float minX = topLeft.X - Project9.Shared.IsometricMath.TileWidth * 2;
            float maxX = bottomRight.X + Project9.Shared.IsometricMath.TileWidth * 2;
            float minY = topLeft.Y - Project9.Shared.IsometricMath.TileHeight * 2;
            float maxY = bottomRight.Y + Project9.Shared.IsometricMath.TileHeight * 2;

            var (minTileX, minTileY) = Project9.Shared.IsometricMath.ScreenToTile(minX, minY);
            var (maxTileX, maxTileY) = Project9.Shared.IsometricMath.ScreenToTile(maxX, maxY);

            minTileX -= 3;
            minTileY -= 3;
            maxTileX += 3;
            maxTileY += 3;

            const int gridCellsPerTile = (int)(Project9.Shared.IsometricMath.TileWidth / gridX);

            // Draw lines parallel to tile edges
            for (int tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                {
                    float cellProgress = gridCell / (float)gridCellsPerTile;
                    float offsetX = cellProgress * (Project9.Shared.IsometricMath.TileWidth / 2.0f);
                    float offsetY = cellProgress * (Project9.Shared.IsometricMath.TileHeight / 2.0f);

                    var (startX, startY) = Project9.Shared.IsometricMath.TileToScreen(tileX, minTileY);
                    startX += offsetX;
                    startY += offsetY;

                    var (endX, endY) = Project9.Shared.IsometricMath.TileToScreen(tileX, maxTileY);
                    endX += offsetX;
                    endY += offsetY;

                    if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                        (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                    {
                        DrawLine(spriteBatch, new Vector2(startX, startY), new Vector2(endX, endY), _gridLineTexture!);
                    }
                }
            }

            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                {
                    float cellProgress = gridCell / (float)gridCellsPerTile;
                    float offsetX = -cellProgress * (Project9.Shared.IsometricMath.TileWidth / 2.0f);
                    float offsetY = cellProgress * (Project9.Shared.IsometricMath.TileHeight / 2.0f);

                    var (startX, startY) = Project9.Shared.IsometricMath.TileToScreen(minTileX, tileY);
                    startX += offsetX;
                    startY += offsetY;

                    var (endX, endY) = Project9.Shared.IsometricMath.TileToScreen(maxTileX, tileY);
                    endX += offsetX;
                    endY += offsetY;

                    if ((startX >= minX && startX <= maxX) || (endX >= minX && endX <= maxX) ||
                        (startX < minX && endX > maxX) || (startX > maxX && endX < minX))
                    {
                        DrawLine(spriteBatch, new Vector2(startX, startY), new Vector2(endX, endY), _gridLineTexture!);
                    }
                }
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Texture2D texture)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();

            spriteBatch.Draw(
                texture,
                start,
                null,
                Color.White,
                angle,
                Vector2.Zero,
                new Vector2(length, 1),
                SpriteEffects.None,
                0.0f
            );
        }

        private void DrawCollisionCells(SpriteBatch spriteBatch, CollisionManager collisionManager)
        {
            if (_collisionDiamondTexture == null)
            {
                CreateCollisionDiamondTexture();
            }

            foreach (var cell in collisionManager.GetCollisionCells())
            {
                Vector2 drawPosition = new Vector2(cell.X, cell.Y) - new Vector2(32, 16);
                spriteBatch.Draw(_collisionDiamondTexture, drawPosition, Color.White);
            }
        }

        private void CreateCollisionDiamondTexture()
        {
            int halfWidth = 32;
            int halfHeight = 16;
            int width = halfWidth * 2;
            int height = halfHeight * 2;
            
            _collisionDiamondTexture = new Texture2D(_graphicsDevice, width, height);
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
                        colorData[y * width + x] = new Color(128, 0, 128, 180);
                    }
                    else
                    {
                        colorData[y * width + x] = Color.Transparent;
                    }
                }
            }
            
            _collisionDiamondTexture.SetData(colorData);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return screenPosition / _camera.Zoom + _camera.Position;
        }

        private void DrawDebugPath(SpriteBatch spriteBatch, Player player)
        {
            // Only draw if player has a target
            if (!player.TargetPosition.HasValue)
                return;
            
            // Create line texture if needed
            if (_pathLineTexture == null)
            {
                _pathLineTexture = new Texture2D(_graphicsDevice, 1, 1);
                _pathLineTexture.SetData(new[] { Color.White });
            }
            
            // If there's a pathfinding path, draw it (this means terrain is blocked)
            // Pathfinding paths use cyan/yellow to indicate going around obstacles
            if (player.Path != null && player.Path.Count > 0)
            {
                // Draw path from player position through all waypoints to target
                Vector2? previousPoint = player.Position;
                
                // Draw lines connecting path waypoints (cyan = going around obstacles)
                foreach (var waypoint in player.Path)
                {
                    if (previousPoint.HasValue)
                    {
                        DrawPathLine(spriteBatch, previousPoint.Value, waypoint, Color.Cyan);
                    }
                    previousPoint = waypoint;
                }
                
                // Draw line to target if it exists and is different from last waypoint
                if (player.TargetPosition.HasValue && previousPoint.HasValue)
                {
                    float distToTarget = Vector2.Distance(previousPoint.Value, player.TargetPosition.Value);
                    if (distToTarget > 5.0f) // Only draw if target is significantly different
                    {
                        DrawPathLine(spriteBatch, previousPoint.Value, player.TargetPosition.Value, Color.Yellow);
                    }
                }
                
                // Draw waypoint markers
                foreach (var waypoint in player.Path)
                {
                    DrawPathWaypoint(spriteBatch, waypoint, Color.Cyan);
                }
            }
            else
            {
                // No pathfinding path - terrain path is clear, draw direct line (green)
                // Enemy collision will be handled during movement via sliding
                // Only draw if target is far enough away to be meaningful
                float distToTarget = Vector2.Distance(player.Position, player.TargetPosition.Value);
                if (distToTarget > 5.0f)
                {
                    // Green = direct path, terrain is clear (enemies will be handled by collision sliding)
                    DrawPathLine(spriteBatch, player.Position, player.TargetPosition.Value, Color.Lime);
                    // Also draw target marker
                    DrawPathWaypoint(spriteBatch, player.TargetPosition.Value, Color.Lime, 8.0f);
                }
            }
            
            // Draw target marker
            if (player.TargetPosition.HasValue)
            {
                DrawPathWaypoint(spriteBatch, player.TargetPosition.Value, Color.Yellow, 8.0f);
            }
        }
        
        private void DrawPathLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            // Use semi-transparent color for path lines
            Color lineColor = new Color(color.R, color.G, color.B, (byte)180);
            
            spriteBatch.Draw(
                _pathLineTexture!,
                start,
                null,
                lineColor,
                angle,
                Vector2.Zero,
                new Vector2(length, 3.0f), // 3 pixel thick line
                SpriteEffects.None,
                0.0f
            );
        }
        
        private void DrawPathWaypoint(SpriteBatch spriteBatch, Vector2 position, Color color, float size = 6.0f)
        {
            if (_pathLineTexture == null)
            {
                _pathLineTexture = new Texture2D(_graphicsDevice, 1, 1);
                _pathLineTexture.SetData(new[] { Color.White });
            }
            
            // Draw a small circle/square at waypoint position
            Color waypointColor = new Color(color.R, color.G, color.B, (byte)220);
            
            // Draw a small diamond shape (matching isometric style)
            float halfSize = size / 2.0f;
            
            // Draw 4 lines forming a diamond
            Vector2[] diamondPoints = new Vector2[]
            {
                position + new Vector2(0, -halfSize),      // Top
                position + new Vector2(halfSize, 0),       // Right
                position + new Vector2(0, halfSize),        // Bottom
                position + new Vector2(-halfSize, 0)       // Left
            };
            
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                DrawPathLine(spriteBatch, diamondPoints[i], diamondPoints[next], waypointColor);
            }
        }
        
        private void DrawClickEffect(SpriteBatch spriteBatch)
        {
            if (!_clickEffectPosition.HasValue)
                return;

            // Create texture if needed
            if (_clickEffectTexture == null)
            {
                int size = 64;
                _clickEffectTexture = new Texture2D(_graphicsDevice, size, size);
                Color[] colorData = new Color[size * size];
                
                Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
                float radius = size / 2.0f;
                
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float normalizedDist = distance / radius;
                        
                        // Outer ring (pulsing effect)
                        if (normalizedDist >= 0.7f && normalizedDist <= 1.0f)
                        {
                            float ringAlpha = 1.0f - (normalizedDist - 0.7f) / 0.3f;
                            colorData[y * size + x] = new Color((byte)100, (byte)200, (byte)255, (byte)(ringAlpha * 200));
                        }
                        // Inner circle
                        else if (normalizedDist <= 0.3f)
                        {
                            float innerAlpha = 1.0f - (normalizedDist / 0.3f);
                            colorData[y * size + x] = new Color((byte)150, (byte)220, (byte)255, (byte)(innerAlpha * 150));
                        }
                        else
                        {
                            colorData[y * size + x] = Color.Transparent;
                        }
                    }
                }
                
                _clickEffectTexture.SetData(colorData);
            }

            // Calculate fade based on timer
            float fadeProgress = _clickEffectTimer / GameConfig.ClickEffectDuration;
            float pulseScale = 1.0f + (1.0f - fadeProgress) * 0.5f; // Grows from 1.0 to 1.5
            byte fadeAlpha = (byte)(fadeProgress * 255);
            
            Vector2 drawPos = _clickEffectPosition.Value - new Vector2(_clickEffectTexture.Width / 2.0f * pulseScale, _clickEffectTexture.Height / 2.0f * pulseScale);
            Color tint = new Color((byte)255, (byte)255, (byte)255, fadeAlpha);
            
            spriteBatch.Draw(
                _clickEffectTexture,
                drawPos,
                null,
                tint,
                0f,
                Vector2.Zero,
                pulseScale,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawHealthBar(SpriteBatch spriteBatch, Player player)
        {
            const int barWidth = 200;
            const int barHeight = 20;
            const int padding = 10;
            const int borderThickness = 2;
            
            // Position in lower left corner
            Vector2 barPosition = new Vector2(
                padding,
                _graphicsDevice.Viewport.Height - barHeight - padding
            );
            
            // Create textures if needed
            if (_healthBarBackgroundTexture == null)
            {
                _healthBarBackgroundTexture = new Texture2D(_graphicsDevice, barWidth, barHeight);
                Color[] bgData = new Color[barWidth * barHeight];
                for (int i = 0; i < bgData.Length; i++)
                {
                    bgData[i] = new Color(40, 40, 40, 230); // Dark gray background
                }
                _healthBarBackgroundTexture.SetData(bgData);
            }
            
            if (_healthBarForegroundTexture == null)
            {
                _healthBarForegroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _healthBarForegroundTexture.SetData(new[] { Color.White });
            }
            
            // Draw background
            spriteBatch.Draw(_healthBarBackgroundTexture, barPosition, Color.White);
            
            // Calculate health percentage
            float healthPercent = player.MaxHealth > 0 ? player.CurrentHealth / player.MaxHealth : 0f;
            healthPercent = MathHelper.Clamp(healthPercent, 0f, 1f);
            
            // Determine health bar color based on health percentage
            Color healthColor;
            if (healthPercent > 0.6f)
            {
                // Green when above 60%
                healthColor = Color.Green;
            }
            else if (healthPercent > 0.3f)
            {
                // Yellow when between 30% and 60%
                healthColor = Color.Yellow;
            }
            else
            {
                // Red when below 30%
                healthColor = Color.Red;
            }
            
            // Draw health bar foreground
            int healthBarWidth = (int)((barWidth - borderThickness * 2) * healthPercent);
            if (healthBarWidth > 0)
            {
                Rectangle healthRect = new Rectangle(
                    (int)barPosition.X + borderThickness,
                    (int)barPosition.Y + borderThickness,
                    healthBarWidth,
                    barHeight - borderThickness * 2
                );
                spriteBatch.Draw(_healthBarForegroundTexture, healthRect, healthColor);
            }
            
            // Draw border
            Rectangle borderRect = new Rectangle(
                (int)barPosition.X,
                (int)barPosition.Y,
                barWidth,
                barHeight
            );
            DrawRectangleOutline(spriteBatch, borderRect, Color.White, borderThickness);
            
            // Draw health text (above the bar)
            if (_uiFont != null)
            {
                string healthText = $"HP: {player.CurrentHealth:F0}/{player.MaxHealth:F0}";
                Vector2 textSize = _uiFont.MeasureString(healthText);
                Vector2 textPosition = new Vector2(
                    barPosition.X + (barWidth - textSize.X) / 2.0f,
                    barPosition.Y - textSize.Y - 5
                );
                spriteBatch.DrawString(_uiFont, healthText, textPosition, Color.White);
            }
        }
        
        private void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            if (_healthBarForegroundTexture == null)
            {
                _healthBarForegroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _healthBarForegroundTexture.SetData(new[] { Color.White });
            }
            
            // Top
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            
            // Bottom
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            
            // Left
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            
            // Right
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
        
        private void DrawEnemyHealthBar(SpriteBatch spriteBatch, Enemy enemy)
        {
            const float barWidth = 60.0f;
            const float barHeight = 6.0f;
            const float borderThickness = 1.0f;
            const float barOffsetY = -40.0f; // Position above enemy (in world space)
            
            // Position health bar above enemy in world space (so it moves with the enemy)
            Vector2 barPosition = new Vector2(
                enemy.Position.X - barWidth / 2.0f,
                enemy.Position.Y + barOffsetY
            );
            
            // Create textures if needed
            if (_healthBarForegroundTexture == null)
            {
                _healthBarForegroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _healthBarForegroundTexture.SetData(new[] { Color.White });
            }
            
            // Draw background (dark red)
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition,
                null,
                new Color(60, 0, 0, 200),
                0f,
                Vector2.Zero,
                new Vector2(barWidth, barHeight),
                SpriteEffects.None,
                0f
            );
            
            // Calculate health percentage
            float healthPercent = enemy.MaxHealth > 0 ? enemy.CurrentHealth / enemy.MaxHealth : 0f;
            healthPercent = MathHelper.Clamp(healthPercent, 0f, 1f);
            
            // Draw health bar (red)
            float healthBarWidth = (barWidth - borderThickness * 2) * healthPercent;
            if (healthBarWidth > 0)
            {
                Color healthColor = new Color(200, 0, 0, 255); // Red
                Vector2 healthPos = barPosition + new Vector2(borderThickness, borderThickness);
                spriteBatch.Draw(
                    _healthBarForegroundTexture,
                    healthPos,
                    null,
                    healthColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(healthBarWidth, barHeight - borderThickness * 2),
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw border (4 lines)
            float borderAlpha = 0.8f;
            byte alphaByte = (byte)(255 * borderAlpha);
            Color semiWhite = new Color((byte)255, (byte)255, (byte)255, alphaByte);
            
            // Top
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition,
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(barWidth, borderThickness),
                SpriteEffects.None,
                0f
            );
            
            // Bottom
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition + new Vector2(0, barHeight - borderThickness),
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(barWidth, borderThickness),
                SpriteEffects.None,
                0f
            );
            
            // Left
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition,
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(borderThickness, barHeight),
                SpriteEffects.None,
                0f
            );
            
            // Right
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition + new Vector2(barWidth - borderThickness, 0),
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(borderThickness, barHeight),
                SpriteEffects.None,
                0f
            );
        }
        
        private void DrawDeathScreen(SpriteBatch spriteBatch, Player player)
        {
            if (_uiFont == null)
                return;
            
            // Draw semi-transparent overlay
            if (_healthBarForegroundTexture == null)
            {
                _healthBarForegroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _healthBarForegroundTexture.SetData(new[] { Color.White });
            }
            
            Rectangle overlayRect = new Rectangle(0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
            spriteBatch.Draw(_healthBarForegroundTexture, overlayRect, new Color(0, 0, 0, 180)); // Dark overlay
            
            // "You are Dead" text
            string deathText = "You are Dead";
            Vector2 deathTextSize = _uiFont.MeasureString(deathText);
            Vector2 deathTextPos = new Vector2(
                _graphicsDevice.Viewport.Width / 2.0f - deathTextSize.X / 2.0f,
                _graphicsDevice.Viewport.Height / 2.0f - 80.0f
            );
            spriteBatch.DrawString(_uiFont, deathText, deathTextPos, Color.Red);
            
            // Countdown text
            int countdownSeconds = (int)Math.Ceiling(player.RespawnTimer);
            if (countdownSeconds < 0) countdownSeconds = 0;
            string countdownText = $"Respawning in {countdownSeconds}...";
            Vector2 countdownTextSize = _uiFont.MeasureString(countdownText);
            Vector2 countdownTextPos = new Vector2(
                _graphicsDevice.Viewport.Width / 2.0f - countdownTextSize.X / 2.0f,
                _graphicsDevice.Viewport.Height / 2.0f - 20.0f
            );
            spriteBatch.DrawString(_uiFont, countdownText, countdownTextPos, Color.White);
            
            // "Press Space to Respawn" message
            if (player.IsRespawning)
            {
                string respawnHintText = "Press Space to Respawn";
                Vector2 respawnHintTextSize = _uiFont.MeasureString(respawnHintText);
                Vector2 respawnHintTextPos = new Vector2(
                    _graphicsDevice.Viewport.Width / 2.0f - respawnHintTextSize.X / 2.0f,
                    _graphicsDevice.Viewport.Height / 2.0f + 20.0f
                );
                spriteBatch.DrawString(_uiFont, respawnHintText, respawnHintTextPos, Color.Yellow);
            }
        }
        
        private void DrawDamageNumbersWorldSpace(SpriteBatch spriteBatch)
        {
            if (_uiFont == null)
                return;
            
            if (_damageNumberCount == 0)
                return;
            
            // Iterate only over active damage numbers
            for (int i = 0; i < _damageNumberCount; i++)
            {
                var damageNumber = _damageNumbers[i];
                // Position in world space (above entity, above health bar)
                Vector2 worldPos = damageNumber.Position + new Vector2(0, GameConfig.DamageNumberOffsetY);
                
                string damageText = $"-{damageNumber.Damage:F0}";
                Vector2 textSize = _uiFont.MeasureString(damageText);
                Vector2 textPos = worldPos - new Vector2(textSize.X / 2.0f, textSize.Y / 2.0f);
                
                // Draw with alpha fade
                float alpha = damageNumber.Alpha;
                if (alpha <= 0.01f)
                    continue; // Skip if invisible
                    
                byte alphaByte = (byte)(255 * alpha);
                byte shadowAlpha = (byte)(128 * alpha);
                Color textColor = new Color((byte)255, (byte)100, (byte)100, alphaByte); // Red damage text
                
                // Draw shadow for better visibility (small offset in world space)
                spriteBatch.DrawString(_uiFont, damageText, textPos + new Vector2(2, 2), new Color((byte)0, (byte)0, (byte)0, shadowAlpha));
                spriteBatch.DrawString(_uiFont, damageText, textPos, textColor);
            }
        }
        
        private void DrawDamageNumbers(SpriteBatch spriteBatch)
        {
            // This method is no longer used - damage numbers are drawn in world space
            // Keeping for compatibility
        }
        
        private void DrawBloodSplat(SpriteBatch spriteBatch, Vector2 position)
        {
            const float splatSize = 48.0f;
            const float splatOffsetY = 8.0f; // Position slightly below enemy center
            
            // Create blood splat texture if needed
            if (_bloodSplatTexture == null)
            {
                CreateBloodSplatTexture();
            }
            
            if (_bloodSplatTexture != null)
            {
                Vector2 splatPosition = position + new Vector2(0, splatOffsetY) - new Vector2(splatSize / 2.0f, splatSize / 2.0f);
                spriteBatch.Draw(_bloodSplatTexture, splatPosition, Color.White);
            }
        }
        
        private void CreateBloodSplatTexture()
        {
            const int size = 48;
            _bloodSplatTexture = new Texture2D(_graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
            Random random = new Random(42); // Fixed seed for consistent splat shape
            
            // Create irregular blood splat shape
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float normalizedDist = distance / (size / 2.0f);
                    
                    // Create irregular shape using noise-like pattern
                    float angle = (float)Math.Atan2(y - center.Y, x - center.X);
                    float radiusVariation = 0.7f + (float)(random.NextDouble() * 0.3f); // Vary radius
                    float baseRadius = (size / 2.0f) * radiusVariation;
                    
                    // Add some spikes/droplets
                    float spikeAngle = angle * 3.0f; // Multiple spikes
                    float spikeAmount = (float)(Math.Sin(spikeAngle) * 0.2f + 1.0f);
                    float effectiveRadius = baseRadius * spikeAmount;
                    
                    if (normalizedDist <= 1.0f)
                    {
                        // Main splat body
                        float alpha = 1.0f - (normalizedDist * 0.8f); // Fade out towards edges
                        alpha = MathHelper.Clamp(alpha, 0f, 1f);
                        
                        // Dark red blood color
                        byte red = (byte)(120 + normalizedDist * 30); // Darker in center
                        byte green = 0;
                        byte blue = 0;
                        byte alphaByte = (byte)(alpha * 200); // Semi-transparent
                        
                        colorData[y * size + x] = new Color(red, green, blue, alphaByte);
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }
            
            // Add some random droplets around the splat
            for (int i = 0; i < 8; i++)
            {
                float dropletAngle = (float)(random.NextDouble() * Math.PI * 2);
                float dropletDistance = (size / 2.0f) * (0.6f + (float)(random.NextDouble() * 0.4f));
                int dropletX = (int)(center.X + Math.Cos(dropletAngle) * dropletDistance);
                int dropletY = (int)(center.Y + Math.Sin(dropletAngle) * dropletDistance);
                int dropletSize = 2 + random.Next(3);
                
                for (int dx = -dropletSize; dx <= dropletSize; dx++)
                {
                    for (int dy = -dropletSize; dy <= dropletSize; dy++)
                    {
                        int px = dropletX + dx;
                        int py = dropletY + dy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (dist <= dropletSize)
                            {
                                colorData[py * size + px] = new Color(150, 0, 0, 180);
                            }
                        }
                    }
                }
            }
            
            _bloodSplatTexture.SetData(colorData);
        }
    }
}

