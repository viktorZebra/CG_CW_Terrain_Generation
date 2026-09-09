using System.Numerics;
namespace Terrain.Core.World;

/// <summary>Five minutes of daylight followed by three minutes of night.</summary>
public static class DayNight
{
    public const float DayDuration = 300;
    public const float NightDuration = 180;
    public const float CycleDuration = DayDuration + NightDuration;

    public static Vector3 Sun(float seconds)
    {
        float time = (seconds % CycleDuration + CycleDuration) % CycleDuration;
        float angle = (time < DayDuration ? time / DayDuration : 1 + (time - DayDuration) / NightDuration) * MathF.PI;
        return new(-MathF.Cos(angle), MathF.Sin(angle), 0);
    }
    public static float Daylight(float seconds) => Smooth(-.12f, .18f, Sun(seconds).Y);
    public static float Exposure(float seconds) => .28f + .72f * Daylight(seconds);
    public static Vector3 Light(float seconds)
    {
        var sun = Sun(seconds);
        return sun.Y >= 0 ? sun : -sun;
    }
    public static Vector3 Horizon(float seconds) => Vector3.Lerp(new(.018f, .027f, .065f),
        Vector3.Lerp(new(.72f, .30f, .17f), new(.49f, .67f, .76f), Math.Max(0, Sun(seconds).Y)), Daylight(seconds));
    public static float Fog(float distance, float end = 5.5f) => Smooth(end * .45f, end, distance);
    private static float Smooth(float a, float b, float v)
    {
        float t = Math.Clamp((v - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static readonly Vector3[] Craters = [new(-.3f, .2f, .28f), new(.25f, -.28f, .32f), new(.37f, .38f, .16f), new(-.4f, -.4f, .16f)];

    public static Vector3 Sky(Vector3 ray, float seconds, float pixelSize)
    {
        var sun = Sun(seconds);
        float day = Daylight(seconds);
        var top = Vector3.Lerp(new(.003f, .006f, .025f), new(.07f, .19f, .34f), day);
        var sky = Vector3.Lerp(Horizon(seconds), top, Math.Clamp(ray.Y, 0, 1));
        // Integer hash avoids CPU/GLSL sin-hash precision differences. Stars stay fixed in world space.
        var p = ray * 180;
        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y), z = (int)MathF.Floor(p.Z);
        uint hash = unchecked((uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)z * 83492791u);
        hash ^= hash >> 13;
        hash = unchecked(hash * 1274126177u);
        hash ^= hash >> 16;
        if (hash % 1000 < 9 && ray.Y > .04f)
        {
            var cell = p - new Vector3(x + .5f, y + .5f, z + .5f);
            float star = (1 - Smooth(.12f, .48f, cell.Length())) * (1 - day);
            sky += new Vector3(.7f, .8f, 1) * star;
        }
        float sunDistance = Vector3.Distance(ray, sun), moonDistance = Vector3.Distance(ray, -sun);
        float sunDisk = (1 - Smooth(.018f, .018f + pixelSize * 2, sunDistance)) * Smooth(-.03f, .01f, sun.Y);
        float moonDisk = (1 - Smooth(.024f, .024f + pixelSize * 2, moonDistance)) * (1 - day);
        sky += new Vector3(1, .6f, .22f) * (.18f * MathF.Exp(-sunDistance * sunDistance / .014f)) * day;
        sky = Vector3.Lerp(sky, new(1, .90f, .65f), sunDisk);
        if (moonDisk <= 0)
            return Vector3.Clamp(sky, Vector3.Zero, Vector3.One);
        var moon = -sun;
        var uv = new Vector2(Vector3.Dot(ray, new Vector3(moon.Y, -moon.X, 0)), ray.Z) / .024f;
        float crater = 0;
        foreach (var center in Craters)
            crater += 1 - Smooth(center.Z * .65f, center.Z, Vector2.Distance(uv, new(center.X, center.Y)));
        float sphere = MathF.Sqrt(Math.Max(0, 1 - uv.LengthSquared()));
        var moonColor = new Vector3(.72f, .79f, .86f) * (.72f + .28f * sphere - .12f * crater);
        sky = Vector3.Lerp(sky, moonColor, moonDisk);
        return Vector3.Clamp(sky, Vector3.Zero, Vector3.One);
    }
}
