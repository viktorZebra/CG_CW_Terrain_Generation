namespace Terrain.Core.Hydrology;

public sealed record HydrologyOptions(float SeaLevel = .02f, float RiverFraction = .008f, bool SeparateSurface = false, float ChannelDepth = 0);
