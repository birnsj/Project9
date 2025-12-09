using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Project9.Shared;

namespace Project9
{

    public class IsometricMap
    {
        private int _mapWidth;
        private int _mapHeight;
        private List<IsometricTile> _tiles;
        private Dictionary<TerrainType, Texture2D> _terrainTextures;
        private MapData? _mapData;

        private GraphicsDevice _graphicsDevice;

        public MapData? MapData => _mapData;

        public IsometricMap(ContentManager content, GraphicsDevice graphicsDevice)
        {
            _tiles = new List<IsometricTile>();
            _terrainTextures = new Dictionary<TerrainType, Texture2D>();
            _graphicsDevice = graphicsDevice;
            LoadTextures(content);
            LoadMap();
        }

        private void LoadTextures(ContentManager content)
        {
            // Load terrain textures from Content/sprites/tiles/template
            foreach (TerrainType terrainType in Enum.GetValues<TerrainType>())
            {
                string texturePath = $"sprites/tiles/template/{terrainType}";
                Console.WriteLine($"[IsometricMap] Loading texture: {texturePath}");
                _terrainTextures[terrainType] = content.Load<Texture2D>(texturePath);
                Console.WriteLine($"[IsometricMap] Successfully loaded {terrainType} - Size: {_terrainTextures[terrainType].Width}x{_terrainTextures[terrainType].Height}");
            }
        }

        private void LoadMap()
        {
            const string mapPath = "Content/world/world.json";
            
            // Resolve path - try current directory, then walk up
            string? resolvedPath = ResolveMapPath(mapPath);
            if (resolvedPath == null)
            {
                resolvedPath = mapPath; // Fallback to original path
            }
            
            if (File.Exists(resolvedPath))
            {
                try
                {
                    // Load map synchronously (MonoGame LoadContent is synchronous)
                    var mapData = Task.Run(async () => await MapSerializer.LoadFromFileAsync(resolvedPath)).Result;
                    
                    if (mapData != null)
                    {
                        // Migrate legacy tile coordinates to pixel coordinates
                        MigrateLegacyCoordinates(mapData);
                        
                        _mapData = mapData; // Store map data for access to enemies
                        _mapWidth = mapData.Width;
                        _mapHeight = mapData.Height;
                        
                        // Create tiles from map data
                        foreach (var tileData in mapData.Tiles)
                        {
                            if (_terrainTextures.ContainsKey(tileData.TerrainType))
                            {
                                var texture = _terrainTextures[tileData.TerrainType];
                                var tile = new IsometricTile(tileData.X, tileData.Y, texture)
                                {
                                    TintColor = Color.White
                                };
                                _tiles.Add(tile);
                            }
                        }
                        
                        Console.WriteLine($"[IsometricMap] Loaded map from {resolvedPath}: {_mapWidth}x{_mapHeight}, {_tiles.Count} tiles, {mapData.Enemies?.Count ?? 0} enemies");
                    }
                    else
                    {
                        Console.WriteLine($"[IsometricMap] Failed to load map from {mapPath}, creating default map");
                        CreateDefaultMap();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IsometricMap] Error loading map from {resolvedPath}: {ex.Message}");
                    CreateDefaultMap();
                }
            }
            else
            {
                Console.WriteLine($"[IsometricMap] Map file not found at {resolvedPath}, creating default map");
                CreateDefaultMap();
            }
        }
        
        private static string? ResolveMapPath(string relativePath)
        {
            // Try current directory first
            string currentDir = Directory.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            // Try executable directory
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            fullPath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            // Try going up to project root (for development)
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

        private void CreateDefaultMap()
        {
            // Create a default 20x20 map with all grass tiles if JSON file doesn't exist
            _mapWidth = 20;
            _mapHeight = 20;
            
            for (int x = 0; x < _mapWidth; x++)
            {
                for (int y = 0; y < _mapHeight; y++)
                {
                    var texture = _terrainTextures[TerrainType.Grass];
                    var tile = new IsometricTile(x, y, texture)
                    {
                        TintColor = Color.White
                    };
                    _tiles.Add(tile);
                }
            }
        }



        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw tiles in proper order (back to front)
            // Sort by screen Y position to ensure correct depth
            var sortedTiles = new List<IsometricTile>(_tiles);
            sortedTiles.Sort((a, b) =>
            {
                Vector2 posA = a.GetScreenPosition();
                Vector2 posB = b.GetScreenPosition();
                int compareY = posA.Y.CompareTo(posB.Y);
                if (compareY != 0) return compareY;
                return posA.X.CompareTo(posB.X);
            });

            foreach (var tile in sortedTiles)
            {
                tile.Draw(spriteBatch);
            }
        }

        public Vector2 GetMapCenter()
        {
            // Calculate center of the map in screen coordinates
            float centerTileX = (_mapWidth - 1) / 2.0f;
            float centerTileY = (_mapHeight - 1) / 2.0f;
            float centerScreenX = (centerTileX - centerTileY) * (IsometricTile.TileWidth / 2.0f);
            float centerScreenY = (centerTileX + centerTileY) * (IsometricTile.TileHeight / 2.0f);
            return new Vector2(centerScreenX, centerScreenY);
        }

        private static void MigrateLegacyCoordinates(MapData mapData)
        {
            // Convert legacy tile coordinates to pixel coordinates
            // If X/Y values are small (< 1000), they're likely tile coordinates
            
            // Migrate player
            if (mapData.Player != null && mapData.Player.X < 1000 && mapData.Player.Y < 1000)
            {
                var (screenX, screenY) = IsometricMath.TileToScreen((int)mapData.Player.X, (int)mapData.Player.Y);
                mapData.Player.X = screenX;
                mapData.Player.Y = screenY;
            }
            
            // Migrate enemies
            foreach (var enemy in mapData.Enemies)
            {
                if (enemy.X < 1000 && enemy.Y < 1000)
                {
                    var (screenX, screenY) = IsometricMath.TileToScreen((int)enemy.X, (int)enemy.Y);
                    enemy.X = screenX;
                    enemy.Y = screenY;
                }
            }
        }
    }
}
