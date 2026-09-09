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
using Terrain.Core.Climate;
using Terrain.Core.Surface;
using Terrain.Core.Erosion;
using Terrain.Core.Hydrology;
using Terrain.Core.Vegetation;
using Terrain.Core.Models;
using Terrain.Core.Rendering;
using Terrain.App.Controls;
using Terrain.App.Services;

namespace Terrain.App.Views;

public sealed partial class MainWindow : Window
{
    private readonly ComboBox mode = new() { ItemsSource = new[] { "CPU · свой Z-буфер", "OpenGL · видеокарта" }, SelectedIndex = 1 };
    private readonly ComboBox generator = new() { ItemsSource = new[] { "Холмовой", "Perlin", "Simplex", "Diamond–Square" }, SelectedIndex = 0 };
    private readonly NumericUpDown size = Number(129, 2, 1025), seed = Number(42, int.MinValue, int.MaxValue);
    private readonly NumericUpDown hills = Number(200, 0, 10000), octaves = Number(5, 1, 10);
    private readonly NumericUpDown frequency = Number(4, 1, 32);
    private readonly CheckBox smooth = new() { Content = "Сглаживание 3×3" }, valley = new() { Content = "Долина (√h)" };
    private readonly CheckBox shadows = new() { Content = "Падающие тени", IsChecked = true };
    private readonly CheckBox softShadows = new() { Content = "Смягчить края теней", IsChecked = true };
    private readonly CheckBox rivers = new() { Content = "Реки и озёра", IsChecked = true };
    private readonly NumericUpDown seaLevel = new() { Value = .02m, Minimum = 0, Maximum = 1, Increment = .01m, FormatString = "0.00" };
    private readonly Slider riverDensity = new() { Minimum = 1, Maximum = 10, Value = 5 };
    private readonly CheckBox vegetation = new() { Content = "Растительность и камни", IsChecked = true };
    private readonly Slider forestDensity = new() { Minimum = 0, Maximum = 100, Value = 55 };
    private readonly CheckBox biomes = new() { Content = "Климат и биомы", IsChecked = true };
    private readonly Slider climateTemperature = new() { Minimum = 0, Maximum = 100, Value = 70 };
    private readonly Slider climateMoisture = new() { Minimum = 0, Maximum = 100, Value = 50 };
    private readonly CheckBox shrubs = new() { Content = "Кустарники", IsChecked = true };
    private readonly Slider shrubDensity = new() { Minimum = 0, Maximum = 100, Value = 50 };
    private readonly CheckBox terrainDetails = new() { Content = "Осыпи и снежные пятна", IsChecked = true };
    private readonly Slider screeDensity = new() { Minimum = 0, Maximum = 100, Value = 50 };
    private readonly CheckBox fogEnabled = new() { Content = "Атмосферная дымка", IsChecked = true };
    private readonly Slider fogDensity = new() { Minimum = 0, Maximum = 100, Value = 15 };
    private readonly CheckBox separateWater = new() { Content = "Отдельная поверхность воды", IsChecked = true };
    private readonly CheckBox carveChannels = new() { Content = "Углубить русла", IsChecked = true };
    private readonly Slider channelDepth = new() { Minimum = 0, Maximum = 80, Value = 20 };
    private readonly CheckBox erosionEnabled = new() { Content = "Эрозия склонов", IsChecked = false };
    private readonly Slider erosionStrength = new() { Minimum = 0, Maximum = 50, Value = 35 };
    private readonly NumericUpDown erosionIterations = Number(16, 0, 60);
    private readonly ComboBox shadowQuality = new() { ItemsSource = new[] { "Тени · быстрые", "Тени · обычные", "Тени · точные" }, SelectedIndex = 2 };
    private int currentSurfaceSeed;
    private readonly CpuTerrainRenderer cpuRenderer = new();
    private readonly Slider roughness = new() { Minimum = .1, Maximum = .9, Value = .5 };
    private readonly Slider rotationX = Angle(35), rotationY = Angle(-30), rotationZ = Angle(0);
    private readonly Slider relief = new() { Minimum = .05, Maximum = 2, Value = .65 };
    private readonly Slider sunPosition = new() { Minimum = 0, Maximum = 100, Value = 30 };
    private readonly TextBlock sunDescription = new() { Text = "Утро · высота 54°", Opacity = .7 };
    private readonly TextBlock status = new() { Text = "Подготовка…", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Image image = new() { Stretch = Stretch.Fill }, preview = new() { Height = 160, Stretch = Stretch.Uniform };
    private readonly OpenGlTerrain gl = new();
    private readonly Grid viewport = new() { Focusable = true, ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(18, 23, 29)), MinWidth = 200, MinHeight = 200 };
    private readonly Button generate = new() { Content = "Сгенерировать", HorizontalAlignment = HorizontalAlignment.Stretch };
    private Mesh? mesh;
    private Scene scene = new(Light: SunPath.Direction(.3f), ShowSky: true, FogDensity: .075f);
    private HeightMap? map;
    private WriteableBitmap? cpuBitmap, previewBitmap;
    private int revision;
    private int mapRequest;
    private bool rendering, initialized, closed;
    private readonly HashSet<Key> cameraKeys = [];
    private readonly DispatcherTimer cameraTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private long cameraTick;
    private Point? previous;
    private bool pan;
    private TaskCompletionSource<Frame>? capture;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Control NavigationSurface => viewport;
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
        var settings = new TerrainSettingsPanel(mode, navigationMode);
        var panel = settings.Section("Карта", "Создание рельефа", "Выберите алгоритм или загрузите карту высот.", true);
        void Label(string text) => panel.Children.Add(new TextBlock { Text = text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        void Add(Control control)
        {
            if (control is NumericUpDown)
                control.HorizontalAlignment = HorizontalAlignment.Left;
            else if (control is ComboBox or Avalonia.Controls.Button)
                control.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.Children.Add(control);
        }
        void AddOverview(Control control)
        {
            overviewControls.Add(control);
            Add(control);
        }
        navigationMode.SelectionChanged += async (_, _) => await ChangeNavigation(navigationMode.SelectedIndex);
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
        generate.Background = new SolidColorBrush(Color.Parse("#327D70"));
        generate.Foreground = Brushes.White;
        generate.CornerRadius = new CornerRadius(8);
        Add(generate);
        panel = settings.Section("Природа", "Вода и русла", "Море, озёра и речная сеть", true);
        Add(rivers);
        var waterSettings = new StackPanel { Spacing = 9 };
        waterSettings.Children.Add(new TextBlock { Text = "Уровень моря (0–1)" });
        waterSettings.Children.Add(seaLevel);
        waterSettings.Children.Add(new TextBlock { Text = "Густота речной сети" });
        waterSettings.Children.Add(riverDensity);
        waterSettings.Children.Add(separateWater);
        waterSettings.Children.Add(carveChannels);
        waterSettings.Children.Add(channelDepth);
        void WaterControls()
        {
            carveChannels.IsVisible = separateWater.IsChecked == true;
            channelDepth.IsVisible = separateWater.IsChecked == true && carveChannels.IsChecked == true;
        }
        separateWater.PropertyChanged += (_, e) => { if (e.Property == CheckBox.IsCheckedProperty) WaterControls(); };
        carveChannels.PropertyChanged += (_, e) => { if (e.Property == CheckBox.IsCheckedProperty) WaterControls(); };
        waterSettings.Children.Add(Button("Обновить воду", async () => await RebuildEnvironment()));
        Add(waterSettings);
        rivers.PropertyChanged += async (_, e) =>
        {
            if (e.Property != CheckBox.IsCheckedProperty)
                return;
            waterSettings.IsVisible = rivers.IsChecked == true;
            await RebuildEnvironment();
        };
        panel = settings.Section("Природа", "Растительность", "Лес и подлесок по биомам", false);
        Add(vegetation);
        var forestSettings = new StackPanel { Spacing = 9 };
        forestSettings.Children.Add(new TextBlock { Text = "Плотность леса" });
        forestSettings.Children.Add(forestDensity);
        forestSettings.Children.Add(Button("Обновить лес и камни", async () => await RebuildEnvironment()));
        Add(forestSettings);
        vegetation.PropertyChanged += async (_, e) =>
        {
            if (e.Property != CheckBox.IsCheckedProperty)
                return;
            forestSettings.IsVisible = vegetation.IsChecked == true;
            await RebuildEnvironment();
        };
        Add(shrubs);
        var shrubSettings = new StackPanel { Spacing = 6 };
        shrubSettings.Children.Add(new TextBlock { Text = "Плотность кустарников" });
        shrubSettings.Children.Add(shrubDensity);
        shrubSettings.Children.Add(Button("Обновить кустарники", async () => await RebuildEnvironment()));
        Add(shrubSettings);
        shrubs.PropertyChanged += async (_, e) =>
        {
            if (e.Property != CheckBox.IsCheckedProperty)
                return;
            shrubSettings.IsVisible = shrubs.IsChecked == true;
            await RebuildEnvironment();
        };
        panel = settings.Section("Природа", "Камни и снег", "Детали скальных склонов", false);
        Add(terrainDetails);
        var detailSettings = new StackPanel { Spacing = 6 };
        detailSettings.Children.Add(new TextBlock { Text = "Плотность осыпей" });
        detailSettings.Children.Add(screeDensity);
        detailSettings.Children.Add(Button("Обновить детали", async () => await RebuildEnvironment()));
        Add(detailSettings);
        terrainDetails.PropertyChanged += async (_, e) =>
        {
            if (e.Property != CheckBox.IsCheckedProperty)
                return;
            detailSettings.IsVisible = terrainDetails.IsChecked == true;
            await RebuildEnvironment();
        };
        panel = settings.Section("Карта", "Эрозия", "Обратимая обработка крутых склонов", false);
        Add(erosionEnabled);
        var erosionSettings = new StackPanel { Spacing = 6, IsVisible = false };
        erosionSettings.Children.Add(new TextBlock { Text = "Сила переноса / итерации" });
        erosionSettings.Children.Add(erosionStrength);
        erosionSettings.Children.Add(erosionIterations);
        erosionSettings.Children.Add(new TextBlock { Text = "Исходные высоты сохраняются. Выключите эрозию, чтобы сравнить.", TextWrapping = TextWrapping.Wrap });
        erosionSettings.Children.Add(Button("Применить эрозию", async () => await RebuildEnvironment()));
        Add(erosionSettings);
        erosionEnabled.PropertyChanged += async (_, e) =>
        {
            if (e.Property != CheckBox.IsCheckedProperty)
                return;
            erosionSettings.IsVisible = erosionEnabled.IsChecked == true;
            await RebuildEnvironment();
        };
        panel = settings.Section("Карта", "Текущая карта", "Высоты и сведения о рельефе", false);
        Add(preview);
        Add(details);
        panel = settings.Section("Вид", "Положение и масштаб", "Поворот модели и высота рельефа", true);
        Label("Поворот X / Y / Z");
        Add(rotationX);
        Add(rotationY);
        Add(rotationZ);
        AddOverview(Row(Button("X +90°", () => Turn(rotationX)), Button("Y +90°", () => Turn(rotationY)), Button("Z +90°", () => Turn(rotationZ))));
        AddOverview(Row(Button("X −90°", () => Turn(rotationX, -90)), Button("Y −90°", () => Turn(rotationY, -90)), Button("Z −90°", () => Turn(rotationZ, -90))));
        Label("Высота рельефа");
        Add(relief);
        panel = settings.Section("Вид", "Солнце и атмосфера", "Свет, дымка и глубина сцены", true);
        var atmospherePanel = panel;
        Label("Солнце");
        Add(sunPosition);
        Add(new TextBlock { Text = "Восход           Зенит           Закат", Opacity = .7 });
        Add(sunDescription);
        Avalonia.Automation.AutomationProperties.SetName(sunPosition, "Положение солнца: от восхода до заката");
        sunPosition.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty)
                return;
            float progress = (float)sunPosition.Value / 100;
            scene = scene with
            {
                Light = SunPath.Direction(progress)
            };
            var phase = progress < .49f ? "Утро" : progress > .51f ? "Вечер" : "Зенит";
            sunDescription.Text = $"{phase} · высота {180 * Math.Min(progress, 1 - progress):F0}°";
            Redraw();
        };
        panel = settings.Section("Природа", "Климат и биомы", "Температура и влажность местности", false);
        Add(biomes);
        var climateSettings = new StackPanel { Spacing = 6 };
        climateSettings.Children.Add(new TextBlock { Text = "Климат · холодный → тёплый" });
        climateSettings.Children.Add(climateTemperature);
        climateSettings.Children.Add(new TextBlock { Text = "Влажность · сухо → влажно" });
        climateSettings.Children.Add(climateMoisture);
        climateSettings.Children.Add(new TextBlock
        {
            Text = "Меняет биомы поверхности. Влияет на состав и густоту леса.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = .7
        });
        Add(climateSettings);
        biomes.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
                climateSettings.IsVisible = biomes.IsChecked == true;
        };
        Add(Button("Обновить окружение", async () => await RebuildEnvironment()));
        panel = atmospherePanel;
        Add(fogEnabled);
        Add(fogDensity);
        void UpdateFog()
        {
            fogDensity.IsVisible = fogEnabled.IsChecked == true;
            scene = scene with
            {
                FogDensity = fogEnabled.IsChecked == true ? (float)fogDensity.Value / 200 : 0
            };
            Redraw();
        }
        fogEnabled.PropertyChanged += (_, e) => { if (e.Property == CheckBox.IsCheckedProperty) UpdateFog(); };
        fogDensity.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) UpdateFog(); };
        panel = settings.Section("Вид", "Качество теней", "Чёткость и скорость отрисовки", false);
        Add(shadows);
        Add(softShadows);
        Add(shadowQuality);
        shadowQuality.SelectionChanged += (_, _) =>
        {
            scene = scene with
            {
                ShadowResolution = shadowQuality.SelectedIndex switch
                {
                    0 => 256,
                    1 => 512,
                    _ => 1024
                }
            };
            Redraw();
        };
        panel = settings.Section("Вид", "Камера и управление", "Навигация по сцене", false);
        AddOverview(Row(Button("←", () => Move(-.1f, 0)), Button("↑", () => Move(0, .1f)), Button("↓", () => Move(0, -.1f)), Button("→", () => Move(.1f, 0))));
        Add(Button("Сбросить вид", ResetView));
        var cameraPanel = panel;
        panel = settings.Section("Карта", "Файлы и экспорт", "Сохраните сцену вместе с её настройками", true);
        Add(Button("Сохранить изображение PNG / BMP", async () => await SaveScene()));
        Add(Button("Сохранить проект…", async () => await SaveProject()));
        Add(Button("Открыть проект…", async () => await LoadProject()));
        panel = cameraPanel;
        Add(new TextBlock { Text = "W/S — вперёд/назад, A/D — поворот. Прогулка: щёлкните по сцене для свободного взгляда мышью; Esc — освободить курсор. Мышь — вращение модели; Shift/правая кнопка — перенос; колесо — масштаб.", TextWrapping = TextWrapping.Wrap, Opacity = .7 });
        image.IsVisible = false;
        viewport.Children.Add(image);
        viewport.Children.Add(gl);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("352,*"), RowDefinitions = new RowDefinitions("*,Auto") };
        var scroll = settings.View;
        layout.Children.Add(scroll);
        var sceneArea = new Grid();
        sceneArea.Children.Add(viewport);
        InitializeWorldPanel(settings, sceneArea);
        // A sibling overlay keeps tab clicks out of the viewport's mouse-look handler.
        var settingsTab = new Button
        {
            Content = "‹",
            Width = 30,
            Height = 64,
            Padding = new Thickness(0),
            FontSize = 28,
            CornerRadius = new CornerRadius(0, 10, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(230, 32, 39, 47))
        };
        void UpdateSettingsTab()
        {
            string label = scroll.IsVisible ? "Свернуть настройки" : "Развернуть настройки";
            settingsTab.Content = scroll.IsVisible ? "‹" : "›";
            ToolTip.SetTip(settingsTab, label);
            Avalonia.Automation.AutomationProperties.SetName(settingsTab, label);
        }
        settingsTab.Click += (_, _) =>
        {
            ReleaseLook();
            scroll.IsVisible = !scroll.IsVisible;
            layout.ColumnDefinitions[0].Width = new GridLength(scroll.IsVisible ? 352 : 0);
            UpdateSettingsTab();
            if (!scroll.IsVisible)
                viewport.Focus();
        };
        UpdateSettingsTab();
        sceneArea.Children.Add(settingsTab);
        Grid.SetColumn(sceneArea, 1);
        layout.Children.Add(sceneArea);
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
            if (e.Property == Slider.ValueProperty && initialized && !switchingNavigation)
            {
                scene = scene with
                {
                    RotationX = scene.Walking ? 0 : (float)rotationX.Value,
                    RotationY = scene.Walking ? 0 : (float)rotationY.Value,
                    RotationZ = scene.Walking ? 0 : (float)rotationZ.Value,
                    HeightScale = (float)relief.Value
                };
                if (scene.Walking)
                    RefreshWalkingSurface();
                Redraw();
            }
        };
        viewport.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                ReleaseLook();
                e.Handled = true;
                return;
            }
            if (e.Key is not (Key.W or Key.S or Key.A or Key.D) || e.KeyModifiers != KeyModifiers.None)
                return;
            cameraKeys.Add(e.Key);
            if (!cameraTimer.IsEnabled)
            {
                cameraTick = Stopwatch.GetTimestamp();
                cameraTimer.Start();
            }
            e.Handled = true;
        };
        viewport.KeyUp += (_, e) =>
        {
            if (!cameraKeys.Remove(e.Key))
                return;
            if (cameraKeys.Count == 0 && !mouseLook.Active)
                cameraTimer.Stop();
            e.Handled = true;
        };
        void StopCamera()
        {
            ReleaseLook();
        }
        viewport.LostFocus += (_, _) => StopCamera();
        Deactivated += (_, _) => StopCamera();
        cameraTimer.Tick += (_, _) =>
        {
            if (!viewport.IsKeyboardFocusWithin || !IsActive)
            {
                StopCamera();
                return;
            }
            float seconds = (float)Math.Min(Stopwatch.GetElapsedTime(cameraTick).TotalSeconds, .05);
            cameraTick = Stopwatch.GetTimestamp();
            float turn = (cameraKeys.Contains(Key.D) ? 1 : 0) - (cameraKeys.Contains(Key.A) ? 1 : 0);
            float forward = (cameraKeys.Contains(Key.W) ? 1 : 0) - (cameraKeys.Contains(Key.S) ? 1 : 0);
            var look = mouseLook.ReadDelta();
            if (turn == 0 && forward == 0 && look.Length == 0)
                return;
            var camera = scene.ViewCamera.Look((worldActive ? 0 : turn * 65 * seconds) + (float)look.X * .18f, -(float)look.Y * .18f);
            camera = worldActive && worldTerrain is not null
                ? MoveInWorld(camera, forward * 1.4f * seconds, turn * 1.4f * seconds)
                : scene.Walking && walkingSurface is not null
                ? walkingSurface.Move(camera, forward * .22f * seconds, scene.HeightScale)
                : camera.Move(forward * 1.2f * seconds);
            if (scene.Walking && (forward != 0 || worldActive && turn != 0) && camera == scene.ViewCamera)
            {
                status.Text = "Достигнута граница карты. Поверните в другую сторону.";
                return;
            }
            scene = scene with
            {
                Camera = camera,
                WorldViewDistance = worldActive ? Math.Min(scene.WorldViewDistance, worldStream!.ViewDistance(camera)) : scene.WorldViewDistance
            };
            Redraw();
        };
        viewport.SizeChanged += (_, _) => Redraw();
        viewport.PointerPressed += (_, e) =>
        {
            viewport.Focus();
            if (scene.Walking)
            {
                if (!StartWalkingLook())
                    status.Text = "Не удалось захватить мышь. WASD доступны; свободный взгляд поддерживается на macOS.";
                e.Handled = true;
                return;
            }
            previous = e.GetPosition(viewport);
            pan = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.GetCurrentPoint(viewport).Properties.IsRightButtonPressed;
            e.Pointer.Capture(viewport);
            e.Handled = true;
        };
        viewport.PointerReleased += (_, e) => { if (scene.Walking) return; previous = null; e.Pointer.Capture(null); };
        viewport.PointerCaptureLost += (_, _) => { if (!scene.Walking) previous = null; };
        viewport.PointerMoved += (_, e) =>
        {
            if (scene.Walking)
                return;
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
            if (scene.Walking)
            {
                e.Handled = true;
                return;
            }
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
            viewport.Focus();
            initialized = true;
            await Generate();
            ready.TrySetResult();
        };
        Closed += (_, _) => { closed = true; worldMapView.Map?.Dispose(); worldCancellation?.Cancel(); worldTimer.Stop(); ReleaseLook(); cameraTimer.Stop(); cameraKeys.Clear(); revision++; cpuBitmap?.Dispose(); previewBitmap?.Dispose(); capture?.TrySetCanceled(); };
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
    private void Turn(Slider slider, double degrees = 90)
    {
        if (!scene.Walking)
            slider.Value = Wrap(slider.Value + degrees);
    }
    private void Move(float x, float y)
    {
        if (scene.Walking)
            return;
        scene = scene with
        {
            PanX = scene.PanX + x,
            PanY = scene.PanY + y
        };
        Redraw();
    }
    internal void ResetView()
    {
        ReleaseLook();
        if (scene.Walking && !restoringProject)
        {
            walkingCamera = null;
            scene = scene with
            {
                Camera = walkingSurface?.Spawn(scene.HeightScale) ?? scene.ViewCamera
            };
            Redraw();
            return;
        }
        cameraKeys.Clear();
        cameraTimer.Stop();
        scene = new Scene(Light: scene.Light, Shadows: scene.Shadows, SoftShadows: scene.SoftShadows, ShadowResolution: scene.ShadowResolution, ShowSky: scene.ShowSky, FogDensity: scene.FogDensity);
        rotationX.Value = 35;
        rotationY.Value = -30;
        rotationZ.Value = 0;
        relief.Value = .65;
        Redraw();
    }
    private HydrologyOptions? WaterOptions() => rivers.IsChecked == true
        ? new((float)(seaLevel.Value ?? .02m), .04f * MathF.Pow(.72f, (float)riverDensity.Value), separateWater.IsChecked == true,
            separateWater.IsChecked == true && carveChannels.IsChecked == true ? (float)channelDepth.Value / 1000 : 0) : null;

    private VegetationOptions? ForestOptions() => vegetation.IsChecked == true
        ? new((float)forestDensity.Value / 100, BiomeAware: true) : null;

    private ClimateOptions? ReadClimateOptions(int climateSeed) => biomes.IsChecked == true
        ? new((float)climateTemperature.Value / 100, (float)climateMoisture.Value / 100, climateSeed) : null;

    private ShrubOptions? ReadShrubOptions(int shrubSeed) => shrubs.IsChecked == true
        ? new((float)shrubDensity.Value / 100, Seed: shrubSeed) : null;

    private DetailOptions? ReadDetailOptions(int detailSeed) => terrainDetails.IsChecked == true
        ? new((float)screeDensity.Value / 100, Seed: detailSeed) : null;

    private ErosionOptions? ReadErosionOptions() => erosionEnabled.IsChecked == true
        ? new((int)(erosionIterations.Value ?? 16), (float)erosionStrength.Value / 100, appliedErosion?.Talus ?? .8f) : null;

    private async Task RebuildEnvironment()
    {
        if (restoringProject || map is not { } source || closed)
            return;
        int request = ++mapRequest;
        int surfaceSeed = currentSurfaceSeed;
        var options = WaterOptions();
        var forestOptions = ForestOptions();
        var climateOptions = ReadClimateOptions(appliedClimate?.Seed ?? surfaceSeed);
        var shrubOptions = ReadShrubOptions(appliedShrubs?.Seed ?? surfaceSeed);
        var detailOptions = ReadDetailOptions(appliedDetails?.Seed ?? surfaceSeed);
        var erosionOptions = ReadErosionOptions();
        status.Text = "Обновление климата, воды, леса и камней…";
        try
        {
            var terrain = await Task.Run(() => Mesh.Build(source, surfaceSeed, options, forestOptions, climateOptions, shrubOptions, detailOptions, erosionOptions));
            if (closed || request != mapRequest)
                return;
            mesh = terrain;
            appliedErosion = erosionOptions;
            appliedDetails = detailOptions;
            appliedShrubs = shrubOptions;
            appliedClimate = climateOptions;
            appliedWater = options;
            appliedForest = forestOptions;
            walkingSurface = null;
            if (scene.Walking)
                RefreshWalkingSurface();
            UpdateDetails();
            Redraw();
        }
        catch (Exception error) { status.Text = "Окружение: " + error.Message; }
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
            var waterOptions = WaterOptions();
            var forestOptions = ForestOptions();
            var climateOptions = ReadClimateOptions(options.Seed);
            var shrubOptions = ReadShrubOptions(options.Seed);
            var detailOptions = ReadDetailOptions(options.Seed);
            var erosionOptions = ReadErosionOptions();
            status.Text = "Генерация карты и окружения…";
            var timer = Stopwatch.StartNew();
            var result = await Task.Run(() => { var generated = Generators.Generate(options); return (generated, Mesh.Build(generated, options.Seed, waterOptions, forestOptions, climateOptions, shrubOptions, detailOptions, erosionOptions)); });
            if (closed || request != mapRequest)
                return;
            SetMap(result.generated, result.Item2, timer.Elapsed.TotalMilliseconds, options.Seed, waterOptions, forestOptions, options, climateOptions, shrubOptions, detailOptions, erosionOptions);
        }
        catch (Exception error) { status.Text = error.Message; }
        finally { generate.IsEnabled = true; }
    }
    internal void SetMap(HeightMap heightMap, Mesh terrain, double milliseconds, int surfaceSeed = 0, HydrologyOptions? water = null, VegetationOptions? forest = null, GenerationOptions? generation = null, ClimateOptions? climate = null, ShrubOptions? shrubOptions = null, DetailOptions? detailOptions = null, ErosionOptions? erosionOptions = null)
    {
        appliedErosion = erosionOptions;
        appliedDetails = detailOptions;
        appliedShrubs = shrubOptions;
        appliedClimate = climate;
        appliedWater = water;
        appliedForest = forest;
        appliedGeneration = generation;
        if (!restoringProject && scene.Walking)
            SetWalking(false);
        walkingCamera = null;
        walkingSurface = null;
        currentSurfaceSeed = surfaceSeed;
        map = heightMap;
        mesh = terrain;
        var old = previewBitmap;
        previewBitmap = Images.Bitmap(Images.Preview(map));
        preview.Source = previewBitmap;
        old?.Dispose();
        UpdateDetails();
        ResetView();
    }
    private void UpdateDetails()
    {
        if (map is null || mesh is null)
            return;
        details.Text = $"{map.Width}×{map.Height} · {mesh.Indices.Length / 3:N0} треугольников\nДеревьев: {mesh.TreeCount} · Камней: {mesh.RockCount} · Кустов: {mesh.ShrubCount}";
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
            var waterOptions = WaterOptions();
            var forestOptions = ForestOptions();
            var climateOptions = ReadClimateOptions(0);
            var shrubOptions = ReadShrubOptions(0);
            var detailOptions = ReadDetailOptions(0);
            var erosionOptions = ReadErosionOptions();
            var result = await Task.Run(() => { var loaded = Images.Load(stream); return (loaded, Mesh.Build(loaded, 0, waterOptions, forestOptions, climateOptions, shrubOptions, detailOptions, erosionOptions)); });
            if (!closed && request == mapRequest)
                SetMap(result.loaded, result.Item2, timer.Elapsed.TotalMilliseconds, 0, waterOptions, forestOptions, climate: climateOptions, shrubOptions: shrubOptions, detailOptions: detailOptions, erosionOptions: erosionOptions);
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
                var old = cpuBitmap;
                cpuBitmap = Images.Bitmap(frame);
                image.Source = cpuBitmap;
                old?.Dispose();
                status.Text = $"CPU · {frame.Width}×{frame.Height} · кадр {timer.Elapsed.TotalMilliseconds:F1} мс";
                // Present completed frames during continuous navigation, then catch up.
                if (version == revision)
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
