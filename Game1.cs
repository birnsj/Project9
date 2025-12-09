using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.Brushes;
using Project9.Shared;

namespace Project9
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Camera _camera = null!;
        private IsometricMap _map = null!;
        private Player _player = null!;
        private List<Enemy> _enemies = new List<Enemy>();
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Desktop _desktop = null!;
        private Label _positionLabel = null!;
        private Label _zoomLabel = null!;
        private Button _resetButton = null!;
        private Vector2 _initialCameraPosition;
        private SpriteFont? _uiFont;
        private Vector2 _screenCenter;
        private bool _cameraFollowingPlayer = true;
        private bool _showGrid64x32 = false;
        private bool _showCollision = true; // Default to showing collision
        private Texture2D? _gridLineTexture;
        private List<CollisionCellData> _collisionCells = new List<CollisionCellData>();
        private Texture2D? _collisionDiamondTexture;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = 1080;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
            _camera = new Camera();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _map = new IsometricMap(Content, GraphicsDevice);

            // Initialize Myra
            MyraEnvironment.Game = this;

            // Load font for Myra UI - Myra needs a SpriteFont to render text
            try
            {
                _uiFont = Content.Load<SpriteFont>("Arial");
            }
            catch (Exception ex)
            {
                // If font loading fails, Myra will try to use default
                System.Diagnostics.Debug.WriteLine($"Failed to load font: {ex.Message}");
                _uiFont = null;
            }

            // Calculate screen center
            _screenCenter = new Vector2(
                GraphicsDevice.Viewport.Width / 2.0f,
                GraphicsDevice.Viewport.Height / 2.0f
            );

            // Initialize player position from map data or default to map center
            Vector2 playerPosition;
            if (_map.MapData?.Player != null)
            {
                // Player position is now stored as pixel coordinates
                playerPosition = new Vector2(_map.MapData.Player.X, _map.MapData.Player.Y);
            }
            else
            {
                playerPosition = _map.GetMapCenter();
            }
            _player = new Player(playerPosition);

            // Load collision cells
            LoadCollisionCells();

            // Load enemies from map data
            if (_map.MapData?.Enemies != null)
            {
                foreach (var enemyData in _map.MapData.Enemies)
                {
                    // Enemy position is now stored as pixel coordinates
                    Vector2 enemyPosition = new Vector2(enemyData.X, enemyData.Y);
                    _enemies.Add(new Enemy(enemyPosition));
                }
            }

            // Center camera on player initially (set directly, no lerp on first frame)
            Vector2 desiredCameraPos = _player.Position - _screenCenter / _camera.Zoom;
            _camera.Position = desiredCameraPos;
            _initialCameraPosition = _camera.Position;

            CreateUI();
        }

        private void CreateUI()
        {
            _desktop = new Desktop();

            // Panel background - smaller vertically
            var panel = new Panel
            {
                Width = 250,
                Height = 80,
                Background = new SolidBrush(new Microsoft.Xna.Framework.Color(40, 40, 40, 230))
            };

            // Position label
            _positionLabel = new Label
            {
                Text = "Position: (0, 0)",
                TextColor = Microsoft.Xna.Framework.Color.White,
                Left = 10,
                Top = 10,
                Width = 230,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            panel.Widgets.Add(_positionLabel);

            // Zoom label
            _zoomLabel = new Label
            {
                Text = "Zoom: 1.0x",
                TextColor = Microsoft.Xna.Framework.Color.White,
                Left = 10,
                Top = 35,
                Width = 230,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            panel.Widgets.Add(_zoomLabel);

            // Create container for panel and button
            var container = new VerticalStackPanel
            {
                Left = 10,
                Top = 10,
                Spacing = 5
            };

            // Add panel to container
            container.Widgets.Add(panel);

            // Reset button - outside the panel, below it
            _resetButton = new Button
            {
                Content = new Label 
                { 
                    Text = "Reset Position",
                    TextColor = Microsoft.Xna.Framework.Color.White
                },
                Width = 150,
                Height = 35,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            _resetButton.Click += (s, a) =>
            {
                // Reset player to map center
                Vector2 mapCenter = _map.GetMapCenter();
                _player.Position = mapCenter;
                _player.ClearTarget();
                _camera.FollowTarget(_player.Position, _screenCenter);
                _camera.Zoom = 1.0f;
            };
            container.Widgets.Add(_resetButton);

            _desktop.Root = container;
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Handle left Ctrl for sneak mode toggle
            if (currentKeyboardState.IsKeyDown(Keys.LeftControl) && 
                !_previousKeyboardState.IsKeyDown(Keys.LeftControl))
            {
                _player.ToggleSneak();
            }

            // Handle space bar to return camera to following player
            if (currentKeyboardState.IsKeyDown(Keys.Space) && 
                !_previousKeyboardState.IsKeyDown(Keys.Space))
            {
                _cameraFollowingPlayer = true;
            }

            // Handle G key to toggle 64x32 grid
            if (currentKeyboardState.IsKeyDown(Keys.G) && 
                !_previousKeyboardState.IsKeyDown(Keys.G))
            {
                _showGrid64x32 = !_showGrid64x32;
            }

            // Handle C key to toggle collision visibility
            if (currentKeyboardState.IsKeyDown(Keys.C) && 
                !_previousKeyboardState.IsKeyDown(Keys.C))
            {
                _showCollision = !_showCollision;
            }

            // Handle WASD for camera panning
            Vector2 panDirection = Vector2.Zero;
            if (currentKeyboardState.IsKeyDown(Keys.W))
                panDirection.Y -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.S))
                panDirection.Y += 1;
            if (currentKeyboardState.IsKeyDown(Keys.A))
                panDirection.X -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.D))
                panDirection.X += 1;

            // If WASD is pressed, switch to manual camera mode
            if (panDirection != Vector2.Zero)
            {
                _cameraFollowingPlayer = false;
                panDirection.Normalize();
                _camera.Pan(panDirection, deltaTime);
            }

            // Handle mouse wheel for zoom (centered on mouse cursor)
            int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                // Get mouse position in screen coordinates
                Vector2 zoomMouseScreenPos = new Vector2(currentMouseState.X, currentMouseState.Y);
                
                // Convert to world coordinates before zoom
                Vector2 mouseWorldBefore = ScreenToWorld(zoomMouseScreenPos);
                
                // Calculate zoom amount (use percentage-based zoom for smoother feel)
                float zoomFactor = scrollDelta > 0 ? 1.1f : 1.0f / 1.1f;
                float oldZoom = _camera.Zoom;
                float newZoom = MathHelper.Clamp(oldZoom * zoomFactor, 0.5f, 4.0f);
                
                // Only apply if zoom actually changed (within limits)
                if (Math.Abs(newZoom - oldZoom) > 0.001f)
                {
                    _camera.Zoom = newZoom;
                    
                    // Convert mouse position to world coordinates after zoom
                    Vector2 mouseWorldAfter = ScreenToWorld(zoomMouseScreenPos);
                    
                    // Adjust camera position to keep the mouse point in the same world position
                    Vector2 worldOffset = mouseWorldBefore - mouseWorldAfter;
                    _camera.Position += worldOffset;
                }
            }

            // Handle mouse input for player movement
            Vector2 mouseScreenPos = new Vector2(currentMouseState.X, currentMouseState.Y);
            Vector2 mouseWorldPos = ScreenToWorld(mouseScreenPos);

            // Left click: check if clicking on enemy or ground
            if (currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                // Check if clicking on any enemy (within attack range)
                float playerAttackRange = 80.0f; // pixels
                bool attackedEnemy = false;
                
                foreach (var enemy in _enemies)
                {
                    Vector2 playerToEnemy = enemy.Position - _player.Position;
                    float distanceToEnemy = playerToEnemy.Length();
                    
                    // Check if click is near enemy or if player is close enough to attack
                    Vector2 clickToEnemy = enemy.Position - mouseWorldPos;
                    float clickDistanceToEnemy = clickToEnemy.Length();
                    
                    if (distanceToEnemy <= playerAttackRange || clickDistanceToEnemy <= 50.0f)
                    {
                        // Attack the enemy
                        enemy.TakeHit();
                        attackedEnemy = true;
                        break; // Only attack one enemy per click
                    }
                }
                
                if (!attackedEnemy)
                {
                    // Move to clicked position
                    _player.SetTarget(mouseWorldPos, CheckCollision);
                }
            }

            // Left button held: follow mouse cursor (only if button was already held last frame)
            Vector2? followPosition = null;
            if (currentMouseState.LeftButton == ButtonState.Pressed && 
                _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                // Button is being held (not just clicked) - follow mouse
                followPosition = mouseWorldPos;
            }

            // Update player movement with collision checking for pathfinding
            _player.Update(followPosition, deltaTime, (pos) => CheckCollision(pos), (from, to) => IsLineOfSightBlocked(from, to));

            // Update all enemies AI (chase and attack player) - pass sneaking state and collision check
            foreach (var enemy in _enemies)
            {
                enemy.Update(_player.Position, deltaTime, _player.IsSneaking, (pos) => CheckCollision(pos), (from, to) => IsLineOfSightBlocked(from, to));

                // Check if enemy hits player
                Vector2 enemyToPlayer = _player.Position - enemy.Position;
                float distanceToPlayer = enemyToPlayer.Length();
                if (enemy.IsAttacking && distanceToPlayer <= enemy.AttackRange)
                {
                    _player.TakeHit();
                    break; // Only take one hit per frame
                }
            }

            // Update camera - follow player if in follow mode, otherwise manual control
            if (_cameraFollowingPlayer)
            {
                _camera.FollowTarget(_player.Position, _screenCenter);
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;

            // Update UI information
            if (_positionLabel != null)
            {
                _positionLabel.Text = $"Position: ({_camera.Position.X:F1}, {_camera.Position.Y:F1})";
            }
            if (_zoomLabel != null)
            {
                _zoomLabel.Text = $"Zoom: {_camera.Zoom:F2}x";
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateGray);

            _spriteBatch.Begin(
                transformMatrix: _camera.GetTransform(),
                samplerState: SamplerState.PointClamp
            );

            _map.Draw(_spriteBatch);

            // Draw 64x32 grid if enabled
            if (_showGrid64x32)
            {
                DrawGrid64x32(_spriteBatch);
            }

            // Draw all enemy aggro radii, sight cones, and enemies
            foreach (var enemy in _enemies)
            {
                // Draw enemy aggro radius (before enemy so it appears behind)
                // Show effective detection range (half when sneaking, but full if already detected)
                float effectiveRange;
                if (enemy.HasDetectedPlayer)
                {
                    // Once detected, always show full range
                    effectiveRange = enemy.DetectionRange;
                }
                else
                {
                    // Not detected yet - show reduced range when sneaking
                    effectiveRange = _player.IsSneaking ? enemy.DetectionRange * 0.5f : enemy.DetectionRange;
                }
                enemy.DrawAggroRadius(_spriteBatch, effectiveRange);
                
                // Draw sight cone (when not chasing)
                enemy.DrawSightCone(_spriteBatch);
                
                // Draw enemy (before player so player appears on top)
                enemy.Draw(_spriteBatch);
            }

            // Draw player
            _player.Draw(_spriteBatch);

            // Draw collision cells (if enabled)
            if (_showCollision)
            {
                DrawCollisionCells(_spriteBatch);
            }

            _spriteBatch.End();

            // Draw version number in lower right corner
            if (_uiFont != null)
            {
                string versionText = "V001";
                Vector2 textSize = _uiFont.MeasureString(versionText);
                Vector2 position = new Vector2(
                    GraphicsDevice.Viewport.Width - textSize.X - 10,
                    GraphicsDevice.Viewport.Height - textSize.Y - 10
                );

                _spriteBatch.Begin();
                _spriteBatch.DrawString(_uiFont, versionText, position, Color.White);
                
                // Draw sneak message if player is sneaking
                if (_player.IsSneaking)
                {
                    string sneakText = "SNEAK";
                    Vector2 sneakTextSize = _uiFont.MeasureString(sneakText);
                    Vector2 sneakPosition = new Vector2(
                        GraphicsDevice.Viewport.Width / 2.0f - sneakTextSize.X / 2.0f,
                        50.0f // Top center of screen
                    );
                    _spriteBatch.DrawString(_uiFont, sneakText, sneakPosition, Color.Purple);
                }
                
                _spriteBatch.End();
            }

            // Draw UI
            _desktop.Render();

            base.Draw(gameTime);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            // Convert screen coordinates to world coordinates
            // Camera transform: Translate(-position) * Scale(zoom)
            // Inverse transform: Scale(1/zoom) * Translate(position)
            // So: world = screen / zoom + position
            Vector2 worldPos = screenPosition / _camera.Zoom + _camera.Position;
            return worldPos;
        }

        private void LoadCollisionCells()
        {
            const string collisionPath = "Content/world/collision.json";
            string? resolvedPath = ResolveCollisionPath(collisionPath);
            
            if (resolvedPath != null && File.Exists(resolvedPath))
            {
                try
                {
                    string json = File.ReadAllText(resolvedPath);
                    var collisionData = System.Text.Json.JsonSerializer.Deserialize<CollisionData>(json);
                    if (collisionData?.Cells != null)
                    {
                        _collisionCells = collisionData.Cells;
                        Console.WriteLine($"[Game1] Loaded {_collisionCells.Count} collision cells");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Game1] Error loading collision cells: {ex.Message}");
                }
            }
        }

        private static string? ResolveCollisionPath(string relativePath)
        {
            string currentDir = Directory.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            fullPath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            var dir = new DirectoryInfo(exeDir);
            while (dir != null && dir.Parent != null)
            {
                string testPath = Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
                if (File.Exists(testPath))
                    return testPath;
                dir = dir.Parent;
            }

            return null;
        }

        private bool CheckCollision(Vector2 position)
        {
            const float halfWidth = 32.0f;  // 64/2 = 32
            const float halfHeight = 16.0f; // 32/2 = 16

            // Check center and 4 corners of the player diamond for collision
            // This is more accurate than single point but not overly sensitive
            Vector2[] checkPoints = new Vector2[]
            {
                position, // Center
                new Vector2(position.X, position.Y - halfHeight), // Top
                new Vector2(position.X + halfWidth, position.Y), // Right
                new Vector2(position.X, position.Y + halfHeight), // Bottom
                new Vector2(position.X - halfWidth, position.Y)  // Left
            };

            foreach (var cell in _collisionCells)
            {
                // Check if any of the player's key points are inside the collision cell diamond
                foreach (var checkPoint in checkPoints)
                {
                    float dx = Math.Abs(checkPoint.X - cell.X);
                    float dy = Math.Abs(checkPoint.Y - cell.Y);
                    float normalizedX = dx / halfWidth;
                    float normalizedY = dy / halfHeight;
                    
                    // Check if point is inside diamond: |x-cx|/hw + |y-cy|/hh <= 1
                    // Use a small tolerance to prevent edge cases
                    if (normalizedX + normalizedY <= 1.01f)
                    {
                        return true; // Collision detected
                    }
                }
            }
            
            return false;
        }

        private bool IsLineOfSightBlocked(Vector2 from, Vector2 to)
        {
            // Check if line of sight is blocked by collision cells
            // Sample points along the line
            Vector2 direction = to - from;
            float distance = direction.Length();
            direction.Normalize();
            
            // Sample every 16 pixels along the line
            int samples = (int)(distance / 16.0f) + 1;
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector2 samplePoint = from + direction * (distance * t);
                
                if (CheckCollision(samplePoint))
                {
                    return true; // Line of sight is blocked
                }
            }
            
            return false; // Line of sight is clear
        }

        private void CreateGridLineTexture(GraphicsDevice graphicsDevice)
        {
            _gridLineTexture = new Texture2D(graphicsDevice, 1, 1);
            _gridLineTexture.SetData(new[] { new Color(100, 80, 80, 80) }); // Dark gray, semi-transparent
        }

        private void DrawGrid64x32(SpriteBatch spriteBatch)
        {
            const float gridX = 64.0f;
            
            // Create grid line texture if needed
            if (_gridLineTexture == null)
            {
                CreateGridLineTexture(spriteBatch.GraphicsDevice);
            }

            // Calculate visible area in world coordinates
            Vector2 topLeft = ScreenToWorld(Vector2.Zero);
            Vector2 bottomRight = ScreenToWorld(new Vector2(
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height
            ));

            // Expand bounds to ensure we draw enough grid lines
            float minX = topLeft.X - IsometricMath.TileWidth * 2;
            float maxX = bottomRight.X + IsometricMath.TileWidth * 2;
            float minY = topLeft.Y - IsometricMath.TileHeight * 2;
            float maxY = bottomRight.Y + IsometricMath.TileHeight * 2;

            // Convert visible area to tile coordinates to find which tiles are visible
            var (minTileX, minTileY) = IsometricMath.ScreenToTile(minX, minY);
            var (maxTileX, maxTileY) = IsometricMath.ScreenToTile(maxX, maxY);

            // Expand tile range
            minTileX -= 3;
            minTileY -= 3;
            maxTileX += 3;
            maxTileY += 3;

            // Grid cells per tile: 1024/64 = 16 cells horizontally
            const int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);

            // Draw lines parallel to tile edges (isometric lines)
            // Lines going northeast-southwest (parallel to tile top/bottom edges)
            for (int tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                {
                    // Calculate offset within the tile
                    float cellProgress = gridCell / (float)gridCellsPerTile;
                    float offsetX = cellProgress * (IsometricMath.TileWidth / 2.0f);
                    float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);

                    // Draw line from bottom to top of visible area
                    var (startX, startY) = IsometricMath.TileToScreen(tileX, minTileY);
                    startX += offsetX;
                    startY += offsetY;

                    var (endX, endY) = IsometricMath.TileToScreen(tileX, maxTileY);
                    endX += offsetX;
                    endY += offsetY;

                    // Clip to visible bounds
                    if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                        (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                    {
                        DrawLine(spriteBatch, new Vector2(startX, startY), new Vector2(endX, endY), _gridLineTexture!);
                    }
                }
            }

            // Lines going northwest-southeast (parallel to tile left/right edges)
            for (int tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (int gridCell = 0; gridCell < gridCellsPerTile; gridCell++)
                {
                    // Calculate offset within the tile (negative X, positive Y)
                    float cellProgress = gridCell / (float)gridCellsPerTile;
                    float offsetX = -cellProgress * (IsometricMath.TileWidth / 2.0f);
                    float offsetY = cellProgress * (IsometricMath.TileHeight / 2.0f);

                    // Draw line from left to right of visible area
                    var (startX, startY) = IsometricMath.TileToScreen(minTileX, tileY);
                    startX += offsetX;
                    startY += offsetY;

                    var (endX, endY) = IsometricMath.TileToScreen(maxTileX, tileY);
                    endX += offsetX;
                    endY += offsetY;

                    // Clip to visible bounds
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

        private void CreateCollisionDiamondTexture(GraphicsDevice graphicsDevice)
        {
            int halfWidth = 32;
            int halfHeight = 16;
            int width = halfWidth * 2;
            int height = halfHeight * 2;
            
            _collisionDiamondTexture = new Texture2D(graphicsDevice, width, height);
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
                        colorData[y * width + x] = new Color(128, 0, 128, 180); // Purple with transparency
                    }
                    else
                    {
                        colorData[y * width + x] = Color.Transparent;
                    }
                }
            }
            
            _collisionDiamondTexture.SetData(colorData);
        }

        private void DrawCollisionCells(SpriteBatch spriteBatch)
        {
            if (_collisionDiamondTexture == null)
            {
                CreateCollisionDiamondTexture(spriteBatch.GraphicsDevice);
            }

            foreach (var cell in _collisionCells)
            {
                Vector2 drawPosition = new Vector2(cell.X, cell.Y) - new Vector2(32, 16);
                spriteBatch.Draw(_collisionDiamondTexture, drawPosition, Color.White);
            }
        }
    }
}
