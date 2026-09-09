using System.Numerics;
using Terrain.Core.Generation;
using Terrain.Core.Climate;

namespace Terrain.Core.Surface;

/// <summary>Deterministic terrain materials, independent of mesh topology and render backend.</summary>
public sealed class TerrainSurface(int seed = 0)
{
    // A separate random stream: appearance must never consume the terrain generator's RNG.
    private readonly GradientNoise noise = new(unchecked(seed ^ 0x51ED270B));

    public SurfaceSample Sample(Vector3 position, Vector3 normal, float water = 0, float moisture = 0, float bank = 0, ClimateSample? climate = null, bool snowPatches = false)
    {
        float height = Math.Clamp(position.Y, 0, 1);
        float upward = normal.LengthSquared() > 1e-12f
            ? Math.Clamp(Vector3.Normalize(normal).Y, 0, 1) : 1;
        float slope = 1 - upward;
        // Model-space coordinates preserve patch sizes when map resolution changes.
        float broad = noise.Perlin(position.X * 3.7f + 17.3f, position.Z * 3.7f - 9.1f);
        float detail = noise.Perlin(position.X * 19.1f - 3.4f, position.Z * 19.1f + 11.7f);
        float materialHeight = height + broad * .055f;

        var grass = Vector3.Lerp(new(.20f, .38f, .13f), new(.43f, .51f, .23f),
            Math.Clamp(.45f + broad * .8f + detail * .2f, 0, 1));
        grass = Vector3.Lerp(grass, new(.16f, .34f, .19f), Math.Clamp(moisture, 0, 1) * .55f);
        if (climate is { } local)
        {
            var weights = local.Biomes;
            // Snow is applied below, after exposed rock, so steep faces remain visible.
            grass = grass * weights.Meadow + new Vector3(.16f, .34f, .19f) * weights.WetForest +
                new Vector3(.58f, .51f, .28f) * weights.Steppe +
                new Vector3(.40f, .46f, .30f) * (weights.Alpine + weights.Snow);
        }
        var stone = Vector3.Lerp(new(.36f, .37f, .35f), new(.59f, .55f, .46f),
            Math.Clamp(.5f + broad * .5f + detail * .4f, 0, 1));
        float cliff = Smooth(.14f, .48f, slope);
        float alpine = climate is { } biome ? biome.Biomes.Alpine * .25f : Smooth(.58f, .86f, materialHeight);
        float rock = 1 - (1 - cliff) * (1 - alpine);
        var color = Vector3.Lerp(grass, stone, rock);
        float snow = (climate?.Biomes.Snow ?? Smooth(.81f, .97f, materialHeight)) * (1 - Smooth(.12f, .42f, slope));
        if (snowPatches)
            snow *= .5f + .5f * Smooth(-.35f, .35f, broad + detail * .4f);
        color = Vector3.Lerp(color, new(.92f, .95f, .96f), snow);
        color *= 1 + detail * .07f;

        float shore = Math.Clamp(bank, 0, 1) * (1 - cliff) * (1 - snow);
        color = Vector3.Lerp(color, new(.56f, .49f, .33f), shore * .8f);
        float wet = Math.Clamp(water, 0, 1);
        color = Vector3.Lerp(color, new(.10f, .36f, .46f), wet);
        return new(height, slope, rock * (1 - wet), snow * (1 - wet), Vector3.Clamp(color, Vector3.Zero, Vector3.One));
    }

    private static float Smooth(float start, float end, float value)
    {
        float t = Math.Clamp((value - start) / (end - start), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
