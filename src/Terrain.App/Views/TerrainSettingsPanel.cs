using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Terrain.App.Views;

/// <summary>Presentation-only grouping; scene state remains in MainWindow.</summary>
internal sealed class TerrainSettingsPanel
{
    private readonly Dictionary<string, StackPanel> pages = new();
    public Border View
    {
        get;
    }
    private readonly TabControl tabs;
    public StackPanel WorldPanel { get; } = new() { Spacing = 14, Margin = new Thickness(18), IsVisible = false };
    public void ShowWorld(bool active)
    {
        tabs.IsVisible = !active;
        WorldPanel.IsVisible = active;
    }

    public TerrainSettingsPanel(Control renderer, Control navigation)
    {
        renderer.HorizontalAlignment = HorizontalAlignment.Stretch;
        navigation.HorizontalAlignment = HorizontalAlignment.Stretch;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var heading = new StackPanel { Spacing = 10, Margin = new Thickness(18, 20, 18, 14) };
        heading.Children.Add(new TextBlock
        {
            Text = "Ландшафт",
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E4ECE9"))
        });
        heading.Children.Add(new TextBlock
        {
            Text = "МАСТЕРСКАЯ ПРИРОДЫ",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#8AB5A9"))
        });
        heading.Children.Add(navigation);
        heading.Children.Add(renderer);
        layout.Children.Add(heading);
        tabs = new TabControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var items = new List<TabItem>();
        foreach (string name in new[] { "Карта", "Природа", "Вид" })
        {
            var page = new StackPanel { Spacing = 12, Margin = new Thickness(12, 12, 12, 20) };
            pages.Add(name, page);
            items.Add(new TabItem
            {
                Header = name,
                FontSize = 15,
                Padding = new Thickness(14, 10),
                Content = new ScrollViewer
                {
                    Content = page,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                }
            });
        }
        tabs.ItemsSource = items;
        tabs.SelectedIndex = 0;
        Grid.SetRow(tabs, 1);
        layout.Children.Add(tabs);
        Grid.SetRow(WorldPanel, 1);
        layout.Children.Add(WorldPanel);
        View = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#172125")),
            BorderBrush = new SolidColorBrush(Color.Parse("#344348")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = layout
        };
    }

    public StackPanel Section(string page, string title, string description, bool expanded)
    {
        var content = new StackPanel { Spacing = 10, Margin = new Thickness(12, 2, 12, 14) };
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeight.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#A4B4B8")),
            MaxWidth = 235
        });
        var section = new Expander
        {
            Header = header,
            Content = content,
            IsExpanded = expanded,
            Background = new SolidColorBrush(Color.Parse("#202D32")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        Avalonia.Automation.AutomationProperties.SetName(section, title);
        pages[page].Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#202D32")),
            BorderBrush = new SolidColorBrush(Color.Parse("#34464C")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = section
        });
        return content;
    }
}
