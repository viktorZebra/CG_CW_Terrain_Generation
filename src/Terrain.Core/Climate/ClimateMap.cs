namespace Terrain.Core.Climate;

/// <summary>Nonnegative biome fractions summing to one; rock and banks are local overlays.</summary>
public readonly record struct BiomeWeights(float Meadow, float WetForest, float Steppe, float Alpine, float Snow);
public readonly record struct ClimateSample(float Temperature, float Moisture, BiomeWeights Biomes);

/// <summary>One sample per source height, before water geometry or visual height scaling.</summary>
public sealed record ClimateMap(int Width, int Height, ClimateSample[] Samples);
