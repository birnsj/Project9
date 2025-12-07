using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Project9.Shared;

namespace Project9.Editor;

public class MapRenderer
{
    private MapData? _mapData;
    private Dictionary<TerrainType, CanvasBitmap> _terrainBitmaps = new();
    private List<TileRenderData> _tiles = new();
    private CanvasDevice? _canvasDevice;
    private bool _isLoaded = false;

    public bool IsLoaded => _isLoaded;
    
    public MapData? GetMapData() => _mapData;
    
    public Dictionary<TerrainType, CanvasBitmap> GetTileBitmaps() => _terrainBitmaps;

    public async Task LoadMapAsync(string mapPath, string tileDirectory)
    {
        // Resolve paths relative to executable directory or project root
        string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        
        // Try to find the Content directory - could be in project root or bin directory
        string? resolvedMapPath = ResolvePath(basePath, mapPath);
        string? resolvedTileDir = ResolvePath(basePath, tileDirectory);

        if (resolvedMapPath == null || !File.Exists(resolvedMapPath))
        {
            throw new FileNotFoundException($"Map file not found: {mapPath} (searched from {basePath})");
        }

        // Load map data
        _mapData = await MapSerializer.LoadFromFileAsync(resolvedMapPath);
        if (_mapData == null)
        {
            throw new FileNotFoundException($"Failed to load map data from: {resolvedMapPath}");
        }

        // Create canvas device
        _canvasDevice = CanvasDevice.GetSharedDevice();

        // Load tile images
        if (resolvedTileDir == null || !Directory.Exists(resolvedTileDir))
        {
            throw new DirectoryNotFoundException($"Tile directory not found: {tileDirectory} (searched from {basePath})");
        }

        foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
        {
            string tilePath = Path.Combine(resolvedTileDir, $"{terrainType}.png");
            if (File.Exists(tilePath))
            {
                _terrainBitmaps[terrainType] = await CanvasBitmap.LoadAsync(_canvasDevice, tilePath);
            }
        }

        // Create tile render data
        foreach (var tileData in _mapData.Tiles)
        {
            if (_terrainBitmaps.ContainsKey(tileData.TerrainType))
            {
                _tiles.Add(new TileRenderData
                {
                    TileX = tileData.X,
                    TileY = tileData.Y,
                    TerrainType = tileData.TerrainType
                });
            }
        }

        // Sort tiles by screen Y position for correct depth rendering
        _tiles = _tiles.OrderBy(t =>
        {
            var (screenX, screenY) = IsometricMath.TileToScreen(t.TileX, t.TileY);
            return screenY;
        }).ThenBy(t =>
        {
            var (screenX, screenY) = IsometricMath.TileToScreen(t.TileX, t.TileY);
            return screenX;
        }).ToList();

        _isLoaded = true;
    }
    
    public async Task LoadMapDataAsync(MapData mapData, string tileDirectory)
    {
        _mapData = mapData;
        
        // Resolve tile directory path
        string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        string? resolvedTileDir = ResolvePath(basePath, tileDirectory);
        
        if (resolvedTileDir == null || !Directory.Exists(resolvedTileDir))
        {
            throw new DirectoryNotFoundException($"Tile directory not found: {tileDirectory} (searched from {basePath})");
        }
        
        // Create canvas device if needed
        if (_canvasDevice == null)
        {
            _canvasDevice = CanvasDevice.GetSharedDevice();
        }
        
        // Load tile images if not already loaded
        foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
        {
            if (!_terrainBitmaps.ContainsKey(terrainType))
            {
                string tilePath = Path.Combine(resolvedTileDir, $"{terrainType}.png");
                if (File.Exists(tilePath))
                {
                    _terrainBitmaps[terrainType] = await CanvasBitmap.LoadAsync(_canvasDevice, tilePath);
                }
            }
        }
        
        // Recreate tile render data
        _tiles.Clear();
        foreach (var tileData in _mapData.Tiles)
        {
            if (_terrainBitmaps.ContainsKey(tileData.TerrainType))
            {
                _tiles.Add(new TileRenderData
                {
                    TileX = tileData.X,
                    TileY = tileData.Y,
                    TerrainType = tileData.TerrainType
                });
            }
        }
        
        // Sort tiles by screen Y position for correct depth rendering
        _tiles = _tiles.OrderBy(t =>
        {
            var (screenX, screenY) = IsometricMath.TileToScreen(t.TileX, t.TileY);
            return screenY;
        }).ThenBy(t =>
        {
            var (screenX, screenY) = IsometricMath.TileToScreen(t.TileX, t.TileY);
            return screenX;
        }).ToList();
        
        _isLoaded = true;
    }

    public Vector2 GetMapCenter()
    {
        if (_mapData == null)
            return Vector2.Zero;

        float centerTileX = (_mapData.Width - 1) / 2.0f;
        float centerTileY = (_mapData.Height - 1) / 2.0f;
        var (screenX, screenY) = IsometricMath.TileToScreen((int)centerTileX, (int)centerTileY);
        return new Vector2(screenX, screenY);
    }

    public void Draw(CanvasDrawingSession drawingSession, Matrix3x2 transform)
    {
        if (!_isLoaded || _mapData == null)
            return;

        drawingSession.Transform = transform;

        foreach (var tile in _tiles)
        {
            if (_terrainBitmaps.TryGetValue(tile.TerrainType, out var bitmap))
            {
                var (screenX, screenY) = IsometricMath.TileToScreen(tile.TileX, tile.TileY);
                drawingSession.DrawImage(bitmap, screenX, screenY);
            }
        }
    }

    private static string? ResolvePath(string basePath, string relativePath)
    {
        // Try direct path first
        string fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            return fullPath;

        // Try going up to project root (for development)
        var dir = new DirectoryInfo(basePath);
        while (dir != null && dir.Parent != null)
        {
            string testPath = Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
            if (File.Exists(testPath) || Directory.Exists(testPath))
                return testPath;
            dir = dir.Parent;
        }

        return null;
    }

    private class TileRenderData
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public TerrainType TerrainType { get; set; }
    }
}

