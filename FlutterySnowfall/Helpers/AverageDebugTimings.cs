using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;

namespace FlutterySnowfall.Helpers;

public static class AverageDebugTimings
{
    private enum TimingType
    {
        Draw,
        Update
    }
    
    private static int _windowSize = 100;

    private static Queue<double> DrawTimings = [];
    private static double DrawTimingAccumulator;
    public static double DrawTimingAverage;
    
    private static Queue<double> UpdateTimings = [];
    private static double UpdateTimingAccumulator;
    public static double UpdateTimingAverage;

    public static void Initialize(Harmony harmony, int windowSize)
    {
        _windowSize = windowSize;
        DrawTimings = new Queue<double>(_windowSize + 1);
        UpdateTimings = new Queue<double>(_windowSize + 1);
        DrawTimingAccumulator = 0.0;
        UpdateTimingAccumulator = 0.0;
        DrawTimingAverage = 0.0;
        UpdateTimingAverage = 0.0;
        
        harmony.Patch(
            original: AccessTools.Method(typeof(DebugTimings), nameof(DebugTimings.Draw)),
            postfix: new HarmonyMethod(typeof(AverageDebugTimings), nameof(DebugTimings_Draw_Postfix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(DebugTimings), nameof(DebugTimings.StopDrawTimer)),
            postfix: new HarmonyMethod(typeof(AverageDebugTimings), nameof(DebugTimings_StopDrawTimer_Postfix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(DebugTimings), nameof(DebugTimings.StopUpdateTimer)),
            postfix: new HarmonyMethod(typeof(AverageDebugTimings), nameof(DebugTimings_StopUpdateTimer_Postfix))
        );
    }
    
    public static void ResetAllTimings()
    {
        ResetDrawTimings();
        ResetUpdateTimings();
    }

    public static void SetWindowSize(int windowSize)
    {
        _windowSize = windowSize;
        DrawTimings = new Queue<double>(_windowSize + 1);
        UpdateTimings = new Queue<double>(_windowSize + 1);
        ResetAllTimings();
    }

    private static void AddTiming(TimingType type, double time)
    {
        Queue<double> queue = type == TimingType.Draw ? DrawTimings : UpdateTimings;
        double accumulator = type == TimingType.Draw ? DrawTimingAccumulator : UpdateTimingAccumulator;
        
        queue.Enqueue(time);
        accumulator += time;
        
        if (queue.Count > _windowSize)
        {
            double removed = queue.Dequeue();
            accumulator -= removed;
        }
        
        if (type == TimingType.Draw)
        {
            DrawTimingAccumulator = accumulator;
            DrawTimingAverage = accumulator / queue.Count;
        }
        else
        {
            UpdateTimingAccumulator = accumulator;
            UpdateTimingAverage = accumulator / queue.Count;
        }
    }
    
    private static void ResetDrawTimings()
    {
        DrawTimings.Clear();
        DrawTimingAccumulator = 0.0;
        DrawTimingAverage = 0.0;
    }
    
    private static void ResetUpdateTimings() 
    {
        UpdateTimings.Clear();
        UpdateTimingAccumulator = 0.0;
        UpdateTimingAverage = 0.0;
    }
    
    private static void DebugTimings_Draw_Postfix(DebugTimings __instance)
    {
        if (!__instance.Active) return;
        if (Game1.game1?.IsMainInstance == false || Game1.spriteBatch is null || Game1.dialogueFont is null) return;
        
        Game1.spriteBatch.Draw(Game1.staminaRect, new Rectangle(0, 64, Game1.viewport.Width, 64), Color.Black * 0.5f);
        Game1.spriteBatch.DrawString(Game1.dialogueFont, $"Avg. Draw: {DrawTimingAverage:00.00} ms  ", DebugTimings.DrawPos + new Vector2(0, 64), Color.White);
        Game1.spriteBatch.DrawString(Game1.dialogueFont, $"Avg. Update: {UpdateTimingAverage:00.00} ms", new Vector2(DebugTimings.DrawPos.X + __instance.DrawTextWidth, DebugTimings.DrawPos.Y + 64), Color.White);
    }

    private static void DebugTimings_StopDrawTimer_Postfix(DebugTimings __instance)
    {
        if (!__instance.Active || !(Game1.game1?.IsMainInstance ?? false)) return;
        AddTiming(TimingType.Draw, __instance.LastTimingDraw);
    }
    
    private static void DebugTimings_StopUpdateTimer_Postfix(DebugTimings __instance)
    {
        if (!__instance.Active || !(Game1.game1?.IsMainInstance ?? false)) return;
        AddTiming(TimingType.Update, __instance.LastTimingUpdate);
    }
}