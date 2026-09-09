using System.Text.Json.Serialization;
using Terrain.Core.Generation;
using Terrain.Core.Surface;
using Terrain.Core.Erosion;
using Terrain.Core.Climate;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.Core.Vegetation;

namespace Terrain.Core.Persistence;

/// <summary>Portable scene: original heights and applied parameters, never external file paths.</summary>
public sealed record SceneDocument(
    [property: JsonRequired] int Version,
    [property: JsonRequired] int Width,
    [property: JsonRequired] int Height,
    [property: JsonRequired] float[] Heights,
    [property: JsonRequired] int SurfaceSeed,
    [property: JsonRequired] GenerationOptions? Generation,
    [property: JsonRequired] HydrologyOptions? Water,
    [property: JsonRequired] VegetationOptions? Vegetation,
    [property: JsonRequired] Scene View,
    [property: JsonRequired] bool OpenGl,
    Scene? Overview = null, CameraPose? WalkCamera = null, ClimateOptions? Climate = null, ShrubOptions? Shrubs = null, DetailOptions? Details = null, ErosionOptions? Erosion = null)
{
    public HeightMap CreateMap()
    {
        SceneFiles.Validate(this);
        var map = new HeightMap(Width, Height);
        Heights.CopyTo(map.Values, 0);
        return map;
    }
}
