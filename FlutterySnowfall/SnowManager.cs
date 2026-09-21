using System.Collections.Generic;
using System.Diagnostics;
using FlutterySnowfall.Extensions;
using FlutterySnowfall.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using xTile.Dimensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace FlutterySnowfall;

public class SnowManager
{
    private static Texture2D _snowTexture
    {
        get
        {
            field ??= ModEntry.ModHelper.ModContent.Load<Texture2D>("assets/snow.png");
            return field;
        }
    }

    private class SnowPuff(int puffType, Vector2 position, Vector2 speed, float scale, float rotationSpeed)
    {
        private Vector2 Position = position;
        private Vector2 Speed = speed;
        private float Rotation;
        private readonly Rectangle SourceRect = Game1.getSquareSourceRectForNonStandardTileSheet(_snowTexture, 6, 6, puffType);
        private readonly Vector2 Origin = puffType switch
        {
            6 => new Vector2(3.5f, 4f),
            7 => new Vector2(4f, 4f),
            _ => new Vector2(2.5f, 2.5f)
        };

        public void Draw(SpriteBatch b)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Position);
            if (screenPos.X < -10f || screenPos.X > Game1.viewport.Width + 10f || screenPos.Y < -10f || screenPos.Y > Game1.viewport.Height + 10f)
                return;
            b.Draw(_snowTexture, screenPos, SourceRect, Color.White, Rotation, Origin, scale, SpriteEffects.None, 1f);
        }

        public bool Update(GameTime time)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Position);

            // Speed = new Vector2(Speed.X, Speed.Y - 0.1f * (float)time.ElapsedGameTime.TotalSeconds);
            Position += Speed * (float)time.ElapsedGameTime.TotalSeconds;
            if (screenPos.X < -10f || screenPos.X > Game1.viewport.Width + 10f || screenPos.Y < -10f || screenPos.Y > Game1.viewport.Height + 10f)
                return false;

            if (Position.Y > ModEntry.ScreenSnowManager.Value!.LocationSizeInPixels.Height + 10f)
                return true;
            
            Rotation += rotationSpeed * (float)time.ElapsedGameTime.TotalSeconds;

            return false;
        }
    }

    private Size LocationSizeInPixels = Size.Zero;
    
    private readonly List<SnowPuff> SnowPuffs = [];

    private bool ShouldSnowHere()
    {
        // return false;
        return true;
    }

    private void AddSnowPuff()
    {
        Vector2 position = new Vector2(Game1.random.Next(ModEntry.ScreenSnowManager.Value!.LocationSizeInPixels.Width), -10f);
        Vector2 speed = new Vector2(-(float)(Game1.random.NextDouble() * 50.0 + 10.0), (float)(Game1.random.NextDouble() * 20.0 + 90.0));
        float scale = (float)(Game1.random.NextDouble() * 2.0f + 0.5f);
        float rotationSpeed = (float)(Game1.random.NextDouble() * 2.0 - 1.0);
        
        int puffType = Game1.random.Next(8);
        SnowPuffs.Add(new SnowPuff(puffType, position, speed, scale, rotationSpeed));
    }
    
    private void ClearSnowPuffs()
    {
        SnowPuffs.Clear();
    }

    public void OnWarped(GameLocation? newLocation)
    {
        LocationSizeInPixels = newLocation?.PixelSize() ?? Size.Zero;
    }

    public void OnButtonPressed(SButton button)
    {
        if (button is SButton.F2)
        {
            ClearSnowPuffs();
        }
    }

    public void Draw(SpriteBatch b)
    {
        if (!ShouldSnowHere()) return;

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        foreach (var puff in SnowPuffs)
        {
            puff.Draw(b);
        }

        b.End();
        b.BeginDefault();
    }
    
    public void Update()
    {
        if (!ShouldSnowHere()) return;
        
        for (int i = SnowPuffs.Count - 1; i >= 0; i--)
        {
            var puff = SnowPuffs[i];
            if (puff.Update(Game1.currentGameTime))
            {
                SnowPuffs.RemoveAt(i);
            }
        }

        if (Game1.random.NextDouble() < 0.9)
        {
            AddSnowPuff();
            AddSnowPuff();
            AddSnowPuff();
            AddSnowPuff();
            AddSnowPuff();
        }
    }
}