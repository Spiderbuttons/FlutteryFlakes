using GenericModConfigMenu;
using StardewModdingAPI;

namespace FlutterySnowfall.Config;

public sealed class ModConfig
{
    public float SnowDensity { get; set; } = 0.05f;

    public ModConfig()
    {
        Init();
    }

    private void Init()
    {
        SnowDensity = 0.05f;
    }

    public void SetupConfig(IGenericModConfigMenuApi configMenu, IManifest ModManifest, IModHelper Helper)
    {
        configMenu.Register(
            mod: ModManifest,
            reset: Init,
            save: () => Helper.WriteConfig(this)
        );
        
        configMenu.OnFieldChanged(
            mod: ModManifest,
            onChange: (fieldId, newValue) =>
            {
                if (fieldId is "SnowDensity") ModEntry.ScreenSnowManager.Value?.SetTargetDensity((float)newValue);
            }
        );

        configMenu.AddNumberOption(
            mod: ModManifest,
            name: () => "Snow Density",
            tooltip: () => "The approximate percentage of the screen covered by snow during snowy weather.",
            getValue: () => SnowDensity,
            setValue: value => SnowDensity = value,
            min: 0.01f,
            max: 1.0f,
            interval: 0.01f,
            formatValue: value => $"{value:P00}",
            fieldId: "SnowDensity"
        );
    }
}