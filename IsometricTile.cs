using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Project9.Shared;

namespace Project9
{
    public class IsometricTile
    {
        public static int TileWidth = 1024;
        public static int TileHeight = 512;

        public int TileX { get; set; }
        public int TileY { get; set; }
        public Texture2D Texture { get; set; }
        public Color TintColor { get; set; }
        public TerrainType TerrainType { get; set; }

        public IsometricTile(int tileX, int tileY, Texture2D texture, TerrainType terrainType)
        {
            TileX = tileX;
            TileY = tileY;
            Texture = texture;
            TintColor = Microsoft.Xna.Framework.Color.White;
            TerrainType = terrainType;
        }

        public Vector2 GetScreenPosition()
        {
            float screenX = (TileX - TileY) * (TileWidth / 2.0f);
            float screenY = (TileX + TileY) * (TileHeight / 2.0f);
            return new Vector2(screenX, screenY);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (Texture != null)
            {
                Vector2 position = GetScreenPosition();
                
                // All tiles align bottom of diamond with grid corner
                // position is the grid corner - where bottom of diamond should align
                
                if (TerrainType == TerrainType.Test)
                {
                    // Test tiles: 1024x1024, but bottom 1024x512 is the diamond, top 512 is overdraw
                    // Grid corner (position) should align with bottom of diamond
                    // TileToScreen returns the top point, which is centered horizontally
                    // Offset upward by TileHeight + overdraw to align bottom diamond with grid corner
                    float overdrawHeight = Texture.Height - TileHeight; // 512 for Test tile
                    float totalOffset = TileHeight + overdrawHeight; // 512 + 512 = 1024
                    
                    // Center horizontally: TileToScreen returns center, so offset by half width
                    float drawX = position.X - (Texture.Width / 2.0f);
                    float drawY = position.Y - totalOffset; // Move up to align bottom diamond
                    Rectangle destinationRect = new Rectangle((int)drawX, (int)drawY, Texture.Width, Texture.Height);
                    spriteBatch.Draw(Texture, destinationRect, null, TintColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                }
                else
                {
                    // Regular tiles: align bottom of diamond with grid corner
                    // Grid corner (position) should align with bottom of diamond
                    // TileToScreen returns the top point, which is centered horizontally
                    // For 1024-wide texture, offset by -512 to get left edge
                    // For standard 1024x512 tiles: offset upward by TileHeight to align bottom
                    // For tiles with overdraw, add overdraw offset
                    float overdrawHeight = Texture.Height > TileHeight 
                        ? (Texture.Height - TileHeight) 
                        : 0;
                    float totalOffset = TileHeight + overdrawHeight;
                    
                    // Center horizontally: TileToScreen returns center, so offset by half width
                    float drawX = position.X - (Texture.Width / 2.0f);
                    float drawY = position.Y - totalOffset;
                    Vector2 drawPosition = new Vector2(drawX, drawY);
                    
                    // Use TintColor for translucency - alpha channel controls transparency
                    spriteBatch.Draw(Texture, drawPosition, TintColor);
                }
            }
        }
    }
}
