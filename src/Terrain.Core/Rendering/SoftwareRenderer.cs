using System.Numerics;
using Terrain.Core.Models;
using Terrain.Core.Rendering.Shadows;

namespace Terrain.Core.Rendering;

/// <summary>Our own triangle rasterizer: edge functions, barycentric interpolation and Z-buffer.</summary>
public static class SoftwareRenderer
{
    public static Frame Render(Vertex[] vertices, int[] indices, Vector3 light, int width, int height, ShadowMap? shadows = null, bool softShadows = true, bool showSky = false, Vector3? skySun = null, Scene? skyScene = null)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var pixels = new byte[checked(width * height * 4)];
        var depth = new float[width * height];
        Array.Fill(depth, float.PositiveInfinity);
        var camera = skyScene?.ViewCamera ?? CameraPose.Default;
        var right = camera.WorldDirection(Vector3.UnitX);
        var up = camera.WorldDirection(Vector3.UnitY);
        var forward = camera.WorldDirection(-Vector3.UnitZ);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            Vector3 color;
            if (showSky && skyScene?.WorldTime is { } time)
            {
                float x = (i / 4 % width + .5f) / width, y = (i / 4 / width + .5f) / height;
                float focal = CameraPose.FocalLength * skyScene.Zoom;
                var ray = Vector3.Normalize(right * ((2 * x - 1) * width / height / focal) + up * ((1 - 2 * y) / focal) + forward);
                color = Terrain.Core.World.WorldClouds.Sky(ray, camera.Position, time, 1f / height / focal, skyScene.CloudSeed, skyScene.CloudTime);
            }
            else if (showSky && skyScene is { Walking: true })
                color = SunPath.WalkingSkyColor((i / 4 % width + .5f) / width, (i / 4 / width + .5f) / height,
                    (float)width / height, light, right, up, forward, CameraPose.FocalLength * skyScene.Zoom, 1f / height);
            else
                color = showSky
                    ? SunPath.SkyColor((i / 4 % width + .5f) / width, (i / 4 / width + .5f) / height, (float)width / height, light, 1f / height, skySun)
                    : new Vector3(18, 23, 29) / 255;
            pixels[i] = (byte)MathF.Round(color.Z * 255);
            pixels[i + 1] = (byte)MathF.Round(color.Y * 255);
            pixels[i + 2] = (byte)MathF.Round(color.X * 255);
            pixels[i + 3] = 255;
        }
        var screen = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i].Position;
            screen[i] = new((p.X + 1) * width / 2, (1 - p.Y) * height / 2, p.Z);
        }
        for (int triangle = 0; triangle < indices.Length; triangle += 3)
        {
            int ia = indices[triangle], ib = indices[triangle + 1], ic = indices[triangle + 2];
            var a = screen[ia];
            var b = screen[ib];
            var c = screen[ic];
            float area = Edge(a, b, c.X, c.Y);
            if (Math.Abs(area) < 1e-8f)
                continue;
            if (area < 0)
            {
                (ib, ic) = (ic, ib);
                (b, c) = (c, b);
                area = -area;
            }
            var shadowPlane = Vector3.Cross(vertices[ib].ShadowPosition - vertices[ia].ShadowPosition,
                vertices[ic].ShadowPosition - vertices[ia].ShadowPosition);
            var depthGradient = Math.Abs(shadowPlane.Z) > shadowPlane.Length() * 1e-5f
                ? new Vector2(-shadowPlane.X, -shadowPlane.Y) / shadowPlane.Z : Vector2.Zero;
            int x0 = Math.Max(0, (int)MathF.Ceiling(Math.Min(a.X, Math.Min(b.X, c.X)) - .5f));
            int x1 = Math.Min(width - 1, (int)MathF.Floor(Math.Max(a.X, Math.Max(b.X, c.X)) - .5f));
            int y0 = Math.Max(0, (int)MathF.Ceiling(Math.Min(a.Y, Math.Min(b.Y, c.Y)) - .5f));
            int y1 = Math.Min(height - 1, (int)MathF.Floor(Math.Max(a.Y, Math.Max(b.Y, c.Y)) - .5f));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float ea = Edge(b, c, x + .5f, y + .5f), eb = Edge(c, a, x + .5f, y + .5f), ec = Edge(a, b, x + .5f, y + .5f);
                    if (!Inside(ea, b, c) || !Inside(eb, c, a) || !Inside(ec, a, b))
                        continue;
                    float wa = ea / area, wb = eb / area, wc = ec / area;
                    float z = a.Z * wa + b.Z * wb + c.Z * wc;
                    int offset = y * width + x;
                    if (z < -1 || z > 1 || z >= depth[offset])
                        continue;
                    depth[offset] = z;
                    // Attributes interpolate with 1/w; screen-space depth remains affine.
                    wa *= vertices[ia].ReciprocalW;
                    wb *= vertices[ib].ReciprocalW;
                    wc *= vertices[ic].ReciprocalW;
                    float total = wa + wb + wc;
                    wa /= total;
                    wb /= total;
                    wc /= total;
                    var normal = vertices[ia].Normal * wa + vertices[ib].Normal * wb + vertices[ic].Normal * wc;
                    var color = vertices[ia].Color * wa + vertices[ib].Color * wb + vertices[ic].Color * wc;
                    float visibility = 1;
                    if (shadows is not null)
                    {
                        var shadowPosition = vertices[ia].ShadowPosition * wa + vertices[ib].ShadowPosition * wb + vertices[ic].ShadowPosition * wc;
                        float nDotL = normal.LengthSquared() > 1e-12f ? Vector3.Dot(Vector3.Normalize(normal), light) : 0;
                        visibility = shadows.Visibility(shadowPosition, nDotL, softShadows, depthGradient);
                    }
                    color = Vector3.Clamp(Scene.Shade(normal, color, light, visibility), Vector3.Zero, Vector3.One);
                    if (skyScene?.WorldTime is { } worldTime)
                    {
                        color *= Terrain.Core.World.DayNight.Exposure(worldTime);
                        float distance = Atmosphere.Distance((x + .5f) / width, (y + .5f) / height, (float)width / height, total, skyScene);
                        float focal = CameraPose.FocalLength * skyScene.Zoom;
                        var ray = Vector3.Normalize(right * (((2 * (x + .5f) / width - 1) * width / height) / focal)
                            + up * ((1 - 2 * (y + .5f) / height) / focal) + forward);
                        color = Vector3.Lerp(color, Terrain.Core.World.WorldClouds.Sky(ray, camera.Position, worldTime, 1f / height / focal, skyScene.CloudSeed, skyScene.CloudTime), Terrain.Core.World.DayNight.Fog(distance, skyScene.WorldViewDistance));
                    }
                    else if (skyScene is { FogDensity: > 0 })
                    {
                        float distance = Atmosphere.Distance((x + .5f) / width, (y + .5f) / height, (float)width / height, total, skyScene);
                        color = Vector3.Lerp(color, Atmosphere.Color(light), Atmosphere.Amount(distance, skyScene.FogDensity));
                    }
                    pixels[offset * 4] = (byte)MathF.Round(color.Z * 255);
                    pixels[offset * 4 + 1] = (byte)MathF.Round(color.Y * 255);
                    pixels[offset * 4 + 2] = (byte)MathF.Round(color.X * 255);
                }
        }
        return new(width, height, pixels);
    }

    /// <summary>Light pass: only interpolate and store depth. No colors, normals or lighting.</summary>
    public static float[] RenderDepth(Vector3[] lightPoints, int[] indices, int size)
    {
        var depth = new float[size * size];
        Array.Fill(depth, 1f);
        var screen = lightPoints.Select(p => new Vector3(p.X * size, p.Y * size, p.Z)).ToArray();
        for (int triangle = 0; triangle < indices.Length; triangle += 3)
        {
            var a = screen[indices[triangle]];
            var b = screen[indices[triangle + 1]];
            var c = screen[indices[triangle + 2]];
            float area = Edge(a, b, c.X, c.Y);
            if (Math.Abs(area) < 1e-8f)
                continue;
            if (area < 0)
            {
                (b, c) = (c, b);
                area = -area;
            }
            int x0 = Math.Max(0, (int)MathF.Ceiling(Math.Min(a.X, Math.Min(b.X, c.X)) - .5f));
            int x1 = Math.Min(size - 1, (int)MathF.Floor(Math.Max(a.X, Math.Max(b.X, c.X)) - .5f));
            int y0 = Math.Max(0, (int)MathF.Ceiling(Math.Min(a.Y, Math.Min(b.Y, c.Y)) - .5f));
            int y1 = Math.Min(size - 1, (int)MathF.Floor(Math.Max(a.Y, Math.Max(b.Y, c.Y)) - .5f));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float ea = Edge(b, c, x + .5f, y + .5f), eb = Edge(c, a, x + .5f, y + .5f), ec = Edge(a, b, x + .5f, y + .5f);
                    if (!Inside(ea, b, c) || !Inside(eb, c, a) || !Inside(ec, a, b))
                        continue;
                    float z = (a.Z * ea + b.Z * eb + c.Z * ec) / area;
                    int offset = y * size + x;
                    if (z >= 0 && z <= 1 && z < depth[offset])
                        depth[offset] = z;
                }
        }
        return depth;
    }
    private static float Edge(Vector3 a, Vector3 b, float x, float y) => (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
    // Top-left fill convention: shared edges belong to exactly one triangle.
    private static bool Inside(float value, Vector3 a, Vector3 b) => value > 0 || (value == 0 && (b.Y < a.Y || (b.Y == a.Y && b.X > a.X)));
}
