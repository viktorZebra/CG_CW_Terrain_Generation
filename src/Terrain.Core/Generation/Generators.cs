using Terrain.Core.Models;

namespace Terrain.Core.Generation;

public static class Generators
{
    public static HeightMap Generate(GenerationOptions options)
    {
        if (options.Size < 2 || options.Size > 1025 || options.Hills < 0 || options.Hills > 10000 ||
            options.Octaves < 1 || options.Octaves > 10 || !float.IsFinite(options.Frequency) || options.Frequency <= 0 ||
            !float.IsFinite(options.Roughness) || options.Roughness <= 0 || options.Roughness >= 1)
            throw new ArgumentException("Проверьте параметры генерации.");
        var map = options.Kind switch
        {
            GeneratorKind.Hills => Hills(options),
            GeneratorKind.Perlin => NoiseMap(options, false),
            GeneratorKind.Simplex => NoiseMap(options, true),
            GeneratorKind.DiamondSquare => DiamondSquare(options),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        map.Normalize();
        if (options.Smooth)
            map = map.Smooth();
        if (options.Valley)
            map.Valley();
        return map;
    }

    private static HeightMap Hills(GenerationOptions o)
    {
        var map = new HeightMap(o.Size, o.Size);
        var random = new Random(o.Seed);
        for (int n = 0; n < o.Hills; n++)
        {
            float radius = (0.015f + random.NextSingle() * 0.085f) * o.Size;
            float cx = random.NextSingle() * (o.Size - 1), cy = random.NextSingle() * (o.Size - 1);
            for (int y = Math.Max(0, (int)(cy - radius)); y <= Math.Min(o.Size - 1, (int)(cy + radius)); y++)
                for (int x = Math.Max(0, (int)(cx - radius)); x <= Math.Min(o.Size - 1, (int)(cx + radius)); x++)
                    // A paraboloid cap, not a hemisphere: h = max(0, r² - distance²).
                    map[x, y] += Math.Max(0, radius * radius - (x - cx) * (x - cx) - (y - cy) * (y - cy));
        }
        return map;
    }

    private static HeightMap NoiseMap(GenerationOptions o, bool simplex)
    {
        var map = new HeightMap(o.Size, o.Size);
        var noise = new GradientNoise(o.Seed);
        for (int y = 0; y < o.Size; y++)
            for (int x = 0; x < o.Size; x++)
            {
                float frequency = o.Frequency, amplitude = 1, sum = 0, weight = 0;
                for (int octave = 0; octave < o.Octaves; octave++)
                {
                    float px = (float)x / (o.Size - 1) * frequency, py = (float)y / (o.Size - 1) * frequency;
                    sum += amplitude * (simplex ? noise.Simplex(px, py) : noise.Perlin(px, py));
                    weight += amplitude;
                    frequency *= 2;
                    amplitude *= o.Roughness;
                }
                map[x, y] = sum / weight;
            }
        return map;
    }

    private static HeightMap DiamondSquare(GenerationOptions o)
    {
        int last = o.Size - 1;
        if ((last & (last - 1)) != 0)
            throw new ArgumentException("Diamond–Square: размер должен быть 2ⁿ + 1 (65, 129, 257…).");
        var map = new HeightMap(o.Size, o.Size);
        var random = new Random(o.Seed);
        float Offset(float amplitude) => (random.NextSingle() * 2 - 1) * amplitude;
        map[0, 0] = Offset(1);
        map[last, 0] = Offset(1);
        map[0, last] = Offset(1);
        map[last, last] = Offset(1);
        float amplitude = 1;
        for (int step = last; step > 1; step /= 2, amplitude *= o.Roughness)
        {
            int half = step / 2;
            // Diamond: centre of each square from its four corners.
            for (int y = half; y < last; y += step)
                for (int x = half; x < last; x += step)
                    map[x, y] = (map[x - half, y - half] + map[x + half, y - half] +
                        map[x - half, y + half] + map[x + half, y + half]) / 4 + Offset(amplitude);
            // Square: edge midpoints; boundaries do not wrap around.
            for (int y = 0; y <= last; y += half)
                for (int x = (y + half) % step; x <= last; x += step)
                {
                    float sum = 0;
                    int count = 0;
                    if (x >= half)
                    {
                        sum += map[x - half, y];
                        count++;
                    }
                    if (x + half <= last)
                    {
                        sum += map[x + half, y];
                        count++;
                    }
                    if (y >= half)
                    {
                        sum += map[x, y - half];
                        count++;
                    }
                    if (y + half <= last)
                    {
                        sum += map[x, y + half];
                        count++;
                    }
                    map[x, y] = sum / count + Offset(amplitude);
                }
        }
        return map;
    }
}
