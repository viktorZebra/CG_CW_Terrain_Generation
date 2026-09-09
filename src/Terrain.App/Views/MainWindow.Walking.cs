using Avalonia.Controls;
using Avalonia.Input;
using Terrain.App.Services;
using Terrain.Core.Hydrology;
using Terrain.Core.Rendering;

namespace Terrain.App.Views;

public sealed partial class MainWindow
{
    private readonly ComboBox navigationMode = new() { ItemsSource = new[] { "Обзор", "Прогулка", "Мир" }, SelectedIndex = 0 };
    private readonly List<Control> overviewControls = [];
    private readonly MacMouseLook mouseLook = new();
    private Scene? overviewScene;
    private CameraPose? walkingCamera;
    private WalkingSurface? walkingSurface;
    private bool switchingNavigation;
    internal bool IsMouseLookActive => mouseLook.Active;

    internal bool StartWalkingLook()
    {
        if (!scene.Walking)
            return false;
        viewport.Focus();
        if (!mouseLook.Begin())
            return false;
        viewport.Cursor = new Cursor(StandardCursorType.None);
        cameraTick = System.Diagnostics.Stopwatch.GetTimestamp();
        cameraTimer.Start();
        return true;
    }

    private void ReleaseLook()
    {
        mouseLook.End();
        viewport.Cursor = Cursor.Default;
        cameraKeys.Clear();
        cameraTimer.Stop();
        previous = null;
    }

    private void RefreshWalkingSurface()
    {
        walkingSurface ??= map is null ? null : new WalkingSurface(mesh?.WorkingMap ?? map,
            mesh?.Water ?? (appliedWater is null ? null : HydrologyBuilder.Build(map, appliedWater)));
        if (!scene.Walking || walkingSurface is null)
            return;
        var camera = walkingSurface.Anchor(scene.ViewCamera, scene.HeightScale) ?? walkingSurface.Spawn(scene.HeightScale);
        if (camera is null)
        {
            SetWalking(false);
            status.Text = "Не удалось выбрать точку прогулки на карте.";
        }
        else
            scene = scene with
            {
                Camera = camera
            };
    }

    internal bool SetWalking(bool enabled)
    {
        if (restoringProject || switchingNavigation)
            return scene.Walking;
        ReleaseLook();
        if (enabled && !scene.Walking)
        {
            RefreshWalkingSurface();
            var camera = walkingCamera is null ? null : walkingSurface?.Anchor(walkingCamera, scene.HeightScale);
            camera ??= walkingSurface?.Spawn(scene.HeightScale);
            if (camera is null)
            {
                switchingNavigation = true;
                navigationMode.SelectedIndex = 0;
                switchingNavigation = false;
                status.Text = "Не удалось выбрать точку прогулки на карте.";
                return false;
            }
            overviewScene = scene;
            scene = scene with
            {
                Walking = true,
                Camera = camera,
                RotationX = 0,
                RotationY = 0,
                RotationZ = 0,
                PanX = 0,
                PanY = 0,
                Zoom = 1
            };
        }
        else if (!enabled && scene.Walking)
        {
            walkingCamera = scene.ViewCamera;
            scene = (overviewScene ?? new Scene()) with
            {
                FogDensity = scene.FogDensity,
                Light = scene.Light,
                Shadows = scene.Shadows,
                SoftShadows = scene.SoftShadows,
                ShadowResolution = scene.ShadowResolution,
                ShowSky = scene.ShowSky,
                HeightScale = scene.HeightScale,
                Walking = false
            };
        }
        SyncNavigationControls();
        Redraw();
        return scene.Walking;
    }

    private void SyncNavigationControls()
    {
        switchingNavigation = true;
        navigationMode.SelectedIndex = worldActive ? 2 : scene.Walking ? 1 : 0;
        rotationX.Value = scene.RotationX;
        rotationY.Value = scene.RotationY;
        rotationZ.Value = scene.RotationZ;
        foreach (var slider in new[] { rotationX, rotationY, rotationZ })
            slider.IsEnabled = !scene.Walking;
        foreach (var control in overviewControls)
            control.IsEnabled = !scene.Walking;
        switchingNavigation = false;
    }
}
