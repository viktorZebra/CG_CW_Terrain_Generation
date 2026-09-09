using System.Numerics;

namespace Terrain.Core.World;

/// <summary>Seeded soft cloud layer at a fixed world altitude, advected by a steady wind.</summary>
public static class WorldClouds
{
    public const float Altitude = 12;
    public static Vector2 Wind => new(.10f, .035f);

    private static float Smooth(float a, float b, float value)
    {
        float t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static float Hash(int x, int z, int seed)
    {
        uint h = unchecked((uint)x * 73856093u ^ (uint)z * 19349663u ^ (uint)seed);
        h ^= h >> 13;
        h = unchecked(h * 1274126177u);
        h ^= h >> 16;
        return (h & 0xffffff) / 16777215f;
    }

    private static float Noise(Vector2 p, int seed)
    {
        int x = (int)MathF.Floor(p.X), z = (int)MathF.Floor(p.Y);
        float u = Smooth(0, 1, p.X - x), v = Smooth(0, 1, p.Y - z);
        float a = Hash(x, z, seed) * (1 - u) + Hash(x + 1, z, seed) * u;
        float b = Hash(x, z + 1, seed) * (1 - u) + Hash(x + 1, z + 1, seed) * u;
        return a * (1 - v) + b * v;
    }

    public static float Density(Vector2 position, float seconds, int seed)
    {
        var p = (position - Wind * seconds) * .13f;
        float sum = Noise(p, seed) * .57f + Noise(p * 2.03f + new Vector2(13.2f, -7.1f), seed) * .27f
            + Noise(p * 4.11f + new Vector2(-3.4f, 19.7f), seed) * .11f
            + Noise(p * 8.21f + new Vector2(27.1f, 5.3f), seed) * .05f;
        return Smooth(.48f, .70f, sum);
    }

    public static float Opacity(Vector3 ray, Vector3 camera, float seconds, int seed)
    {
        if (ray.Y <= .025f || camera.Y >= Altitude)
            return 0;
        float distance = (Altitude - camera.Y) / ray.Y;
        var position = camera + ray * distance;
        return Density(new(position.X, position.Z), seconds, seed) * .78f * Smooth(.025f, .16f, ray.Y);
    }

    public static Vector3 Sky(Vector3 ray, Vector3 camera, float daySeconds, float pixelSize, int? seed, float cloudSeconds)
    {
        var sky = DayNight.Sky(ray, daySeconds, pixelSize);
        if (seed is not { } value)
            return sky;
        float opacity = Opacity(ray, camera, cloudSeconds, value);
        if (opacity <= 0)
            return sky;
        float daylight = DayNight.Daylight(daySeconds);
        float elevation = Math.Clamp(DayNight.Sun(daySeconds).Y, 0, 1);
        var daylightColor = Vector3.Lerp(new(.92f, .58f, .39f), new(.93f, .95f, .98f), Smooth(0, .4f, elevation));
        var color = Vector3.Lerp(new(.085f, .11f, .17f), daylightColor, daylight) * (.82f + opacity * .18f);
        return Vector3.Lerp(sky, color, opacity);
    }

    // Two exact 16-bit components avoid losing seed bits in float uniforms.
    public static Vector3 Parameters(int? seed, float seconds) => seed is { } value
        ? new(unchecked((uint)value) & 0xffff, unchecked((uint)value) >> 16, seconds) : new(-1, 0, 0);
}
