using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    /// <summary>
    /// Displays on-screen log messages for debugging
    /// </summary>
    public class LogOverlay
    {
        private static LogOverlay? _instance;
        public static LogOverlay Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LogOverlay();
                }
                return _instance;
            }
        }
        
        private SpriteFont? _font;
        private bool _isVisible = false; // Hidden by default - press L to toggle
        private static Texture2D? _whiteTexture;
        
        // Log message storage
        private Queue<LogMessage> _messages = new Queue<LogMessage>();
        private const int MAX_MESSAGES = 20;
        private const float MESSAGE_LIFETIME = 10.0f; // Messages fade after 10 seconds
        
        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }
        
        public void Initialize(SpriteFont font)
        {
            _font = font;
            _instance = this;
            // Startup message removed - log overlay initialized silently
        }
        
        /// <summary>
        /// Static method to add log message (for easy access from anywhere)
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Instance.AddMessage(message, level);
            // Also output to console for debugging
            Console.WriteLine($"[{level}] {message}");
        }
        
        public void Toggle()
        {
            _isVisible = !_isVisible;
        }
        
        /// <summary>
        /// Add a log message to display
        /// </summary>
        public void AddMessage(string message, LogLevel level = LogLevel.Info)
        {
            _messages.Enqueue(new LogMessage
            {
                Text = message,
                Level = level,
                Timestamp = DateTime.Now,
                Age = 0.0f
            });
            
            // Keep only the most recent messages
            while (_messages.Count > MAX_MESSAGES)
            {
                _messages.Dequeue();
            }
        }
        
        /// <summary>
        /// Update message ages and remove old ones
        /// </summary>
        public void Update(float deltaTime)
        {
            // Update ages
            var messagesToRemove = new List<LogMessage>();
            foreach (var msg in _messages)
            {
                msg.Age += deltaTime;
                if (msg.Age > MESSAGE_LIFETIME)
                {
                    messagesToRemove.Add(msg);
                }
            }
            
            // Remove old messages
            foreach (var msg in messagesToRemove)
            {
                var tempQueue = new Queue<LogMessage>();
                while (_messages.Count > 0)
                {
                    var m = _messages.Dequeue();
                    if (m != msg)
                        tempQueue.Enqueue(m);
                }
                _messages = tempQueue;
            }
        }
        
        /// <summary>
        /// Draw log overlay
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
        {
            if (!_isVisible)
                return;
                
            // Always show something even if no messages yet
            if (_font == null)
            {
                // Font not loaded yet - skip drawing
                return;
            }
            
            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;
            
            // Position on left side, below UI panel (which is at top-left 10,10 with height ~130)
            float margin = 10f;
            float uiPanelHeight = 140f; // Space for UI panel at top
            float panelWidth = 600f; // Wider for log messages
            Vector2 panelPos = new Vector2(margin, margin + uiPanelHeight);
            
            // If no messages, show a placeholder
            if (_messages.Count == 0)
            {
                // Draw a small panel to indicate log is active
                float placeholderHeight = 40f;
                Vector2 placeholderSize = new Vector2(panelWidth, placeholderHeight);
                DrawPanel(spriteBatch, graphicsDevice, panelPos, placeholderSize, new Color(0, 0, 0, 220));
                Vector2 placeholderTextPos = panelPos + new Vector2(10, 10);
                DrawText(spriteBatch, "=== LOG (L to toggle) - Waiting for messages ===", placeholderTextPos, 0, Color.Yellow);
                return;
            }
            
            // Adjust panel height to account for UI panel space
            float availableHeight = screenHeight - (margin * 2) - uiPanelHeight;
            
            // Calculate panel height based on message count
            float lineHeight = _font.LineSpacing + 2;
            float panelHeight = Math.Min(_messages.Count * lineHeight + 20, availableHeight);
            Vector2 panelSize = new Vector2(panelWidth, panelHeight);
            
            // Draw semi-transparent background
            DrawPanel(spriteBatch, graphicsDevice, panelPos, panelSize, new Color(0, 0, 0, 200));
            
            // Draw messages (newest at top)
            Vector2 textPos = panelPos + new Vector2(10, 10);
            int lineIndex = 0;
            
            // Draw header
            DrawText(spriteBatch, "=== LOG (L to toggle) ===", textPos, lineIndex++, Color.Yellow);
            lineIndex++; // Blank line
            
            // Draw messages in reverse order (newest first)
            var messagesArray = _messages.ToArray();
            for (int i = messagesArray.Length - 1; i >= 0 && lineIndex < MAX_MESSAGES + 2; i--)
            {
                var msg = messagesArray[i];
                Color msgColor = GetLevelColor(msg.Level);
                
                // Fade out old messages
                float alpha = 1.0f - (msg.Age / MESSAGE_LIFETIME);
                alpha = MathHelper.Clamp(alpha, 0.3f, 1.0f);
                msgColor = new Color(msgColor.R, msgColor.G, msgColor.B, (byte)(msgColor.A * alpha));
                
                // Truncate long messages
                string displayText = msg.Text;
                if (displayText.Length > 80)
                {
                    displayText = displayText.Substring(0, 77) + "...";
                }
                
                DrawText(spriteBatch, displayText, textPos, lineIndex++, msgColor);
            }
        }
        
        private void DrawText(SpriteBatch spriteBatch, string text, Vector2 basePos, int lineIndex, Color color)
        {
            if (_font == null) return;
            
            float lineHeight = _font.LineSpacing + 2;
            Vector2 position = basePos + new Vector2(0, lineIndex * lineHeight);
            
            // Draw shadow for better readability
            spriteBatch.DrawString(_font, text, position + new Vector2(1, 1), Color.Black);
            spriteBatch.DrawString(_font, text, position, color);
        }
        
        private void DrawPanel(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, Vector2 position, Vector2 size, Color color)
        {
            // Use cached white texture
            if (_whiteTexture == null || _whiteTexture.IsDisposed)
            {
                _whiteTexture = new Texture2D(graphicsDevice, 1, 1);
                _whiteTexture.SetData(new[] { Color.White });
            }
            
            spriteBatch.Draw(_whiteTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
        }
        
        private Color GetLevelColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Error => Color.Red,
                LogLevel.Warning => Color.Orange,
                LogLevel.Info => Color.White,
                LogLevel.Debug => Color.LightGray,
                _ => Color.White
            };
        }
        
        private class LogMessage
        {
            public string Text { get; set; } = "";
            public LogLevel Level { get; set; }
            public DateTime Timestamp { get; set; }
            public float Age { get; set; }
        }
    }
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}

