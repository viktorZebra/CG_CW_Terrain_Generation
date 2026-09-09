using System.Numerics;
using Terrain.Core.Generation;

namespace Terrain.Core.World;

public readonly record struct RiverSample(float Level, float Coverage, float Bank);

/// <summary>Seeded north-to-south channels with a non-increasing longitudinal water profile.</summary>
public sealed class WorldRivers
{
    private const float Step = .25f;
    private readonly Vector3[][] paths;
    public IReadOnlyList<Vector3> Sources
    {
        get;
    }

    internal WorldRivers(Vector3[] peaks, GradientNoise noise, Func<float, float, float> height)
    {
        paths = new Vector3[peaks.Length][];
        for (int r = 0; r < peaks.Length; r++)
        {
            var peak = peaks[r];
            float start = peak.Z + 2;
            int count = (int)MathF.Ceiling((WorldTerrain.HalfExtent + 4 - start) / Step) + 1;
            var path = paths[r] = new Vector3[count];
            float level = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                float z = start + i * Step;
                float x = peak.X + noise.Perlin(r * 13 + 4, z * .11f) * 3;
                level = Math.Min(level - .0003f * Step, height(x, z) - .025f);
                path[i] = new(x, level, z);
            }
        }
        Sources = Array.AsReadOnly(paths.Select(p => p[0]).ToArray());
    }

    public RiverSample Sample(float x, float z)
    {
        RiverSample best = new(0, -1, 0);
        foreach (var path in paths)
        {
            float position = (z - path[0].Z) / Step;
            if (position < 0 || position >= path.Length - 1)
                continue;
            int i = (int)position;
            var p = Vector3.Lerp(path[i], path[i + 1], position - i);
            float width = .16f + .10f * Math.Clamp(position / 100, 0, 1);
            float spring = Math.Clamp((z - path[0].Z) / 1.2f, 0, 1);
            width *= spring * spring * (3 - 2 * spring);
            float distance = Math.Abs(x - p.X);
            float coverage = Math.Min(width - distance, z - path[0].Z);
            float bank = Math.Clamp((width + .35f - distance) / .35f, 0, 1);
            bank = bank * bank * (3 - 2 * bank);
            // A spring gradually deepens into the channel, avoiding an abrupt cut at its source.
            float source = Math.Clamp((z - path[0].Z) / .4f, 0, 1);
            bank *= source * source * (3 - 2 * source);
            if (bank > best.Bank)
                best = new(p.Y, coverage, bank);
        }
        return best;
    }
}
