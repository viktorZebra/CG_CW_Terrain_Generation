namespace Terrain.Core.Erosion;

public sealed record ErosionOptions(int Iterations = 16, float Strength = .35f, float Talus = .8f);
