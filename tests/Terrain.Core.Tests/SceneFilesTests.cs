using System.Numerics;
using System.Text;
using Terrain.Core.Generation;
using Terrain.Core.Climate;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;
using Terrain.Core.Persistence;
using Terrain.Core.Rendering;
using Terrain.Core.Vegetation;
using Xunit;

namespace Terrain.Core.Tests;

public sealed class SceneFilesTests
{
    private static SceneDocument Document()
    {
        var options = new GenerationOptions(GeneratorKind.Perlin, 33, Seed: -123);
        var map = Generators.Generate(options);
        return new(1, map.Width, map.Height, map.Values, -123, options, new HydrologyOptions(), new VegetationOptions(.7f),
            new Scene(12, -34, 56, Zoom: 1.3f, PanX: .2f, PanY: -.1f, Light: SunPath.Direction(.7f), ShowSky: true,
                Camera: new CameraPose(new Vector3(1, 2, 3), 27)), true);
    }

    [Fact]
    public async Task RoundTripPreservesHeightsCameraLightAndExactGeometry()
    {
        var source = Document();
        using var stream = new MemoryStream();
        await SceneFiles.WriteAsync(stream, source);
        stream.Position = 0;
        var restored = await SceneFiles.ReadAsync(stream);
        Assert.Equal(source.Heights, restored.Heights);
        Assert.Equal(source.View, restored.View);
        Assert.Equal(source.Generation, restored.Generation);
        Assert.Equal(source.Water, restored.Water);
        Assert.Equal(source.Vegetation, restored.Vegetation);
        Assert.Equal(source.SurfaceSeed, restored.SurfaceSeed);
        Assert.Equal(source.OpenGl, restored.OpenGl);
        var before = Mesh.Build(source.CreateMap(), source.SurfaceSeed, source.Water, source.Vegetation);
        var after = Mesh.Build(restored.CreateMap(), restored.SurfaceSeed, restored.Water, restored.Vegetation);
        Assert.Equal(before.Vertices, after.Vertices);
        Assert.Equal(before.Indices, after.Indices);
    }

    [Fact]
    public async Task ClimateRoundTripAndLegacyPaletteRemainReproducible()
    {
        foreach (int version in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })
        {
            var source = Document() with
            {
                Version = version,
                Details = version >= 6 ? new(Seed: 78) : null,
                Erosion = version >= 9 ? new() : null,
                Water = version >= 8 ? new(SeparateSurface: true, ChannelDepth: .02f) : new(),
                View = Document().View with
                {
                    FogDensity = version >= 7 ? .1f : 0
                },
                Shrubs = version >= 5 ? new(.7f, 150, 918) : null,
                Vegetation = new(BiomeAware: version >= 4),
                Climate = version >= 3 ? new(.32f, .84f, -789) : null
            };
            using var stream = new MemoryStream();
            await SceneFiles.WriteAsync(stream, source);
            stream.Position = 0;
            var restored = await SceneFiles.ReadAsync(stream);
            Assert.Equal(source.Climate, restored.Climate);
            Assert.Equal(source.Shrubs, restored.Shrubs);
            var before = Mesh.Build(source.CreateMap(), source.SurfaceSeed, source.Water, source.Vegetation, source.Climate, source.Shrubs, source.Details, source.Erosion);
            var after = Mesh.Build(restored.CreateMap(), restored.SurfaceSeed, restored.Water, restored.Vegetation, restored.Climate, restored.Shrubs, restored.Details, restored.Erosion);
            Assert.Equal(before.Vertices, after.Vertices);
            if (version < 3)
                Assert.Null(after.Climate);
        }
        Assert.Throws<InvalidDataException>(() => SceneFiles.Validate(Document() with { Version = 3, Vegetation = new(BiomeAware: true) }));
        Assert.Throws<InvalidDataException>(() => SceneFiles.Validate(Document() with { Climate = new() }));
        Assert.Throws<InvalidDataException>(() => SceneFiles.Validate(Document() with { Version = 3, Climate = new(float.NaN) }));
        Assert.Throws<InvalidDataException>(() => SceneFiles.Validate(Document() with { Version = 3, Climate = new(Moisture: 2) }));
    }

    [Fact]
    public async Task ImportedRectangularMapNeedsNoGeneratorOrExternalImage()
    {
        var source = Document() with
        {
            Width = 3,
            Height = 2,
            Heights = [0, .1f, .23f, .57f, .82f, 1],
            Generation = null,
            Water = null,
            Vegetation = null,
            OpenGl = false
        };
        using var stream = new MemoryStream();
        await SceneFiles.WriteAsync(stream, source);
        stream.Position = 0;
        var restored = await SceneFiles.ReadAsync(stream);
        Assert.Equal(source.Heights, restored.CreateMap().Values);
        Assert.Null(restored.Water);
        Assert.Null(restored.Vegetation);
        Assert.False(restored.OpenGl);
    }

    [Fact]
    public void ValidationRejectsUnsupportedVersionsAndInvalidRenderInputs()
    {
        var source = Document();
        foreach (var invalid in new[]
        {
            source with { Version = 99 }, source with { Heights = [0] }, source with { Width = int.MaxValue },
            source with { View = source.View with { HeightScale = 0 } },
            source with { View = source.View with { Camera = new CameraPose(new Vector3(float.NaN)) } },
            source with { Water = new(float.PositiveInfinity) }, source with { Vegetation = new(2) }
        })
            Assert.Throws<InvalidDataException>(() => SceneFiles.Validate(invalid));
    }

    [Fact]
    public async Task WalkingModeAndOverviewRoundTripAlongsideVersionOneFiles()
    {
        var source = Document();
        var walking = source with
        {
            Version = 2,
            Overview = source.View,
            View = new Scene(0, 0, 0, Camera: new CameraPose(new Vector3(0, .2f, 0), 120, 45), Walking: true),
            WalkCamera = new CameraPose(new Vector3(0, .2f, 0), 120, 45)
        };
        using var stream = new MemoryStream();
        await SceneFiles.WriteAsync(stream, walking);
        stream.Position = 0;
        var restored = await SceneFiles.ReadAsync(stream);
        Assert.Equal(walking.View, restored.View);
        Assert.Equal(walking.Overview, restored.Overview);
        Assert.Equal(walking.WalkCamera, restored.WalkCamera);
        SceneFiles.Validate(source); // v1 remains supported
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"version\":1,")]
    public async Task TruncatedOrIncompleteFilesAreRejected(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await Assert.ThrowsAsync<InvalidDataException>(() => SceneFiles.ReadAsync(stream));
    }
}
