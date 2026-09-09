using System.Numerics;

namespace Terrain.Core.Rendering;

/// <summary>A sun arc in the fixed camera's XY plane, independent of model rotation.</summary>
public static class SunPath
{
    public static Vector3 Direction(float progress)
    {
        if (!float.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
        float angle = Math.Clamp(progress, 0, 1) * MathF.PI;
        return Vector3.Normalize(new(-MathF.Cos(angle), Math.Max(0, MathF.Sin(angle)), 0));
    }

    // x/y are viewport fractions (top-left origin). Distances use viewport height
    // so the disk stays circular on wide windows. Keep paired with SkyShader.
    public static Vector3 SkyColor(float x, float y, float aspect, Vector3 light, float pixelSize)
    {
        float elevation = Math.Clamp(light.Y, 0, 1);
        var top = Vector3.Lerp(new(.12f, .10f, .22f), new(.07f, .19f, .34f), elevation);
        var horizon = Vector3.Lerp(new(.72f, .30f, .17f), new(.49f, .67f, .76f), elevation);
        float blend = Math.Clamp(y / .85f, 0, 1);
        var sky = Vector3.Lerp(top, horizon, blend * blend);
        var delta = new Vector2((x - (.5f + .42f * light.X)) * aspect, y - (.65f - .53f * light.Y));
        float distance = delta.Length();
        var warm = Vector3.Lerp(new(1, .42f, .12f), new(1, .88f, .56f), elevation);
        sky += warm * (.24f * MathF.Exp(-distance * distance / .018f));
        float edge = Math.Clamp((distance - .024f) / Math.Max(pixelSize, 1e-6f), 0, 1);
        float disk = 1 - edge * edge * (3 - 2 * edge);
        return Vector3.Clamp(Vector3.Lerp(sky, Vector3.Lerp(warm, Vector3.One, .5f), disk), Vector3.Zero, Vector3.One);
    }
}
