using System.Numerics;
using Terrain.Core.Generation;
using Terrain.Core.Climate;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;

namespace Terrain.Core.Vegetation;

/// <summary>Seeded, clustered placement on actual terrain triangles, in model coordinates.</summary>
public static class ObjectScatter
{
    public static ObjectInstance[] Place(HeightMap map, Mesh terrain, WaterMap? water, int seed, VegetationOptions options)
    {
        if (!float.IsFinite(options.Density) || options.Density < 0 || options.Density > 1 ||
            options.MaxTrees < 0 || options.MaxTrees > 2000 || options.MaxRocks < 0 || options.MaxRocks > 500)
            throw new ArgumentOutOfRangeException(nameof(options));
        bool useBiomes = options.BiomeAware && terrain.Climate is not null;
        var result = new List<ObjectInstance>();
        var noise = new GradientNoise(unchecked(seed ^ 0x32AE1529));
        float step = 2f / (Math.Max(map.Width, map.Height) - 1);
        float width = (map.Width - 1) * step, depth = (map.Height - 1) * step;
        const float spacing = .045f;
        var occupied = new Dictionary<(int X, int Y), List<Vector2>>();
        void Scatter(bool trees)
        {
            var random = new Random(unchecked(seed ^ (trees ? 0x4C921A3D : 0x19B72E01)));
            int limit = trees ? options.MaxTrees : options.MaxRocks;
            int placed = 0;
            for (int attempt = 0; attempt < (trees ? 12000 : 2500) && placed < limit; attempt++)
            {
                float x = (float)random.NextDouble() * width - width / 2;
                float z = (float)random.NextDouble() * depth - depth / 2;
                float chance = (float)random.NextDouble();
                float size = (trees ? .075f : .025f) * (.75f + (float)random.NextDouble() * .7f);
                float yaw = (float)random.NextDouble() * MathF.Tau;
                float tint = .85f + (float)random.NextDouble() * .3f;
                float kind = (float)random.NextDouble();
                float radius = size * (trees ? .35f : .55f);
                if (Math.Abs(x) + radius >= width / 2 || Math.Abs(z) + radius >= depth / 2)
                    continue;
                var sample = Sample(map, terrain, water, x, z);
                if ((!useBiomes && sample.Height > (trees ? .79f : .96f)) || sample.Upward < (trees ? .75f : .5f))
                    continue;
                if (useBiomes && sample.Climate.Biomes.Snow > .05f)
                    continue;
                // Check the footprint, not only the trunk centre, against the water mask.
                if (sample.Wet || Sample(map, terrain, water, x + radius, z).Wet ||
                    Sample(map, terrain, water, x - radius, z).Wet || Sample(map, terrain, water, x, z + radius).Wet ||
                    Sample(map, terrain, water, x, z - radius).Wet)
                    continue;
                float cluster = Math.Clamp(.45f + noise.Perlin(x * 3.2f + 5.3f, z * 3.2f - 2.7f) * 1.8f, 0, 1);
                float probability = trees
                    ? options.Density * cluster * (.10f + .13f * sample.Moisture)
                    : .045f;
                if (trees && useBiomes)
                {
                    var biome = sample.Climate.Biomes;
                    // Alpine/snow weights contribute no habitat: a smooth climate-based tree line.
                    float habitat = .10f * biome.Meadow + .24f * biome.WetForest + .012f * biome.Steppe;
                    probability = options.Density * cluster * habitat;
                }
                if (chance > probability)
                    continue;
                var bucket = ((int)MathF.Floor(x / spacing), (int)MathF.Floor(z / spacing));
                bool nearby = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (occupied.TryGetValue((bucket.Item1 + dx, bucket.Item2 + dy), out var points))
                            foreach (var point in points)
                                if (Vector2.DistanceSquared(point, new(x, z)) < spacing * spacing)
                                    nearby = true;
                if (nearby)
                    continue;
                if (!occupied.TryGetValue(bucket, out var cell))
                    occupied[bucket] = cell = [];
                cell.Add(new(x, z));
                float pineChance = useBiomes ? .20f + .75f * (1 - Smooth(.35f, .65f, sample.Climate.Temperature))
                    : sample.Height > .55f ? 1 : .55f;
                result.Add(new(trees ? (kind < pineChance ? ObjectKind.Pine : ObjectKind.Broadleaf) : ObjectKind.Rock,
                    new(x, sample.Height, z), size, yaw, tint));
                placed++;
            }
        }
        Scatter(true);
        Scatter(false);
        return result.ToArray();
    }

    private static float Smooth(float from, float to, float value)
    {
        float t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    internal static (float Height, float Upward, float Moisture, bool Wet, ClimateSample Climate, float Bank) Sample(HeightMap map, Mesh terrain, WaterMap? water, float x, float z)
    {
        float step = 2f / (Math.Max(map.Width, map.Height) - 1);
        float gx = Math.Clamp(x / step + (map.Width - 1) / 2f, 0, map.Width - 1);
        float gy = Math.Clamp(z / step + (map.Height - 1) / 2f, 0, map.Height - 1);
        int ix = Math.Min((int)gx, map.Width - 2), iy = Math.Min((int)gy, map.Height - 2);
        float u = gx - ix, v = gy - iy;
        int a = iy * map.Width + ix, b = a + 1, c = a + map.Width, d = c + 1;
        // Match Mesh.Build's diagonal b-c exactly; bilinear interpolation would float above some triangles.
        int i0 = u + v <= 1 ? a : d;
        float w0 = u + v <= 1 ? 1 - u - v : u + v - 1;
        float wb = u + v <= 1 ? u : 1 - v;
        float wc = u + v <= 1 ? v : 1 - u;
        float height = terrain.Vertices[i0].Position.Y * w0 + terrain.Vertices[b].Position.Y * wb + terrain.Vertices[c].Position.Y * wc;
        var normal = terrain.Vertices[i0].Normal * w0 + terrain.Vertices[b].Normal * wb + terrain.Vertices[c].Normal * wc;
        float moisture = water is null ? .15f : water.Moisture[i0] * w0 + water.Moisture[b] * wb + water.Moisture[c] * wc;
        bool wet = water is not null && (water.Coverage[a] > .05f || water.Coverage[b] > .05f || water.Coverage[c] > .05f || water.Coverage[d] > .05f);
        ClimateSample climate = default;
        if (terrain.Climate is { } data)
        {
            var s0 = data.Samples[i0];
            var sb = data.Samples[b];
            var sc = data.Samples[c];
            float Mix(float a, float b, float c) => a * w0 + b * wb + c * wc;
            climate = new(Mix(s0.Temperature, sb.Temperature, sc.Temperature),
                Mix(s0.Moisture, sb.Moisture, sc.Moisture), new(
                    Mix(s0.Biomes.Meadow, sb.Biomes.Meadow, sc.Biomes.Meadow),
                    Mix(s0.Biomes.WetForest, sb.Biomes.WetForest, sc.Biomes.WetForest),
                    Mix(s0.Biomes.Steppe, sb.Biomes.Steppe, sc.Biomes.Steppe),
                    Mix(s0.Biomes.Alpine, sb.Biomes.Alpine, sc.Biomes.Alpine),
                    Mix(s0.Biomes.Snow, sb.Biomes.Snow, sc.Biomes.Snow)));
        }
        float bank = water is null ? 0 : water.Bank[i0] * w0 + water.Bank[b] * wb + water.Bank[c] * wc;
        return (height, Vector3.Normalize(normal).Y, moisture, wet, climate, bank);
    }
}
