using System.Text.Json.Serialization;

namespace Project9.Shared
{
    /// <summary>
    /// Represents a single tile in the map for JSON serialization
    /// </summary>
    public class TileData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("terrainType")]
        public TerrainType TerrainType { get; set; }
    }

    /// <summary>
    /// Represents an enemy spawn position in the map for JSON serialization
    /// </summary>
    public class EnemyData
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
        
        // Legacy support: if X/Y are integers, treat as tile coordinates
        [JsonIgnore]
        public int TileX => (int)Math.Round(X);
        
        [JsonIgnore]
        public int TileY => (int)Math.Round(Y);
    }

    /// <summary>
    /// Represents the player spawn position in the map for JSON serialization
    /// </summary>
    public class PlayerData
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
        
        // Legacy support: if X/Y are integers, treat as tile coordinates
        [JsonIgnore]
        public int TileX => (int)Math.Round(X);
        
        [JsonIgnore]
        public int TileY => (int)Math.Round(Y);
    }

    /// <summary>
    /// Complete map data structure for JSON serialization
    /// </summary>
    public class MapData
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("tiles")]
        public List<TileData> Tiles { get; set; } = new List<TileData>();

        [JsonPropertyName("enemies")]
        public List<EnemyData> Enemies { get; set; } = new List<EnemyData>();

        [JsonPropertyName("player")]
        public PlayerData? Player { get; set; }

        /// <summary>
        /// Creates a default empty map with specified dimensions
        /// </summary>
        public static MapData CreateDefault(int width = 20, int height = 20)
        {
            var map = new MapData
            {
                Width = width,
                Height = height
            };

            // Initialize with all grass tiles
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    map.Tiles.Add(new TileData
                    {
                        X = x,
                        Y = y,
                        TerrainType = TerrainType.Grass
                    });
                }
            }

            // Set default player position at map center (in pixel coordinates)
            float centerTileX = (width - 1) / 2.0f;
            float centerTileY = (height - 1) / 2.0f;
            var (centerScreenX, centerScreenY) = IsometricMath.TileToScreen((int)centerTileX, (int)centerTileY);
            map.Player = new PlayerData
            {
                X = centerScreenX,
                Y = centerScreenY
            };

            return map;
        }

        /// <summary>
        /// Gets the tile at the specified coordinates
        /// </summary>
        public TileData? GetTile(int x, int y)
        {
            return Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
        }

        /// <summary>
        /// Sets or creates a tile at the specified coordinates
        /// </summary>
        public void SetTile(int x, int y, TerrainType terrainType)
        {
            var existing = GetTile(x, y);
            if (existing != null)
            {
                existing.TerrainType = terrainType;
            }
            else
            {
                Tiles.Add(new TileData
                {
                    X = x,
                    Y = y,
                    TerrainType = terrainType
                });
            }
        }
    }
}
