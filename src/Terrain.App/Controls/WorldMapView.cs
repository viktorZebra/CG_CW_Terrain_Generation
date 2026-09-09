using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Terrain.Core.Rendering;
using Terrain.Core.World;

namespace Terrain.App.Controls;

internal sealed class WorldMapView : Control
{
    public WriteableBitmap? Map
    {
        get; set;
    }
    public CameraPose Camera { get; set; } = CameraPose.Default;
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double size = Math.Min(Bounds.Width, Bounds.Height);
        var rectangle = new Rect((Bounds.Width - size) / 2, (Bounds.Height - size) / 2, size, size);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#172125")), rectangle);
        if (Map is not null)
            context.DrawImage(Map, new Rect(Map.Size), rectangle);
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)), 1);
        for (int i = 1; i < 8; i++)
        {
            double offset = size * i / 8;
            context.DrawLine(grid, new(rectangle.X + offset, rectangle.Y), new(rectangle.X + offset, rectangle.Bottom));
            context.DrawLine(grid, new(rectangle.X, rectangle.Y + offset), new(rectangle.Right, rectangle.Y + offset));
        }
        var location = WorldAtlas.ToMap(Camera.Position);
        var center = new Point(rectangle.X + location.X * size, rectangle.Y + location.Y * size);
        var forward = Camera.Forward;
        var tip = new Point(center.X + forward.X * 22, center.Y + forward.Z * 22);
        context.DrawEllipse(Brushes.Black, null, center, 7, 7);
        context.DrawEllipse(Brushes.White, null, center, 4, 4);
        context.DrawLine(new Pen(Brushes.Black, 6), center, tip);
        context.DrawLine(new Pen(Brushes.White, 3), center, tip);
        var side = new Vector(-forward.Z * 5, forward.X * 5);
        var back = new Point(tip.X - forward.X * 7, tip.Y - forward.Z * 7);
        context.DrawLine(new Pen(Brushes.White, 3), tip, back + side);
        context.DrawLine(new Pen(Brushes.White, 3), tip, back - side);
    }
}
