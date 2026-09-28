using System;
using System.Collections.Generic;
using FlutteryFlakes.Extensions;
using FlutteryFlakes.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace FlutteryFlakes;

public class SnowManager
{
    private static Texture2D _snowTexture
    {
        get
        {
            if (field is not null) return field;
            field = ModEntry.ModHelper.ModContent.Load<Texture2D>("assets/snow.png");
            field.Name = null;
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

        private static readonly Vector2 ConsistentSpeed = new(-40, 20);

        private readonly SnowManager Manager;

        private int GridCellKey = -1;
        
        private Vector2 Position;
        private float Rotation;
        
        private readonly float _scale;
        private readonly Rectangle _sourceRect;
        private readonly Vector2 _origin;
        private readonly Vector2 _speed;
        private readonly float _rotationSpeed;
        private readonly uint _noiseCoord = (uint)Game1.random.Next(0, int.MaxValue);
        private readonly float _colourVarianceFactor = Game1.random.Next(-100, 100) / 100f;
        private readonly float _layerDepth = Game1.random.NextSingle();

        public Snowflake(SnowManager manager, int flakeType, Vector2 position, Vector2 speed, float scale, float rotationSpeed)
        {
            Manager = manager;
            
            _scale = scale;
            _rotationSpeed = rotationSpeed;
            
            
            Position = position;
            _speed = speed;
            _sourceRect = Game1.getSquareSourceRectForNonStandardTileSheet(_snowTexture, 6, 6, flakeType);
            _origin = flakeType switch
            {
                5 => new Vector2(4f, 4f),
                6 => new Vector2(3.5f, 4f),
                7 => new Vector2(4f, 4f),
                _ => new Vector2(2.5f, 2.5f)
            };

            CalculateGridCellKey();
        }

        private void CalculateGridCellKey()
        {
            int gridWidth = SnowflakeCell.GetGridCellWidth();
            int gridHeight = SnowflakeCell.GetGridCellHeight();
            Vector2 localPosition = Game1.GlobalToLocal(Position);
            int cellX = (int)(localPosition.X + gridWidth) / gridWidth;
            int cellY = (int)(localPosition.Y + gridHeight) / gridHeight;
            GridCellKey = cellY * 10 + cellX;
        }
        
        public int GetGridCellKey()
        {
            return GridCellKey;
        }

        private Vector2 GetMovementSpeed()
        {
            Vector2 baseSpeed = Manager.SnowMovementType is MovementType.Consistent ? ConsistentSpeed : _speed;
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
            b.Draw(_snowTexture, screenPos, _sourceRect, drawColour, Rotation, _origin, _scale * Manager.ScaleMultiplier, SpriteEffects.None, _layerDepth);
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
            
            var cellBounds = cell.Bounds(local: true);
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
        private Rectangle _localBounds;
        private bool _localBoundsDirty = true;
        
        private int TotalSnowflakeArea => (int)(Snowflakes.Count * AverageFlakeArea * SourceRectArea);

        public Rectangle Bounds(bool local = false)
        {
            if (!_localBoundsDirty)
            {
                if (local) return _localBounds;
                var viewport = Game1.viewport;
                if (Game1.game1.takingMapScreenshot) viewport = new xTile.Dimensions.Rectangle(0, 0, Game1.currentLocation?.PixelSize().Width ?? 0, Game1.currentLocation?.PixelSize().Height ?? 0);
                return LocalToGlobal(viewport, _localBounds);
            }
            
            int gridWidth = GetGridCellWidth();
            int gridHeight = GetGridCellHeight();
            int cellX = CellId % 10;
            int cellY = CellId / 10;
            Rectangle localRectangle = new Rectangle(
                x: cellX * gridWidth - gridWidth,
                y: cellY * gridHeight - gridHeight,
                width: gridWidth,
                height: gridHeight
            );
            var viewport2 = Game1.viewport;
            if (Game1.game1.takingMapScreenshot) viewport2 = new xTile.Dimensions.Rectangle(0, 0, Game1.currentLocation?.PixelSize().Width ?? 0, Game1.currentLocation?.PixelSize().Height ?? 0);
            _localBounds = localRectangle;
            _localBoundsDirty = false;
            return local ? _localBounds : LocalToGlobal(viewport2, _localBounds);
        }

        public void MarkBoundsDirty()
        {
            _localBoundsDirty = true;
        }

        private void AddSnowflake()
        {
            float xSpeed = -(float)(Game1.random.NextDouble() * 20.0 + 30.0);
            float ySpeed = (float)(Game1.random.NextDouble() * 20.0 + 10.0);
            Rectangle bounds = Bounds();
            Vector2 position = new Vector2(Game1.random.Next(bounds.Left, bounds.Right), Game1.random.Next(bounds.Top, bounds.Bottom));
            Vector2 speed = new Vector2(xSpeed, ySpeed);
            float scale = (float)(Game1.random.NextDouble() * 2.5f + 1f);
            float rotationSpeed = (float)(Game1.random.NextDouble() * 2.0 - 1.0);
            
            int flakeType = Game1.random.Next(8);
            Snowflakes.Add(new Snowflake(Manager, flakeType, position, speed, scale, rotationSpeed));
        }

        private void AddSnowflake(Snowflake flake)
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
            if (!_initialFill)
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
        
        private void FillToTargetDensity(float targetDensity)
        {
            while (GetSnowflakeDensity() < targetDensity)
            {
                AddSnowflake();
            }
        }

        private float GetSnowflakeDensity()
        {
            Rectangle bounds = Bounds();
            int cellArea = bounds.Width * bounds.Height;
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

        private static int _gridCellWidth;
        private static int _gridCellHeight;
        private static bool _gridCellWidthDirty = true;
        private static bool _gridCellHeightDirty = true;
        
        public static int GetGridCellWidth()
        {
            if (!_gridCellWidthDirty) return _gridCellWidth;

            if (Game1.game1.takingMapScreenshot) _gridCellWidth = Game1.currentLocation?.PixelSize().Width / 8 ?? 0;
            else _gridCellWidth = (ShouldPreviewSnow ? Game1.uiViewport.Width : Game1.viewport.Width) / 8;
            
            _gridCellWidthDirty = false;
            return _gridCellWidth;
        }

        public static int GetGridCellHeight()
        {
            if (!_gridCellHeightDirty) return _gridCellHeight;
            
            if (Game1.game1.takingMapScreenshot) _gridCellHeight = Game1.currentLocation?.PixelSize().Height / 5 ?? 0;
            else _gridCellHeight = (ShouldPreviewSnow ? Game1.uiViewport.Height : Game1.viewport.Height) / 5;
            
            _gridCellHeightDirty = false;
            return _gridCellHeight;
        }
        
        public static void MarkGridCellSizeDirty()
        {
            _gridCellWidthDirty = true;
            _gridCellHeightDirty = true;
        }
    }

    private readonly Dictionary<int, SnowflakeCell> SnowflakeGrid = [];
    
    #region Configuration Variables
    private float? _targetDensity;
    public float TargetDensity
    {
        get
        {
            if (_targetDensity.HasValue) return _targetDensity.Value;
            if (ModEntry.Config.SnowDensityVariance is 0 || ModEntry.IsConfiguring(false))
            {
                _targetDensity = Math.Clamp(ModEntry.Config.SnowDensity, 0.01f, 0.9f);
                return _targetDensity.Value;
            }
            Random rng = Utility.CreateDaySaveRandom();
            float variance = (float)(rng.NextDouble() * 2 - 1) * ModEntry.Config.SnowDensityVariance;
            var density = Math.Clamp(ModEntry.Config.SnowDensity + variance, 0.01f, 0.9f);
            if (Context.IsSplitScreen) density /= 2f;
            _targetDensity = density;
            return _targetDensity.Value;
        }
        set => _targetDensity = value;
    }

    private float? _scaleMultiplier;
    public float ScaleMultiplier
    {
        get
        {
            if (_scaleMultiplier.HasValue) return _scaleMultiplier.Value;
            if (ModEntry.Config.ScaleVariance is 0)
            {
                _scaleMultiplier = Math.Clamp(ModEntry.Config.ScaleMultiplier, 0.1f, 2f);
                return _scaleMultiplier.Value;
            }
            Random rng = Utility.CreateDaySaveRandom();
            float variance = (float)(rng.NextDouble() * 2 - 1) * ModEntry.Config.ScaleVariance;
            _scaleMultiplier = Math.Clamp(ModEntry.Config.ScaleMultiplier + variance, 0.1f, 2f);
            return _scaleMultiplier.Value;
        }
        set => _scaleMultiplier = value;
    }

    private float? _windSpeedMultiplier;
    public float WindSpeedMultiplier
    {
        get
        {
            if (_windSpeedMultiplier.HasValue) return _windSpeedMultiplier.Value;
            if (ModEntry.Config.WindSpeedVariance is 0)
            {
                _windSpeedMultiplier = Math.Clamp(ModEntry.Config.WindSpeedMultiplier, 0.1f, 5f);
                return _windSpeedMultiplier.Value;
            }
            Random rng = Utility.CreateDaySaveRandom();
            float variance = (float)(rng.NextDouble() * 2 - 1) * ModEntry.Config.WindSpeedVariance;
            _windSpeedMultiplier = Math.Clamp(ModEntry.Config.WindSpeedMultiplier + variance, 0.1f, 5f);
            return _windSpeedMultiplier.Value;
        }
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
    
    private bool? _pixelatedSnowflakes;
    public bool PixelatedSnowflakes
    {
        get => _pixelatedSnowflakes ??= ModEntry.Config.PixelatedSnowflakes;
        set => _pixelatedSnowflakes = value;
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
    #endregion
    
    private static bool ShouldPreviewSnow => ModEntry.IsConfiguring();

    private bool _didJustScreenshot;
    private bool _didJustStartConfiguring;

    public SnowManager()
    {
        for (int i = 0; i < 70; i++)
        {
            SnowflakeGrid[i] = new SnowflakeCell(this, i);
        }
    }

    private bool ShouldSnowHere()
    {
        if (ShouldPreviewSnow) return this == ModEntry.PreviewManager.Value;
        return Context.IsWorldReady && Game1.currentLocation.IsOutdoors && Game1.currentLocation.IsSnowingHere();
    }

    public void ResetCells(bool clearSnowflakes = true, bool changeSeed = false)
    {
        SnowflakeCell.MarkGridCellSizeDirty();
        foreach (var cell in SnowflakeGrid.Values)
        {
            if (clearSnowflakes) cell.ClearSnowflakes();
            cell.ResetInitialFill();
            cell.MarkBoundsDirty();
        }
        if (changeSeed) Snowflake.Noise.SetSeed(Game1.random.Next());
    }

    public void ResetConfigurationVariables()
    {
        _targetDensity = null;
        _scaleMultiplier = null;
        _windSpeedMultiplier = null;
        _rotationSpeedMultiplier = null;
        _snowMovementType = null;
        _highFramerate = null;
        _pixelatedSnowflakes = null;
        _snowflakeColour = null;
        _snowflakeAlpha = null;
        _snowflakeColourVariance = null;
        _fogColour = null;
        _fogAlpha = null;
    }

    public void Draw(SpriteBatch b)
    {
        if (!ShouldSnowHere()) return;

        if (FogColour.A > 0)
        {
            int width = Game1.game1.takingMapScreenshot ? Game1.currentLocation?.PixelSize().Width ?? 0 : Game1.uiViewport.Width;
            int height = Game1.game1.takingMapScreenshot ? Game1.currentLocation?.PixelSize().Height ?? 0 : Game1.uiViewport.Height;
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, width, height), FogColour * FogAlpha);
        }
        
        if (SnowflakeColour.A <= 0) return;

        if (!PixelatedSnowflakes)
        {
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        }
        
        if (Game1.game1.takingMapScreenshot)
        {
            _didJustScreenshot = true;
            SnowflakeCell.MarkGridCellSizeDirty();
        }

        for (int i = 0, n = SnowflakeGrid.Count; i < n; i++)
        {
            // I don't like having to do this in Draw() but the function that takes the screenshot doesn't update things first.
            if (_didJustScreenshot)
            {
                SnowflakeGrid[i].MarkBoundsDirty();
                SnowflakeGrid[i].ResetInitialFill();
                SnowflakeGrid[i].Update();
            }
            SnowflakeGrid[i].Draw(b);
        }

        if (!PixelatedSnowflakes)
        {
            b.End();
            b.BeginDefault();
        }
    }
    
    public void Update()
    {
        if (!ShouldSnowHere()) return;

        if (!_didJustStartConfiguring && ShouldPreviewSnow)
        {
            _didJustStartConfiguring = true;
            ResetCells();
            SnowflakeCell.MarkGridCellSizeDirty();
        }

        if (_didJustScreenshot)
        {
            ResetCells();
            _didJustScreenshot = false;
        }
        
        var tickParity = HighFramerate ? 0 : (int)(Game1.currentGameTime.TotalGameTime.Ticks % 2);
        var increment = HighFramerate ? 1 : 2;
        for (int i = 0 + tickParity, n = SnowflakeGrid.Count; i < n; i += increment)
        {
            SnowflakeGrid[i].Update();
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