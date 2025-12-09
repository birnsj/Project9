using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.IO;
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
        private bool _collisionMode = false;
        private List<CollisionCellData> _collisionCells = new List<CollisionCellData>();
        private PointF? _collisionHoverPosition = null; // Snapped grid position for collision hover preview
        private float _tileOpacity = 0.7f; // Default opacity for placed tiles (0.0 to 1.0)

        public float TileOpacity
        {
            get => _tileOpacity;
            set
            {
                _tileOpacity = Math.Clamp(value, 0.0f, 1.0f);
                Invalidate();
            }
        }

        public bool ShowGrid64x32
        {
            get => _showGrid64x32;
            set
            {
                _showGrid64x32 = value;
                Invalidate();
            }
        }

        public bool CollisionMode
        {
            get => _collisionMode;
            set
            {
                _collisionMode = value;
                if (!value)
                {
                    // Clear collision hover when disabling collision mode
                    _collisionHoverPosition = null;
                }
                Invalidate();
            }
        }

        public List<CollisionCellData> CollisionCells => _collisionCells;

        public void ClearAllCollisionCells()
        {
            _collisionCells.Clear();
            SaveCollisionCells();
            Invalidate();
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
            
            // Load collision cells
            LoadCollisionCells();
            
            // Snap all enemies to grid
            SnapAllEnemiesToGrid();
            
            // Center camera on map initially
            CenterCameraOnMap();
            
            Invalidate();
        }

        private void SnapAllEnemiesToGrid()
        {
            foreach (var enemy in _mapData.MapData.Enemies)
            {
                var snappedPos = SnapToGrid(new PointF(enemy.X, enemy.Y));
                enemy.X = snappedPos.X;
                enemy.Y = snappedPos.Y;
            }
        }

        private void LoadCollisionCells()
        {
            string collisionPath = "Content/world/collision.json";
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
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MapRenderControl] Error loading collision cells: {ex.Message}");
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

        private void SaveCollisionCells()
        {
            string collisionPath = "Content/world/collision.json";
            string? resolvedPath = ResolveCollisionPath(collisionPath);
            
            if (resolvedPath == null)
            {
                // Try to create the directory if it doesn't exist
                string dir = Path.GetDirectoryName(collisionPath) ?? "Content/world";
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                resolvedPath = Path.GetFullPath(collisionPath);
            }

            try
            {
                var collisionData = new CollisionData { Cells = _collisionCells };
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string json = System.Text.Json.JsonSerializer.Serialize(collisionData, options);
                File.WriteAllText(resolvedPath, json);
                Console.WriteLine($"[MapRenderControl] Saved collision cells to {resolvedPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MapRenderControl] Error saving collision cells: {ex.Message}");
            }
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
                // Get mouse position in screen coordinates
                Point mouseScreen = e.Location;
                
                // Convert to world coordinates before zoom
                PointF mouseWorldBefore = ScreenToWorld(mouseScreen);
                
                // Calculate zoom amount (use a percentage-based zoom for smoother feel)
                float zoomFactor = e.Delta > 0 ? 1.1f : 1.0f / 1.1f;
                float oldZoom = _camera.Zoom;
                float newZoom = Math.Clamp(oldZoom * zoomFactor, 0.5f, 4.0f);
                
                // Only apply if zoom actually changed (within limits)
                if (Math.Abs(newZoom - oldZoom) > 0.001f)
                {
                    _camera.Zoom = newZoom;
                    
                    // Convert mouse position to world coordinates after zoom
                    PointF mouseWorldAfter = ScreenToWorld(mouseScreen);
                    
                    // Adjust camera position to keep the mouse point in the same world position
                    PointF worldOffset = new PointF(
                        mouseWorldBefore.X - mouseWorldAfter.X,
                        mouseWorldBefore.Y - mouseWorldAfter.Y
                    );
                    
                    _camera.Position = new PointF(
                        _camera.Position.X + worldOffset.X * _camera.Zoom,
                        _camera.Position.Y + worldOffset.Y * _camera.Zoom
                    );
                    
                    Invalidate();
                }
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
            else if (_collisionMode)
            {
                // Update collision hover preview position (snapped to grid)
                PointF worldPos = ScreenToWorld(e.Location);
                PointF snappedPos = SnapToGrid(worldPos);
                
                if (!_collisionHoverPosition.HasValue || 
                    Math.Abs(_collisionHoverPosition.Value.X - snappedPos.X) > 0.1f || 
                    Math.Abs(_collisionHoverPosition.Value.Y - snappedPos.Y) > 0.1f)
                {
                    _collisionHoverPosition = snappedPos;
                    Invalidate();
                }
                
                // Clear tile hover when in collision mode
                if (_hoverTileX.HasValue || _hoverTileY.HasValue)
                {
                    _hoverTileX = null;
                    _hoverTileY = null;
                }
            }
            else
            {
                // Clear collision hover when not in collision mode
                if (_collisionHoverPosition.HasValue)
                {
                    _collisionHoverPosition = null;
                    Invalidate();
                }
                
                // Update tile hover for normal tile editing
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
            
            // Find the tile whose bottom center is closest to the cursor
            // The bottom center of an isometric diamond is at (screenX, screenY + TileHeight)
            int? foundTileX = null;
            int? foundTileY = null;
            float minDistance = float.MaxValue;
            
            // Check tiles in a small area around the approximate position
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int testX = approxTileX + dx;
                    int testY = approxTileY + dy;
                    
                    if (testX >= 0 && testX < _mapData.Width && testY >= 0 && testY < _mapData.Height)
                    {
                        // Get tile coordinate point (grid point)
                        var (tileScreenX, tileScreenY) = IsometricMath.TileToScreen(testX, testY);
                        
                        // The grid point is where we want the bottom center to align
                        // Distance from cursor to this tile's grid point (where bottom center will be)
                        float dx2 = worldPos.X - tileScreenX;
                        float dy2 = worldPos.Y - tileScreenY;
                        float distance = (float)Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                        
                        if (distance < minDistance)
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
            const float halfHeight = 16.0f; // Half height of 64x32 diamond (32/2)
            
            // Grid cells per tile: 1024/64 = 16 cells
            const int gridCellsPerTile = (int)(IsometricMath.TileWidth / gridX);
            
            // Find the nearest grid cell bottom corner by checking nearby positions
            float minDistance = float.MaxValue;
            PointF nearestCellBottomCorner = position;
            
            // Check nearby tiles
            var (tileX, tileY) = IsometricMath.ScreenToTile(position.X, position.Y);
            
            for (int dtX = -1; dtX <= 1; dtX++)
            {
                for (int dtY = -1; dtY <= 1; dtY++)
                {
                    var (tileScreenX, tileScreenY) = IsometricMath.TileToScreen(tileX + dtX, tileY + dtY);
                    
                    // Check all grid cells in this tile
                    for (int gridCellX = 0; gridCellX < gridCellsPerTile; gridCellX++)
                    {
                        for (int gridCellY = 0; gridCellY < gridCellsPerTile; gridCellY++)
                        {
                            // Calculate grid cell center position
                            float progressX = (gridCellX + 0.5f) / gridCellsPerTile;
                            float progressY = (gridCellY + 0.5f) / gridCellsPerTile;
                            
                            float cellOffsetX = (progressX - progressY) * (IsometricMath.TileWidth / 2.0f);
                            float cellOffsetY = (progressX + progressY) * (IsometricMath.TileHeight / 2.0f);
                            
                            float cellCenterX = tileScreenX + cellOffsetX;
                            float cellCenterY = tileScreenY + cellOffsetY;
                            
                            // Bottom corner of grid cell is at the bottom point of the diamond
                            // Bottom point: (centerX, centerY + halfHeight)
                            float cellBottomX = cellCenterX;
                            float cellBottomY = cellCenterY + halfHeight;
                            
                            float distance = (float)Math.Sqrt(Math.Pow(position.X - cellBottomX, 2) + Math.Pow(position.Y - cellBottomY, 2));
                            
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                nearestCellBottomCorner = new PointF(cellBottomX, cellBottomY);
                            }
                        }
                    }
                }
            }
            
            // Return position where entity center should be so its bottom point is at the grid cell bottom corner
            // Entity bottom point is at (centerX, centerY + halfHeight)
            // We want: entityCenterY + halfHeight = gridCellBottomY
            // So: entityCenterY = gridCellBottomY - halfHeight
            return new PointF(nearestCellBottomCorner.X, nearestCellBottomCorner.Y - halfHeight);
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
                
                // If not dragging, handle clicks
                if (!_isDragging)
                {
                    if (_collisionMode)
                    {
                        // Left click: Place collision cell
                        PointF snappedPos = SnapToGrid(worldPos);
                        
                        // Check if there's already a collision cell at this position
                        var existingCell = _collisionCells.FirstOrDefault(c => 
                            Math.Abs(c.X - snappedPos.X) < 1 && Math.Abs(c.Y - snappedPos.Y) < 1);
                        
                        if (existingCell == null)
                        {
                            // Add collision cell
                            _collisionCells.Add(new CollisionCellData { X = snappedPos.X, Y = snappedPos.Y });
                            SaveCollisionCells();
                            Invalidate();
                        }
                    }
                    else
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
            else if (e.Button == MouseButtons.Right)
            {
                // Right click: Delete collision cell (only in collision mode)
                if (_collisionMode)
                {
                    PointF worldPos = ScreenToWorld(e.Location);
                    PointF snappedPos = SnapToGrid(worldPos);
                    
                    // Find and remove collision cell at this position
                    var existingCell = _collisionCells.FirstOrDefault(c => 
                        Math.Abs(c.X - snappedPos.X) < 1 && Math.Abs(c.Y - snappedPos.Y) < 1);
                    
                    if (existingCell != null)
                    {
                        _collisionCells.Remove(existingCell);
                        SaveCollisionCells();
                        Invalidate();
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
                    
                    // All tiles place at grid corner point
                    // screenX, screenY is the grid corner (top of isometric diamond)
                    // Draw tiles at their natural position - top of diamond at grid corner
                    
                    if (_selectedTerrainType == TerrainType.Test)
                    {
                        // Test tiles: 1024x1024, but bottom 1024x512 is the diamond, top 512 is overdraw
                        // Grid corner (screenX, screenY) should align with bottom of diamond
                        // TileToScreen returns the top point, which is centered horizontally
                        // Offset upward by TileHeight + overdraw to align bottom diamond with grid corner
                        float overdrawHeight = texture.Height - IsometricMath.TileHeight; // 512 for Test tile
                        float totalOffset = IsometricMath.TileHeight + overdrawHeight; // 512 + 512 = 1024
                        
                        // Center horizontally: TileToScreen returns center, so offset by half width
                        float drawX = screenX - (texture.Width / 2.0f);
                        float drawY = screenY - totalOffset; // Move up to align bottom diamond
                        
                        // Round to nearest pixel for perfect alignment
                        int finalDrawX = (int)Math.Round(drawX);
                        int finalDrawY = (int)Math.Round(drawY);
                        
                        g.DrawImage(
                            texture,
                            new Rectangle(finalDrawX, finalDrawY, texture.Width, texture.Height),
                            0, 0, texture.Width, texture.Height,
                            System.Drawing.GraphicsUnit.Pixel,
                            imageAttributes);
                    }
                    else
                    {
                        // Regular tiles: align bottom of diamond with grid corner
                        // Grid corner (screenX, screenY) should align with bottom of diamond
                        // TileToScreen returns the top point, which is centered horizontally
                        // For 1024-wide texture, offset by -512 to get left edge
                        // For standard 1024x512 tiles: offset upward by TileHeight to align bottom
                        // For tiles with overdraw (taller than TileHeight), offset by overdraw amount
                        float overdrawHeight = texture.Height > IsometricMath.TileHeight 
                            ? (texture.Height - IsometricMath.TileHeight) 
                            : 0;
                        float totalOffset = IsometricMath.TileHeight + overdrawHeight;
                        
                        // Center horizontally: TileToScreen returns center, so offset by half width
                        float drawX = screenX - (texture.Width / 2.0f);
                        float drawY = screenY - totalOffset; // Move up to align bottom
                        
                        // Round to nearest pixel for perfect alignment
                        int finalDrawX = (int)Math.Round(drawX);
                        int finalDrawY = (int)Math.Round(drawY);
                        
                        g.DrawImage(
                            texture,
                            new Rectangle(finalDrawX, finalDrawY, texture.Width, texture.Height),
                            0, 0, texture.Width, texture.Height,
                            System.Drawing.GraphicsUnit.Pixel,
                            imageAttributes);
                    }
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
            g.PixelOffsetMode = PixelOffsetMode.None; // Changed to None for pixel-perfect alignment

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
                    // All tiles place at grid corner point
                    // screenX, screenY is the grid corner (top of isometric diamond)
                    // Draw tiles at their natural position - top of diamond at grid corner
                    
                    if (tile.type == TerrainType.Test)
                    {
                        using (System.Drawing.Imaging.ImageAttributes imageAttributes = new System.Drawing.Imaging.ImageAttributes())
                        {
                            System.Drawing.Imaging.ColorMatrix colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                            {
                                new float[] {1, 0, 0, 0, 0},
                                new float[] {0, 1, 0, 0, 0},
                                new float[] {0, 0, 1, 0, 0},
                                new float[] {0, 0, 0, _tileOpacity, 0}, // Use configurable opacity
                                new float[] {0, 0, 0, 0, 1}
                            });
                            imageAttributes.SetColorMatrix(colorMatrix);
                            
                            // Test tiles: 1024x1024, but bottom 1024x512 is the diamond, top 512 is overdraw
                            // Grid corner (screenX, screenY) should align with bottom of diamond
                            // TileToScreen returns the top point, which is centered horizontally
                            // Offset upward by TileHeight + overdraw to align bottom diamond with grid corner
                            float overdrawHeight = texture.Height - IsometricMath.TileHeight; // 512 for Test tile
                            float totalOffset = IsometricMath.TileHeight + overdrawHeight; // 512 + 512 = 1024
                            
                            // Center horizontally: TileToScreen returns center, so offset by half width
                            float drawX = screenX - (texture.Width / 2.0f);
                            float drawY = screenY - totalOffset; // Move up to align bottom diamond
                            
                            // Round to nearest pixel for perfect alignment
                            int finalDrawX = (int)Math.Round(drawX);
                            int finalDrawY = (int)Math.Round(drawY);
                            
                            g.DrawImage(
                                texture,
                                new Rectangle(finalDrawX, finalDrawY, texture.Width, texture.Height),
                                0, 0, texture.Width, texture.Height,
                                System.Drawing.GraphicsUnit.Pixel,
                                imageAttributes);
                        }
                    }
                    else
                    {
                        // Regular tiles: align bottom of diamond with grid corner
                        // Grid corner (screenX, screenY) should align with bottom of diamond
                        // TileToScreen returns the top point, which is centered horizontally
                        // For 1024-wide texture, offset by -512 to get left edge
                        // For standard 1024x512 tiles: offset upward by TileHeight to align bottom
                        // For tiles with overdraw (taller than TileHeight), add overdraw offset
                        float overdrawHeight = texture.Height > IsometricMath.TileHeight 
                            ? (texture.Height - IsometricMath.TileHeight) 
                            : 0;
                        float totalOffset = IsometricMath.TileHeight + overdrawHeight;
                        
                        // Center horizontally: TileToScreen returns center, so offset by half width
                        float drawX = screenX - (texture.Width / 2.0f);
                        float drawY = screenY - totalOffset; // Move up to align bottom
                        
                        // Round to nearest pixel for perfect alignment
                        int finalDrawX = (int)Math.Round(drawX);
                        int finalDrawY = (int)Math.Round(drawY);
                        g.DrawImage(texture, finalDrawX, finalDrawY, texture.Width, texture.Height);
                    }
                }
            }

            // Draw hover preview (only if not in collision mode)
            if (!_collisionMode && _hoverTileX.HasValue && _hoverTileY.HasValue && !_isDragging)
            {
                DrawHoverPreview(g);
            }

            // Draw enemies
            for (int i = 0; i < _mapData.MapData.Enemies.Count; i++)
            {
                var enemy = _mapData.MapData.Enemies[i];
                DrawEnemy(g, enemy, enemy == _draggedEnemy, i);
            }

            // Draw player
            if (_mapData.MapData.Player != null)
            {
                DrawPlayer(g, _mapData.MapData.Player, _isDraggingPlayer);
            }
            else
            {
                // Debug: log if player is null
                Console.WriteLine("[MapRenderControl] Player is null, not drawing");
            }

            // Draw 64x32 grid if enabled
            if (_showGrid64x32)
            {
                DrawGrid64x32(g);
            }

            // Draw collision cells
            DrawCollisionCells(g);
            
            // Draw collision hover preview (64x32 diamond cursor)
            if (_collisionMode && _collisionHoverPosition.HasValue && !_isDragging)
            {
                DrawCollisionHoverPreview(g, _collisionHoverPosition.Value);
            }

            // Restore original transform
            g.Transform = originalTransform;
        }

        private void DrawCollisionCells(Graphics g)
        {
            const float halfWidth = 32.0f;  // 64/2 = 32
            const float halfHeight = 16.0f; // 32/2 = 16

            foreach (var cell in _collisionCells)
            {
                float centerX = cell.X;
                float centerY = cell.Y;

                // Define the 4 points of the isometric diamond (64x32 size)
                PointF[] diamondPoints = new PointF[]
                {
                    new PointF(centerX, centerY - halfHeight),                    // Top
                    new PointF(centerX + halfWidth, centerY),                     // Right
                    new PointF(centerX, centerY + halfHeight),                    // Bottom
                    new PointF(centerX - halfWidth, centerY)                      // Left
                };

                // Draw filled isometric diamond in purple
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(180, 128, 0, 128))) // Purple with transparency
                {
                    g.FillPolygon(brush, diamondPoints);
                }

                // Draw outline
                using (Pen pen = new Pen(Color.Purple, 2))
                {
                    g.DrawPolygon(pen, diamondPoints);
                }
            }
        }

        private void DrawCollisionHoverPreview(Graphics g, PointF position)
        {
            const float halfWidth = 32.0f;  // 64/2 = 32
            const float halfHeight = 16.0f; // 32/2 = 16

            float centerX = position.X;
            float centerY = position.Y;

            // Define the 4 points of the isometric diamond (64x32 size)
            PointF[] diamondPoints = new PointF[]
            {
                new PointF(centerX, centerY - halfHeight),                    // Top
                new PointF(centerX + halfWidth, centerY),                     // Right
                new PointF(centerX, centerY + halfHeight),                    // Bottom
                new PointF(centerX - halfWidth, centerY)                      // Left
            };

            // Check if there's already a collision cell at this position
            bool cellExists = _collisionCells.Any(c => 
                Math.Abs(c.X - centerX) < 1 && Math.Abs(c.Y - centerY) < 1);

            // Draw filled isometric diamond preview (lighter/more transparent for hover)
            if (cellExists)
            {
                // Red tint if cell already exists (will be deleted on click)
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 255, 100, 100))) // Light red with transparency
                {
                    g.FillPolygon(brush, diamondPoints);
                }
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    g.DrawPolygon(pen, diamondPoints);
                }
            }
            else
            {
                // Light purple for placement preview
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 200, 100, 200))) // Light purple with transparency
                {
                    g.FillPolygon(brush, diamondPoints);
                }
                using (Pen pen = new Pen(Color.Purple, 2))
                {
                    g.DrawPolygon(pen, diamondPoints);
                }
            }
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

        private void DrawEnemy(Graphics g, EnemyData enemy, bool isDragging, int index)
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
            
            // Draw label above the enemy
            string label = $"Enemy {index}";
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                SizeF textSize = g.MeasureString(label, font);
                
                // Position label above the enemy (at the top of the diamond)
                float labelX = centerX - textSize.Width / 2.0f;
                float labelY = centerY - halfHeight - textSize.Height - 4; // 4 pixels above the diamond
                
                // Draw background rectangle for better visibility
                RectangleF backgroundRect = new RectangleF(
                    labelX - 2,
                    labelY - 1,
                    textSize.Width + 4,
                    textSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, backgroundRect);
                
                // Draw text
                g.DrawString(label, font, textBrush, labelX, labelY);
            }
        }

        private void DrawPlayer(Graphics g, PlayerData player, bool isDragging)
        {
            float centerX = player.X;
            float centerY = player.Y;
            
            // Isometric diamond dimensions (64x32, same as enemies)
            float halfWidth = 32.0f;  // Half width of the isometric box (64/2)
            float halfHeight = 16.0f; // Half height of the isometric box (32/2)
            
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
            
            // Draw label above the player
            string label = "Player";
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                SizeF textSize = g.MeasureString(label, font);
                
                // Position label above the player (at the top of the diamond)
                float labelX = centerX - textSize.Width / 2.0f;
                float labelY = centerY - halfHeight - textSize.Height - 4; // 4 pixels above the diamond
                
                // Draw background rectangle for better visibility
                RectangleF backgroundRect = new RectangleF(
                    labelX - 2,
                    labelY - 1,
                    textSize.Width + 4,
                    textSize.Height + 2
                );
                g.FillRectangle(backgroundBrush, backgroundRect);
                
                // Draw text
                g.DrawString(label, font, textBrush, labelX, labelY);
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

