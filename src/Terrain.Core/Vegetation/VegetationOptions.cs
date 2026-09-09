namespace Terrain.Core.Vegetation;

public sealed record VegetationOptions(float Density = .55f, int MaxTrees = 500, int MaxRocks = 100, bool BiomeAware = false);
