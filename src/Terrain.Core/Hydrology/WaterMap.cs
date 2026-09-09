namespace Terrain.Core.Hydrology;

/// <summary>Derived surface; source heights are never modified. Flow counts assume uniform rain.</summary>
public sealed record WaterMap(int Width, int Height, float[] Levels, int[] Downstream,
    int[] Accumulation, bool[] StandingWater, float[] Coverage, float[] Moisture, float[] Bank, bool SeparateGeometry = false);
