using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering;

namespace Terrain.Core.Rendering.Shadows;

public sealed class ShadowMap(ShadowProjection projection, int size, float[] depth)
{
    public ShadowProjection Projection { get; } = projection;
    public int Size { get; } = size;
    public ReadOnlyMemory<float> Depth => depth;

    public static ShadowMap Build(Mesh mesh, Scene scene)
    {
        if (scene.ShadowResolution < 16 || scene.ShadowResolution > 4096)
            throw new ArgumentOutOfRangeException(nameof(scene.ShadowResolution));
        var projection = ShadowProjection.Create(mesh, scene);
        var points = mesh.Vertices.Select(v => projection.Project(scene.WorldPosition(v.Position))).ToArray();
        return new(projection, scene.ShadowResolution, SoftwareRenderer.RenderDepth(points, mesh.Indices, scene.ShadowResolution));
    }

    public static float Bias(float normalDotLight) => .0015f + .004f * (1 - Math.Clamp(normalDotLight, 0, 1));

    public float Visibility(Vector3 p, float normalDotLight, bool soft, Vector2 depthGradient = default)
    {
        if (p.X < 0 || p.X >= 1 || p.Y < 0 || p.Y >= 1 || p.Z < 0 || p.Z > 1)
            return 1;
        int cx = (int)MathF.Floor(p.X * Size), cy = (int)MathF.Floor(p.Y * Size);
        int radius = soft ? 1 : 0, visible = 0, count = 0;
        // Percentage-closer filtering: average comparisons, never average depths.
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx, y = cy + dy;
                // Compare against the receiver plane at the texel centre, not at the
                // screen pixel's UV. This avoids staircase acne on steep triangles.
                var delta = new Vector2((x + .5f) / Size - p.X, (y + .5f) / Size - p.Y);
                float reference = p.Z + Vector2.Dot(depthGradient, delta) - Bias(normalDotLight);
                if (x < 0 || x >= Size || y < 0 || y >= Size || reference <= depth[y * Size + x])
                    visible++;
                count++;
            }
        return (float)visible / count;
    }
}
