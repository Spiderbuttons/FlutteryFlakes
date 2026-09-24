using System;
using System.Collections.Generic;
using FlutterySnowfall.Extensions;
using FlutterySnowfall.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
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

    private class Snowflake
    {
        public static readonly FastNoiseLite Noise = new(696969);
        static Snowflake()
        {
            Noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            Noise.SetFrequency(0.005f);
        }
        
        private int GridCellKey = -1;
        
        private Vector2 Position;
        private Vector2 Speed;
        private float Rotation;
        private readonly Rectangle SourceRect;
        private readonly Vector2 Origin;

        private readonly float _scale;
        private readonly float _rotationSpeed;
        private uint _noiseCoord;

        public Snowflake(int flakeType, Vector2 position, Vector2 speed, float scale, float rotationSpeed)
        {
            _scale = scale;
            _rotationSpeed = rotationSpeed;
            _noiseCoord = (uint)Game1.random.Next(0, int.MaxValue);
            Position = position;
            Speed = speed;
            SourceRect = Game1.getSquareSourceRectForNonStandardTileSheet(_snowTexture, 6, 6, flakeType);
            Origin = flakeType switch
            {
                5 => new Vector2(4f, 4f),
                6 => new Vector2(3.5f, 4f),
                7 => new Vector2(4f, 4f),
                _ => new Vector2(2.5f, 2.5f)
            };

            CalculateGridCellKey();
        }

        public void CalculateGridCellKey()
        {
            int gridWidth = SnowflakeCell.GetGridCellWidth();
            int gridHeight = SnowflakeCell.GetGridCellHeight();
            Vector2 globalPosition = Game1.GlobalToLocal(Position);
            int cellX = (int)(globalPosition.X + gridWidth) / gridWidth;
            int cellY = (int)(globalPosition.Y + gridHeight) / gridHeight;
            GridCellKey = cellY * 10 + cellX;
        }
        
        public int GetGridCellKey()
        {
            return GridCellKey;
        }

        public void Draw(SpriteBatch b)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Position);
            if (!Game1.viewport.ToXna().ContainsWithMargin(Position, 8)) return;
            
            b.Draw(_snowTexture, screenPos, SourceRect, Color.Lerp(Color.AliceBlue, Color.LightSkyBlue, 0.05f), Rotation, Origin, _scale, SpriteEffects.None, 1f);
        }

        public bool Update(GameTime time)
        {
            if (!ModEntry.ScreenSnowManager.Value!.SnowflakeGrid.TryGetValue(GridCellKey, out var cell))
            {
                return true;
            }

            Noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            Noise.SetFrequency(0.0065f);
            Noise.SetSeed(69);
            var noiseValue = Noise.GetNoise(Position.X, _noiseCoord);
            float perlinSpeed = (noiseValue + 1) / 2;


            Position.X += (Speed.X + Speed.X * noiseValue) * (float)time.ElapsedGameTime.TotalSeconds * (HighFramerate ? 1f : 2f);
            Position.Y += Speed.Y * (float)time.ElapsedGameTime.TotalSeconds * (HighFramerate ? 1f : 2f);
            Rotation += (_rotationSpeed + _rotationSpeed * (perlinSpeed / perlinSpeed)) * (float)time.ElapsedGameTime.TotalSeconds;
            
            CalculateGridCellKey();
            if (!ModEntry.ScreenSnowManager.Value!.SnowflakeGrid.TryGetValue(GridCellKey, out cell))
            {
                return true;
            }

            Rectangle cellBounds = Game1.GlobalToLocal(Game1.viewport, cell.Bounds());
            return !cellBounds.Contains(Game1.GlobalToLocal(Game1.viewport, Position));
        }
    }

    private class SnowflakeCell(int cellId)
    {
        private const float AverageFlakeScale = 3.5f / 2f;
        
        private readonly List<Snowflake> Snowflakes = [];
        private bool _initialFill;
        
        public int SnowflakeCount => Snowflakes.Count;
        private int TotalSnowflakeArea => (int)(Snowflakes.Count * AverageFlakeScale * AverageFlakeScale * 36);
        
        private int MaxFlakes;

        public Rectangle Bounds(bool local = false)
        {
            int gridWidth = GetGridCellWidth();
            int gridHeight = GetGridCellHeight();
            int cellX = (cellId) % 10;
            int cellY = (cellId) / 10;
            Rectangle localRectangle = new Rectangle(
                x: cellX * gridWidth - gridWidth,
                y: cellY * gridHeight - gridHeight,
                width: gridWidth,
                height: gridHeight
            );
            return local ? localRectangle : LocalToGlobal(Game1.viewport, localRectangle);
        }

        public void AddSnowflake()
        {
            Vector2 position = new Vector2(Game1.random.Next(Bounds().Left, Bounds().Right), Game1.random.Next(Bounds().Top, Bounds().Bottom));
            Vector2 speed = new Vector2(-(float)(Game1.random.NextDouble() * 20.0 + 30.0), (float)(Game1.random.NextDouble() * 20.0 + 10.0));
            float scale = (float)(Game1.random.NextDouble() * 2.5f + 1f);
            float rotationSpeed = (float)(Game1.random.NextDouble() * 2.0 - 1.0);
            
            int flakeType = Game1.random.Next(8);
            Snowflakes.Add(new Snowflake(flakeType, position, speed, scale, rotationSpeed));
        }

        public void AddSnowflake(Snowflake flake)
        {
            Snowflakes.Add(flake);
        }

        public void ClearSnowflakes()
        {
            Snowflakes.Clear();
            MaxFlakes = 0;
        }

        public void ResetInitialFill()
        {
            _initialFill = false;
        }

        public void Update()
        {
            MaxFlakes = Math.Max(MaxFlakes, Snowflakes.Count);
            for (int i = Snowflakes.Count - 1; i >= 0; i--)
            {
                var flake = Snowflakes[i];
                int cellKey = flake.GetGridCellKey();
                if (cellKey != cellId)
                {
                    Snowflakes.RemoveAt(i);
                    if (cellKey is >= 0 and <= 69)
                    {
                        ModEntry.ScreenSnowManager.Value!.SnowflakeGrid[cellKey].AddSnowflake(flake);
                    }

                    continue;
                }

                if (flake.Update(Game1.currentGameTime))
                {
                    Snowflakes.RemoveAt(i);
                }
            }
            
            // Only wanna fill the ones on screen if they were just created or the weather just started.
            if (!_initialFill && GetSnowflakeDensity() < TargetDensity)
            {
                FillToTargetDensity(TargetDensity);
                _initialFill = true;
            }
            // Otherwise, we only wanna fill the edge cells to make sure the player doesn't stop seeing snowflakes when they move around.
            // This first elif handles the top and bottom rows.
            else if (cellId is < 10 or > 59) FillToTargetDensity(TargetDensity / 2f); // I'm not really sure why this needs to be divided by 2, but there's way too much snow otherwise.
            // This second elif handles the left and right rows.
            else if (cellId % 10 == 0 || cellId % 10 == 9) FillToTargetDensity(TargetDensity / 2f);
        }
        
        private void FillToMinimumCount(int minCount)
        {
            while (Snowflakes.Count < minCount)
            {
                AddSnowflake();
            }
        }
        
        private void FillToTargetDensity(float targetDensity)
        {
            while (GetSnowflakeDensity() < targetDensity)
            {
                AddSnowflake();
            }
        }

        public float GetSnowflakeDensity()
        {
            int cellArea = Bounds().Width * Bounds().Height;
            return TotalSnowflakeArea / (float)cellArea;
        }

        public void Draw(SpriteBatch b)
        {
            foreach (var flake in Snowflakes)
            {
                flake.Draw(b);
            }
            
            // Density debug stuff
            // string densityText = GetSnowflakeDensity().ToString("0.00");
            // Vector2 textSize = Game1.dialogueFont.MeasureString(densityText);
            // Rectangle localBounds = GlobalToLocal(Game1.viewport, Bounds());
            // Vector2 textPosition = new Vector2(localBounds.Center.X - textSize.X / 2, localBounds.Center.Y - textSize.Y / 2);
            // float alpha = MathHelper.Clamp(1f - (GetSnowflakeDensity() / TargetDensity), 0f, 1f);
            // b.Draw(Game1.staminaRect, localBounds, Color.Red * alpha);
            // b.DrawString(Game1.dialogueFont, densityText, textPosition, Color.White);

            // Other debug stuff
            // Random rng = new Random(cellId);
            // Color randomColor = new Color(rng.Next(256), rng.Next(256), rng.Next(256), 255);
            // b.Draw(Game1.staminaRect, GlobalToLocal(Game1.viewport, Bounds()), randomColor * 0.15f);
            // string countText = $"{Snowflakes.Count} / {MaxFlakes}";
            // Vector2 textSize = Game1.dialogueFont.MeasureString(countText);
            // Rectangle localBounds = GlobalToLocal(Game1.viewport, Bounds());
            // Vector2 textPosition = new Vector2(localBounds.Center.X - textSize.X / 2, localBounds.Center.Y - textSize.Y / 2);
            // b.DrawString(Game1.dialogueFont, countText, textPosition - new Vector2(2, -2), Color.Black, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
            // b.DrawString(Game1.dialogueFont, countText, textPosition, Color.White);
        }
        
        public static int GetGridCellWidth()
        { 
            return Game1.viewport.Width / 8;
        }

        public static int GetGridCellHeight()
        {
            return Game1.viewport.Height / 5;
        }
    }

    private readonly Dictionary<int, SnowflakeCell> SnowflakeGrid = [];
    
    private static float? _targetDensity;
    private static float TargetDensity
    {
        get => _targetDensity ??= ModEntry.Config.SnowDensity;
        set => _targetDensity = value;
    }

    private bool IsConfiguring => ModEntry.GMCM?.TryGetCurrentMenu(out IManifest? mod, out _) == true && mod?.UniqueID == ModEntry.UNIQUE_ID;
    private bool ShouldPreviewSnow => IsConfiguring;
    private static bool HighFramerate => true;


    public SnowManager()
    {
        for (int i = 0; i < 70; i++)
        {
            SnowflakeGrid[i] = new SnowflakeCell(i);
        }
    }

    private bool ShouldSnowHere()
    {
        return (Context.IsWorldReady && Game1.currentLocation.IsOutdoors && Game1.currentLocation.IsSnowingHere()) || ShouldPreviewSnow;
    }

    public void ClearSnowflakes()
    {
        foreach (var cell in SnowflakeGrid.Values)
        {
            cell.ClearSnowflakes();
            cell.ResetInitialFill();
        }
    }
    
    public void SetTargetDensity(float newDensity)
    {
        TargetDensity = newDensity;
        foreach (var cell in SnowflakeGrid.Values)
        {
            cell.ResetInitialFill();
        }
    }

    public void Draw(SpriteBatch b)
    {
        if (!ShouldSnowHere()) return;
        
        b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.AliceBlue * 0.15f);

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        
        for (int i = 0, n = SnowflakeGrid.Count; i < n; i++)
        {
            SnowflakeGrid[i].Draw(b);
        }

        b.End();
        b.BeginDefault();
    }
    
    public void Update()
    {
        if (!ShouldSnowHere()) return;
        
        var tickParity = HighFramerate ? 0 : (int)(Game1.currentGameTime.TotalGameTime.Ticks % 2);
        var increment = HighFramerate ? 1 : 2;
        for (int i = 0 + tickParity, n = SnowflakeGrid.Count; i < n; i += increment)
        {
            SnowflakeGrid[i].Update();
        }
    }
    
    public void OnWarped(GameLocation? newLocation)
    {
        Log.Info(newLocation?.Name);
        foreach (var cell in SnowflakeGrid.Values)
        {
            cell.ResetInitialFill();
        }
        Snowflake.Noise.SetSeed(Game1.random.Next());
    }

    public void OnButtonPressed(SButton button)
    {
        if (button is SButton.F2)
        {
            ClearSnowflakes();
            Log.Warn(SnowflakeGrid[11].Bounds());
            int newSeed = Game1.random.Next();
            Snowflake.Noise.SetSeed(newSeed);
            Log.Info($"New noise seed: {newSeed}");
        }

        if (button is SButton.MouseLeft)
        {
            foreach (var (key, cell) in SnowflakeGrid)
            {
                if (GlobalToLocal(Game1.viewport, cell.Bounds()).Contains(Game1.getMousePosition()))
                {
                    Log.Info("------------------------------------------");
                    Log.Debug($"Clicked on snowflake cell {key}");
                    Log.Debug($"Cell bounds: {cell.Bounds()} (Area: {cell.Bounds().Width * cell.Bounds().Height})");
                    Log.Debug($"Cell snowflake count: {cell.SnowflakeCount}");
                    Log.Debug($"Cell Snowflake Area: {cell.SnowflakeCount * (2.5f / 2f) * (2.5f / 2f) * 36}");
                    Log.Debug($"Cell snowflake density: {cell.GetSnowflakeDensity()}");
                }
            }
        }
    }
    
    public static Rectangle GlobalToLocal(xTile.Dimensions.Rectangle viewport, Rectangle globalPosition)
    {
        return new Rectangle(globalPosition.X - viewport.X, globalPosition.Y - viewport.Y, globalPosition.Width, globalPosition.Height);
    }

    public static Rectangle LocalToGlobal(xTile.Dimensions.Rectangle viewport, Rectangle localPosition)
    {
        return new Rectangle(localPosition.X + viewport.X, localPosition.Y + viewport.Y, localPosition.Width, localPosition.Height);
    }
}