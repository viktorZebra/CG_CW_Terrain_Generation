using System.Numerics;
using Terrain.Core.Generation;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;
using Terrain.Core.Surface;

namespace Terrain.Core.Vegetation;

/// <summary>Seeded talus patches on and below exposed slopes, with smaller fragments at patch edges.</summary>
public static class RockScatter
{
    public static ObjectInstance[] Place(HeightMap map, Mesh terrain, WaterMap? water,
        IReadOnlyList<ObjectInstance> trees, DetailOptions options)
    {
        if (!float.IsFinite(options.RockDensity) || options.RockDensity is < 0 or > 1 || options.MaxRocks is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options));
        var random = new Random(unchecked(options.Seed ^ 0x391D27AB));
        var noise = new GradientNoise(unchecked(options.Seed ^ 0x6A219F03));
        var result = new List<ObjectInstance>();
        float step = 2f / (Math.Max(map.Width, map.Height) - 1);
        float width = (map.Width - 1) * step, depth = (map.Height - 1) * step;
        for (int i = 0; i < 10000 && result.Count < options.MaxRocks; i++)
        {
            float x = ((float)random.NextDouble() - .5f) * width;
            float z = ((float)random.NextDouble() - .5f) * depth;
            float chance = (float)random.NextDouble();
            float size = .009f + (float)random.NextDouble() * .025f;
            float yaw = (float)random.NextDouble() * MathF.Tau;
            float tint = .85f + (float)random.NextDouble() * .3f;
            if (Math.Abs(x) + size >= width / 2 || Math.Abs(z) + size >= depth / 2)
                continue;
            var s = ObjectScatter.Sample(map, terrain, water, x, z);
            if (s.Wet || s.Bank > .35f || s.Upward < .45f || s.Climate.Biomes.Snow > .08f || (terrain.Climate is null && s.Height > .85f))
                continue;
            float exposure = 1 - s.Upward;
            foreach (var offset in new Vector2[] { new(.06f, 0), new(-.06f, 0), new(0, .06f), new(0, -.06f) })
            {
                var nearby = ObjectScatter.Sample(map, terrain, water, x + offset.X, z + offset.Y);
                if (nearby.Height >= s.Height)
                    exposure = Math.Max(exposure, 1 - nearby.Upward);
            }
            float patch = Math.Clamp(.45f + noise.Perlin(x * 6.3f + 4, z * 6.3f - 7) * 2, 0, 1);
            float slope = Math.Clamp((exposure - .07f) / .3f, 0, 1);
            if (chance > options.RockDensity * slope * patch * .4f)
                continue;
            float distance(ObjectInstance o) => Vector2.Distance(new(x, z), new(o.Position.X, o.Position.Z));
            if (trees.Any(o => distance(o) < o.Size * .3f + size) || result.Any(o => distance(o) < .012f))
                continue;
            result.Add(new(ObjectKind.Rock, new(x, s.Height, z), size * (.65f + patch * .35f), yaw, tint));
        }
        return result.ToArray();
    }
}
