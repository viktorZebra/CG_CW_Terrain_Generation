using System.Numerics;
using Terrain.Core.Models;

namespace Terrain.Core.Hydrology;

/// <summary>One drainage pass, then bounded channel carving and a separately clipped water surface.</summary>
public static class WaterGeometry
{
    public static (HeightMap Ground, WaterMap Water) Prepare(HeightMap source, WaterMap input, HydrologyOptions options)
    {
        var ground = new HeightMap(source.Width, source.Height);
        source.Values.CopyTo(ground.Values, 0);
        var levels = (float[])input.Levels.Clone();
        var coverage = (float[])input.Coverage.Clone();
        int count = source.Values.Length, width = source.Width;
        int threshold = Math.Max(8, (int)MathF.Ceiling(count * options.RiverFraction));
        // Rasterize continuous capsules along D8 edges, not isolated discs at cells.
        for (int i = 0; i < count; i++)
        {
            int to = input.Downstream[i];
            if (to < 0 || input.Accumulation[i] < threshold || input.StandingWater[i])
                continue;
            var a = new Vector2(i % width, i / width);
            var b = new Vector2(to % width, to / width);
            float radius = Math.Clamp(Math.Max(width, source.Height) / 129f * (.65f + .35f * MathF.Log2((float)input.Accumulation[i] / threshold)), .8f, Math.Max(1, Math.Max(width, source.Height) / 43f));
            int reach = (int)MathF.Ceiling(radius + 1);
            for (int y = Math.Max(0, (int)Math.Min(a.Y, b.Y) - reach); y <= Math.Min(source.Height - 1, (int)Math.Max(a.Y, b.Y) + reach); y++)
                for (int x = Math.Max(0, (int)Math.Min(a.X, b.X) - reach); x <= Math.Min(width - 1, (int)Math.Max(a.X, b.X) + reach); x++)
                {
                    int j = y * width + x;
                    if (input.StandingWater[j])
                        continue;
                    float t = Math.Clamp(Vector2.Dot(new Vector2(x, y) - a, b - a) / Vector2.DistanceSquared(a, b), 0, 1);
                    float distance = Vector2.Distance(new(x, y), Vector2.Lerp(a, b, t));
                    float mask = Math.Clamp(radius + .5f - distance, 0, 1);
                    if (mask <= 0)
                        continue;
                    float level = input.Levels[i] * (1 - t) + input.Levels[to] * t;
                    // Do not spread the ribbon through an adjacent ridge.
                    if (source.Values[j] > level + .025f)
                        continue;
                    coverage[j] = Math.Max(coverage[j], mask);
                    if (options.ChannelDepth > 0)
                    {
                        float profile = Math.Clamp(1 - distance / (radius + .5f), 0, 1);
                        profile = profile * profile * (3 - 2 * profile);
                        ground.Values[j] = Math.Min(ground.Values[j], Math.Max(0, level - options.ChannelDepth * profile));
                        if (input.Accumulation[j] < threshold)
                            levels[j] = Math.Min(levels[j], level);
                    }
                }
        }
        // Dry samples use actual ground. A small fixed separation prevents coincident river triangles.
        for (int i = 0; i < count; i++)
        {
            if (coverage[i] <= 0)
                levels[i] = ground.Values[i];
            else
                levels[i] += .0005f;
        }
        // Expand the exclusion mask to include the new ribbon; retain broad moisture from drainage.
        var bank = new float[count];
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                bank[i] = input.Bank[i];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < source.Height && coverage[ny * width + nx] > .5f)
                            bank[i] = Math.Max(bank[i], .7f * (1 - coverage[i]));
                    }
            }
        return (ground, input with
        {
            Levels = levels,
            Coverage = coverage,
            Bank = bank,
            SeparateGeometry = true
        });
    }

    public static Mesh Append(Mesh terrain, HeightMap ground, WaterMap water)
    {
        var vertices = new List<Vertex>(terrain.Vertices);
        var indices = new List<int>(terrain.Indices);
        // Each water triangle is clipped against both the coverage contour and the shoreline depth.
        for (int y = 0; y < ground.Height - 1; y++)
            for (int x = 0; x < ground.Width - 1; x++)
            {
                int a = y * ground.Width + x, b = a + 1, c = a + ground.Width, d = c + 1;
                Triangle(a, c, b);
                Triangle(b, c, d);
            }
        void Triangle(int a, int b, int c)
        {
            var polygon = new List<Point>(3) { At(a), At(b), At(c) };
            polygon = Clip(polygon, p => p.Coverage - .5f);
            polygon = Clip(polygon, p => p.Depth);
            if (polygon.Count < 3)
                return;
            int start = vertices.Count;
            var normal = Vector3.Cross(polygon[1].Position - polygon[0].Position, polygon[2].Position - polygon[0].Position);
            if (normal.LengthSquared() < 1e-16f)
                return;
            normal = Vector3.Normalize(normal);
            foreach (var p in polygon)
                vertices.Add(new(p.Position, normal, new(.10f, .36f, .46f)));
            for (int n = 1; n < polygon.Count - 1; n++)
            {
                indices.Add(start);
                indices.Add(start + n);
                indices.Add(start + n + 1);
            }
        }
        Point At(int i)
        {
            var p = terrain.Vertices[i].Position;
            p.Y = water.Levels[i];
            return new(p, water.Coverage[i], water.Levels[i] - ground.Values[i]);
        }
        return terrain with
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray()
        };
    }
    private readonly record struct Point(Vector3 Position, float Coverage, float Depth);
    private static List<Point> Clip(List<Point> polygon, Func<Point, float> value)
    {
        var output = new List<Point>();
        if (polygon.Count == 0)
            return output;
        var prev = polygon[^1];
        float pv = value(prev);
        foreach (var current in polygon)
        {
            float cv = value(current);
            if ((pv >= 0) != (cv >= 0))
            {
                float t = pv / (pv - cv);
                output.Add(new(Vector3.Lerp(prev.Position, current.Position, t), prev.Coverage + (current.Coverage - prev.Coverage) * t, prev.Depth + (current.Depth - prev.Depth) * t));
            }
            if (cv >= 0)
                output.Add(current);
            prev = current;
            pv = cv;
        }
        return output;
    }
}
