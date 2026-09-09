using System.Numerics;

namespace Terrain.Core.Rendering;

/// <summary>A sun arc in the world XY plane, independent of model rotation.</summary>
public static class SunPath
{
    public static Vector3 Direction(float progress)
    {
        if (!float.IsFinite(progress))
            throw new ArgumentOutOfRangeException(nameof(progress));
        float angle = Math.Clamp(progress, 0, 1) * MathF.PI;
        return Vector3.Normalize(new(-MathF.Cos(angle), Math.Max(0, MathF.Sin(angle)), 0));
    }

    public static Vector3 WalkingSkyColor(float x, float y, float aspect, Vector3 light,
        Vector3 right, Vector3 up, Vector3 forward, float focal, float pixelSize)
    {
        var ray = Vector3.Normalize(right * ((2 * x - 1) * aspect / focal) + up * ((1 - 2 * y) / focal) + forward);
        float elevation = Math.Clamp(light.Y, 0, 1);
        var top = Vector3.Lerp(new(.12f, .10f, .22f), new(.07f, .19f, .34f), elevation);
        var horizon = Vector3.Lerp(new(.72f, .30f, .17f), new(.49f, .67f, .76f), elevation);
        var sky = Vector3.Lerp(horizon, top, Math.Clamp(ray.Y, 0, 1));
        if (ray.Y < 0)
            sky *= 1 + Math.Max(-.7f, ray.Y) * .45f;
        float distance = Vector3.Distance(ray, light);
        var warm = Vector3.Lerp(new(1, .42f, .12f), new(1, .88f, .56f), elevation);
        sky += warm * (.24f * MathF.Exp(-distance * distance / .018f));
        float edge = Math.Clamp((distance - .018f) / Math.Max(2 * pixelSize / focal, 1e-6f), 0, 1);
        float disk = 1 - edge * edge * (3 - 2 * edge);
        return Vector3.Clamp(Vector3.Lerp(sky, Vector3.Lerp(warm, Vector3.One, .5f), disk), Vector3.Zero, Vector3.One);
    }

    // x/y are viewport fractions (top-left origin). Distances use viewport height
    // so the disk stays circular on wide windows. Keep paired with SkyShader.
    public static Vector3 SkyColor(float x, float y, float aspect, Vector3 light, float pixelSize, Vector3? sunPosition = null)
    {
        float elevation = Math.Clamp(light.Y, 0, 1);
        var top = Vector3.Lerp(new(.12f, .10f, .22f), new(.07f, .19f, .34f), elevation);
        var horizon = Vector3.Lerp(new(.72f, .30f, .17f), new(.49f, .67f, .76f), elevation);
        float blend = Math.Clamp(y / .85f, 0, 1);
        var sky = Vector3.Lerp(top, horizon, blend * blend);
        var sun = sunPosition ?? new Vector3(light.X, light.Y, -1);
        if (sun.Z >= 0)
            return Vector3.Clamp(sky, Vector3.Zero, Vector3.One);
        var delta = new Vector2((x - (.5f + .42f * sun.X)) * aspect, y - (.65f - .53f * sun.Y));
        float distance = delta.Length();
        var warm = Vector3.Lerp(new(1, .42f, .12f), new(1, .88f, .56f), elevation);
        sky += warm * (.24f * MathF.Exp(-distance * distance / .018f));
        float edge = Math.Clamp((distance - .024f) / Math.Max(pixelSize, 1e-6f), 0, 1);
        float disk = 1 - edge * edge * (3 - 2 * edge);
        return Vector3.Clamp(Vector3.Lerp(sky, Vector3.Lerp(warm, Vector3.One, .5f), disk), Vector3.Zero, Vector3.One);
    }
}
