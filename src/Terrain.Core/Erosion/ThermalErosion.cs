using Terrain.Core.Models;

namespace Terrain.Core.Erosion;

/// <summary>Conservative synchronous transport across each undirected grid edge once per iteration.</summary>
public static class ThermalErosion
{
    public static HeightMap Apply(HeightMap source, ErosionOptions options)
    {
        if (options.Iterations is < 0 or > 60 || !float.IsFinite(options.Strength) || options.Strength is < 0 or > .5f ||
            !float.IsFinite(options.Talus) || options.Talus is < .1f or > 3)
            throw new ArgumentOutOfRangeException(nameof(options));
        var result = new HeightMap(source.Width, source.Height);
        source.Values.CopyTo(result.Values, 0);
        var delta = new float[source.Values.Length];
        float step = 2f / (Math.Max(source.Width, source.Height) - 1);
        (int X, int Y)[] neighbors = [(1, 0), (0, 1), (1, 1), (-1, 1)];
        for (int iteration = 0; iteration < options.Iterations; iteration++)
        {
            Array.Clear(delta);
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                    foreach (var (dx, dy) in neighbors)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= source.Width || ny >= source.Height)
                            continue;
                        int a = y * source.Width + x, b = ny * source.Width + nx;
                        float difference = result.Values[a] - result.Values[b];
                        float excess = Math.Max(0, Math.Abs(difference) - options.Talus * step * (dx != 0 && dy != 0 ? MathF.Sqrt(2) : 1));
                        float transfer = MathF.CopySign(excess * options.Strength / 8, difference);
                        delta[a] -= transfer;
                        delta[b] += transfer;
                    }
            for (int i = 0; i < delta.Length; i++)
                result.Values[i] += delta[i];
        }
        return result;
    }
}
