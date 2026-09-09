using Terrain.Core.Generation;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;

namespace Terrain.Core.Climate;

public static class ClimateBuilder
{
    public static ClimateMap Build(HeightMap map, ClimateOptions options, WaterMap? water = null)
    {
        if (!float.IsFinite(options.Temperature) || options.Temperature is < 0 or > 1 ||
            !float.IsFinite(options.Moisture) || options.Moisture is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (water is not null && (water.Width != map.Width || water.Height != map.Height || water.Moisture.Length != map.Values.Length))
            throw new ArgumentException("Water and climate maps must have matching dimensions.", nameof(water));
        var temperatureNoise = new GradientNoise(unchecked(options.Seed ^ 0x36A9F127));
        var moistureNoise = new GradientNoise(unchecked(options.Seed ^ 0x62E4B953));
        var samples = new ClimateSample[map.Values.Length];
        float scale = 2f / (Math.Max(map.Width, map.Height) - 1);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int i = y * map.Width + x;
                float px = (x - (map.Width - 1) / 2f) * scale;
                float py = (y - (map.Height - 1) / 2f) * scale;
                float temperature = Math.Clamp(options.Temperature - map.Values[i] * .65f +
                    temperatureNoise.Perlin(px * 1.4f + 13.7f, py * 1.4f - 8.3f) * .08f, 0, 1);
                float background = Math.Clamp(options.Moisture +
                    moistureNoise.Perlin(px * 1.8f - 21.2f, py * 1.8f + 4.6f) * .4f, 0, 1);
                // Water adds moisture without drying an already humid location.
                float moisture = 1 - (1 - background) * (1 - (water?.Moisture[i] ?? 0) * .75f);
                samples[i] = new(temperature, moisture, Blend(temperature, moisture));
            }
        return new(map.Width, map.Height, samples);
    }

    private static BiomeWeights Blend(float temperature, float moisture)
    {
        float snow = 1 - Smooth(.06f, .20f, temperature);
        float alpine = (1 - snow) * (1 - Smooth(.22f, .42f, temperature));
        float temperate = 1 - snow - alpine;
        float dry = 1 - Smooth(.20f, .48f, moisture);
        float wet = Smooth(.52f, .80f, moisture);
        return new(temperate * (1 - dry - wet), temperate * wet, temperate * dry, alpine, snow);
    }

    private static float Smooth(float from, float to, float value)
    {
        float t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
