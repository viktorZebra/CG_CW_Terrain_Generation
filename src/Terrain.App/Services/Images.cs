using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using Terrain.Core.Models;
using Terrain.Core.Rendering;

namespace Terrain.App.Services;

internal static class Images
{
    public static HeightMap Load(Stream stream)
    {
        using var data = SKData.Create(stream);
        using var codec = SKCodec.Create(data) ?? throw new ArgumentException("Не удалось прочитать изображение.");
        var map = new HeightMap(codec.Info.Width, codec.Info.Height); // validate before pixel allocation
        using var bitmap = SKBitmap.Decode(codec) ?? throw new ArgumentException("Формат изображения не поддерживается.");
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                map[x, y] = (.2126f * pixel.Red + .7152f * pixel.Green + .0722f * pixel.Blue) / 255;
            }
        return map;
    }
    public static WriteableBitmap Bitmap(Frame frame)
    {
        var bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = bitmap.Lock();
        for (int y = 0; y < frame.Height; y++)
            Marshal.Copy(frame.Pixels, y * frame.Width * 4, locked.Address + y * locked.RowBytes, frame.Width * 4);
        return bitmap;
    }
    public static Frame Preview(HeightMap map)
    {
        var pixels = new byte[map.Width * map.Height * 4];
        for (int i = 0; i < map.Values.Length; i++)
        {
            byte value = (byte)Math.Clamp((int)MathF.Round(map.Values[i] * 255), 0, 255);
            pixels[4 * i] = pixels[4 * i + 1] = pixels[4 * i + 2] = value;
            pixels[4 * i + 3] = 255;
        }
        return new(map.Width, map.Height, pixels);
    }
    public static void Save(Frame frame, Stream stream, bool bmp)
    {
        if (!bmp)
        {
            using var bitmap = Bitmap(frame);
            bitmap.Save(stream);
            return;
        }
        // Uncompressed 32-bit BMP, bottom-up rows.
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x4D42);
        writer.Write(54 + frame.Pixels.Length);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(frame.Width);
        writer.Write(frame.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(frame.Pixels.Length);
        writer.Write(3780);
        writer.Write(3780);
        writer.Write(0);
        writer.Write(0);
        for (int y = frame.Height - 1; y >= 0; y--)
            writer.Write(frame.Pixels, y * frame.Width * 4, frame.Width * 4);
    }
}
