using Microsoft.Xna.Framework;
using StardewValley;
using xTile.Dimensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace FlutteryFlakes.Extensions
{
    public static class RectangleExtensions
    {
        extension(Rectangle rect)
        {
            public bool ContainsWithMargin(Point point, int margin)
            {
                return point.X >= rect.X - margin &&
                       point.X <= rect.Right + margin &&
                       point.Y >= rect.Y - margin &&
                       point.Y <= rect.Bottom + margin;
            }
            
            public bool ContainsWithMargin(Rectangle other, int margin)
            {
                return other.X >= rect.X - margin &&
                       other.Right <= rect.Right + margin &&
                       other.Y >= rect.Y - margin &&
                       other.Bottom <= rect.Bottom + margin;
            }

            public bool ContainsWithMargin(Vector2 point, int margin)
            {
                return point.X >= rect.X - margin &&
                       point.X <= rect.Right + margin &&
                       point.Y >= rect.Y - margin &&
                       point.Y <= rect.Bottom + margin;
            }
        }
    }
}