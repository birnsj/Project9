using System.Text.Json.Serialization;

namespace Project9.Shared
{
    /// <summary>
    /// Represents a collision cell position for JSON serialization
    /// </summary>
    public class CollisionCellData
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
    }

    /// <summary>
    /// Collection of collision cells for a map
    /// </summary>
    public class CollisionData
    {
        [JsonPropertyName("cells")]
        public List<CollisionCellData> Cells { get; set; } = new List<CollisionCellData>();
    }
}

