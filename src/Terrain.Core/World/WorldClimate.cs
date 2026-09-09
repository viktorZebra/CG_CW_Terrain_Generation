using Terrain.Core.Generation;

namespace Terrain.Core.World;

public readonly record struct WorldBiomeWeights(float Forest, float Desert, float Winter, float Swamp, float Steppe)
{
    public float this[int index] => index switch
    {
        0 => Forest,
        1 => Desert,
        2 => Winter,
        3 => Swamp,
        4 => Steppe,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

/// <summary>North (-Z) is cold, south (+Z) is dry; moisture creates local forest wetlands.</summary>
public sealed class WorldClimate
{
    private readonly GradientNoise borders;
    private readonly GradientNoise moisture;

    public WorldClimate(int seed)
    {
        borders = new(unchecked(seed ^ 0x47B29A13));
        moisture = new(unchecked(seed ^ 0x19D538A7));
    }

    public WorldBiomeWeights Sample(float x, float z)
    {
        // Two spatial scales perturb latitude continuously, without rotating cardinal directions.
        float latitude = z + borders.Perlin(x * .065f + 11, z * .06f - 17) * 7
            + borders.Perlin(x * .2f - 23, z * .17f + 5) * 1.5f;
        float winter = 1 - Smooth(-19, -9, latitude);
        float desert = Smooth(18, 26, latitude);
        float grassland = Smooth(5, 15, latitude);
        float middle = (1 - winter) * (1 - grassland);
        float wetness = moisture.Perlin(x * .16f + 8, z * .16f - 13) * .7f
            + moisture.Perlin(x * .39f - 19, z * .39f + 4) * .3f;
        float swamp = middle * Smooth(.04f, .22f, wetness);
        return new(middle - swamp, desert, winter, swamp, (1 - winter) * grassland - desert);
    }

    private static float Smooth(float from, float to, float value)
    {
        float t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
