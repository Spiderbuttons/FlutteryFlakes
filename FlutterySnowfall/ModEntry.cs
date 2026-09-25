using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using GenericModConfigMenu;
using FlutterySnowfall.Config;
using FlutterySnowfall.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        internal static IManifest Manifest { get; set; } = null!;
        internal static IMonitor ModMonitor { get; set; } = null!;
        internal static ModConfig Config { get; set; } = null!;
        private static Harmony Harmony { get; set; } = null!;
        
        public static IGenericModConfigMenuApi? GMCM;
        
        internal static readonly PerScreen<SnowManager?> ScreenSnowManager = new();
        internal static readonly PerScreen<SnowManager?> PreviewManager = new();

        private static Texture2D? NoiseDemo;
        private static readonly FastNoiseLite Noise = new(69);

        public override void Entry(IModHelper helper)
        {
            i18n.Init(helper.Translation);
            ModHelper = helper;
            Manifest = ModManifest;
            ModMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();
            Harmony = new Harmony(ModManifest.UniqueID);
            
            Harmony.PatchAll();
            Harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.drawWeather)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(Game1_drawWeather_Transpiler)){ priority = Priority.First }
            );
            Harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.updateViewportForScreenSizeChange)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Game1_updateViewportForScreenSizeChange_Postfix))
            );
            
            AverageDebugTimings.Initialize(Harmony, 120);

            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.Player.Warped += OnWarped;
            Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            Helper.Events.Display.RenderedWorld += OnRenderedWorld;
            Helper.Events.Display.RenderedStep += OnRenderedStep;
            Helper.Events.Display.MenuChanged += OnMenuChanged;
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
            ScreenSnowManager.Value!.OnWarped(Game1.player.currentLocation);
        }
        
        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            ScreenSnowManager.Value?.OnWarped(e.NewLocation);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            ScreenSnowManager.Value?.Update();
            PreviewManager.Value?.Update();
        }

        private void OnRenderedStep(object? sender, RenderedStepEventArgs e)
        {
            if (e.Step is RenderSteps.Menu && GMCM?.TryGetCurrentMenu(out IManifest? mod, out _) == true && mod?.UniqueID == ModManifest.UniqueID)
            {
                PreviewManager.Value ??= new SnowManager();
                PreviewManager.Value.Draw(e.SpriteBatch);
            }
            else if (e.Step is RenderSteps.World_Weather) ScreenSnowManager.Value?.Draw(e.SpriteBatch);
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            
            if (NoiseDemo != null)
            {
                int screenWidth = Game1.graphics.GraphicsDevice.Viewport.Width;
                int screenHeight = Game1.graphics.GraphicsDevice.Viewport.Height;
                int x = (screenWidth - NoiseDemo.Width) / 2;
                int y = (screenHeight - NoiseDemo.Height) / 2;
                // e.SpriteBatch.Draw(NoiseDemo, new Vector2(x, y), Color.White);
            }

            // ScreenSnowManager.Value?.Draw(e.SpriteBatch);
        }
        
        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is null) PreviewManager.Value = null;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            ScreenSnowManager.Value?.OnButtonPressed(e.Button);
            
            if (!Context.IsWorldReady)
                return;

            if (e.Button is SButton.F2)
            {
                AverageDebugTimings.SetWindowSize(240);
            }

            if (e.Button is SButton.F3)
            {
                NoiseDemo = new Texture2D(Game1.graphics.GraphicsDevice, 512, 512);
                Noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
                Noise.SetFrequency(0.005f);
                Noise.SetSeed(696969);
                Color[] noiseColors = new Color[512 * 512];
                for (int x = 0; x < 512; x++)
                {
                    for (int y = 0; y < 512; y++)
                    {
                        float noiseValue = Noise.GetNoise(x, y);
                        float normalizedValue = (noiseValue + 1) / 2;
                        byte colorValue = (byte)(normalizedValue * 255);
                        noiseColors[x + y * 512] = new Color(colorValue, colorValue, colorValue);
                    }
                }
                NoiseDemo.SetData(noiseColors);
            }
        }

        private static void Game1_updateViewportForScreenSizeChange_Postfix(int width, int height)
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