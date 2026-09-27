using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using GenericModConfigMenu;
using FlutterySnowfall.Config;
using FlutterySnowfall.Helpers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Mods;

namespace FlutterySnowfall
{
    internal sealed class ModEntry : Mod
    {
        internal static string UNIQUE_ID => Manifest.UniqueID;
        
        internal static IModHelper ModHelper { get; set; } = null!;
        private static IManifest Manifest { get; set; } = null!;
        internal static IMonitor ModMonitor { get; set; } = null!;
        internal static ModConfig Config { get; set; } = null!;
        private static Harmony Harmony { get; set; } = null!;

        private static IGenericModConfigMenuApi? GMCM;
        
        internal static readonly PerScreen<SnowManager?> ScreenSnowManager = new();
        internal static readonly PerScreen<SnowManager?> PreviewManager = new();

        public override void Entry(IModHelper helper)
        {
            i18n.Init(helper.Translation);
            ModHelper = helper;
            Manifest = ModManifest;
            ModMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();
            Harmony = new Harmony(ModManifest.UniqueID);
            
            Harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.drawWeather)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(Game1_drawWeather_Transpiler)){ priority = Priority.First }
            );
            Harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.updateViewportForScreenSizeChange)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Game1_updateViewportForScreenSizeChange_Postfix))
            );
            
            AverageDebugTimings.Initialize(Harmony, 120);
            
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.Player.Warped += OnWarped;
            Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            Helper.Events.Display.RenderedStep += OnRenderedStep;
            Helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        internal static bool IsConfiguring(bool checkForPreview = true)
        {
            return GMCM?.TryGetCurrentMenu(out IManifest? mod, out _) == true && mod?.UniqueID == UNIQUE_ID && (!checkForPreview || Config.PreviewSnowflakes);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            ScreenSnowManager.Value?.ResetCells(clearSnowflakes: true, changeSeed: true);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            GMCM = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (GMCM != null) Config.SetupConfig(GMCM, ModManifest, Helper);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            PreviewManager.Value = null;
            ScreenSnowManager.Value = new SnowManager();
            ScreenSnowManager.Value!.ResetCells(clearSnowflakes: true, changeSeed: true);
        }
        
        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            ScreenSnowManager.Value?.ResetCells(clearSnowflakes: true, changeSeed: true);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            ScreenSnowManager.Value?.Update();
            if (IsConfiguring()) PreviewManager.Value?.Update();
            else if (!IsConfiguring(false)) PreviewManager.Value = null;
        }

        private void OnRenderedStep(object? sender, RenderedStepEventArgs e)
        {
            if (e.Step is RenderSteps.Menu && IsConfiguring())
            {
                PreviewManager.Value ??= new SnowManager();
                PreviewManager.Value.Draw(e.SpriteBatch);
            }
            else if (e.Step is RenderSteps.World_Weather) ScreenSnowManager.Value?.Draw(e.SpriteBatch);
        }
        
        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is null) PreviewManager.Value = null;
        }

        private static void Game1_updateViewportForScreenSizeChange_Postfix()
        {
            ScreenSnowManager.Value?.ResetCells(false);
            PreviewManager.Value?.ResetCells(false);
        }

        private static IEnumerable<CodeInstruction> Game1_drawWeather_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var code = instructions.ToList();
            try
            {
                var matcher = new CodeMatcher(code, il);

                matcher.MatchEndForward(
                    new CodeMatch(op => op.Calls(AccessTools.Method(typeof(GameLocation), nameof(GameLocation.IsSnowingHere)))),
                    new CodeMatch(OpCodes.Brfalse)
                ).ThrowIfNotMatch($"Failed to find entry point.");

                matcher.Insert(
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Ldc_I4_0)
                );

                return matcher.InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in {nameof(Game1_drawWeather_Transpiler)}: {ex}");
                return code;
            }
        }
    }
}