using System.Numerics;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;

namespace Terrain.Core.Rendering;

/// <summary>Triangle-accurate ground following. Only map boundaries limit movement; water uses its visible surface level.</summary>
public sealed class WalkingSurface(HeightMap map, WaterMap? water)
{
    public const float EyeHeight = .035f;
    public bool TryGround(float x, float z, float heightScale, out float ground)
    {
        ground = 0;
        float step = 2f / (Math.Max(map.Width, map.Height) - 1);
        float halfWidth = (map.Width - 1) * step / 2, halfDepth = (map.Height - 1) * step / 2;
        if (x < -halfWidth || x > halfWidth || z < -halfDepth || z > halfDepth)
            return false;
        float gx = Math.Clamp(x / step + (map.Width - 1) / 2f, 0, map.Width - 1);
        float gy = Math.Clamp(z / step + (map.Height - 1) / 2f, 0, map.Height - 1);
        int ix = Math.Min((int)gx, map.Width - 2), iy = Math.Min((int)gy, map.Height - 2);
        int a = iy * map.Width + ix, b = a + 1, c = a + map.Width, d = c + 1;
        float u = gx - ix, v = gy - iy;
        var heights = water?.Levels ?? map.Values;
        float h = u + v <= 1 ? heights[a] * (1 - u - v) + heights[b] * u + heights[c] * v
            : heights[d] * (u + v - 1) + heights[b] * (1 - v) + heights[c] * (1 - u);
        if (water?.SeparateGeometry == true)
        {
            float Blend(float[] values) => u + v <= 1 ? values[a] * (1 - u - v) + values[b] * u + values[c] * v
                : values[d] * (u + v - 1) + values[b] * (1 - v) + values[c] * (1 - u);
            float land = Blend(map.Values);
            h = Blend(water.Coverage) >= .5f ? Math.Max(land, h) : land;
        }
        ground = (h - .5f) * heightScale;
        return true;
    }

    public CameraPose? Spawn(float heightScale)
        => Anchor(new CameraPose(Vector3.Zero), heightScale);

    public CameraPose? Anchor(CameraPose camera, float scale)
        => TryGround(camera.Position.X, camera.Position.Z, scale, out float ground)
            ? camera with
            {
                Position = new(camera.Position.X, ground + EyeHeight, camera.Position.Z)
            } : null;

    public CameraPose Move(CameraPose camera, float distance, float scale)
    {
        float step = 2f / (Math.Max(map.Width, map.Height) - 1);
        float halfWidth = (map.Width - 1) * step / 2, halfDepth = (map.Height - 1) * step / 2;
        var next = camera.Move(distance);
        next = next with
        {
            Position = new(Math.Clamp(next.Position.X, -halfWidth, halfWidth), next.Position.Y,
            Math.Clamp(next.Position.Z, -halfDepth, halfDepth))
        };
        return Anchor(next, scale) ?? camera;
    }
}
