using Avalonia.Platform.Storage;
using Terrain.Core.Generation;
using Terrain.Core.Surface;
using Terrain.Core.Erosion;
using Terrain.Core.Climate;
using Terrain.Core.Hydrology;
using Terrain.Core.Models;
using Terrain.Core.Persistence;
using Terrain.Core.Vegetation;

namespace Terrain.App.Views;

public sealed partial class MainWindow
{
    private ErosionOptions? appliedErosion;
    private DetailOptions? appliedDetails;
    private ShrubOptions? appliedShrubs;
    private ClimateOptions? appliedClimate;
    private HydrologyOptions? appliedWater;
    private VegetationOptions? appliedForest;
    private GenerationOptions? appliedGeneration;
    private bool restoringProject;

    internal SceneDocument CaptureProject()
    {
        if (worldActive)
            throw new InvalidOperationException("Вернитесь к своей карте для сохранения проекта.");
        if (map is null)
            throw new InvalidOperationException("Сначала создайте или загрузите карту.");
        return new(SceneFiles.CurrentVersion, map.Width, map.Height, (float[])map.Values.Clone(),
            currentSurfaceSeed, appliedGeneration, appliedWater, appliedForest, scene, UseOpenGl, overviewScene, scene.Walking ? scene.ViewCamera : walkingCamera, appliedClimate, appliedShrubs, appliedDetails, appliedErosion);
    }

    internal async Task RestoreProject(SceneDocument document)
    {
        // Validate and build before touching the current map or UI controls.
        int request = ++mapRequest;
        var result = await Task.Run(() =>
        {
            var source = document.CreateMap();
            return (source, terrain: Mesh.Build(source, document.SurfaceSeed, document.Water, document.Vegetation, document.Climate, document.Shrubs, document.Details, document.Erosion));
        });
        if (closed || request != mapRequest)
            return;
        restoringProject = true;
        bool wasInitialized = initialized;
        initialized = false;
        try
        {
            SetMap(result.source, result.terrain, 0, document.SurfaceSeed, document.Water, document.Vegetation, document.Generation, document.Climate, document.Shrubs, document.Details, document.Erosion);
            if (document.Generation is { } options)
            {
                generator.SelectedIndex = (int)options.Kind;
                size.Value = options.Size;
                seed.Value = options.Seed;
                hills.Value = options.Hills;
                octaves.Value = options.Octaves;
                frequency.Value = (decimal)options.Frequency;
                roughness.Value = options.Roughness;
                smooth.IsChecked = options.Smooth;
                valley.IsChecked = options.Valley;
            }
            else
                seed.Value = document.SurfaceSeed;
            erosionEnabled.IsChecked = document.Erosion is not null;
            erosionIterations.Value = document.Erosion?.Iterations ?? 16;
            erosionStrength.Value = (document.Erosion?.Strength ?? .35f) * 100;
            terrainDetails.IsChecked = document.Details is not null;
            screeDensity.Value = (document.Details?.RockDensity ?? .5f) * 100;
            shrubs.IsChecked = document.Shrubs is not null;
            shrubDensity.Value = (document.Shrubs?.Density ?? .5f) * 100;
            biomes.IsChecked = document.Climate is not null;
            climateTemperature.Value = (document.Climate?.Temperature ?? .7f) * 100;
            climateMoisture.Value = (document.Climate?.Moisture ?? .5f) * 100;
            separateWater.IsChecked = document.Water?.SeparateSurface == true;
            carveChannels.IsChecked = document.Water?.ChannelDepth > 0;
            channelDepth.Value = (document.Water?.ChannelDepth ?? .02f) * 1000;
            rivers.IsChecked = document.Water is not null;
            if (document.Water is { } water)
            {
                seaLevel.Value = (decimal)water.SeaLevel;
                riverDensity.Value = Math.Log(water.RiverFraction / .04) / Math.Log(.72);
            }
            vegetation.IsChecked = document.Vegetation is not null;
            if (document.Vegetation is { } forest)
                forestDensity.Value = forest.Density * 100;
            fogEnabled.IsChecked = document.View.FogDensity > 0;
            fogDensity.Value = document.View.FogDensity * 200;
            rotationX.Value = document.View.RotationX;
            rotationY.Value = document.View.RotationY;
            rotationZ.Value = document.View.RotationZ;
            relief.Value = document.View.HeightScale;
            shadowQuality.SelectedIndex = document.View.ShadowResolution <= 256 ? 0 : document.View.ShadowResolution <= 512 ? 1 : 2;
            shadows.IsChecked = document.View.Shadows;
            softShadows.IsChecked = document.View.SoftShadows;
            softShadows.IsEnabled = document.View.Shadows;
            var light = document.View.LightDirection;
            sunPosition.Value = Math.Clamp(Math.Atan2(light.Y, -light.X) / Math.PI * 100, 0, 100);
            UseOpenGl = document.OpenGl;
            // Control change handlers may update the intermediate scene; the saved view wins.
            scene = document.View;
            overviewScene = document.Overview;
            walkingCamera = document.WalkCamera;
            walkingSurface = null;
            SyncNavigationControls();
        }
        finally
        {
            initialized = wasInitialized;
            restoringProject = false;
        }
        if (scene.Walking)
            RefreshWalkingSurface();
        Redraw();
    }

    private static FilePickerFileType ProjectFileType => new("Проект ландшафта") { Patterns = ["*.terrain.json"] };

    private async Task SaveProject()
    {
        try
        {
            var document = CaptureProject();
            // Serialize before opening the output stream, so validation cannot truncate a file.
            using var buffer = new MemoryStream();
            await SceneFiles.WriteAsync(buffer, document);
            using var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить проект ландшафта",
                SuggestedFileName = "landscape.terrain.json",
                DefaultExtension = "terrain.json",
                FileTypeChoices = [ProjectFileType]
            });
            if (file is null)
                return;
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
                stream.SetLength(0);
            buffer.Position = 0;
            await buffer.CopyToAsync(stream);
            status.Text = "Проект сохранён: " + file.Name;
        }
        catch (Exception error) { status.Text = "Сохранение проекта: " + error.Message; }
    }

    private async Task LoadProject()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Открыть проект ландшафта",
                AllowMultiple = false,
                FileTypeFilter = [ProjectFileType]
            });
            if (files.Count == 0)
                return;
            using var file = files[0];
            await using var stream = await file.OpenReadAsync();
            var document = await SceneFiles.ReadAsync(stream);
            await RestoreProject(document);
        }
        catch (Exception error) { status.Text = "Открытие проекта: " + error.Message; }
    }
}
