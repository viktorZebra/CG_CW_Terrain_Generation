using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Terrain.App.Controls;

namespace Terrain.App.Views;

public sealed partial class MainWindow
{
    private readonly WorldMapView worldMapView = new();
    private readonly TextBlock worldMapPosition = new() { TextWrapping = TextWrapping.Wrap };
    private Border? worldMapOverlay;
    private bool mapKeyHeld;
    internal bool IsWorldMapOpen => worldMapOverlay?.IsVisible == true;

    private void InitializeWorldMap(Grid area)
    {
        var card = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(20) };
        var header = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock { Text = "Карта мира · север ↑", FontSize = 22, FontWeight = FontWeight.SemiBold });
        header.Children.Add(worldMapPosition);
        card.Children.Add(header);
        Grid.SetRow(worldMapView, 1);
        card.Children.Add(worldMapView);
        var footer = new StackPanel { Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(new TextBlock { Text = "Лес · песчаная пустыня · снежные горы · болото · степь\nБелая стрелка — ваше положение и направление взгляда.", TextWrapping = TextWrapping.Wrap });
        footer.Children.Add(Button("Закрыть карту · M / Esc", () => SetWorldMap(false)));
        Grid.SetRow(footer, 2);
        card.Children.Add(footer);
        var panel = new Border
        {
            Child = card,
            Background = new SolidColorBrush(Color.Parse("#202D32")),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(20),
            MaxWidth = 660,
            MaxHeight = 730
        };
        worldMapOverlay = new Border { Child = panel, Background = new SolidColorBrush(Color.FromArgb(210, 10, 16, 21)), IsVisible = false };
        area.Children.Add(worldMapOverlay);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!worldActive)
                return;
            if (e.Key == Key.M && e.KeyModifiers == KeyModifiers.None)
            {
                if (!mapKeyHeld && worldLoader?.IsVisible != true)
                    SetWorldMap(!IsWorldMapOpen);
                mapKeyHeld = true;
                e.Handled = true;
            }
            else if (IsWorldMapOpen && e.Key == Key.Escape)
            {
                SetWorldMap(false);
                e.Handled = true;
            }
            else if (IsWorldMapOpen && e.Key is Key.W or Key.A or Key.S or Key.D)
                e.Handled = true;
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => { if (e.Key == Key.M) mapKeyHeld = false; }, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => mapKeyHeld = false;
    }

    internal void SetWorldMap(bool open)
    {
        if (worldMapOverlay is null || (open && (!worldActive || worldLoader?.IsVisible == true)))
            return;
        ReleaseLook();
        worldMapView.Camera = scene.ViewCamera;
        worldMapView.InvalidateVisual();
        worldMapPosition.Text = $"X {scene.ViewCamera.Position.X:F1} · Z {scene.ViewCamera.Position.Z:F1} · M / Esc — вернуться к прогулке";
        worldMapOverlay.IsVisible = open;
        if (!open && !closed)
            viewport.Focus();
    }
}
