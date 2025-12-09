using System;
using System.IO;
using System.Threading.Tasks;
using Project9.Shared;

namespace Project9.Editor
{
    public class EditorMapData
    {
        private MapData _mapData;
        private string _filePath;

        public MapData MapData => _mapData;
        public string FilePath => _filePath;
        public int Width => _mapData.Width;
        public int Height => _mapData.Height;

        public EditorMapData()
        {
            _mapData = new MapData();
            _filePath = "Content/world/world.json";
        }

        public async Task LoadAsync(string? filePath = null)
        {
            string pathToLoad = filePath ?? _filePath;
            string? resolvedPath = ResolveMapPath(pathToLoad);
            
            if (resolvedPath == null)
            {
                resolvedPath = pathToLoad;
            }

            if (File.Exists(resolvedPath))
            {
                try
                {
                    var loadedData = await MapSerializer.LoadFromFileAsync(resolvedPath);
                    if (loadedData != null)
                    {
                        _mapData = loadedData;
                        MigrateLegacyCoordinates();
                        _filePath = resolvedPath;
                        Console.WriteLine($"[EditorMapData] Loaded map from {resolvedPath}: {_mapData.Width}x{_mapData.Height}, {_mapData.Tiles.Count} tiles");
                    }
                    else
                    {
                        Console.WriteLine($"[EditorMapData] Failed to load map from {resolvedPath}, creating default map");
                        CreateDefaultMap();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EditorMapData] Error loading map from {resolvedPath}: {ex.Message}");
                    CreateDefaultMap();
                }
            }
            else
            {
                Console.WriteLine($"[EditorMapData] Map file not found at {resolvedPath}, creating default map");
                CreateDefaultMap();
            }
        }

        public async Task SaveAsync(string? filePath = null)
        {
            string pathToSave = filePath ?? _filePath;
            await MapSerializer.SaveToFileAsync(_mapData, pathToSave);
            _filePath = pathToSave;
            Console.WriteLine($"[EditorMapData] Saved map to {pathToSave}");
        }

        public TileData? GetTile(int x, int y)
        {
            return _mapData.GetTile(x, y);
        }

        public void SetTile(int x, int y, TerrainType terrainType)
        {
            _mapData.SetTile(x, y, terrainType);
        }

        private void MigrateLegacyCoordinates()
        {
            // Convert legacy tile coordinates to pixel coordinates
            // If X/Y values are small (< 1000), they're likely tile coordinates
            
            // Migrate player
            if (_mapData.Player != null && _mapData.Player.X < 1000 && _mapData.Player.Y < 1000)
            {
                var (screenX, screenY) = IsometricMath.TileToScreen((int)_mapData.Player.X, (int)_mapData.Player.Y);
                _mapData.Player.X = screenX;
                _mapData.Player.Y = screenY;
            }
            else if (_mapData.Player == null)
            {
                // Create player at center of map if missing
                float centerTileX = (_mapData.Width - 1) / 2.0f;
                float centerTileY = (_mapData.Height - 1) / 2.0f;
                var (screenX, screenY) = IsometricMath.TileToScreen((int)centerTileX, (int)centerTileY);
                _mapData.Player = new PlayerData { X = screenX, Y = screenY };
            }
            
            // Migrate enemies
            foreach (var enemy in _mapData.Enemies)
            {
                if (enemy.X < 1000 && enemy.Y < 1000)
                {
                    var (screenX, screenY) = IsometricMath.TileToScreen((int)enemy.X, (int)enemy.Y);
                    enemy.X = screenX;
                    enemy.Y = screenY;
                }
            }
        }

        private void CreateDefaultMap()
        {
            _mapData = MapData.CreateDefault(20, 20);
            
            // Ensure player exists if not present
            if (_mapData.Player == null)
            {
                // Place player at center of map
                float centerTileX = (_mapData.Width - 1) / 2.0f;
                float centerTileY = (_mapData.Height - 1) / 2.0f;
                var (screenX, screenY) = IsometricMath.TileToScreen((int)centerTileX, (int)centerTileY);
                _mapData.Player = new PlayerData { X = screenX, Y = screenY };
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
    }
}


