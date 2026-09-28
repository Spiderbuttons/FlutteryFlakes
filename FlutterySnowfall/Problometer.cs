using System;
using FlutterySnowfall.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FlutterySnowfall;

public static class Problometer
{
    private static readonly Color[] BarColours =
    [
        new( 97, 187, 70),
        new(138, 199, 64),
        new(156, 204, 65),
        new(186, 213, 60),
        new(225, 224, 46),
        new(244, 218, 43),
        new(252, 209, 41),
        new(250, 183, 35),
        new(252, 162, 45),
        new(246, 130, 42),
        new(239,  72, 35),
        new(237,  19,  0)
    ];
    
    private static readonly string PreviewText = i18n.Problemeter_PreviewFirst();
    
    public static void Draw(SpriteBatch b, Vector2 position)
    {
        double avgUpdate = AverageDebugTimings.UpdateTimingAverage;
        double avgDraw = AverageDebugTimings.DrawTimingAverage;
        double totalAvg = avgUpdate + avgDraw;

        const double baseline = 0.30d;
        const double max = 15d;

        bool isPreviewing = ModEntry.IsConfiguring();
        
        double normalized = (totalAvg - baseline) / (max - baseline);
        int colourIndex = isPreviewing ? (int)(normalized * (BarColours.Length - 1)) : 0;
        colourIndex = Math.Clamp(colourIndex, 0, BarColours.Length - 1);
        float maxBarWidth = Math.Min(1200, Game1.uiViewport.Width - 200) / 3f;
        int barHeight = Game1.dialogueFont.MeasureString("0000.00 ms").ToPoint().Y;
        
        const int barGap = 1;
        for (int i = 0; i < BarColours.Length; i++)
        {
            float barWidth = maxBarWidth / BarColours.Length - barGap;
            float barX = position.X + i * barWidth + i * barGap;
            float barY = position.Y;
            float blackFactor = isPreviewing ? 0.65f : 0.9f;
            Color currentColour = isPreviewing && i <= colourIndex ? BarColours[i] : Color.Lerp(BarColours[i], Color.Black, blackFactor);
            b.Draw(Game1.staminaRect, new Rectangle((int)barX, (int)barY, (int)barWidth, barHeight), currentColour);
        }

        if (!isPreviewing)
        {
            Vector2 textSize = Game1.dialogueFont.MeasureString(PreviewText);
            Vector2 textPosition = new Vector2(position.X + maxBarWidth / 2 - textSize.X / 2, position.Y + barHeight / 2f - textSize.Y / 2.5f);
            b.DrawString(Game1.dialogueFont, PreviewText, textPosition - new Vector2(2, -2), Color.Black * 0.6f);
            b.DrawString(Game1.dialogueFont, PreviewText, textPosition, Color.White);
        }

        b.DrawString(Game1.dialogueFont, $"{totalAvg:F2} ms/f", position + new Vector2(maxBarWidth + 5, 0) - new Vector2(3, -3), Color.Black * 0.2f);
        b.DrawString(Game1.dialogueFont, $"{totalAvg:F2} ms/f", position + new Vector2(maxBarWidth + 5, 0), Color.Black);
    }
}