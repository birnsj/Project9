using System;
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
        private Camera _camera = null!;
        private IsometricMap _map = null!;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Desktop _desktop = null!;
        private Label _positionLabel = null!;
        private Label _zoomLabel = null!;
        private Button _resetButton = null!;
        private Vector2 _initialCameraPosition;
        private SpriteFont? _uiFont;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
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

            // Center camera on map initially (offset by screen center to properly center the view)
            Vector2 mapCenter = _map.GetMapCenter();
            Vector2 screenCenter = new Vector2(
                GraphicsDevice.Viewport.Width / 2.0f,
                GraphicsDevice.Viewport.Height / 2.0f
            );
            // Initial zoom is 1.0, so divide by 1.0 (no change needed)
            _initialCameraPosition = mapCenter - screenCenter;
            _camera.Position = _initialCameraPosition;

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
                _camera.Position = _initialCameraPosition;
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

            // Handle WASD input for panning
            Vector2 panDirection = Vector2.Zero;
            
            if (currentKeyboardState.IsKeyDown(Keys.W))
                panDirection.Y -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.S))
                panDirection.Y += 1;
            if (currentKeyboardState.IsKeyDown(Keys.A))
                panDirection.X -= 1;
            if (currentKeyboardState.IsKeyDown(Keys.D))
                panDirection.X += 1;

            if (panDirection != Vector2.Zero)
            {
                panDirection.Normalize();
                _camera.Pan(panDirection, (float)gameTime.ElapsedGameTime.TotalSeconds);
            }

            // Handle mouse wheel for zoom
            int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                float zoomAmount = scrollDelta > 0 ? 0.1f : -0.1f;
                if (zoomAmount > 0)
                    _camera.ZoomIn(zoomAmount);
                else
                    _camera.ZoomOut(Math.Abs(zoomAmount));
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
                _spriteBatch.End();
            }

            // Draw UI
            _desktop.Render();

            base.Draw(gameTime);
        }
    }
}
