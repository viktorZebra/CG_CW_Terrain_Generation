using System.Numerics;

namespace Terrain.Core.Vegetation;

public readonly record struct ObjectInstance(ObjectKind Kind, Vector3 Position, float Size, float Yaw, float Tint);
