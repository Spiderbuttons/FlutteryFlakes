using StardewValley;
using xTile.Dimensions;

namespace FlutteryFlakes.Extensions
{
    public static class GameLocationExtensions
    {
        extension(GameLocation loc)
        {
            public Size TileSize()
            {
                return new Size(loc.Map.DisplayWidth / Game1.tileSize, loc.Map.DisplayHeight / Game1.tileSize);
            }

            public Size PixelSize()
            {
                return new Size(loc.Map.DisplayWidth, loc.Map.DisplayHeight);
            }
        }
    }
}