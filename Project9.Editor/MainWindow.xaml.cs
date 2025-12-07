using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Windows.System;
using Project9.Shared;

namespace Project9.Editor;

public sealed partial class MainWindow : Window
{
    private EditorCamera _camera = null!;
    private MapRenderer _mapRenderer = null!;
    private bool _isDragging = false;
    private Vector2 _lastMousePosition;
    private Vector2 _cursorPosition = Vector2.Zero;
    private DispatcherTimer? _updateTimer;
    private HashSet<VirtualKey> _pressedKeys = new();
    private TerrainType? _selectedTerrainType = null;
    private string? _currentMapPath = null;

    private CanvasControl? MapCanvas;

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "Map Editor";
        
        // Set window size
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        
        _camera = new EditorCamera();
        _mapRenderer = new MapRenderer();
        
        // Create CanvasControl programmatically to avoid XAML compiler issues
        MapCanvas = new CanvasControl
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkSlateGray),
            IsTabStop = true
        };
        MapCanvas.Draw += MapCanvas_Draw;
        MapCanvas.PointerWheelChanged += MapCanvas_PointerWheelChanged;
        MapCanvas.PointerPressed += MapCanvas_PointerPressed;
        MapCanvas.PointerMoved += MapCanvas_PointerMoved;
        MapCanvas.PointerReleased += MapCanvas_PointerReleased;
        MapCanvas.KeyDown += MapCanvas_KeyDown;
        MapCanvas.KeyUp += MapCanvas_KeyUp;
        MapCanvas.Loaded += MapCanvas_Loaded;
        
        // Add canvas to row 1, column 1 (below menu bar, in map area)
        Grid.SetRow(MapCanvas, 1);
        Grid.SetColumn(MapCanvas, 1);
        RootGrid.Children.Insert(0, MapCanvas);
        
        // Add keyboard handlers to the root grid as backup
        RootGrid.KeyDown += RootGrid_KeyDown;
        RootGrid.KeyUp += RootGrid_KeyUp;
        
        // Clear pressed keys when window loses focus or becomes hidden
        this.VisibilityChanged += MainWindow_VisibilityChanged;
        
        // Setup menu bar
        SetupMenuBar();
        
        this.Activated += MainWindow_Activated;
    }
    
    private void SetupTileBrowser()
    {
        if (!_mapRenderer.IsLoaded)
            return;
            
        TileBrowserPanel.Children.Clear();
        
        // Add title
        var title = new TextBlock
        {
            Text = "Tile Browser",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        TileBrowserPanel.Children.Add(title);
        
        // Get tile bitmaps from renderer
        var tileBitmaps = _mapRenderer.GetTileBitmaps();
        
        // Calculate proper scale for tiles (isometric tiles are 1024x512)
        // Browser width is ~180px (200px - margins), so scale to fit
        const float availableWidth = 170f; // Account for padding and margins
        const float availableHeight = 110f; // Height for tile preview area
        
        // Scale to fit maintaining aspect ratio
        float scaleX = availableWidth / IsometricMath.TileWidth;
        float scaleY = availableHeight / IsometricMath.TileHeight;
        float scale = Math.Min(scaleX, scaleY);
        
        float previewWidth = IsometricMath.TileWidth * scale;
        float previewHeight = IsometricMath.TileHeight * scale;
        
        foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
        {
            if (tileBitmaps.TryGetValue(terrainType, out var bitmap))
            {
                // Create a button for each tile - make it wide enough for the preview
                var button = new Button
                {
                    Width = double.NaN, // Auto width
                    MinWidth = previewWidth + 20, // Ensure enough width
                    MaxWidth = availableWidth + 20,
                    Margin = new Thickness(0, 0, 0, 15),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(2),
                    BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Padding = new Thickness(5),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                
                // Create a canvas to display the tile image - use exact preview dimensions
                var canvas = new Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl
                {
                    Width = previewWidth,
                    Height = previewHeight,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                canvas.Draw += (s, args) =>
                {
                    if (bitmap != null)
                    {
                        // Draw the tile scaled to fit, centered in canvas
                        args.DrawingSession.DrawImage(bitmap, new Windows.Foundation.Rect(0, 0, previewWidth, previewHeight));
                    }
                };
                
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                
                stackPanel.Children.Add(canvas);
                
                // Add tile info
                var infoPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 3
                };
                
                infoPanel.Children.Add(new TextBlock
                {
                    Text = terrainType.ToString(),
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                
                if (bitmap != null)
                {
                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = $"Size: {(int)bitmap.Size.Width} × {(int)bitmap.Size.Height}",
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                }
                
                stackPanel.Children.Add(infoPanel);
                
                button.Content = stackPanel;
                
                // Store terrain type in tag
                button.Tag = terrainType;
                
                // Handle click to select tile
                button.Click += (s, e) =>
                {
                    _selectedTerrainType = terrainType;
                    UpdateSelectedTileLabel();
                    UpdateTileBrowserSelection();
                };
                
                TileBrowserPanel.Children.Add(button);
            }
        }
    }
    
    private void UpdateSelectedTileLabel()
    {
        if (SelectedTileLabel != null)
        {
            SelectedTileLabel.Text = _selectedTerrainType.HasValue 
                ? $"Selected: {_selectedTerrainType.Value}" 
                : "Selected: None";
        }
    }
    
    private void UpdateTileBrowserSelection()
    {
        foreach (var child in TileBrowserPanel.Children)
        {
            if (child is Button button && button.Tag is TerrainType terrainType)
            {
                if (terrainType == _selectedTerrainType)
                {
                    button.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Yellow);
                    button.BorderThickness = new Thickness(3);
                }
                else
                {
                    button.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                    button.BorderThickness = new Thickness(2);
                }
            }
        }
    }
    
    private void MainWindow_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs e)
    {
        // Clear pressed keys when window becomes hidden
        if (!e.Visible)
        {
            _pressedKeys.Clear();
        }
    }
    
    private void SetupMenuBar()
    {
        // File menu
        var loadItem = new MenuFlyoutItem { Text = "Load" };
        loadItem.Click += LoadMenuItem_Click;
        
        var saveItem = new MenuFlyoutItem { Text = "Save" };
        saveItem.Click += SaveMenuItem_Click;
        
        var saveAsItem = new MenuFlyoutItem { Text = "Save As" };
        saveAsItem.Click += SaveAsMenuItem_Click;
        
        var fileMenuBarItem = new MenuBarItem { Title = "File" };
        fileMenuBarItem.Items.Add(loadItem);
        fileMenuBarItem.Items.Add(saveItem);
        fileMenuBarItem.Items.Add(saveAsItem);
        
        // About menu
        var aboutItem = new MenuFlyoutItem { Text = "About" };
        aboutItem.Click += AboutMenuItem_Click;
        var aboutMenuBarItem = new MenuBarItem { Title = "About" };
        aboutMenuBarItem.Items.Add(aboutItem);
        
        MainMenuBar.Items.Add(fileMenuBarItem);
        MainMenuBar.Items.Add(aboutMenuBarItem);
    }
    
    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.W || e.Key == VirtualKey.S || 
            e.Key == VirtualKey.A || e.Key == VirtualKey.D)
        {
            if (!_pressedKeys.Contains(e.Key))
            {
                _pressedKeys.Add(e.Key);
            }
            // Don't mark as handled so canvas can also receive it
        }
    }
    
    private void RootGrid_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.W || e.Key == VirtualKey.S || 
            e.Key == VirtualKey.A || e.Key == VirtualKey.D)
        {
            _pressedKeys.Remove(e.Key);
            e.Handled = true;
        }
    }

    private void MapCanvas_Loaded(object sender, RoutedEventArgs e)
    {
        if (MapCanvas != null)
            MapCanvas.Focus(FocusState.Programmatic);
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_mapRenderer.IsLoaded)
            return;

        try
        {
            await LoadMapAsync();
            SetupUpdateLoop();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MainWindow_Activated: {ex}");
        }
    }

    private async Task LoadMapAsync()
    {
        try
        {
            string defaultPath = ResolveWorldJsonPath() ?? "Content/world/world.json";
            _currentMapPath = defaultPath;
            
            await _mapRenderer.LoadMapAsync("Content/world/world.json", "Content/sprites/tiles/template");
            
            // Setup tile browser after map is loaded
            SetupTileBrowser();
            
            // Center camera on map initially
            var mapCenter = _mapRenderer.GetMapCenter();
            if (MapCanvas != null)
            {
                var windowBounds = MapCanvas.ActualSize;
                var screenCenter = new Vector2((float)windowBounds.X / 2.0f, (float)windowBounds.Y / 2.0f);
                _camera.Position = mapCenter - screenCenter;
                MapCanvas.Invalidate();
            }
        }
        catch (Exception ex)
        {
            // Show error message to user
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Error Loading Map",
                Content = $"Failed to load map: {ex.Message}\n\nStack trace: {ex.StackTrace}",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void SetupUpdateLoop()
    {
        _updateTimer = new DispatcherTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private void UpdateTimer_Tick(object? sender, object e)
    {
        // Handle continuous WASD key presses
        Vector2 panDirection = Vector2.Zero;
        const float deltaTime = 0.016f;

        if (_pressedKeys.Contains(VirtualKey.W))
            panDirection.Y -= 1;
        if (_pressedKeys.Contains(VirtualKey.S))
            panDirection.Y += 1;
        if (_pressedKeys.Contains(VirtualKey.A))
            panDirection.X -= 1;
        if (_pressedKeys.Contains(VirtualKey.D))
            panDirection.X += 1;

        if (panDirection != Vector2.Zero)
        {
            panDirection = Vector2.Normalize(panDirection);
            _camera.Pan(panDirection, deltaTime);
        }

        UpdateCameraInfo();
        if (MapCanvas != null)
            MapCanvas.Invalidate();
    }

    private void MapCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var transform = _camera.GetTransform();
        _mapRenderer.Draw(args.DrawingSession, transform);
        
        // Draw cursor tile if one is selected
        if (_selectedTerrainType.HasValue && !_isDragging)
        {
            var tileBitmaps = _mapRenderer.GetTileBitmaps();
            if (tileBitmaps.TryGetValue(_selectedTerrainType.Value, out var cursorBitmap))
            {
                // Convert cursor screen position to world position
                System.Numerics.Matrix3x2.Invert(transform, out var inverseTransform);
                var worldCursorPos = Vector2.Transform(_cursorPosition, inverseTransform);
                
                // Adjust for bottom-middle placement (same as in placement logic)
                worldCursorPos.Y -= IsometricMath.TileHeight;
                worldCursorPos.X -= IsometricMath.TileWidth / 2.0f;
                
                // Convert to tile coordinates
                var (tileX, tileY) = IsometricMath.ScreenToTile(worldCursorPos.X, worldCursorPos.Y);
                
                // Get the screen position of the tile (top point of tile)
                var (tileScreenX, tileScreenY) = IsometricMath.TileToScreen(tileX, tileY);
                
                // Draw the tile with reduced opacity to show it's a preview
                args.DrawingSession.Transform = transform;
                args.DrawingSession.Blend = Microsoft.Graphics.Canvas.CanvasBlend.SourceOver;
                args.DrawingSession.DrawImage(cursorBitmap, tileScreenX, tileScreenY, new Windows.Foundation.Rect(0, 0, (float)cursorBitmap.Size.Width, (float)cursorBitmap.Size.Height), 0.5f);
            }
        }
    }

    private void UpdateCameraInfo()
    {
        PositionLabel.Text = $"Position: ({_camera.Position.X:F1}, {_camera.Position.Y:F1})";
        ZoomLabel.Text = $"Zoom: {_camera.Zoom:F2}x";
    }

    private void MapCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(MapCanvas).Properties.MouseWheelDelta;
        if (delta > 0)
            _camera.ZoomIn(0.1f);
        else
            _camera.ZoomOut(0.1f);
        
        if (MapCanvas != null)
            MapCanvas.Invalidate();
        e.Handled = true;
    }

    private void MapCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (MapCanvas == null) return;
        
        var point = e.GetCurrentPoint(MapCanvas);
        var position = new Vector2((float)point.Position.X, (float)point.Position.Y);
        
        if (e.GetCurrentPoint(MapCanvas).Properties.IsLeftButtonPressed)
        {
            // Always ensure canvas has focus when starting to interact
            MapCanvas.Focus(FocusState.Pointer);
            _lastMousePosition = position;
            MapCanvas.CapturePointer(e.Pointer);
            _isDragging = false; // Will be set to true on move if it's a drag
        }
        else
        {
            // Ensure focus even on non-left clicks
            MapCanvas.Focus(FocusState.Pointer);
        }
    }
    
    private async void PlaceTile(int tileX, int tileY, TerrainType terrainType)
    {
        var mapData = _mapRenderer.GetMapData();
        if (mapData == null)
            return;
            
        // Check bounds
        if (tileX < 0 || tileX >= mapData.Width || tileY < 0 || tileY >= mapData.Height)
            return;
            
        // Update or create the tile
        mapData.SetTile(tileX, tileY, terrainType);
        
        // Reload the map renderer to reflect changes
        string basePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        string tileDirectory = "Content/sprites/tiles/template";
        await _mapRenderer.LoadMapDataAsync(mapData, tileDirectory);
        if (MapCanvas != null)
            MapCanvas.Invalidate();
    }

    private void MapCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (MapCanvas == null) return;
        var point = e.GetCurrentPoint(MapCanvas);
        var currentPosition = new Vector2((float)point.Position.X, (float)point.Position.Y);
        
        // Update cursor position for tile preview
        _cursorPosition = currentPosition;
        
        if (point.Properties.IsLeftButtonPressed)
        {
            var delta = currentPosition - _lastMousePosition;
            // If moved more than a few pixels, it's a drag
            if (delta.Length() > 5)
            {
                _isDragging = true;
                _camera.Position += delta;
                _lastMousePosition = currentPosition;
                if (MapCanvas != null)
                    MapCanvas.Invalidate();
            }
        }
        else
        {
            // Update cursor preview even when not dragging
            if (MapCanvas != null && _selectedTerrainType.HasValue)
                MapCanvas.Invalidate();
        }
    }

    private void MapCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (MapCanvas == null) return;
        
        var point = e.GetCurrentPoint(MapCanvas);
        var position = new Vector2((float)point.Position.X, (float)point.Position.Y);
        
        if (!_isDragging && _selectedTerrainType.HasValue)
        {
            // It was a click, not a drag - place the tile
            var transform = _camera.GetTransform();
            System.Numerics.Matrix3x2.Invert(transform, out var inverseTransform);
            var worldPos = Vector2.Transform(position, inverseTransform);
            
            // Adjust for bottom-middle of tile placement
            // The tile is drawn from top point, but we want to place at bottom-middle
            // Bottom-middle of tile in screen space is at (TileWidth/2, TileHeight) from top point
            // So we need to adjust the world position to account for this
            worldPos.Y -= IsometricMath.TileHeight; // Move up by tile height to get to top point
            worldPos.X -= IsometricMath.TileWidth / 2.0f; // Move left by half width to get to top point
            
            // Convert world coordinates to tile coordinates
            var (tileX, tileY) = IsometricMath.ScreenToTile(worldPos.X, worldPos.Y);
            
            // Place the tile
            PlaceTile(tileX, tileY, _selectedTerrainType.Value);
        }
        
        if (_isDragging)
        {
            _isDragging = false;
            // Clear any stuck keys when releasing mouse
            _pressedKeys.Clear();
        }
        
        MapCanvas.ReleasePointerCapture(e.Pointer);
        
        // Restore focus so keyboard input continues to work
        var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(10);
        timer.Tick += (s, args) =>
        {
            timer.Stop();
            if (MapCanvas != null)
            {
                MapCanvas.Focus(FocusState.Programmatic);
            }
        };
        timer.Start();
    }

    private void MapCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.W || e.Key == VirtualKey.S || 
            e.Key == VirtualKey.A || e.Key == VirtualKey.D)
        {
            if (!_pressedKeys.Contains(e.Key))
            {
                _pressedKeys.Add(e.Key);
            }
            e.Handled = true;
        }
    }

    private void MapCanvas_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.W || e.Key == VirtualKey.S || 
            e.Key == VirtualKey.A || e.Key == VirtualKey.D)
        {
            _pressedKeys.Remove(e.Key);
            e.Handled = true;
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (MapCanvas == null) return;
        var mapCenter = _mapRenderer.GetMapCenter();
        var windowBounds = MapCanvas.ActualSize;
        var screenCenter = new Vector2((float)windowBounds.X / 2.0f, (float)windowBounds.Y / 2.0f);
        _camera.Position = mapCenter - screenCenter;
        _camera.Zoom = 1.0f;
        MapCanvas.Invalidate();
        MapCanvas.Focus(FocusState.Programmatic);
    }
    
    private string? ResolveWorldJsonPath()
    {
        string basePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        string mapPath = "Content/world/world.json";
        
        // Try direct path first
        string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, mapPath));
        if (System.IO.File.Exists(fullPath))
            return fullPath;

        // Try going up to project root (for development)
        var dir = new System.IO.DirectoryInfo(basePath);
        while (dir != null && dir.Parent != null)
        {
            string testPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir.FullName, mapPath));
            if (System.IO.File.Exists(testPath))
                return testPath;
            dir = dir.Parent;
        }

        return null;
    }
    
    private async void LoadMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Always show file picker, but default to world.json location
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            
            // Try to set default path to Content/world directory
            string? defaultPath = ResolveWorldJsonPath();
            if (defaultPath != null)
            {
                string? directory = System.IO.Path.GetDirectoryName(defaultPath);
                if (directory != null && System.IO.Directory.Exists(directory))
                {
                    try
                    {
                        // Convert to absolute path and get folder
                        string absolutePath = System.IO.Path.GetFullPath(directory);
                        var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(absolutePath);
                        // Set the folder as the suggested start location
                        // Note: WinUI 3 doesn't directly support setting custom folders,
                        // but we can try to use the folder if available
                        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                    }
                    catch (Exception ex)
                    {
                        // If we can't get the folder, just use default location
                        System.Diagnostics.Debug.WriteLine($"Could not set folder: {ex.Message}");
                        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                    }
                }
                else
                {
                    picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                }
            }
            else
            {
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            }
            
            picker.FileTypeFilter.Add(".json");
            
            var file = await picker.PickSingleFileAsync();
            if (file == null)
                return;
            
            // Store the current map path
            _currentMapPath = file.Path;
            
            // Load the map
            var mapData = await MapSerializer.LoadFromFileAsync(file.Path);
            if (mapData != null)
            {
                // Resolve tile directory path
                string basePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
                string tileDirectory = "Content/sprites/tiles/template";
                
                await _mapRenderer.LoadMapDataAsync(mapData, tileDirectory);
                
                // Setup tile browser after map is loaded
                SetupTileBrowser();
                
                // Center camera on map
                var mapCenter = _mapRenderer.GetMapCenter();
                if (MapCanvas != null)
                {
                    var windowBounds = MapCanvas.ActualSize;
                    var screenCenter = new Vector2((float)windowBounds.X / 2.0f, (float)windowBounds.Y / 2.0f);
                    _camera.Position = mapCenter - screenCenter;
                    MapCanvas.Invalidate();
                }
            }
            else
            {
                await ShowErrorDialog("Failed to load map file.");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Error loading map: {ex.Message}\n\n{ex.StackTrace}");
        }
    }
    
    private async void SaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mapData = _mapRenderer.GetMapData();
            if (mapData == null)
            {
                await ShowErrorDialog("No map data to save.");
                return;
            }
            
            // If no current map path, use default or show Save As dialog
            if (string.IsNullOrEmpty(_currentMapPath))
            {
                // No current file, use Save As instead
                SaveAsMenuItem_Click(sender, e);
                return;
            }
            
            // Ensure directory exists
            string? directoryPath = System.IO.Path.GetDirectoryName(_currentMapPath);
            if (directoryPath != null && !System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }
            
            // Save to current file
            await MapSerializer.SaveToFileAsync(mapData, _currentMapPath);
            
            // Show success message
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Map Saved",
                Content = "Map saved successfully.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Error saving map: {ex.Message}\n\n{ex.StackTrace}");
        }
    }
    
    private async void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mapData = _mapRenderer.GetMapData();
            if (mapData == null)
            {
                await ShowErrorDialog("No map data to save.");
                return;
            }
            
            // Always show file picker, but default to world.json location
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            
            // Try to set default path to Content/world directory
            string? defaultPath = ResolveWorldJsonPath();
            picker.SuggestedFileName = "world";
            
            if (defaultPath != null)
            {
                string? directory = System.IO.Path.GetDirectoryName(defaultPath);
                if (directory != null && System.IO.Directory.Exists(directory))
                {
                    try
                    {
                        // Convert to absolute path and get folder
                        string absolutePath = System.IO.Path.GetFullPath(directory);
                        var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(absolutePath);
                        // Set the folder as the suggested start location
                        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                    }
                    catch (Exception ex)
                    {
                        // If we can't get the folder, just use default location
                        System.Diagnostics.Debug.WriteLine($"Could not set folder: {ex.Message}");
                        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                    }
                }
                else
                {
                    picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                }
            }
            else
            {
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            }
            
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            
            var file = await picker.PickSaveFileAsync();
            if (file == null)
                return;
            
            // Update current map path
            _currentMapPath = file.Path;
            
            // Ensure directory exists
            string? directoryPath = System.IO.Path.GetDirectoryName(file.Path);
            if (directoryPath != null && !System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }
            
            await MapSerializer.SaveToFileAsync(mapData, file.Path);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Error saving map: {ex.Message}\n\n{ex.StackTrace}");
        }
    }
    
    private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "About",
            Content = "IsoEditorV1",
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
    
    private async Task ShowErrorDialog(string message)
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

