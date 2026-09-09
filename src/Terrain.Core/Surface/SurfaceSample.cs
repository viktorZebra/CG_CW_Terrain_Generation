using System.Numerics;

namespace Terrain.Core.Surface;

/// <summary>Local material properties before model transforms and lighting.</summary>
public readonly record struct SurfaceSample(float Height, float Slope, float Rock, float Snow, Vector3 Color);
