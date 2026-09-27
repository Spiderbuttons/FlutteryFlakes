using System.Collections.Generic;
using System.Diagnostics;
using FlutterySnowfall.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FlutterySnowfall;

public static class Problometer
{
    // private enum TimingType
    // {
    //     Draw,
    //     Update
    // }
    //
    // private static bool Active;
    //
    // private static readonly Stopwatch StopwatchDraw = new();
    // private static readonly Stopwatch StopwatchUpdate = new();
    //
    // private static int _windowSize = 100;
    //
    // private static Queue<double> DrawTimings = [];
    // private static double DrawTimingAccumulator;
    // private static double DrawTimingAverage;
    //
    // private static Queue<double> UpdateTimings = [];
    // private static double UpdateTimingAccumulator;
    // private static double UpdateTimingAverage;
    //
    // private static double LastTimingDraw;
    // private static double LastTimingUpdate;
    //
    // public static void Initialize(int windowSize)
    // {
    //     _windowSize = windowSize;
    //     DrawTimings = new Queue<double>(_windowSize + 1);
    //     UpdateTimings = new Queue<double>(_windowSize + 1);
    //     DrawTimingAccumulator = 0.0;
    //     UpdateTimingAccumulator = 0.0;
    //     DrawTimingAverage = 0.0;
    //     UpdateTimingAverage = 0.0;
    // }
    //
    // private static void AddTiming(TimingType type, double time)
    // {
    //     Queue<double> queue = type == TimingType.Draw ? DrawTimings : UpdateTimings;
    //     double accumulator = type == TimingType.Draw ? DrawTimingAccumulator : UpdateTimingAccumulator;
    //     
    //     queue.Enqueue(time);
    //     accumulator += time;
    //     
    //     if (queue.Count > _windowSize)
    //     {
    //         double removed = queue.Dequeue();
    //         accumulator -= removed;
    //     }
    //     
    //     if (type == TimingType.Draw)
    //     {
    //         DrawTimingAccumulator = accumulator;
    //         DrawTimingAverage = accumulator / queue.Count;
    //     }
    //     else
    //     {
    //         UpdateTimingAccumulator = accumulator;
    //         UpdateTimingAverage = accumulator / queue.Count;
    //     }
    // }
    //
    // private stat
    
    public static void Draw(SpriteBatch b, Vector2 position)
    {
        double avgUpdate = AverageDebugTimings.UpdateTimingAverage;
        double avgDraw = AverageDebugTimings.DrawTimingAverage;
        double totalAvg = avgUpdate + avgDraw;
        
        b.DrawString(Game1.dialogueFont, $"{totalAvg:F2} ms", position, Color.White);
    }
}