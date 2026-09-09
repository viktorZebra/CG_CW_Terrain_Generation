using System.Numerics;
using Terrain.Core.Rendering;

namespace Terrain.Core.World;

public static class WorldAtlas
{
    // North is -Z, right is +X, matching the default camera heading.
    public static Vector2 ToMap(Vector3 position) => new(
        Math.Clamp((position.X + WorldTerrain.HalfExtent) / (2 * WorldTerrain.HalfExtent), 0, 1),
        Math.Clamp((position.Z + WorldTerrain.HalfExtent) / (2 * WorldTerrain.HalfExtent), 0, 1));

    public static Frame Build(WorldTerrain terrain, CancellationToken cancellation)
    {
        const int size = 384;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < size; x++)
            {
                float wx = ((x + .5f) / size * 2 - 1) * WorldTerrain.HalfExtent;
                float wz = ((y + .5f) / size * 2 - 1) * WorldTerrain.HalfExtent;
                var sample = terrain.Sample(wx, wz);
                float slope = terrain.Sample(wx - .12f, wz - .12f).Height - terrain.Sample(wx + .12f, wz + .12f).Height;
                var color = sample.Water ? new Vector3(.13f, .29f, .31f) : sample.Color * Math.Clamp(.88f + slope * .6f, .55f, 1.15f);
                color = Vector3.Clamp(color, Vector3.Zero, Vector3.One);
                int i = (y * size + x) * 4;
                pixels[i] = (byte)(color.Z * 255);
                pixels[i + 1] = (byte)(color.Y * 255);
                pixels[i + 2] = (byte)(color.X * 255);
                pixels[i + 3] = 255;
            }
        }
        return new(size, size, pixels);
    }
}
