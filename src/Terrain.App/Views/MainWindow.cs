using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Terrain.Core.Generation;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.App.Controls;
using Terrain.App.Services;

namespace Terrain.App.Views;

public sealed class MainWindow : Window
{
    private readonly ComboBox mode = new() { ItemsSource = new[] { "CPU · свой Z-буфер", "OpenGL · видеокарта" }, SelectedIndex = 0 };
    private readonly ComboBox generator = new() { ItemsSource = new[] { "Холмовой", "Perlin", "Simplex", "Diamond–Square" }, SelectedIndex = 0 };
    private readonly NumericUpDown size = Number(129, 2, 1025), seed = Number(42, int.MinValue, int.MaxValue);
    private readonly NumericUpDown hills = Number(200, 0, 10000), octaves = Number(5, 1, 10);
    private readonly NumericUpDown frequency = Number(4, 1, 32);
    private readonly CheckBox smooth = new() { Content = "Сглаживание 3×3" }, valley = new() { Content = "Долина (√h)" };
    private readonly CheckBox shadows = new() { Content = "Падающие тени", IsChecked = true };
    private readonly CheckBox softShadows = new() { Content = "Смягчить края теней", IsChecked = true };
    private readonly CpuTerrainRenderer cpuRenderer = new();
    private readonly Slider roughness = new() { Minimum = .1, Maximum = .9, Value = .5 };
    private readonly Slider rotationX = Angle(35), rotationY = Angle(-30), rotationZ = Angle(0);
    private readonly Slider relief = new() { Minimum = .05, Maximum = 2, Value = .65 };
    private readonly Slider sunPosition = new() { Minimum = 0, Maximum = 100, Value = 30 };
    private readonly TextBlock sunDescription = new() { Text = "Утро · высота 54°", Opacity = .7 };
    private readonly TextBlock status = new() { Text = "Подготовка…", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Image image = new() { Stretch = Stretch.Fill }, preview = new() { Height = 160, Stretch = Stretch.Uniform };
    private readonly OpenGlTerrain gl = new() { IsVisible = false };
    private readonly Grid viewport = new() { ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(18, 23, 29)), MinWidth = 200, MinHeight = 200 };
    private readonly Button generate = new() { Content = "Сгенерировать", HorizontalAlignment = HorizontalAlignment.Stretch };
    private Mesh? mesh;
    private Scene scene = new(Light: SunPath.Direction(.3f), ShowSky: true);
    private HeightMap? map;
    private WriteableBitmap? cpuBitmap, previewBitmap;
    private int revision;
    private int mapRequest;
    private bool rendering, initialized, closed;
    private Point? previous;
    private bool pan;
    private TaskCompletionSource<Frame>? capture;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Ready => ready.Task;
    internal OpenGlTerrain GlRenderer => gl;
    internal Mesh? CurrentMesh
    {
        get => mesh; set => mesh = value;
    }
    internal Scene CurrentScene
    {
        get => scene; set => scene = value;
    }
    internal bool UseOpenGl
    {
        get => mode.SelectedIndex == 1; set => mode.SelectedIndex = value ? 1 : 0;
    }

    public MainWindow()
    {
        Title = "Ландшафт · CPU / OpenGL";
        Width = 1180;
        Height = 850;
        MinWidth = 820;
        MinHeight = 620;
        RequestedThemeVariant = ThemeVariant.Dark;
        var panel = new StackPanel { Spacing = 9, Margin = new Thickness(16) };
        void Label(string text) => panel.Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        void Add(Control control) => panel.Children.Add(control);
        Label("ЛАНДШАФТ / 2026");
        Add(mode);
        Add(Button("Загрузить карту BMP / PNG", async () => await LoadMap()));
        Add(generator);
        Label("Размер карты");
        Add(size);
        var diamondSizeHint = new TextBlock
        {
            Text = "Diamond–Square: 2ⁿ + 1 — например, 65, 129, 257, 513 или 1025.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = .7
        };
        Add(diamondSizeHint);
        Label("Seed");
        Add(seed);
        var hillsSettings = new StackPanel { Spacing = 9 };
        hillsSettings.Children.Add(new TextBlock { Text = "Количество холмов", FontWeight = FontWeight.SemiBold });
        hillsSettings.Children.Add(hills);
        Add(hillsSettings);
        var noiseSettings = new StackPanel { Spacing = 9 };
        noiseSettings.Children.Add(new TextBlock { Text = "Октавы / частота шума", FontWeight = FontWeight.SemiBold });
        noiseSettings.Children.Add(Row(octaves, frequency));
        Add(noiseSettings);
        var roughnessLabel = new TextBlock { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        var roughnessSettings = new StackPanel { Spacing = 9 };
        roughnessSettings.Children.Add(roughnessLabel);
        roughnessSettings.Children.Add(roughness);
        Add(roughnessSettings);
        void UpdateGeneratorSettings()
        {
            var kind = (GeneratorKind)generator.SelectedIndex;
            bool isNoise = kind is GeneratorKind.Perlin or GeneratorKind.Simplex;
            hillsSettings.IsVisible = kind == GeneratorKind.Hills;
            noiseSettings.IsVisible = isNoise;
            roughnessSettings.IsVisible = isNoise || kind == GeneratorKind.DiamondSquare;
            roughnessLabel.Text = isNoise ? "Затухание амплитуды октав" : "Шероховатость рельефа";
            diamondSizeHint.IsVisible = kind == GeneratorKind.DiamondSquare;
        }
        generator.SelectionChanged += (_, _) => UpdateGeneratorSettings();
        UpdateGeneratorSettings();
        Add(Row(smooth, valley));
        Add(generate);
        Add(preview);
        Add(details);
        Label("Поворот X / Y / Z");
        Add(rotationX);
        Add(rotationY);
        Add(rotationZ);
        Add(Row(Button("X +90°", () => Turn(rotationX)), Button("Y +90°", () => Turn(rotationY)), Button("Z +90°", () => Turn(rotationZ))));
        Add(Row(Button("X −90°", () => Turn(rotationX, -90)), Button("Y −90°", () => Turn(rotationY, -90)), Button("Z −90°", () => Turn(rotationZ, -90))));
        Label("Высота рельефа");
        Add(relief);
        Label("Солнце");
        Add(sunPosition);
        Add(new TextBlock { Text = "Восход           Зенит           Закат", Opacity = .7 });
        Add(sunDescription);
        Avalonia.Automation.AutomationProperties.SetName(sunPosition, "Положение солнца: от восхода до заката");
        sunPosition.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty) return;
            float progress = (float)sunPosition.Value / 100;
            scene = scene with { Light = SunPath.Direction(progress) };
            var phase = progress < .49f ? "Утро" : progress > .51f ? "Вечер" : "Зенит";
            sunDescription.Text = $"{phase} · высота {180 * Math.Min(progress, 1 - progress):F0}°";
            Redraw();
        };
        Add(shadows);
        Add(softShadows);
        Add(Row(Button("←", () => Move(-.1f, 0)), Button("↑", () => Move(0, .1f)), Button("↓", () => Move(0, -.1f)), Button("→", () => Move(.1f, 0))));
        Add(Button("Сбросить вид", ResetView));
        Add(Button("Сохранить сцену PNG / BMP", async () => await SaveScene()));
        Add(new TextBlock { Text = "Мышь: вращение · Shift/правая кнопка: перенос · колесо: масштаб", TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        viewport.Children.Add(image);
        viewport.Children.Add(gl);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*"), RowDefinitions = new RowDefinitions("*,Auto") };
        var scroll = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        layout.Children.Add(scroll);
        Grid.SetColumn(viewport, 1);
        layout.Children.Add(viewport);
        var footer = new Border { Child = status, Padding = new Thickness(12), Background = new SolidColorBrush(Color.FromRgb(32, 39, 47)) };
        Grid.SetRow(footer, 1);
        Grid.SetColumnSpan(footer, 2);
        layout.Children.Add(footer);
        Content = layout;

        generate.Click += async (_, _) => await Generate();
        foreach (var checkbox in new[] { shadows, softShadows })
            checkbox.PropertyChanged += (_, e) =>
        {
            if (e.Property != CheckBox.IsCheckedProperty)
                return;
            scene = scene with
            {
                Shadows = shadows.IsChecked == true,
                SoftShadows = softShadows.IsChecked == true
            };
            softShadows.IsEnabled = scene.Shadows;
            Redraw();
        };
        mode.SelectionChanged += (_, _) => { gl.IsVisible = mode.SelectedIndex == 1; image.IsVisible = !gl.IsVisible; Redraw(); };
        foreach (var slider in new[] { rotationX, rotationY, rotationZ, relief })
            slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && initialized)
            {
                scene = scene with
                {
                    RotationX = (float)rotationX.Value,
                    RotationY = (float)rotationY.Value,
                    RotationZ = (float)rotationZ.Value,
                    HeightScale = (float)relief.Value
                };
                Redraw();
            }
        };
        viewport.SizeChanged += (_, _) => Redraw();
        viewport.PointerPressed += (_, e) =>
        {
            previous = e.GetPosition(viewport);
            pan = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.GetCurrentPoint(viewport).Properties.IsRightButtonPressed;
            e.Pointer.Capture(viewport);
            e.Handled = true;
        };
        viewport.PointerReleased += (_, e) => { previous = null; e.Pointer.Capture(null); };
        viewport.PointerCaptureLost += (_, _) => previous = null;
        viewport.PointerMoved += (_, e) =>
        {
            if (previous is not { } start)
                return;
            var current = e.GetPosition(viewport);
            var delta = current - start;
            previous = current;
            if (pan)
                Move((float)(delta.X * 2 / viewport.Bounds.Height), (float)(-delta.Y * 2 / viewport.Bounds.Height));
            else
            {
                rotationY.Value = Wrap(rotationY.Value + delta.X * .5);
                rotationX.Value = Wrap(rotationX.Value + delta.Y * .5);
            }
        };
        viewport.PointerWheelChanged += (_, e) =>
        {
            scene = scene with
            {
                Zoom = Math.Clamp(scene.Zoom * MathF.Exp((float)e.Delta.Y * .12f), .1f, 20)
            };
            Redraw();
            e.Handled = true;
        };
        gl.Status += text => Dispatcher.UIThread.Post(() => { if (mode.SelectedIndex == 1) status.Text = text; });
        gl.Captured += frame => Dispatcher.UIThread.Post(() => capture?.TrySetResult(frame));
        Opened += async (_, _) =>
        {
            initialized = true;
            await Generate();
            ready.TrySetResult();
        };
        Closed += (_, _) => { closed = true; revision++; cpuBitmap?.Dispose(); previewBitmap?.Dispose(); capture?.TrySetCanceled(); };
    }

    private static NumericUpDown Number(decimal value, decimal min, decimal max) => new() { Value = value, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", Width = 130 };
    private static Slider Angle(double value) => new() { Minimum = -180, Maximum = 180, Value = value };
    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var child in children)
            row.Children.Add(child);
        return row;
    }
    private static Button Button(string title, Action action)
    {
        var button = new Button { Content = title };
        button.Click += (_, _) => action();
        return button;
    }
    private static double Wrap(double angle) => (angle + 540) % 360 - 180;
    private void Turn(Slider slider, double degrees = 90) => slider.Value = Wrap(slider.Value + degrees);
    private void Move(float x, float y)
    {
        scene = scene with
        {
            PanX = scene.PanX + x,
            PanY = scene.PanY + y
        };
        Redraw();
    }
    internal void ResetView()
    {
        scene = new Scene(Light: scene.Light, Shadows: scene.Shadows, SoftShadows: scene.SoftShadows, ShadowResolution: scene.ShadowResolution, ShowSky: scene.ShowSky);
        rotationX.Value = 35;
        rotationY.Value = -30;
        rotationZ.Value = 0;
        relief.Value = .65;
        Redraw();
    }
    private async Task Generate()
    {
        int request = ++mapRequest;
        generate.IsEnabled = false;
        try
        {
            var options = new GenerationOptions((GeneratorKind)generator.SelectedIndex, (int)(size.Value ?? 129), (int)(seed.Value ?? 42),
                (int)(hills.Value ?? 200), smooth.IsChecked == true, valley.IsChecked == true,
                (int)(octaves.Value ?? 5), (float)(frequency.Value ?? 4), (float)roughness.Value);
            status.Text = "Генерация карты…";
            var timer = Stopwatch.StartNew();
            var result = await Task.Run(() => { var generated = Generators.Generate(options); return (generated, Mesh.Build(generated)); });
            if (closed || request != mapRequest)
                return;
            SetMap(result.generated, result.Item2, timer.Elapsed.TotalMilliseconds);
        }
        catch (Exception error) { status.Text = error.Message; }
        finally { generate.IsEnabled = true; }
    }
    internal void SetMap(HeightMap heightMap, Mesh terrain, double milliseconds)
    {
        map = heightMap;
        mesh = terrain;
        var old = previewBitmap;
        previewBitmap = Images.Bitmap(Images.Preview(map));
        preview.Source = previewBitmap;
        old?.Dispose();
        details.Text = $"{map.Width}×{map.Height} · {mesh.Indices.Length / 3:N0} треугольников\nКарта + сетка: {milliseconds:F1} мс";
        ResetView();
    }
    private async Task LoadMap()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Карта высот",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Карты высот") { Patterns = ["*.bmp", "*.png"] }]
            });
            if (files.Count == 0)
                return;
            int request = ++mapRequest;
            using var file = files[0];
            await using var stream = await file.OpenReadAsync();
            var timer = Stopwatch.StartNew();
            var result = await Task.Run(() => { var loaded = Images.Load(stream); return (loaded, Mesh.Build(loaded)); });
            if (!closed && request == mapRequest)
                SetMap(result.loaded, result.Item2, timer.Elapsed.TotalMilliseconds);
        }
        catch (Exception error) { status.Text = "Загрузка: " + error.Message; }
    }
    private (int Width, int Height) Resolution() => (Math.Max(1, (int)Math.Round(viewport.Bounds.Width * RenderScaling)), Math.Max(1, (int)Math.Round(viewport.Bounds.Height * RenderScaling)));
    internal void Redraw()
    {
        revision++;
        if (!initialized || mesh is null || closed)
            return;
        gl.Mesh = mesh;
        gl.Scene = scene;
        if (mode.SelectedIndex == 1)
            gl.RequestNextFrameRendering();
        else if (!rendering)
            _ = RenderCpu();
    }
    private async Task RenderCpu()
    {
        rendering = true;
        try
        {
            while (!closed && mode.SelectedIndex == 0 && mesh is { } currentMesh)
            {
                int version = revision;
                var currentScene = scene;
                var resolution = Resolution();
                var timer = Stopwatch.StartNew();
                var frame = await Task.Run(() => cpuRenderer.Render(currentMesh, currentScene, resolution.Width, resolution.Height));
                if (closed || mode.SelectedIndex != 0)
                    break;
                if (version != revision)
                    continue;
                var old = cpuBitmap;
                cpuBitmap = Images.Bitmap(frame);
                image.Source = cpuBitmap;
                old?.Dispose();
                status.Text = $"CPU · {frame.Width}×{frame.Height} · кадр {timer.Elapsed.TotalMilliseconds:F1} мс";
                break;
            }
        }
        catch (Exception error) { status.Text = "CPU: " + error.Message; }
        finally { rendering = false; }
    }
    private async Task SaveScene()
    {
        if (mesh is null || capture is not null)
            return;
        try
        {
            Frame frame;
            if (mode.SelectedIndex == 1)
            {
                capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
                gl.CaptureRequested = true;
                gl.RequestNextFrameRendering();
                frame = await capture.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            else
            {
                var currentMesh = mesh;
                var currentScene = scene;
                var resolution = Resolution();
                frame = await Task.Run(() => cpuRenderer.Render(currentMesh, currentScene, resolution.Width, resolution.Height));
            }
            using var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить сцену",
                SuggestedFileName = "terrain.png",
                DefaultExtension = "png",
                FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }, new FilePickerFileType("BMP") { Patterns = ["*.bmp"] }]
            });
            if (file is null)
                return;
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
                stream.SetLength(0);
            Images.Save(frame, stream, file.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase));
            status.Text = "Сохранено: " + file.Name;
        }
        catch (Exception error) { status.Text = "Сохранение: " + error.Message; }
        finally { capture = null; gl.CaptureRequested = false; }
    }

}
