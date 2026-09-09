using System.Numerics;
using Terrain.Core.Generation;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;

namespace Terrain.Core.Vegetation;

/// <summary>Independent low vegetation in clearings, away from shores and existing objects.</summary>
public static class ShrubScatter
{
    public static ObjectInstance[] Place(HeightMap map, Mesh terrain, WaterMap? water,
        IReadOnlyList<ObjectInstance> objects, int surfaceSeed, ShrubOptions options)
    {
        if (!float.IsFinite(options.Density) || options.Density is < 0 or > 1 || options.MaxShrubs is < 0 or > 1500)
            throw new ArgumentOutOfRangeException(nameof(options));
        var result = new List<ObjectInstance>();
        if (options.Density == 0 || options.MaxShrubs == 0)
            return [];
        var random = new Random(unchecked(options.Seed ^ 0x24B85A13));
        var patches = new GradientNoise(unchecked(options.Seed ^ 0x72C94311));
        var forest = new GradientNoise(unchecked(surfaceSeed ^ 0x32AE1529));
        float step = 2f / (Math.Max(map.Width, map.Height) - 1);
        float width = (map.Width - 1) * step, depth = (map.Height - 1) * step;
        for (int attempt = 0; attempt < 8000 && result.Count < options.MaxShrubs; attempt++)
        {
            float x = (float)random.NextDouble() * width - width / 2;
            float z = (float)random.NextDouble() * depth - depth / 2;
            float chance = (float)random.NextDouble();
            float size = .025f + (float)random.NextDouble() * .02f;
            float yaw = (float)random.NextDouble() * MathF.Tau;
            float tint = .85f + (float)random.NextDouble() * .3f;
            var kind = random.Next(2) == 0 ? ObjectKind.RoundShrub : ObjectKind.WideShrub;
            float radius = size * .6f;
            if (Math.Abs(x) + radius >= width / 2 || Math.Abs(z) + radius >= depth / 2)
                continue;
            var sample = ObjectScatter.Sample(map, terrain, water, x, z);
            bool Excluded(float px, float pz)
            {
                var s = ObjectScatter.Sample(map, terrain, water, px, pz);
                return s.Wet || s.Bank > .15f || s.Upward < .8f ||
                    (terrain.Climate is not null ? s.Climate.Biomes.Snow > .05f : s.Height > .78f);
            }
            if (Excluded(x, z) || Excluded(x + radius, z) || Excluded(x - radius, z) ||
                Excluded(x, z + radius) || Excluded(x, z - radius))
                continue;
            var b = sample.Climate.Biomes;
            float habitat = terrain.Climate is null ? .08f :
                .08f * b.Meadow + .16f * b.WetForest + .015f * b.Steppe + .025f * b.Alpine;
            float cluster = Math.Clamp(.5f + patches.Perlin(x * 5.3f + 8, z * 5.3f - 4) * 1.5f, 0, 1);
            float forestCluster = Math.Clamp(.45f + forest.Perlin(x * 3.2f + 5.3f, z * 3.2f - 2.7f) * 1.8f, 0, 1);
            float edge = 1 - Math.Abs(forestCluster - .45f);
            if (chance > options.Density * habitat * cluster * edge)
                continue;
            bool Nearby(ObjectInstance item, float gap) => Vector2.DistanceSquared(new(x, z), new(item.Position.X, item.Position.Z)) < gap * gap;
            if (objects.Any(o => Nearby(o, o.Size * .4f + radius + .01f)) || result.Any(o => Nearby(o, .035f)))
                continue;
            result.Add(new(kind, new(x, sample.Height, z), size, yaw, tint));
        }
        return result.ToArray();
    }
}
