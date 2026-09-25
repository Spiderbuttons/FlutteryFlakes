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

    public enum MovementType
    {
        Consistent,
        Varied,
        Noisy
    }

    private class Snowflake
    {
        public static readonly FastNoiseLite Noise = new(69);
        static Snowflake()
        {
            Noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            Noise.SetFrequency(0.0065f);
        }

        public static readonly Vector2 ConsistentSpeed = new(-40, 20);

        private readonly SnowManager Manager;

        private int GridCellKey = -1;
        
        private Vector2 Position;
        private Vector2 Speed;
        private float Rotation;
        private readonly Rectangle SourceRect;
        private readonly Vector2 Origin;

        private readonly float _scale;
        private readonly float _rotationSpeed;
        private uint _noiseCoord;
        private float _colourVarianceFactor;

        public Snowflake(SnowManager manager, int flakeType, Vector2 position, Vector2 speed, float scale, float rotationSpeed)
        {
            Manager = manager;
            
            _scale = scale;
            _rotationSpeed = rotationSpeed;
            _noiseCoord = (uint)Game1.random.Next(0, int.MaxValue);
            _colourVarianceFactor = Game1.random.Next(-100, 100) / 100f;
            
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

        private Vector2 GetMovementSpeed()
        {
            Vector2 baseSpeed = Manager.SnowMovementType is MovementType.Consistent ? ConsistentSpeed : Speed;
            float variedSpeed = 0;
            if (Manager.SnowMovementType is MovementType.Noisy)
            {
                variedSpeed = Noise.GetNoise(Position.X, _noiseCoord);
            }
            variedSpeed *= baseSpeed.X;

            return new Vector2(baseSpeed.X + variedSpeed, baseSpeed.Y);
        }
        
        private float GetRotationSpeed()
        {
            float variedSpeed = 0;
            if (Manager.SnowMovementType is MovementType.Noisy)
            {
                variedSpeed = Noise.GetNoise(Position.X, _noiseCoord);
                variedSpeed = (variedSpeed + 1) / 2;
            }
            if (variedSpeed is not 0) variedSpeed /= Math.Abs(variedSpeed);

            return _rotationSpeed + _rotationSpeed * variedSpeed;
        }

        private Color GetDrawColour()
        {
            int colourToAdd = (int)(Manager.SnowflakeColourVariance * _colourVarianceFactor);
            int r = Math.Clamp(Manager.SnowflakeColour.R + colourToAdd, 0, 255);
            int g = Math.Clamp(Manager.SnowflakeColour.G + colourToAdd, 0, 255);
            int b = Math.Clamp(Manager.SnowflakeColour.B + colourToAdd, 0, 255);
            return new Color(r, g, b);
        }

        public void Draw(SpriteBatch b)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Position);
            Rectangle referenceViewport = ShouldPreviewSnow ? Game1.uiViewport.ToXna() : Game1.viewport.ToXna();
            if (!referenceViewport.ContainsWithMargin(Position, 8)) return;

            Color drawColour = GetDrawColour() * Manager.SnowflakeAlpha;
            b.Draw(_snowTexture, screenPos, SourceRect, drawColour, Rotation, Origin, _scale * Manager.ScaleMultiplier, SpriteEffects.None, 1f);
        }

        public bool Update(GameTime time)
        {
            if (!Manager.SnowflakeGrid.TryGetValue(GridCellKey, out var cell))
            {
                return true;
            }

            Vector2 movement = GetMovementSpeed();
            float rotationSpeed = GetRotationSpeed();
            float timeFactor = (float)time.ElapsedGameTime.TotalSeconds * (Manager.HighFramerate ? 1f : 2f);
            Position.X += movement.X * timeFactor * Manager.WindSpeedMultiplier;
            Position.Y += movement.Y * timeFactor * Manager.WindSpeedMultiplier;
            Rotation += rotationSpeed * timeFactor * Manager.RotationSpeedMultiplier;
            
            CalculateGridCellKey();
            if (!Manager.SnowflakeGrid.TryGetValue(GridCellKey, out cell))
            {
                return true;
            }

            Rectangle cellBounds = Game1.GlobalToLocal(Game1.viewport, cell.Bounds());
            return !cellBounds.Contains(Game1.GlobalToLocal(Game1.viewport, Position));
        }
    }

    private class SnowflakeCell(SnowManager Manager, int CellId)
    {
        private const float AverageFlakeScale = 3.5f / 2f;
        private const float AverageFlakeArea = AverageFlakeScale * AverageFlakeScale;
        private const int SourceRectArea = 36;

        private readonly List<Snowflake> Snowflakes = [];
        private bool _initialFill;
        
        public int SnowflakeCount => Snowflakes.Count;
        private int TotalSnowflakeArea => (int)(Snowflakes.Count * AverageFlakeArea * SourceRectArea);

        public Rectangle Bounds(bool local = false)
        {
            int gridWidth = GetGridCellWidth();
            int gridHeight = GetGridCellHeight();
            int cellX = (CellId) % 10;
            int cellY = (CellId) / 10;
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
            float xSpeed = -(float)(Game1.random.NextDouble() * 20.0 + 30.0);
            float ySpeed = (float)(Game1.random.NextDouble() * 20.0 + 10.0);
            Vector2 position = new Vector2(Game1.random.Next(Bounds().Left, Bounds().Right), Game1.random.Next(Bounds().Top, Bounds().Bottom));
            Vector2 speed = new Vector2(xSpeed, ySpeed);
            float scale = (float)(Game1.random.NextDouble() * 2.5f + 1f);
            float rotationSpeed = (float)(Game1.random.NextDouble() * 2.0 - 1.0);
            
            int flakeType = Game1.random.Next(8);
            Snowflakes.Add(new Snowflake(Manager, flakeType, position, speed, scale, rotationSpeed));
        }

        public void AddSnowflake(Snowflake flake)
        {
            Snowflakes.Add(flake);
        }

        public void ClearSnowflakes()
        {
            Snowflakes.Clear();
        }

        public void ResetInitialFill()
        {
            _initialFill = false;
        }

        public void Update()
        {
            while (!_initialFill && GetSnowflakeDensity() > Manager.TargetDensity)
            {
                if (Snowflakes.Count > 0)
                {
                    int indexToRemove = Game1.random.Next(Snowflakes.Count);
                    Snowflakes.RemoveAt(indexToRemove);
                }
                else break;
            }
            
            for (int i = Snowflakes.Count - 1; i >= 0; i--)
            {
                var flake = Snowflakes[i];
                int cellKey = flake.GetGridCellKey();
                if (cellKey != CellId)
                {
                    Snowflakes.RemoveAt(i);
                    if (cellKey is >= 0 and <= 69)
                    {
                        Manager.SnowflakeGrid[cellKey].AddSnowflake(flake);
                    }

                    continue;
                }

                if (flake.Update(Game1.currentGameTime))
                {
                    Snowflakes.RemoveAt(i);
                }
            }
            
            // Only wanna fill the ones on screen if they were just created or the weather just started.
            if (!_initialFill && GetSnowflakeDensity() < Manager.TargetDensity)
            {
                FillToTargetDensity(Manager.TargetDensity);
                _initialFill = true;
            }
            // Otherwise, we only wanna fill the edge cells to make sure the player doesn't stop seeing snowflakes when they move around.
            // This first elif handles the top and bottom rows.
            else if (CellId is < 10 or > 59) FillToTargetDensity(Manager.TargetDensity / 2f); // I'm not really sure why this needs to be divided by 2, but there's way too much snow otherwise.
            // This second elif handles the left and right rows.
            else if (CellId % 10 == 0 || CellId % 10 == 9) FillToTargetDensity(Manager.TargetDensity / 2f);
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
            // Random rng = new Random(CellId);
            // Color randomColor = new Color(rng.Next(256), rng.Next(256), rng.Next(256), 255);
            // b.Draw(Game1.staminaRect, Bounds(true), randomColor * 0.5f);
            // string countText = $"{Snowflakes.Count}";
            // Vector2 textSize = Game1.dialogueFont.MeasureString(countText);
            // Rectangle localBounds = GlobalToLocal(Game1.uiViewport, Bounds());
            // Vector2 textPosition = new Vector2(localBounds.Center.X - textSize.X / 2, localBounds.Center.Y - textSize.Y / 2);
            // b.DrawString(Game1.dialogueFont, countText, textPosition - new Vector2(2, -2), Color.Black, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
            // b.DrawString(Game1.dialogueFont, countText, textPosition, Color.White);
        }
        
        public static int GetGridCellWidth()
        {
            return (ShouldPreviewSnow ? Game1.uiViewport.Width : Game1.viewport.Width) / 8;
        }

        public static int GetGridCellHeight()
        {
            return (ShouldPreviewSnow ? Game1.uiViewport.Height : Game1.viewport.Height) / 5;
        }
    }

    private readonly Dictionary<int, SnowflakeCell> SnowflakeGrid = [];
    
    private float? _targetDensity;
    public float TargetDensity
    {
        get => _targetDensity ??= ModEntry.Config.SnowDensity;
        set => _targetDensity = value;
    }
    
    private float? _scaleMultiplier;
    public float ScaleMultiplier
    {
        get => _scaleMultiplier ??= ModEntry.Config.ScaleMultiplier;
        set => _scaleMultiplier = value;
    }
    
    private float? _windSpeedMultiplier;
    public float WindSpeedMultiplier
    {
        get => _windSpeedMultiplier ??= ModEntry.Config.WindSpeedMultiplier;
        set => _windSpeedMultiplier = value;
    }
    
    private float? _rotationSpeedMultiplier;
    public float RotationSpeedMultiplier
    {
        get => _rotationSpeedMultiplier ??= ModEntry.Config.RotationSpeedMultiplier;
        set => _rotationSpeedMultiplier = value;
    }
    
    private MovementType? _snowMovementType;
    public MovementType SnowMovementType
    {
        get => _snowMovementType ??= ModEntry.Config.MovementType;
        set => _snowMovementType = value;
    }

    private bool? _highFramerate;
    public bool HighFramerate
    {
        get => _highFramerate ??= ModEntry.Config.HighFramerate;
        set => _highFramerate = value;
    }
    
    private Color? _snowflakeColour;
    public Color SnowflakeColour
    {
        get => _snowflakeColour ??= new Color(ModEntry.Config.SnowflakeColour.R, ModEntry.Config.SnowflakeColour.G, ModEntry.Config.SnowflakeColour.B);
        set => _snowflakeColour = new Color(value.R, value.G, value.B);
    }
    
    private float? _snowflakeAlpha;
    public float SnowflakeAlpha
    {
        get => _snowflakeAlpha ??= ModEntry.Config.SnowflakeColour.A / 255f;
        set => _snowflakeAlpha = value;
    }

    private int? _snowflakeColourVariance;
    public int SnowflakeColourVariance
    {
        get => _snowflakeColourVariance ??= ModEntry.Config.SnowflakeColourVariance;
        set => _snowflakeColourVariance = value;
    }
    
    private Color? _fogColour;
    public Color FogColour
    {
        get => _fogColour ??= new Color(ModEntry.Config.FogColour.R, ModEntry.Config.FogColour.G, ModEntry.Config.FogColour.B);
        set => _fogColour = new Color(value.R, value.G, value.B);
    }
    
    private float? _fogAlpha;
    public float FogAlpha
    {
        get => _fogAlpha ??= ModEntry.Config.FogColour.A / 255f;
        set => _fogAlpha = value;
    }

    private static bool IsConfiguring => ModEntry.GMCM?.TryGetCurrentMenu(out IManifest? mod, out _) == true && mod?.UniqueID == ModEntry.UNIQUE_ID;
    private static bool ShouldPreviewSnow => ModEntry.Config.PreviewSnowflakes && IsConfiguring;


    public SnowManager()
    {
        for (int i = 0; i < 70; i++)
        {
            SnowflakeGrid[i] = new SnowflakeCell(this, i);
        }
    }

    private bool ShouldSnowHere()
    {
        if (IsConfiguring) return ShouldPreviewSnow;
        return Context.IsWorldReady && Game1.currentLocation.IsOutdoors && Game1.currentLocation.IsSnowingHere();
    }

    public void ResetCells(bool clearSnowflakes = true)
    {
        foreach (var cell in SnowflakeGrid.Values)
        {
            if (clearSnowflakes) cell.ClearSnowflakes();
            cell.ResetInitialFill();
        }
    }

    public void ResetConfigurationVariables()
    {
        _targetDensity = null;
        _scaleMultiplier = null;
        _windSpeedMultiplier = null;
        _rotationSpeedMultiplier = null;
        _snowMovementType = null;
        _highFramerate = null;
        _snowflakeColour = null;
        _snowflakeAlpha = null;
        _snowflakeColourVariance = null;
        _fogColour = null;
        _fogAlpha = null;
    }

    public void Draw(SpriteBatch b)
    {
        if (!ShouldSnowHere()) return;
        
        if (FogColour.A > 0) b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), FogColour * FogAlpha);
        
        if (SnowflakeColour.A <= 0) return;

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
            ResetCells();
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