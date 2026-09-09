
namespace Terrain.Core.Generation;

public sealed record GenerationOptions(GeneratorKind Kind, int Size = 129, int Seed = 42,
    int Hills = 200, bool Smooth = false, bool Valley = false, int Octaves = 5,
    float Frequency = 4, float Roughness = 0.5f);
