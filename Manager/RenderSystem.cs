using System;
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
        private Camera _camera;
        private SpriteFont? _uiFont;
        
        private bool _showGrid64x32 = false;
        private bool _showCollision = true;
        private bool _showCollisionSpheres = true; // Show collision spheres for entities
        private bool _showPath = true; // Show path debug visualization
        private Texture2D? _gridLineTexture;
        private Texture2D? _collisionDiamondTexture;
        private Texture2D? _clickEffectTexture;
        private Texture2D? _pathLineTexture;
        
        // Click effect
        private Vector2? _clickEffectPosition;
        private float _clickEffectTimer = 0.0f;
        private const float CLICK_EFFECT_DURATION = 0.5f;
        
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

        public RenderSystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, IsometricMap map, Camera camera, SpriteFont? uiFont)
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
            _clickEffectTimer = CLICK_EFFECT_DURATION;
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

            _map.Draw(_spriteBatch);
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

            // Draw enemies
            foreach (var enemy in entityManager.Enemies)
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
                
                enemy.Draw(_spriteBatch);
                // Draw direction indicator
                enemy.DrawDirectionIndicator(_spriteBatch, enemy.Rotation);
                _lastDrawCallCount += _showCollisionSpheres ? 5 : 4; // Aggro, sight cone, collision sphere (optional), sprite, direction
            }

            // Draw player
            if (_showCollisionSpheres)
            {
                entityManager.Player.DrawCollisionSphere(_spriteBatch);
            }
            entityManager.Player.Draw(_spriteBatch);
            // Draw direction indicator
            entityManager.Player.DrawDirectionIndicator(_spriteBatch, entityManager.Player.Rotation);
            _lastDrawCallCount += _showCollisionSpheres ? 3 : 2; // Collision sphere (optional), sprite, direction
            
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

            _spriteBatch.End();

            // Screen space rendering (UI)
            if (_uiFont != null)
            {
                _spriteBatch.Begin();

                // Version number
                string versionText = "V002";
                Vector2 textSize = _uiFont.MeasureString(versionText);
                Vector2 position = new Vector2(
                    _graphicsDevice.Viewport.Width - textSize.X - 10,
                    _graphicsDevice.Viewport.Height - textSize.Y - 10
                );
                _spriteBatch.DrawString(_uiFont, versionText, position, Color.White);

                // Sneak indicator
                if (entityManager.Player.IsSneaking)
                {
                    string sneakText = "SNEAK";
                    Vector2 sneakTextSize = _uiFont.MeasureString(sneakText);
                    Vector2 sneakPosition = new Vector2(
                        _graphicsDevice.Viewport.Width / 2.0f - sneakTextSize.X / 2.0f,
                        50.0f
                    );
                    _spriteBatch.DrawString(_uiFont, sneakText, sneakPosition, Color.Purple);
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
            float fadeProgress = _clickEffectTimer / CLICK_EFFECT_DURATION;
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
    }
}

