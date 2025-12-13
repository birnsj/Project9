using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    /// <summary>
    /// Tracks and displays performance diagnostics
    /// </summary>
    public class DiagnosticsOverlay
    {
        private SpriteFont? _font;
        private bool _isVisible = false;
        private static Texture2D? _whiteTexture;
        
        // FPS tracking
        private Queue<float> _frameTimeHistory = new Queue<float>(60);
        private float _currentFps = 0.0f;
        private float _minFps = float.MaxValue;
        private float _maxFps = 0.0f;
        
        // Performance metrics
        private int _drawCallCount = 0;
        private float _entityUpdateTimeMs = 0.0f;
        private float _pathfindingTimeMs = 0.0f;
        private int _activePathfindingCount = 0;
        
        // Update timing
        private float _totalUpdateTimeMs = 0.0f;
        private float _inputUpdateTimeMs = 0.0f;
        private float _collisionUpdateTimeMs = 0.0f;
        
        // Memory tracking
        private long _memoryUsageMB = 0;
        private int _gcGen0 = 0;
        private int _gcGen1 = 0;
        private int _gcGen2 = 0;
        
        // GC allocation tracking
        private long _previousMemoryBytes = 0;
        private int _previousGen0 = 0;
        private int _previousGen1 = 0;
        private int _previousGen2 = 0;
        private long _allocationsThisFrame = 0;
        private float _allocationRateMBps = 0.0f; // MB per second
        private int _gcCollectionsThisFrame = 0;
        
        // Entity counts
        private int _totalEntities = 0;
        private int _movingEntities = 0;
        
        // Collision cache stats
        private int _cacheHits = 0;
        private int _cacheMisses = 0;
        private int _cacheSize = 0;
        private int _cacheHitRate = 0;
        
        public bool IsVisible => _isVisible;
        
        public void Initialize(SpriteFont font)
        {
            _font = font;
        }
        
        public void Toggle()
        {
            _isVisible = !_isVisible;
            Console.WriteLine($"[Diagnostics] Overlay {(_isVisible ? "enabled" : "disabled")}");
        }
        
        public void Show() => _isVisible = true;
        public void Hide() => _isVisible = false;
        
        /// <summary>
        /// Update FPS counter
        /// </summary>
        public void UpdateFPS(float deltaTime)
        {
            if (deltaTime > 0)
            {
                _frameTimeHistory.Enqueue(deltaTime);
                if (_frameTimeHistory.Count > 60)
                    _frameTimeHistory.Dequeue();
                
                float avgFrameTime = _frameTimeHistory.Average();
                _currentFps = 1.0f / avgFrameTime;
                _minFps = Math.Min(_minFps, _currentFps);
                _maxFps = Math.Max(_maxFps, _currentFps);
            }
        }
        
        /// <summary>
        /// Update performance metrics
        /// </summary>
        public void UpdateMetrics(
            int drawCallCount,
            float entityUpdateTimeMs,
            float pathfindingTimeMs,
            int activePathfindingCount,
            float totalUpdateTimeMs,
            float inputUpdateTimeMs,
            float collisionUpdateTimeMs,
            int totalEntities,
            int movingEntities,
            int cacheHits = 0,
            int cacheMisses = 0,
            int cacheSize = 0,
            int cacheHitRate = 0)
        {
            _drawCallCount = drawCallCount;
            _entityUpdateTimeMs = entityUpdateTimeMs;
            _pathfindingTimeMs = pathfindingTimeMs;
            _activePathfindingCount = activePathfindingCount;
            _totalUpdateTimeMs = totalUpdateTimeMs;
            _inputUpdateTimeMs = inputUpdateTimeMs;
            _collisionUpdateTimeMs = collisionUpdateTimeMs;
            _totalEntities = totalEntities;
            _movingEntities = movingEntities;
            _cacheHits = cacheHits;
            _cacheMisses = cacheMisses;
            _cacheSize = cacheSize;
            _cacheHitRate = cacheHitRate;
            
            // Update memory stats and track allocations
            long currentMemoryBytes = GC.GetTotalMemory(false);
            _memoryUsageMB = currentMemoryBytes / (1024 * 1024);
            
            int currentGen0 = GC.CollectionCount(0);
            int currentGen1 = GC.CollectionCount(1);
            int currentGen2 = GC.CollectionCount(2);
            
            // Calculate allocations this frame (only if no GC occurred)
            _gcCollectionsThisFrame = (currentGen0 - _previousGen0) + (currentGen1 - _previousGen1) + (currentGen2 - _previousGen2);
            
            if (_gcCollectionsThisFrame == 0 && _previousMemoryBytes > 0)
            {
                // No GC occurred, so we can track allocations
                _allocationsThisFrame = currentMemoryBytes - _previousMemoryBytes;
                if (_allocationsThisFrame < 0) _allocationsThisFrame = 0; // Can't have negative allocations
            }
            else
            {
                // GC occurred, can't accurately track allocations
                _allocationsThisFrame = 0;
            }
            
            _gcGen0 = currentGen0;
            _gcGen1 = currentGen1;
            _gcGen2 = currentGen2;
            
            _previousMemoryBytes = currentMemoryBytes;
            _previousGen0 = currentGen0;
            _previousGen1 = currentGen1;
            _previousGen2 = currentGen2;
        }
        
        /// <summary>
        /// Update allocation rate (call with deltaTime to calculate MB/s)
        /// </summary>
        public void UpdateAllocationRate(float deltaTime)
        {
            if (deltaTime > 0 && _allocationsThisFrame > 0)
            {
                float allocationsMB = _allocationsThisFrame / (1024.0f * 1024.0f);
                _allocationRateMBps = allocationsMB / deltaTime;
            }
            else
            {
                _allocationRateMBps = 0.0f;
            }
        }
        
        /// <summary>
        /// Reset min/max FPS
        /// </summary>
        public void ResetFPSStats()
        {
            _minFps = _currentFps;
            _maxFps = _currentFps;
        }
        
        /// <summary>
        /// Draw diagnostics overlay
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
        {
            if (!_isVisible || _font == null)
                return;
            
            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;
            
            // Position on right side with margin
            float margin = 10f;
            float panelWidth = 320f; // Fixed width
            Vector2 panelPos = new Vector2(screenWidth - panelWidth - margin, margin);
            
            // Calculate available height
            float availableHeight = screenHeight - (margin * 2);
            
            // Count total lines
            int totalLines = 32; // Count of all text lines including headers and blank lines
            
            // Calculate base line height and scale to fit
            float baseLineHeight = _font.LineSpacing + 2;
            float requiredHeight = (totalLines * baseLineHeight) + 20; // Add padding
            
            // Scale line height if content doesn't fit
            float lineHeight = baseLineHeight;
            float scale = 1.0f;
            if (requiredHeight > availableHeight)
            {
                scale = (availableHeight - 20) / (totalLines * baseLineHeight);
                lineHeight = baseLineHeight * scale;
            }
            
            // Store scale for DrawText to use
            _currentLineHeight = lineHeight;
            _currentTextScale = scale;
            
            float panelHeight = (totalLines * lineHeight) + 20;
            Vector2 panelSize = new Vector2(panelWidth, panelHeight);
            
            // Draw semi-transparent background
            DrawPanel(spriteBatch, graphicsDevice, panelPos, panelSize, new Color(0, 0, 0, 180));
            
            // Draw text with scaling
            Vector2 textPos = panelPos + new Vector2(10, 10);
            int lineIndex = 0;
            
            // Header
            DrawText(spriteBatch, "=== DIAGNOSTICS (F3) ===", textPos, lineIndex++, Color.Yellow);
            lineIndex++; // Blank line
            
            // FPS Section
            DrawText(spriteBatch, "--- PERFORMANCE ---", textPos, lineIndex++, Color.Cyan);
            DrawText(spriteBatch, $"FPS: {_currentFps:F1} ({_minFps:F0}-{_maxFps:F0})", textPos, lineIndex++, GetFPSColor(_currentFps));
            DrawText(spriteBatch, $"Frame Time: {(_frameTimeHistory.Any() ? _frameTimeHistory.Average() * 1000 : 0):F2}ms", textPos, lineIndex++, Color.White);
            DrawText(spriteBatch, $"Draw Calls: {_drawCallCount}", textPos, lineIndex++, Color.White);
            lineIndex++; // Blank line
            
            // Update Timing Section
            DrawText(spriteBatch, "--- UPDATE TIMING ---", textPos, lineIndex++, Color.Cyan);
            DrawText(spriteBatch, $"Total Update: {_totalUpdateTimeMs:F2}ms", textPos, lineIndex++, GetTimingColor(_totalUpdateTimeMs));
            DrawText(spriteBatch, $"  Input: {_inputUpdateTimeMs:F2}ms", textPos, lineIndex++, Color.LightGray);
            DrawText(spriteBatch, $"  Entities: {_entityUpdateTimeMs:F2}ms", textPos, lineIndex++, GetTimingColor(_entityUpdateTimeMs));
            DrawText(spriteBatch, $"  Collision: {_collisionUpdateTimeMs:F2}ms", textPos, lineIndex++, Color.LightGray);
            lineIndex++; // Blank line
            
            // Pathfinding Section
            DrawText(spriteBatch, "--- PATHFINDING ---", textPos, lineIndex++, Color.Cyan);
            DrawText(spriteBatch, $"Execution Time: {_pathfindingTimeMs:F2}ms", textPos, lineIndex++, GetTimingColor(_pathfindingTimeMs));
            DrawText(spriteBatch, $"Active Searches: {_activePathfindingCount}", textPos, lineIndex++, Color.White);
            lineIndex++; // Blank line
            
            // Collision Cache Section
            DrawText(spriteBatch, "--- COLLISION CACHE ---", textPos, lineIndex++, Color.Cyan);
            DrawText(spriteBatch, $"Hit Rate: {_cacheHitRate}%", textPos, lineIndex++, GetCacheHitRateColor(_cacheHitRate));
            DrawText(spriteBatch, $"Hits/Misses: {_cacheHits}/{_cacheMisses}", textPos, lineIndex++, Color.White);
            DrawText(spriteBatch, $"Cache Size: {_cacheSize} cells", textPos, lineIndex++, Color.White);
            lineIndex++; // Blank line
            
            // Entity Section
            DrawText(spriteBatch, "--- ENTITIES ---", textPos, lineIndex++, Color.Cyan);
            DrawText(spriteBatch, $"Total: {_totalEntities}", textPos, lineIndex++, Color.White);
            DrawText(spriteBatch, $"Moving: {_movingEntities}", textPos, lineIndex++, Color.White);
            lineIndex++; // Blank line
            
            // Memory Section
            DrawText(spriteBatch, "--- MEMORY ---", textPos, lineIndex++, Color.Cyan);
            DrawText(spriteBatch, $"Usage: {_memoryUsageMB} MB", textPos, lineIndex++, GetMemoryColor(_memoryUsageMB));
            DrawText(spriteBatch, $"Alloc Rate: {_allocationRateMBps:F2} MB/s", textPos, lineIndex++, GetAllocationRateColor(_allocationRateMBps));
            DrawText(spriteBatch, $"GC: Gen0={_gcGen0} Gen1={_gcGen1} Gen2={_gcGen2}", textPos, lineIndex++, Color.White);
            if (_gcCollectionsThisFrame > 0)
            {
                DrawText(spriteBatch, $"GC This Frame: {_gcCollectionsThisFrame}", textPos, lineIndex++, Color.Orange);
            }
            lineIndex++; // Blank line
            
            // Instructions
            DrawText(spriteBatch, "Press F3 to toggle", textPos, lineIndex++, Color.Gray);
            DrawText(spriteBatch, "Press R to reset FPS stats", textPos, lineIndex++, Color.Gray);
        }
        
        private float _currentLineHeight = 0f;
        private float _currentTextScale = 1.0f;
        
        private void DrawText(SpriteBatch spriteBatch, string text, Vector2 basePos, int lineIndex, Color color)
        {
            if (_font == null) return;
            
            Vector2 position = basePos + new Vector2(0, lineIndex * _currentLineHeight);
            
            // Draw shadow for better readability
            if (_currentTextScale != 1.0f)
            {
                // Scale text by drawing with scale
                spriteBatch.DrawString(_font, text, position + new Vector2(1, 1) * _currentTextScale, Color.Black, 0f, Vector2.Zero, _currentTextScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, text, position, color, 0f, Vector2.Zero, _currentTextScale, SpriteEffects.None, 0f);
            }
            else
            {
                spriteBatch.DrawString(_font, text, position + new Vector2(1, 1), Color.Black);
                spriteBatch.DrawString(_font, text, position, color);
            }
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
        
        private Color GetFPSColor(float fps)
        {
            if (fps >= 55) return Color.LimeGreen;
            if (fps >= 30) return Color.Yellow;
            return Color.Red;
        }
        
        private Color GetTimingColor(float timeMs)
        {
            if (timeMs < 5) return Color.LimeGreen;
            if (timeMs < 10) return Color.Yellow;
            return Color.Orange;
        }
        
        private Color GetMemoryColor(long memoryMB)
        {
            if (memoryMB < 256) return Color.LimeGreen;
            if (memoryMB < 512) return Color.Yellow;
            return Color.Orange;
        }
        
        private Color GetCacheHitRateColor(int hitRate)
        {
            if (hitRate >= 80) return Color.LimeGreen;
            if (hitRate >= 50) return Color.Yellow;
            if (hitRate >= 20) return Color.Orange;
            return Color.Red;
        }
        
        private Color GetAllocationRateColor(float rateMBps)
        {
            if (rateMBps < 1.0f) return Color.LimeGreen; // < 1 MB/s is good
            if (rateMBps < 5.0f) return Color.Yellow; // 1-5 MB/s is moderate
            if (rateMBps < 10.0f) return Color.Orange; // 5-10 MB/s is high
            return Color.Red; // > 10 MB/s is very high
        }
    }
}

