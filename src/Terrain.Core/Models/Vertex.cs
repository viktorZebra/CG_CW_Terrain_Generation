using System.Numerics;

namespace Terrain.Core.Models;

public readonly record struct Vertex(Vector3 Position, Vector3 Normal, Vector3 Color, Vector3 ShadowPosition = default);
