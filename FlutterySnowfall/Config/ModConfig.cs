using System;
using FlutterySnowfall.Helpers;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace FlutterySnowfall.Config;

public sealed class ModConfig
{
    public bool PreviewSnowflakes { get; set; }
    public float SnowDensity { get; set; } = 0.01f;
    public float SnowDensityVariance { get; set; } = 0.05f;
    public float ScaleMultiplier { get; set; } = 1f;
    public float ScaleVariance { get; set; } = 0.2f;
    public float WindSpeedMultiplier { get; set; } = 1f;
    public float WindSpeedVariance { get; set; } = 0.2f;
    public float RotationSpeedMultiplier { get; set; } = 1f;
    public SnowManager.MovementType MovementType { get; set; } = SnowManager.MovementType.Noisy;
    public bool HighFramerate { get; set; }
    public bool PixelatedSnowflakes { get; set; } = true;
    
    public Color SnowflakeColour { get; set; } = new(238, 238, 255);
    public int SnowflakeColourVariance { get; set; } = 20;
    public Color FogColour { get; set; } = new(240, 248, 255, 38);

    public ModConfig()
    {
        Init();
    }

    private void Init()
    {
        SnowDensity = 0.01f;
        SnowDensityVariance = 0.05f;
        ScaleMultiplier = 1f;
        ScaleVariance = 0.2f;
        WindSpeedMultiplier = 1f;
        WindSpeedVariance = 0.2f;
        RotationSpeedMultiplier = 1f;
        MovementType = SnowManager.MovementType.Noisy;
        SnowflakeColour = new Color(238, 238, 255);
        SnowflakeColourVariance = 20;
        FogColour = new Color(240, 248, 255, 38);
        HighFramerate = false;
        PixelatedSnowflakes = true;
        
        ModEntry.PreviewManager.Value?.ResetConfigurationVariables();
        ModEntry.PreviewManager.Value?.ResetCells(clearSnowflakes: false);
    }

    public void SetupConfig(IGenericModConfigMenuApi configMenu, IManifest ModManifest, IModHelper Helper)
    {
        configMenu.Register(
            mod: ModManifest,
            reset: Init,
            save: () =>
            {
                Helper.WriteConfig(this);
                foreach (var (_, manager) in ModEntry.ScreenSnowManager.GetActiveValues())
                {
                    manager?.ResetConfigurationVariables();
                    manager?.ResetCells(clearSnowflakes: true);
                }
            });
        
        configMenu.OnFieldChanged(
            mod: ModManifest,
            onChange: (fieldId, newValue) =>
            {
                switch (fieldId)
                {
                    case "PreviewSnowflakes":
                        PreviewSnowflakes = (bool)newValue;
                        break;
                    case "SnowDensity":
                        ModEntry.PreviewManager.Value?.TargetDensity = (float)newValue;
                        ModEntry.PreviewManager.Value?.ResetCells(clearSnowflakes: false);
                        AverageDebugTimings.ResetAllTimings();
                        break;
                    case "ScaleMultiplier":
                        ModEntry.PreviewManager.Value?.ScaleMultiplier = (float)newValue;
                        break;
                    case "WindSpeedMultiplier":
                        ModEntry.PreviewManager.Value?.WindSpeedMultiplier = (float)newValue;
                        AverageDebugTimings.ResetAllTimings();
                        break;
                    case "RotationSpeedMultiplier":
                        ModEntry.PreviewManager.Value?.RotationSpeedMultiplier = (float)newValue;
                        break;
                    case "MovementType":
                        ModEntry.PreviewManager.Value?.SnowMovementType = Enum.Parse<SnowManager.MovementType>((string)newValue);
                        AverageDebugTimings.ResetAllTimings();
                        break;
                    case "HighFramerate":
                        ModEntry.PreviewManager.Value?.HighFramerate = (bool)newValue;
                        AverageDebugTimings.ResetAllTimings();
                        break;
                    case "PixelatedSnowflakes":
                        ModEntry.PreviewManager.Value?.PixelatedSnowflakes = (bool)newValue;
                        break;
                    case "SnowflakeRed":
                    case "SnowflakeGreen":
                    case "SnowflakeBlue":
                        Color prev = ModEntry.PreviewManager.Value?.SnowflakeColour ?? SnowflakeColour;
                        Color newColour = fieldId switch
                        {
                            "SnowflakeRed" => new Color((byte)(int)newValue, prev.G, prev.B),
                            "SnowflakeGreen" => new Color(prev.R, (byte)(int)newValue, prev.B),
                            "SnowflakeBlue" => new Color(prev.R, prev.G, (byte)(int)newValue),
                            _ => prev
                        };
                        ModEntry.PreviewManager.Value?.SnowflakeColour = newColour;
                        break;
                    case "SnowflakeAlpha":
                        ModEntry.PreviewManager.Value?.SnowflakeAlpha = (float)newValue;
                        break;
                    case "SnowflakeColourVariance":
                        ModEntry.PreviewManager.Value?.SnowflakeColourVariance = (int)newValue;
                        break;
                    case "FogRed":
                    case "FogGreen":
                    case "FogBlue":
                        Color prevFog = ModEntry.PreviewManager.Value?.FogColour ?? FogColour;
                        Color newFogColour = fieldId switch
                        {
                            "FogRed" => new Color((byte)(int)newValue, prevFog.G, prevFog.B),
                            "FogGreen" => new Color(prevFog.R, (byte)(int)newValue, prevFog.B),
                            "FogBlue" => new Color(prevFog.R, prevFog.G, (byte)(int)newValue),
                            _ => prevFog
                        };
                        ModEntry.PreviewManager.Value?.FogColour = newFogColour;
                        break;
                    case "FogAlpha":
                        ModEntry.PreviewManager.Value?.FogAlpha = (float)newValue;
                        break;
                }
            }
        );
        
        configMenu.AddComplexOption(
            mod: ModManifest,
            name: () => "Performance Impact",
            tooltip: () => "Measures how close you are to no longer being able to reach 60 FPS.",
            draw: Problometer.Draw,
            beforeMenuOpened: () => Game1.debugTimings.Active = true,
            beforeMenuClosed: () => Game1.debugTimings.Active = false,
            afterReset: AverageDebugTimings.ResetAllTimings,
            height: () => (int)Game1.dialogueFont.MeasureString("Performance Impact:").Y
        );
        
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: () => "Preview",
            tooltip: () => "If enabled, snowflakes will be shown in this config menu.",
            getValue: () => PreviewSnowflakes,
            setValue: value => PreviewSnowflakes = value,
            fieldId: "PreviewSnowflakes"
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snow Density",
            tooltip: () => "The approximate percentage of the screen covered by snow during snowy weather. Higher density incurs a higher performance cost.",
            getValue: () => SnowDensity,
            setValue: value => SnowDensity = value,
            min: 0.01f,
            max: 0.9f,
            interval: 0.01f,
            formatValue: value => $"{value:P00}",
            fieldId: "SnowDensity"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snow Density Variance",
            tooltip: () => "How much the snow density can randomly increase or decrease from your chosen snow density setting on any given day.",
            getValue: () => SnowDensityVariance,
            setValue: value => SnowDensityVariance = value,
            min: 0f,
            max: 0.9f,
            interval: 0.01f,
            formatValue: value => $"{value:P00}",
            fieldId: "SnowDensityVariance"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snowflake Scale",
            tooltip: () => "A multiplier for the size of snowflakes. Higher values will make snowflakes larger.",
            getValue: () => ScaleMultiplier,
            setValue: value => ScaleMultiplier = value,
            min: 0.1f,
            max: 2f,
            interval: 0.1f,
            formatValue: value => $"{value:F1}x",
            fieldId: "ScaleMultiplier"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snowflake Scale Variance",
            tooltip: () => "How much the scale of snowflakes can randomly increase or decrease from your chosen scale setting on any given day.",
            getValue: () => ScaleVariance,
            setValue: value => ScaleVariance = value,
            min: 0f,
            max: 2f,
            interval: 0.01f,
            formatValue: value => $"{value:F1}x",
            fieldId: "ScaleVariance"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Wind Speed",
            tooltip: () => "A multiplier for the speed of the implied wind. Higher values will make snowflakes move faster.",
            getValue: () => WindSpeedMultiplier,
            setValue: value => WindSpeedMultiplier = value,
            min: 0.1f,
            max: 5f,
            interval: 0.1f,
            formatValue: value => $"{value:F1}x",
            fieldId: "WindSpeedMultiplier"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Wind Speed Variance",
            tooltip: () => "How much the wind speed can randomly increase or decrease from your chosen wind speed setting on any given day.",
            getValue: () => WindSpeedVariance,
            setValue: value => WindSpeedVariance = value,
            min: 0f,
            max: 5f,
            interval: 0.1f,
            formatValue: value => $"{value:F1}x",
            fieldId: "WindSpeedVariance"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Rotation Speed",
            tooltip: () => "A multiplier for the speed of a snowflake's rotation. Higher values will make snowflakes rotate faster.",
            getValue: () => RotationSpeedMultiplier,
            setValue: value => RotationSpeedMultiplier = value,
            min: 0f,
            max: 5f,
            interval: 0.1f,
            formatValue: value => $"{value:F1}x",
            fieldId: "RotationSpeedMultiplier"
        );
        
        configMenu.AddTextOption(
            mod: ModManifest,
            name: () => "Snowflake Movement",
            tooltip: () => "Determines how the snowflakes move across the screen.",
            getValue: () => MovementType.ToString(),
            setValue: value =>
            {
                MovementType = Enum.TryParse(value, out SnowManager.MovementType movementType) ? movementType : SnowManager.MovementType.Noisy;
            },
            allowedValues: Enum.GetNames(typeof(SnowManager.MovementType)),
            fieldId: "MovementType"
        );
        
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: () => "High Framerate",
            tooltip: () => "If enabled, snowflakes will be updated at a higher framerate. This may or may not improve visual smoothness, but will incur a higher performance cost.",
            getValue: () => HighFramerate,
            setValue: value => HighFramerate = value,
            fieldId: "HighFramerate"
        );
        
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: () => "Pixelated Snowflakes",
            tooltip: () => "If enabled, snowflakes will be drawn pixelated like the rest of the game. If disabled, they will be drawn with a smoother, non-pixelated look.",
            getValue: () => PixelatedSnowflakes,
            setValue: value => PixelatedSnowflakes = value,
            fieldId: "PixelatedSnowflakes"
        );
        
        configMenu.AddPageLink(
            mod: ModManifest,
            pageId: "ColourSettings",
            text: () => "Colour Settings",
            tooltip: () => "Configure the colours of the snowflakes and fog."
        );
        
        configMenu.AddPage(
            mod: ModManifest,
            pageId: "ColourSettings",
            pageTitle: () => "Colour Settings"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snowflake Red",
            tooltip: () => "The red component of the snowflake colour.",
            getValue: () => SnowflakeColour.R,
            setValue: value => SnowflakeColour = new Color((byte)value, SnowflakeColour.G, SnowflakeColour.B, SnowflakeColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeRed"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snowflake Green",
            tooltip: () => "The green component of the snowflake colour.",
            getValue: () => SnowflakeColour.G,
            setValue: value => SnowflakeColour = new Color(SnowflakeColour.R, (byte)value, SnowflakeColour.B, SnowflakeColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeGreen"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snowflake Blue",
            tooltip: () => "The blue component of the snowflake colour.",
            getValue: () => SnowflakeColour.B,
            setValue: value => SnowflakeColour = new Color(SnowflakeColour.R, SnowflakeColour.G, (byte)value, SnowflakeColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeBlue"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snowflake Alpha",
            tooltip: () => "The alpha (transparency) component of the snowflake colour.",
            getValue: () => SnowflakeColour.A / 255f,
            setValue: value => SnowflakeColour = new Color(SnowflakeColour.R, SnowflakeColour.G, SnowflakeColour.B, (byte)(value * 255f)),
            min: 0f,
            max: 1f,
            interval: 0.01f,
            formatValue: value => $"{value:P0}",
            fieldId: "SnowflakeAlpha"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snowflake Colour Variance",
            tooltip: () => "How much two snowflakes can randomly differ from each other in colour.",
            getValue: () => SnowflakeColourVariance,
            setValue: value => SnowflakeColourVariance = value,
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeColourVariance"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Fog Red",
            tooltip: () => "The red component of the fog colour.",
            getValue: () => FogColour.R,
            setValue: value => FogColour = new Color((byte)value, FogColour.G, FogColour.B, FogColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "FogRed"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Fog Green",
            tooltip: () => "The green component of the fog colour.",
            getValue: () => FogColour.G,
            setValue: value => FogColour = new Color(FogColour.R, (byte)value, FogColour.B, FogColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "FogGreen"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Fog Blue",
            tooltip: () => "The blue component of the fog colour.",
            getValue: () => FogColour.B,
            setValue: value => FogColour = new Color(FogColour.R, FogColour.G, (byte)value, FogColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "FogBlue"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Fog Alpha",
            tooltip: () => "The alpha (transparency) component of the fog colour.",
            getValue: () => FogColour.A / 255f,
            setValue: value => FogColour = new Color(FogColour.R, FogColour.G, FogColour.B, (byte)(value * 255f)),
            min: 0f,
            max: 0.9f,
            interval: 0.01f,
            formatValue: value => $"{value:P0}",
            fieldId: "FogAlpha"
        );
    }
}