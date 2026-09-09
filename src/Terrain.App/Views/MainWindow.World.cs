using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.Core.World;

namespace Terrain.App.Views;

public sealed partial class MainWindow
{
    private bool worldActive, worldBuilding;
    private int worldSession;
    private WorldTerrain? worldTerrain;
    private WorldStream? worldStream;
    private CancellationTokenSource? worldCancellation;
    private ChunkDetail[] worldSelection = [];
    private Mesh? editorMesh;
    private Scene? editorScene;
    private bool editorOpenGl;
    private TerrainSettingsPanel? settingsPanel;
    private readonly DispatcherTimer worldTimer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
    private readonly Stopwatch worldClock = new();
    private readonly TextBlock worldDescription = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock worldLoadingText = new() { Text = "Готовим мир…", FontSize = 20, TextWrapping = TextWrapping.Wrap };
    private Border? worldLoader;
    private long lastWorldSelection;

    private void InitializeWorldPanel(TerrainSettingsPanel settings, Grid sceneArea)
    {
        settingsPanel = settings;
        settings.WorldPanel.Children.Add(new TextBlock { Text = "Живой мир", FontSize = 22, FontWeight = FontWeight.SemiBold });
        settings.WorldPanel.Children.Add(worldDescription);
        settings.WorldPanel.Children.Add(new TextBlock
        {
            Text = "Леса · пустыни · зима · болота · степи\n\nW/S — вперёд/назад, A/D — шаг влево/вправо. Щёлкните по сцене и смотрите мышью. Esc освобождает курсор. M — карта мира.\n\nДень длится 5 минут, ночь — 3 минуты. Мир создаётся случайно при каждом новом запуске.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = .8
        });
        settings.WorldPanel.Children.Add(Button("Создать другой мир", async () => { ExitWorld(); await EnterWorld(); }));
        settings.WorldPanel.Children.Add(Button("Вернуться к своей карте", () => { ExitWorld(); SetWalking(false); }));
        var loading = new StackPanel { Spacing = 18, MaxWidth = 330, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        loading.Children.Add(worldLoadingText);
        loading.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 5 });
        loading.Children.Add(new TextBlock { Text = "Создаём биомы, землю и растительность вокруг точки старта. Дальние участки появятся по мере прогулки.", TextWrapping = TextWrapping.Wrap, Opacity = .8 });
        loading.Children.Add(Button("Отмена", ExitWorld));
        worldLoader = new Border { Background = new SolidColorBrush(Color.Parse("#172125")), Child = loading, IsVisible = false };
        sceneArea.Children.Add(worldLoader);
        InitializeWorldMap(sceneArea);
        worldTimer.Tick += async (_, _) =>
        {
            if (!worldActive || worldTerrain is null || closed)
                return;
            float seconds = (float)(worldClock.Elapsed.TotalSeconds % DayNight.CycleDuration);
            float desiredDistance = worldStream!.ViewDistance(scene.ViewCamera);
            // Sky animates at 30 Hz; light/shadow direction is refreshed at 2 Hz.
            scene = scene with
            {
                WorldTime = seconds,
                CloudTime = (float)worldClock.Elapsed.TotalSeconds,
                WorldViewDistance = Math.Min(desiredDistance, scene.WorldViewDistance + .4f / 3),
                Light = DayNight.Light(MathF.Floor(seconds * 2) / 2)
            };
            var sample = worldTerrain.Sample(scene.ViewCamera.Position.X, scene.ViewCamera.Position.Z);
            string biome = sample.Biome switch
            {
                WorldBiome.Forest => "Лес",
                WorldBiome.Desert => "Пустыня",
                WorldBiome.Winter => "Зимние горы",
                WorldBiome.Swamp => "Болото",
                _ => "Степь"
            };
            worldDescription.Text = $"{biome} · {(seconds < DayNight.DayDuration ? "день" : "ночь")}\n{(worldBuilding ? "Подготавливаем путь…" : "Можно исследовать мир")}";
            Redraw();
            if (Stopwatch.GetElapsedTime(lastWorldSelection).TotalSeconds >= .35)
            {
                lastWorldSelection = Stopwatch.GetTimestamp();
                await UpdateWorldChunks(false);
            }
        };
    }

    private async Task ChangeNavigation(int selected)
    {
        if (switchingNavigation || restoringProject)
            return;
        if (selected == 2)
        {
            if (!worldActive)
                await EnterWorld();
        }
        else
        {
            if (worldActive)
                ExitWorld();
            SetWalking(selected == 1);
        }
    }

    internal async Task EnterWorld()
    {
        if (closed || mesh is null || worldActive)
        {
            SyncNavigationControls();
            return;
        }
        ReleaseLook();
        ++mapRequest; // Invalidate a pending editor rebuild before switching worlds.
        editorMesh = mesh;
        editorScene = scene;
        editorOpenGl = UseOpenGl;
        worldActive = true;
        int session = ++worldSession;
        worldCancellation = new();
        worldTerrain = new(RandomNumberGenerator.GetInt32(int.MaxValue));
        worldStream = new(worldTerrain);
        worldSelection = [];
        worldBuilding = false;
        settingsPanel!.ShowWorld(true);
        worldLoader!.IsVisible = true;
        worldLoadingText.Text = "Создаём новый мир…";
        scene = new Scene(RotationX: 0, RotationY: 0, HeightScale: 1, Light: DayNight.Light(0),
            ShadowResolution: 1024, ShowSky: true, Camera: worldTerrain.Anchor(new(new Vector3(0, 0, 0))), Walking: true, WorldTime: 0, CloudSeed: worldTerrain.Seed);
        UseOpenGl = true;
        SyncNavigationControls();
        try
        {
            var terrain = worldTerrain;
            var token = worldCancellation.Token;
            worldLoadingText.Text = "Составляем карту биомов…";
            var atlas = await Task.Run(() => WorldAtlas.Build(terrain, token), token);
            if (closed || !worldActive || session != worldSession)
                return;
            worldMapView.Map = Terrain.App.Services.Images.Bitmap(atlas);
            await UpdateWorldChunks(true);
            if (!worldActive || session != worldSession)
                return;
            worldLoader.IsVisible = false;
            worldClock.Restart();
            worldTimer.Start();
            viewport.Focus();
            status.Text = "Мир готов. Щёлкните по сцене для прогулки.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (session == worldSession)
            {
                ExitWorld();
                status.Text = "Не удалось создать мир: " + error.Message;
            }
        }
    }

    internal void ExitWorld()
    {
        if (!worldActive)
            return;
        SetWorldMap(false);
        mapKeyHeld = false;
        worldMapView.Map?.Dispose();
        worldMapView.Map = null;
        worldActive = false;
        ++worldSession;
        worldCancellation?.Cancel();
        worldCancellation = null;
        worldTimer.Stop();
        worldClock.Stop();
        ReleaseLook();
        if (editorMesh is not null)
            mesh = editorMesh;
        if (editorScene is not null)
            scene = editorScene;
        UseOpenGl = editorOpenGl;
        worldTerrain = null;
        worldStream = null;
        worldSelection = [];
        worldBuilding = false;
        editorMesh = null;
        editorScene = null;
        settingsPanel!.ShowWorld(false);
        worldLoader!.IsVisible = false;
        SyncNavigationControls();
        Redraw();
    }

    internal CameraPose MoveInWorld(CameraPose camera, float distance, float sideways = 0)
    {
        var moved = worldStream!.Anchor(camera.Move(distance, sideways));
        // Never walk into an unpublished mesh if generation is slower than movement.
        return worldStream!.HasGround(moved.Position) ? moved : worldStream.Anchor(camera);
    }

    private async Task UpdateWorldChunks(bool initial)
    {
        if (!worldActive || worldBuilding || worldStream is null || worldCancellation is null)
            return;
        var wanted = WorldLod.Select(scene.ViewCamera, worldSelection);
        if (wanted.Length == worldSelection.Length && wanted.ToHashSet().SetEquals(worldSelection))
            return;
        int session = worldSession;
        var stream = worldStream;
        var token = worldCancellation.Token;
        worldBuilding = true;
        try
        {
            IProgress<int>? progress = initial ? new Progress<int>(value =>
            {
                if (worldActive && session == worldSession)
                    worldLoadingText.Text = $"Создаём мир · {value}%";
            }) : null;
            var result = await Task.Run(() => stream.BuildDetailed(wanted, token, progress), token);
            if (closed || !worldActive || session != worldSession)
                return;
            mesh = result;
            worldSelection = wanted;
            stream.PublishDetailed(wanted);
            scene = scene with
            {
                Camera = stream.Anchor(scene.ViewCamera),
                WorldViewDistance = Math.Min(scene.WorldViewDistance, stream.ViewDistance(scene.ViewCamera))
            };
            Redraw();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) when (!initial)
        {
            if (session == worldSession)
            {
                ExitWorld();
                status.Text = "Загрузка мира: " + error.Message;
            }
        }
        finally { if (session == worldSession) worldBuilding = false; }
    }
}
