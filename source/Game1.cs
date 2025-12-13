using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.Brushes;

namespace Project9
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        
        // Core systems
        private ViewportCamera _camera = null!;
        private IsometricMap _map = null!;
        private CollisionManager _collisionManager = null!;
        private KeyboardState _previousKeyboardState;
        private EntityManager _entityManager = null!;
        private InputManager _inputManager = null!;
        private RenderSystem _renderSystem = null!;
        private DiagnosticsOverlay _diagnostics = null!;
        private LogOverlay _logOverlay = null!;
        
        // Performance tracking
        private System.Diagnostics.Stopwatch _updateStopwatch = new System.Diagnostics.Stopwatch();
        private System.Diagnostics.Stopwatch _inputStopwatch = new System.Diagnostics.Stopwatch();
        private System.Diagnostics.Stopwatch _entityStopwatch = new System.Diagnostics.Stopwatch();
        private System.Diagnostics.Stopwatch _collisionStopwatch = new System.Diagnostics.Stopwatch();
        
        // UI
        private Desktop _desktop = null!;
        private Label _positionLabel = null!;
        private Label _zoomLabel = null!;
        private Button _resetButton = null!;
        private SpriteFont? _uiFont;
        private Vector2 _screenCenter;
        private Vector2 _initialCameraPosition;
        private bool _cameraFollowingPlayer = true;

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
            _camera = new ViewportCamera();
            _previousKeyboardState = Keyboard.GetState();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _map = new IsometricMap(Content, GraphicsDevice);

            // Initialize Myra
            MyraEnvironment.Game = this;

            // Load font
            try
            {
                _uiFont = Content.Load<SpriteFont>("Arial");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load font: {ex.Message}");
                _uiFont = null;
            }

            // Calculate screen center
            _screenCenter = new Vector2(
                GraphicsDevice.Viewport.Width / 2.0f,
                GraphicsDevice.Viewport.Height / 2.0f
            );

            // Initialize player
            Vector2 playerPosition;
            if (_map.MapData?.Player != null)
            {
                playerPosition = new Vector2(_map.MapData.Player.X, _map.MapData.Player.Y);
            }
            else
            {
                playerPosition = _map.GetMapCenter();
            }
            Player player = new Player(playerPosition);

            // Initialize managers
            // Create EntityManager first (without CollisionManager)
            _entityManager = new EntityManager(player);
            _entityManager.LoadEnemies(_map.MapData);
            _entityManager.LoadCameras(_map.MapData);
            
            // Create CollisionManager once with loaded enemies
            _collisionManager = new CollisionManager(_entityManager.Enemies);
            _collisionManager.LoadCollisionCells();
            
            // Set CollisionManager in EntityManager
            _entityManager.SetCollisionManager(_collisionManager);
            
            _inputManager = new InputManager(_camera, ScreenToWorld);
            _renderSystem = new RenderSystem(GraphicsDevice, _spriteBatch, _map, _camera, _uiFont);
            
            // Set RenderSystem in EntityManager (for damage numbers)
            _entityManager.SetRenderSystem(_renderSystem);
            _diagnostics = new DiagnosticsOverlay();
            _logOverlay = new LogOverlay();
            
            // Initialize diagnostics and log overlay with font
            if (_uiFont != null)
            {
                _diagnostics.Initialize(_uiFont);
                _logOverlay.Initialize(_uiFont);
                // Startup log removed - log overlay ready without message
            }
            else
            {
                Console.WriteLine("[Game] Warning: UI font not loaded - log overlay will not display");
            }

            // Pre-create all entity textures (optimization: avoid creating textures during Draw)
            player.InitializeTextures(GraphicsDevice);
            foreach (var enemy in _entityManager.Enemies)
            {
                enemy.InitializeTextures(GraphicsDevice);
            }
            foreach (var camera in _entityManager.Cameras)
            {
                camera.InitializeTextures(GraphicsDevice);
            }

            // Center camera on player
            Vector2 desiredCameraPos = player.Position - _screenCenter / _camera.Zoom;
            _camera.Position = desiredCameraPos;
            _initialCameraPosition = _camera.Position;

            CreateUI();
        }

        private void CreateUI()
        {
            _desktop = new Desktop();

            var panel = new Panel
            {
                Width = 250,
                Height = 80,
                Background = new SolidBrush(new Color(40, 40, 40, 230))
            };

            _positionLabel = new Label
            {
                Text = "Position: (0, 0)",
                TextColor = Color.White,
                Left = 10,
                Top = 10,
                Width = 230,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            panel.Widgets.Add(_positionLabel);

            _zoomLabel = new Label
            {
                Text = "Zoom: 1.0x",
                TextColor = Color.White,
                Left = 10,
                Top = 35,
                Width = 230,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            panel.Widgets.Add(_zoomLabel);

            var container = new VerticalStackPanel
            {
                Left = 10,
                Top = 10,
                Spacing = 5
            };

            container.Widgets.Add(panel);

            _resetButton = new Button
            {
                Content = new Label 
                { 
                    Text = "Reset Position",
                    TextColor = Color.White
                },
                Width = 150,
                Height = 35,
                Padding = new Myra.Graphics2D.Thickness(5)
            };
            _resetButton.Click += (s, a) =>
            {
                Vector2 mapCenter = _map.GetMapCenter();
                _entityManager.ResetPlayerPosition(mapCenter);
                _camera.FollowTarget(_entityManager.Player.Position, _screenCenter);
                _camera.Zoom = 1.0f;
            };
            container.Widgets.Add(_resetButton);

            _desktop.Root = container;
        }

        protected override void Update(GameTime gameTime)
        {
            _updateStopwatch.Restart();
            
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Check for space key to respawn immediately during countdown
            KeyboardState keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.Space) && 
                !_previousKeyboardState.IsKeyDown(Keys.Space) && 
                _entityManager.Player.IsDead && 
                _entityManager.Player.IsRespawning)
            {
                _entityManager.Player.Respawn();
                LogOverlay.Log("[Game1] Player respawned early via Space key", LogLevel.Info);
            }
            _previousKeyboardState = keyboardState;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update FPS
            _diagnostics.UpdateFPS(deltaTime);

            // Process input - Myra Desktop input disabled to prevent click consumption
            _inputStopwatch.Restart();
            var inputEvent = _inputManager.ProcessInput(deltaTime, _entityManager.Player, _entityManager.Enemies);
            _inputStopwatch.Stop();
            
            // Don't call Myra Desktop UpdateInput - it may be consuming/altering mouse state
            // Myra UI will still render correctly, we just won't process its input events
            // _desktop.UpdateInput();

            // Handle input events
            Vector2? followPosition = null;
            
            if (inputEvent != null)
            {
                switch (inputEvent.Action)
                {
                    case InputAction.Attack:
                        if (inputEvent.TargetEnemy != null)
                        {
                            _entityManager.AttackEnemy(inputEvent.TargetEnemy);
                        }
                        // IMPORTANT: In Diablo 2 style, attacks don't block movement
                        // Player can immediately click to move after attacking
                        break;

                    case InputAction.MoveTo:
                        LogOverlay.Log($"[Input] Move to: ({inputEvent.WorldPosition.X:F1}, {inputEvent.WorldPosition.Y:F1})", LogLevel.Info);
                        // CRITICAL: Always process movement - never block it
                        // This should work even if player is being attacked or just attacked
                        _entityManager.MovePlayerTo(inputEvent.WorldPosition);
                        _renderSystem.ShowClickEffect(inputEvent.WorldPosition);
                        break;

                    case InputAction.DragFollow:
                        followPosition = inputEvent.WorldPosition;
                        break;

                    case InputAction.None:
                        if (!_inputManager.IsDragging)
                        {
                            // Drag ended - clear target
                            _entityManager.ClearPlayerTarget();
                        }
                        break;

                    case InputAction.ToggleSneak:
                        _entityManager.TogglePlayerSneak();
                        break;

                    case InputAction.ReturnCamera:
                        _cameraFollowingPlayer = true;
                        break;

                    case InputAction.ToggleGrid:
                        _renderSystem.ShowGrid64x32 = !_renderSystem.ShowGrid64x32;
                        break;

                    case InputAction.ToggleCollision:
                        _renderSystem.ShowCollision = !_renderSystem.ShowCollision;
                        break;
                    
                    case InputAction.ToggleCollisionSpheres:
                        _renderSystem.ShowCollisionSpheres = !_renderSystem.ShowCollisionSpheres;
                        Console.WriteLine($"[Game] Collision spheres: {(_renderSystem.ShowCollisionSpheres ? "ON" : "OFF")}");
                        break;
                    
                    case InputAction.TogglePath:
                        _renderSystem.ShowPath = !_renderSystem.ShowPath;
                        Console.WriteLine($"[Game] Path debug: {(_renderSystem.ShowPath ? "ON" : "OFF")}");
                        break;
                    
                    case InputAction.ToggleDiagnostics:
                        _diagnostics.Toggle();
                        break;
                    
                    case InputAction.ResetDiagnostics:
                        _diagnostics.ResetFPSStats();
                        Console.WriteLine("[Diagnostics] FPS stats reset");
                        break;
                    
                    case InputAction.ToggleLog:
                        _logOverlay.Toggle();
                        break;

                    case InputAction.PanCamera:
                        _cameraFollowingPlayer = false;
                        _camera.Pan(inputEvent.Direction, deltaTime);
                        break;

                    case InputAction.Zoom:
                        HandleZoom(inputEvent.ZoomDelta, inputEvent.WorldPosition);
                        break;
                }
            }

            // Update click effect
            _renderSystem.UpdateClickEffect(deltaTime);
            
            // Update damage numbers
            _renderSystem.UpdateDamageNumbers(deltaTime);
            
            // Update log overlay
            _logOverlay.Update(deltaTime);

            // Update entities
            _entityStopwatch.Restart();
            _entityManager.Update(deltaTime, followPosition);
            _entityStopwatch.Stop();

            // Update camera
            if (_cameraFollowingPlayer)
            {
                _camera.FollowTarget(_entityManager.Player.Position, _screenCenter);
            }

            // Update UI
            if (_positionLabel != null)
            {
                _positionLabel.Text = $"Position: ({_camera.Position.X:F1}, {_camera.Position.Y:F1})";
            }
            if (_zoomLabel != null)
            {
                _zoomLabel.Text = $"Zoom: {_camera.Zoom:F2}x";
            }
            
            _updateStopwatch.Stop();
            
            // Update diagnostics
            int movingEntities = (_entityManager.Player.TargetPosition.HasValue ? 1 : 0) +
                                _entityManager.Enemies.Where(e => e.TargetPosition.HasValue).Count();
            
            var cacheStats = _collisionManager.GetCacheStats();
            
            _diagnostics.UpdateMetrics(
                drawCallCount: _renderSystem.LastDrawCallCount,
                entityUpdateTimeMs: (float)_entityStopwatch.Elapsed.TotalMilliseconds,
                pathfindingTimeMs: _entityManager.LastPathfindingTimeMs,
                activePathfindingCount: _entityManager.ActivePathfindingCount,
                totalUpdateTimeMs: (float)_updateStopwatch.Elapsed.TotalMilliseconds,
                inputUpdateTimeMs: (float)_inputStopwatch.Elapsed.TotalMilliseconds,
                collisionUpdateTimeMs: _collisionManager.LastCollisionCheckTimeMs,
                totalEntities: 1 + _entityManager.Enemies.Count,
                movingEntities: movingEntities,
                cacheHits: cacheStats.hits,
                cacheMisses: cacheStats.misses,
                cacheSize: cacheStats.cacheSize,
                cacheHitRate: cacheStats.hitRate
            );

            base.Update(gameTime);
        }

        private void HandleZoom(float scrollDelta, Vector2 mouseWorldBefore)
        {
            float zoomFactor = scrollDelta > 0 ? 1.1f : 1.0f / 1.1f;
            float oldZoom = _camera.Zoom;
            float newZoom = MathHelper.Clamp(oldZoom * zoomFactor, 0.5f, 4.0f);
            
            if (Math.Abs(newZoom - oldZoom) > 0.001f)
            {
                _camera.Zoom = newZoom;
                
                Vector2 mouseWorldAfter = ScreenToWorld(new Vector2(Mouse.GetState().X, Mouse.GetState().Y));
                Vector2 worldOffset = mouseWorldBefore - mouseWorldAfter;
                _camera.Position += worldOffset;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            _renderSystem.Render(_entityManager, _collisionManager, _desktop);
            
            // Draw log overlay
            _spriteBatch.Begin();
            _logOverlay.Draw(_spriteBatch, GraphicsDevice);
            _spriteBatch.End();
            
            // Draw diagnostics overlay on top of everything
            if (_diagnostics.IsVisible)
            {
                _spriteBatch.Begin();
                _diagnostics.Draw(_spriteBatch, GraphicsDevice);
                _spriteBatch.End();
            }
            
            base.Draw(gameTime);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return screenPosition / _camera.Zoom + _camera.Position;
        }
    }
}
