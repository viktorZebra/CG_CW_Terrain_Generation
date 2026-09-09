namespace Terrain.Core.Climate;

/// <summary>Stylized climate in normalized units, not degrees Celsius or measured rainfall.</summary>
public sealed record ClimateOptions(float Temperature = .7f, float Moisture = .5f, int Seed = 0);
