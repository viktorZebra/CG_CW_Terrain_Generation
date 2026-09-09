using System.Numerics;

namespace Terrain.Core.Rendering;

public sealed record CameraPose(Vector3 Position, float Yaw = 0, float Pitch = 0)
{
    public static CameraPose Default { get; } = new(new Vector3(0, 0, 4));
    public const float Near = .05f;
    public const float Far = 100;
    public const float FocalLength = 4 / 1.65f;
    public Vector3 Forward => new(MathF.Sin(Yaw * MathF.PI / 180), 0, -MathF.Cos(Yaw * MathF.PI / 180));

    public CameraPose Move(float distance) => this with { Position = Position + Forward * distance };
    public CameraPose Move(float forward, float sideways)
    {
        var input = new Vector2(sideways, forward);
        float distance = Math.Max(Math.Abs(forward), Math.Abs(sideways));
        if (input.LengthSquared() == 0)
            return this;
        input = Vector2.Normalize(input) * distance;
        var right = Vector3.Cross(Forward, Vector3.UnitY);
        return this with
        {
            Position = Position + Forward * input.Y + right * input.X
        };
    }
    public CameraPose Turn(float degrees) => this with { Yaw = ((Yaw + degrees) % 360 + 540) % 360 - 180 };
    public CameraPose Look(float yaw, float pitch) => Turn(yaw) with { Pitch = Math.Clamp(Pitch + pitch, -89, 89) };
    public Vector3 WorldDirection(Vector3 vector)
    {
        float pitch = Pitch * MathF.PI / 180, yaw = Yaw * MathF.PI / 180;
        vector = new(vector.X, vector.Y * MathF.Cos(pitch) - vector.Z * MathF.Sin(pitch), vector.Y * MathF.Sin(pitch) + vector.Z * MathF.Cos(pitch));
        return new(vector.X * MathF.Cos(yaw) - vector.Z * MathF.Sin(yaw), vector.Y, vector.X * MathF.Sin(yaw) + vector.Z * MathF.Cos(yaw));
    }
    public Vector3 ViewDirection(Vector3 vector)
    {
        float angle = Yaw * MathF.PI / 180;
        var p = new Vector3(vector.X * MathF.Cos(angle) + vector.Z * MathF.Sin(angle), vector.Y,
            -vector.X * MathF.Sin(angle) + vector.Z * MathF.Cos(angle));
        float pitch = Pitch * MathF.PI / 180;
        return new(p.X, p.Y * MathF.Cos(pitch) + p.Z * MathF.Sin(pitch), -p.Y * MathF.Sin(pitch) + p.Z * MathF.Cos(pitch));
    }
    public Vector3 ViewPosition(Vector3 world) => ViewDirection(world - Position);
}
