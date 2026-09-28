using System;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace FlutterySnowfall.Helpers;

public class ColourConverter : JsonConverter<Color>
{
    public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
    {
        var hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}";
        writer.WriteValue(hex);
    }

    public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.StartObject)
        {
            var color = new Color(255, 255, 255, 255);
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;
                
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var propertyName = (string?)reader.Value;
                    reader.Read();
                    switch (propertyName)
                    {
                        case "R":
                            color.R = Convert.ToByte(reader.Value);
                            break;
                        case "G":
                            color.G = Convert.ToByte(reader.Value);
                            break;
                        case "B":
                            color.B = Convert.ToByte(reader.Value);
                            break;
                        case "A":
                            color.A = Convert.ToByte(reader.Value);
                            break;
                    }
                }
            }
            return color;
        }
        
        var hex = (string?)reader.Value ?? throw new JsonSerializationException("Expected a string value when reading Color from config.");
        if (hex.StartsWith("#"))
            hex = hex[1..];

        if (hex.Length == 6)
            hex += "FF";

        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        var a = Convert.ToByte(hex.Substring(6, 2), 16);

        return new Color(r, g, b, a);
    }
}