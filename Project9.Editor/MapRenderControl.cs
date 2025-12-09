using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Project9.Shared;

namespace Project9.Editor
{
    public class MapRenderControl : UserControl
    {
        private EditorCamera _camera;
        private EditorMapData _mapData;
        private TileTextureLoader _textureLoader;
        private TerrainType _selectedTerrainType;
        private readonly HashSet<Keys> _pressedKeys;
        private System.Windows.Forms.Timer _updateTimer;
        private DateTime _lastUpdateTime;
        private Point _mousePosition;
        private int? _hoverTileX;
        private int? _hoverTileY;
        private bool _isDragging;
        private EnemyData? _draggedEnemy;
        private bool _isDraggingPlayer;
        private PointF _dragOffset;
        private bool _showGrid64x32 = false;

        public bool ShowGrid64x32
        {
            get => _showGrid64x32;
            set
            {
                _showGrid64x32 = value;
                Invalidate();
            }
        }

        public TerrainType SelectedTerrainType
        {
            get => _selectedTerrainType;
            set
            {
                _selectedTerrainType = value;
                Invalidate();
            }
        }

        public EditorCamera Camera => _camera;
        public EditorMapData MapData => _mapData;

        public MapRenderControl()
        {
            _camera = new EditorCamera();
            _mapData = new EditorMapData();
            _textureLoader = new TileTextureLoader();
            _selectedTerrainType = TerrainType.Grass;
            _pressedKeys = new HashSet<Keys>();
            
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = 16; // ~60 FPS
            _updateTimer.Tick += UpdateTimer_Tick;
            _lastUpdateTime = DateTime.Now;
            _updateTimer.Start();

            this.MouseWheel += MapRenderControl_MouseWheel;
            this.MouseClick += MapRenderControl_MouseClick;
            this.MouseMove += MapRenderControl_MouseMove;
            this.MouseLeave += MapRenderControl_MouseLeave;
            this.MouseDown += MapRenderControl_MouseDown;
            this.MouseUp += MapRenderControl_MouseUp;
            this.KeyDown += MapRenderControl_KeyDown;
            this.KeyUp += MapRenderControl_KeyUp;
            this.SetStyle(ControlStyles.Selectable, true);
            this.TabStop = true;
            this.MouseEnter += MapRenderControl_MouseEnter;
            this.BackColor = Color.Black;
        }

        private void MapRenderControl_MouseEnter(object? sender, EventArgs e)
        {
            // Auto-focus when mouse enters to enable keyboard input
            if (!this.Focused)
            {
                this.Focus();
            }
        }

        public void Initialize(EditorMapData mapData, TileTextureLoader textureLoader)
        {
            _mapData = mapData;
            _textureLoader = textureLoader;
            
            // Center camera on map initially
            CenterCameraOnMap();
            
            Invalidate();
        }

        private void CenterCameraOnMap()
        {
            if (_mapData.Width == 0 || _mapData.Height == 0)
                return;

            // Calculate center of the map in screen coordinates
            float centerTileX = (_mapData.Width - 1) / 2.0f;
            float centerTileY = (_mapData.Height - 1) / 2.0f;
            var (centerScreenX, centerScreenY) = IsometricMath.TileToScreen((int)centerTileX, (int)centerTileY);
            
            // Offset by control center to properly center the view
            float screenCenterX = this.Width / 2.0f;
            float screenCenterY = this.Height / 2.0f;
            
            _camera.Position = new PointF(
                centerScreenX - screenCenterX / _camera.Zoom,
                centerScreenY - screenCenterY / _camera.Zoom
            );
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            DateTime currentTime = DateTime.Now;
            float deltaTime = (float)(currentTime - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = currentTime;

            // Handle WASD input for panning
            PointF panDirection = PointF.Empty;
            
            if (_pressedKeys.Contains(Keys.W))
                panDirection.Y -= 1;
            if (_pressedKeys.Contains(Keys.S))
                panDirection.Y += 1;
            if (_pressedKeys.Contains(Keys.A))
                panDirection.X -= 1;
            if (_pressedKeys.Contains(Keys.D))
                panDirection.X += 1;

            if (panDirection.X != 0 || panDirection.Y != 0)
            {
                _camera.Pan(panDirection, deltaTime);
                Invalidate();
            }
        }

        private void MapRenderControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W || e.KeyCode == Keys.A || e.KeyCode == Keys.S || e.KeyCode == Keys.D)
            {
                _pressedKeys.Add(e.KeyCode);
                e.Handled = true;
            }
        }

        private void MapRenderControl_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W || e.KeyCode == Keys.A || e.KeyCode == Keys.S || e.KeyCode == Keys.D)
            {
                _pressedKeys.Remove(e.KeyCode);
                e.Handled = true;
            }
        }

        private void MapRenderControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (this.Focused)
            {
                float zoomAmount = e.Delta > 0 ? 0.1f : -0.1f;
                if (zoomAmount > 0)
                    _camera.ZoomIn(zoomAmount);
                else
                    _camera.ZoomOut(Math.Abs(zoomAmount));
                Invalidate();
            }
        }

        private void MapRenderControl_MouseMove(object? sender, MouseEventArgs e)
        {
            _mousePosition = e.Location;
            
            if (_isDragging)
            {
                PointF worldPos = ScreenToWorld(e.Location);
                PointF targetPos = new PointF(worldPos.X - _dragOffset.X, worldPos.Y - _dragOffset.Y);
                
                // Snap to 64x32 grid
                targetPos = SnapToGrid(targetPos);
                
                if (_isDraggingPlayer && _mapData.MapData.Player != null)
                {
                    _mapData.MapData.Player.X = targetPos.X;
                    _mapData.MapData.Player.Y = targetPos.Y;
                    Invalidate();
                }
                else if (_draggedEnemy != null)
                {
                    _draggedEnemy.X = targetPos.X;
                    _draggedEnemy.Y = targetPos.Y;
                    Invalidate();
                }
            }
            else
            {
                UpdateHoveredTile(e.Location);
            }
        }

        private void MapRenderControl_MouseLeave(object? sender, EventArgs e)
        {
            _hoverTileX = null;
            _hoverTileY = null;
            Invalidate();
        }

        private void UpdateHoveredTile(Point screenPoint)
        {
            // Convert screen to world coordinates
            PointF worldPos = ScreenToWorld(screenPoint);
            
            // Get approximate tile first
            var (approxTileX, approxTileY) = IsometricMath.ScreenToTile(worldPos.X, worldPos.Y);
            
            // Check nearby tiles to find which one actually contains the point
            int? foundTileX = null;
            int? foundTileY = null;
            float minDistance = float.MaxValue;
            
            // Check tiles in a small area around the approximate position
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int testX = approxTileX + dx;
                    int testY = approxTileY + dy;
                    
                    if (testX >= 0 && testX < _mapData.Width && testY >= 0 && testY < _mapData.Height)
                    {
                        // Get tile screen position (top-left corner)
                        var (tileScreenX, tileScreenY) = IsometricMath.TileToScreen(testX, testY);
                        
                        // Check if point is within tile bounds (diamond shape)
                        // For isometric tiles, check if point is within the diamond
                        float halfWidth = IsometricMath.TileWidth / 2.0f;
                        float halfHeight = IsometricMath.TileHeight / 2.0f;
                        float centerX = tileScreenX + halfWidth;
                        float centerY = tileScreenY + halfHeight;
                        
                        // Distance from point to tile center
                        float dx2 = worldPos.X - centerX;
                        float dy2 = worldPos.Y - centerY;
                        float distance = (float)Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                        
                        // Check if point is inside diamond: |x-cx|/hw + |y-cy|/hh <= 1
                        float normalizedX = Math.Abs(dx2) / halfWidth;
                        float normalizedY = Math.Abs(dy2) / halfHeight;
                        
                        if (normalizedX + normalizedY <= 1.0f && distance < minDistance)
                        {
                            minDistance = distance;
                            foundTileX = testX;
                            foundTileY = testY;
                        }
                    }
                }
            }
            
            // Use found tile or approximate
            int tileX = foundTileX ?? approxTileX;
            int tileY = foundTileY ?? approxTileY;
            
            // Update hovered tile if changed
            if (tileX >= 0 && tileX < _mapData.Width && tileY >= 0 && tileY < _mapData.Height)
            {
                if (_hoverTileX != tileX || _hoverTileY != tileY)
                {
                    _hoverTileX = tileX;
                    _hoverTileY = tileY;
                    Invalidate();
                }
            }
            else
            {
                if (_hoverTileX.HasValue || _hoverTileY.HasValue)
                {
                    _hoverTileX = null;
                    _hoverTileY = null;
                    Invalidate();
                }
            }
        }

        private PointF SnapToGrid(PointF position)
        {
            const float gridX = 64.0f;
            const float gridY = 32.0f;
            float snappedX = (float)(Math.Round(position.X / gridX) * gridX);
            float snappedY = (float)(Math.Round(position.Y / gridY) * gridY);
            return new PointF(snappedX, snappedY);
        }

        private void MapRenderControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointF worldPos = ScreenToWorld(e.Location);
                
                // Check if clicking on player
                if (_mapData.MapData.Player != null)
                {
                    float playerScreenX = _mapData.MapData.Player.X;
                    float playerScreenY = _mapData.MapData.Player.Y;
                    float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - playerScreenX, 2) + Math.Pow(worldPos.Y - playerScreenY, 2));
                    if (distance < 50) // Click radius
                    {
                        _isDraggingPlayer = true;
                        _isDragging = true;
                        _dragOffset = new PointF(worldPos.X - playerScreenX, worldPos.Y - playerScreenY);
                        Invalidate();
                        return;
                    }
                }
                
                // Check if clicking on any enemy
                foreach (var enemy in _mapData.MapData.Enemies)
                {
                    float enemyScreenX = enemy.X;
                    float enemyScreenY = enemy.Y;
                    float distance = (float)Math.Sqrt(Math.Pow(worldPos.X - enemyScreenX, 2) + Math.Pow(worldPos.Y - enemyScreenY, 2));
                    if (distance < 50) // Click radius
                    {
                        _draggedEnemy = enemy;
                        _isDragging = true;
                        _dragOffset = new PointF(worldPos.X - enemyScreenX, worldPos.Y - enemyScreenY);
                        Invalidate();
                        return;
                    }
                }
                
                // If not dragging, treat as tile click
                if (!_isDragging)
                {
                    // Use the hovered tile coordinates if available (matches the preview)
                    if (_hoverTileX.HasValue && _hoverTileY.HasValue)
                    {
                        _mapData.SetTile(_hoverTileX.Value, _hoverTileY.Value, _selectedTerrainType);
                        Invalidate();
                    }
                    else
                    {
                        // Fallback: calculate from mouse position
                        var (tileX, tileY) = IsometricMath.ScreenToTile(worldPos.X, worldPos.Y);
                        
                        if (tileX >= 0 && tileX < _mapData.Width && tileY >= 0 && tileY < _mapData.Height)
                        {
                            _mapData.SetTile(tileX, tileY, _selectedTerrainType);
                            Invalidate();
                        }
                    }
                }
            }
        }

        private void MapRenderControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
                _isDraggingPlayer = false;
                _draggedEnemy = null;
                Invalidate();
            }
        }

        private void MapRenderControl_MouseClick(object? sender, MouseEventArgs e)
        {
            // Click handling is now in MouseDown
        }

        private void DrawHoverPreview(Graphics g)
        {
            if (_hoverTileX == null || _hoverTileY == null)
                return;

            // Use the same drawing method as regular tiles
            var (screenX, screenY) = IsometricMath.TileToScreen(_hoverTileX.Value, _hoverTileY.Value);
            Bitmap? texture = _textureLoader.GetTexture(_selectedTerrainType);
            
            if (texture != null)
            {
                // Draw semi-transparent preview (same position as regular tiles)
                using (System.Drawing.Imaging.ImageAttributes imageAttributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                    {
                        new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 0.5f, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });
                    imageAttributes.SetColorMatrix(colorMatrix);
                    
                    g.DrawImage(
                        texture,
                        new Rectangle((int)screenX, (int)screenY, IsometricMath.TileWidth, IsometricMath.TileHeight),
                        0, 0, texture.Width, texture.Height,
                        System.Drawing.GraphicsUnit.Pixel,
                        imageAttributes);
                }
            }
        }

        private PointF ScreenToWorld(Point screenPoint)
        {
            // Apply inverse camera transform
            // Transform is: Translate(-pos) * Scale(zoom)
            // Inverse is: Scale(1/zoom) * Translate(pos)
            // So: world = (screen + pos) / zoom
            float worldX = (screenPoint.X + _camera.Position.X) / _camera.Zoom;
            float worldY = (screenPoint.Y + _camera.Position.Y) / _camera.Zoom;
            return new PointF(worldX, worldY);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            // Apply camera transform
            Matrix originalTransform = g.Transform;
            g.Transform = _camera.GetTransformMatrix();

            // Get all tiles and sort them for proper rendering order (back to front)
            var tiles = new List<(int x, int y, TerrainType type)>();
            for (int x = 0; x < _mapData.Width; x++)
            {
                for (int y = 0; y < _mapData.Height; y++)
                {
                    var tile = _mapData.GetTile(x, y);
                    if (tile != null)
                    {
                        tiles.Add((x, y, tile.TerrainType));
                    }
                }
            }

            // Sort by screen Y position to ensure correct depth
            tiles = tiles.OrderBy(t =>
            {
                var (screenX, screenY) = IsometricMath.TileToScreen(t.x, t.y);
                return screenY;
            }).ThenBy(t =>
            {
                var (screenX, screenY) = IsometricMath.TileToScreen(t.x, t.y);
                return screenX;
            }).ToList();

            // Draw tiles
            foreach (var tile in tiles)
            {
                var (screenX, screenY) = IsometricMath.TileToScreen(tile.x, tile.y);
                Bitmap? texture = _textureLoader.GetTexture(tile.type);
                
                if (texture != null)
                {
                    g.DrawImage(texture, screenX, screenY, IsometricMath.TileWidth, IsometricMath.TileHeight);
                }
            }

            // Draw hover preview
            if (_hoverTileX.HasValue && _hoverTileY.HasValue && !_isDragging)
            {
                DrawHoverPreview(g);
            }

            // Draw enemies
            foreach (var enemy in _mapData.MapData.Enemies)
            {
                DrawEnemy(g, enemy, enemy == _draggedEnemy);
            }

            // Draw player
            if (_mapData.MapData.Player != null)
            {
                DrawPlayer(g, _mapData.MapData.Player, _isDraggingPlayer);
            }

            // Draw 64x32 grid if enabled
            if (_showGrid64x32)
            {
                DrawGrid64x32(g);
            }

            // Restore original transform
            g.Transform = originalTransform;
        }

        private void DrawGrid64x32(Graphics g)
        {
            const float gridX = 64.0f;
            
            // Use a darker, less intense color (semi-transparent dark gray)
            using (Pen gridPen = new Pen(Color.FromArgb(100, 80, 80, 80), 1))
            {
                // Calculate visible area in world coordinates (already transformed by camera)
                PointF topLeft = ScreenToWorld(new Point(0, 0));
                PointF bottomRight = ScreenToWorld(new Point(this.Width, this.Height));
                
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
                
                // Grid cells per tile: 1024/64 = 16 cells horizontally, 512/32 = 16 cells vertically
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
                        // Start from bottom of visible tiles
                        var (startX, startY) = IsometricMath.TileToScreen(tileX, minTileY);
                        startX += offsetX;
                        startY += offsetY;
                        
                        // End at top of visible tiles
                        var (endX, endY) = IsometricMath.TileToScreen(tileX, maxTileY);
                        endX += offsetX;
                        endY += offsetY;
                        
                        // Clip to visible bounds
                        if ((startY >= minY && startY <= maxY) || (endY >= minY && endY <= maxY) ||
                            (startY < minY && endY > maxY) || (startY > maxY && endY < minY))
                        {
                            g.DrawLine(gridPen, startX, startY, endX, endY);
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
                            g.DrawLine(gridPen, startX, startY, endX, endY);
                        }
                    }
                }
            }
        }

        private void DrawEnemy(Graphics g, EnemyData enemy, bool isDragging)
        {
            float centerX = enemy.X;
            float centerY = enemy.Y;
            
            // Isometric diamond dimensions (scaled down from tile size)
            float halfWidth = 32.0f;  // Half width of the isometric box
            float halfHeight = 16.0f; // Half height of the isometric box
            
            // Define the 4 points of the isometric diamond
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };
            
            // Draw filled isometric diamond
            using (SolidBrush brush = new SolidBrush(isDragging ? Color.Orange : Color.DarkRed))
            {
                g.FillPolygon(brush, diamondPoints);
            }
            
            // Draw outline
            using (Pen pen = new Pen(Color.White, 2))
            {
                g.DrawPolygon(pen, diamondPoints);
            }
        }

        private void DrawPlayer(Graphics g, PlayerData player, bool isDragging)
        {
            float centerX = player.X;
            float centerY = player.Y;
            
            // Isometric diamond dimensions (slightly larger than enemy)
            float halfWidth = 40.0f;  // Half width of the isometric box
            float halfHeight = 20.0f; // Half height of the isometric box
            
            // Define the 4 points of the isometric diamond
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };
            
            // Draw filled isometric diamond
            using (SolidBrush brush = new SolidBrush(isDragging ? Color.Yellow : Color.Red))
            {
                g.FillPolygon(brush, diamondPoints);
            }
            
            // Draw outline
            using (Pen pen = new Pen(Color.White, 2))
            {
                g.DrawPolygon(pen, diamondPoints);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _textureLoader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

