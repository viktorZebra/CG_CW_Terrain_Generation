using System.Numerics;
using Terrain.Core.Rendering;

namespace Terrain.Core.World;

public readonly record struct ChunkDetail(ChunkKey Key, int Cells);

public static class WorldLod
{
    public const int NearCells = 128;
    public const int MediumCells = 64;

    public static ChunkDetail[] Select(CameraPose camera, IEnumerable<ChunkDetail>? previous = null)
    {
        var old = previous?.ToDictionary(p => p.Key, p => p.Cells) ?? new();
        return WorldStream.Select(camera).Select(key =>
        {
            // Distance to the chunk rectangle, so the camera's own chunk is always detailed.
            float dx = Math.Max(0, Math.Abs(camera.Position.X - (key.X * 2 + 1)) - 1);
            float dz = Math.Max(0, Math.Abs(camera.Position.Z - (key.Z * 2 + 1)) - 1);
            float distance = MathF.Sqrt(dx * dx + dz * dz);
            int prior = old.GetValueOrDefault(key, WorldTerrain.Cells);
            int cells = distance < (prior == NearCells ? 1.8f : 1.3f) ? NearCells
                : distance < (prior >= MediumCells ? 3.8f : 3.3f) ? MediumCells : WorldTerrain.Cells;
            return new ChunkDetail(key, cells);
        }).ToArray();
    }

    // All chunk boundaries share the same 32 segments. Boundary fans remove T-junctions
    // even when two adjacent chunks have different resolutions; no skirts hide cracks.
    public static int Index(int x, int z, int cells)
    {
        int ratio = cells / WorldTerrain.Cells;
        if (x == 0)
            z = z / ratio * ratio;
        else if (x == cells)
            z = (z + ratio - 1) / ratio * ratio;
        if (z == 0)
            x = x / ratio * ratio;
        else if (z == cells)
            x = (x + ratio - 1) / ratio * ratio;
        return z * (cells + 1) + x;
    }

    public static bool Weights(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out Vector3 weights)
    {
        float denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (Math.Abs(denominator) < 1e-12f)
        {
            weights = default;
            return false;
        }
        float u = ((b.Y - c.Y) * (point.X - c.X) + (c.X - b.X) * (point.Y - c.Y)) / denominator;
        float v = ((c.Y - a.Y) * (point.X - c.X) + (a.X - c.X) * (point.Y - c.Y)) / denominator;
        weights = new(u, v, 1 - u - v);
        return weights.X >= -1e-5f && weights.Y >= -1e-5f && weights.Z >= -1e-5f;
    }
}
