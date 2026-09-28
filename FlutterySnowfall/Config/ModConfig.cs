using System;
using FlutterySnowfall.Helpers;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
// ReSharper disable MemberCanBePrivate.Global

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
    
    [JsonConverter(typeof(ColourConverter))]
    public Color SnowflakeColour { get; set; } = new(238, 238, 255);
    public int SnowflakeColourVariance { get; set; } = 20;
    [JsonConverter(typeof(ColourConverter))]
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
                        AverageDebugTimings.ResetAllTimings();
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
            name: i18n.Config_PerformanceImpact_Name,
            tooltip: i18n.Config_PerformanceImpact_Tooltip,
            draw: Problometer.Draw,
            beforeMenuOpened: () => Game1.debugTimings.Active = true,
            beforeMenuClosed: () => Game1.debugTimings.Active = false,
            afterReset: AverageDebugTimings.ResetAllTimings,
            height: () => (int)Game1.dialogueFont.MeasureString("Performance Impact:").Y
        );
        
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: i18n.Config_Preview_Name,
            tooltip: i18n.Config_Preview_Tooltip,
            getValue: () => PreviewSnowflakes,
            setValue: value => PreviewSnowflakes = value,
            fieldId: "PreviewSnowflakes"
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_SnowDensity_Name,
            tooltip: i18n.Config_SnowDensity_Tooltip,
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
            name: i18n.Config_SnowDensityVariance_Name,
            tooltip: i18n.Config_SnowDensityVariance_Tooltip,
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
            name: i18n.Config_SnowflakeScale_Name,
            tooltip: i18n.Config_SnowflakeScale_Tooltip,
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
            name: i18n.Config_SnowflakeScaleVariance_Name,
            tooltip: i18n.Config_SnowflakeScaleVariance_Tooltip,
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
            name: i18n.Config_WindSpeed_Name,
            tooltip: i18n.Config_WindSpeed_Tooltip,
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
            name: i18n.Config_WindSpeedVariance_Name,
            tooltip: i18n.Config_WindSpeedVariance_Tooltip,
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
            name: i18n.Config_RotationSpeed_Name,
            tooltip: i18n.Config_RotationSpeed_Tooltip,
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
            name: i18n.Config_SnowflakeMovement_Name,
            tooltip: i18n.Config_SnowflakeMovement_Tooltip,
            getValue: () => MovementType.ToString(),
            setValue: value =>
            {
                MovementType = Enum.TryParse(value, out SnowManager.MovementType movementType) ? movementType : SnowManager.MovementType.Noisy;
            },
            allowedValues: Enum.GetNames(typeof(SnowManager.MovementType)),
            formatAllowedValue: value => i18n.GetByKey($"Config.SnowflakeMovement.{value}"),
            fieldId: "MovementType"
        );
        
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: i18n.Config_HighFramerate_Name,
            tooltip: i18n.Config_HighFramerate_Tooltip,
            getValue: () => HighFramerate,
            setValue: value => HighFramerate = value,
            fieldId: "HighFramerate"
        );
        
        configMenu.AddBoolOption(
            mod: ModManifest,
            name: i18n.Config_PixelatedSnowflakes_Name,
            tooltip: i18n.Config_PixelatedSnowflakes_Tooltip,
            getValue: () => PixelatedSnowflakes,
            setValue: value => PixelatedSnowflakes = value,
            fieldId: "PixelatedSnowflakes"
        );
        
        configMenu.AddPageLink(
            mod: ModManifest,
            pageId: "ColourSettings",
            text: i18n.Config_ColourSettings_Name,
            tooltip: i18n.Config_ColourSettings_Tooltip
        );
        
        configMenu.AddPage(
            mod: ModManifest,
            pageId: "ColourSettings",
            pageTitle: i18n.Config_ColourSettings_Name
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_SnowflakeRed_Name,
            tooltip: i18n.Config_SnowflakeRed_Tooltip,
            getValue: () => SnowflakeColour.R,
            setValue: value => SnowflakeColour = new Color((byte)value, SnowflakeColour.G, SnowflakeColour.B, SnowflakeColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeRed"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_SnowflakeGreen_Name,
            tooltip: i18n.Config_SnowflakeGreen_Tooltip,
            getValue: () => SnowflakeColour.G,
            setValue: value => SnowflakeColour = new Color(SnowflakeColour.R, (byte)value, SnowflakeColour.B, SnowflakeColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeGreen"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_SnowflakeBlue_Name,
            tooltip: i18n.Config_SnowflakeBlue_Tooltip,
            getValue: () => SnowflakeColour.B,
            setValue: value => SnowflakeColour = new Color(SnowflakeColour.R, SnowflakeColour.G, (byte)value, SnowflakeColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeBlue"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_SnowflakeAlpha_Name,
            tooltip: i18n.Config_SnowflakeAlpha_Tooltip,
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
            name: i18n.Config_SnowflakeColourVariance_Name,
            tooltip: i18n.Config_SnowflakeColourVariance_Tooltip,
            getValue: () => SnowflakeColourVariance,
            setValue: value => SnowflakeColourVariance = value,
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "SnowflakeColourVariance"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_FogRed_Name,
            tooltip: i18n.Config_FogRed_Tooltip,
            getValue: () => FogColour.R,
            setValue: value => FogColour = new Color((byte)value, FogColour.G, FogColour.B, FogColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "FogRed"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_FogGreen_Name,
            tooltip: i18n.Config_FogGreen_Tooltip,
            getValue: () => FogColour.G,
            setValue: value => FogColour = new Color(FogColour.R, (byte)value, FogColour.B, FogColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "FogGreen"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_FogBlue_Name,
            tooltip: i18n.Config_FogBlue_Tooltip,
            getValue: () => FogColour.B,
            setValue: value => FogColour = new Color(FogColour.R, FogColour.G, (byte)value, FogColour.A),
            min: 0,
            max: 255,
            interval: 1,
            fieldId: "FogBlue"
        );
        
        configMenu.AddNumberOption(
            mod: ModManifest,
            name: i18n.Config_FogAlpha_Name,
            tooltip: i18n.Config_FogAlpha_Tooltip,
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