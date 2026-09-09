using System.Numerics;
using System.Text.Json;

namespace Terrain.Core.Persistence;

public static class SceneFiles
{
    public const int CurrentVersion = 9;
    private const int MaxBytes = 96 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task WriteAsync(Stream stream, SceneDocument document)
    {
        Validate(document);
        await JsonSerializer.SerializeAsync(stream, document, Json);
    }

    public static async Task<SceneDocument> ReadAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[65536];
        int read;
        while ((read = await stream.ReadAsync(chunk)) != 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new InvalidDataException("Файл сцены слишком большой (максимум 96 МБ).");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        SceneDocument document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<SceneDocument>(buffer, Json)
                ?? throw new InvalidDataException("Файл сцены пуст.");
        }
        catch (JsonException error) { throw new InvalidDataException("Повреждённый или неполный файл сцены.", error); }
        Validate(document);
        return document;
    }

    public static void Validate(SceneDocument document)
    {
        void Require(bool valid, string field)
        {
            if (!valid)
                throw new InvalidDataException("Некорректный параметр сцены: " + field);
        }
        bool Range(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
        bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        Require(document.Version is >= 1 and <= CurrentVersion, "версия файла не поддерживается");
        Require(document.Width is >= 2 and <= 2049 && document.Height is >= 2 and <= 2049, "размер карты");
        Require(document.Heights is not null && document.Heights.Length == document.Width * document.Height, "число высот");
        Require(document.Heights!.All(h => Range(h, 0, 1)), "высоты");
        Require(document.View is not null, "камера и освещение");
        var view = document.View!;
        Require(view.WorldTime is null && view.CloudSeed is null && view.CloudTime == 0, "автоматический мир не является проектом карты");
        Require(Range(view.RotationX, -180, 180) && Range(view.RotationY, -180, 180) && Range(view.RotationZ, -180, 180), "поворот модели");
        Require(Range(view.Zoom, .1f, 20) && Range(view.HeightScale, .05f, 2), "масштаб");
        Require(float.IsFinite(view.PanX) && float.IsFinite(view.PanY), "перенос");
        Require(Finite(view.Light) && float.IsFinite(view.Light.LengthSquared()), "свет");
        Require(Range(view.FogDensity, 0, .5f) && (document.Version >= 7 || view.FogDensity == 0), "дымка");
        Require(view.ShadowResolution is >= 16 and <= 4096, "размер карты теней");
        Require(Finite(view.ViewCamera.Position) && float.IsFinite(view.ViewCamera.Yaw) && Range(view.ViewCamera.Pitch, -89, 89), "камера");
        Require(document.Version >= 2 || (!view.Walking && document.Overview is null && document.WalkCamera is null), "режим прогулки требует версию 2");
        if (view.Walking)
            Require(view.RotationX == 0 && view.RotationY == 0 && view.RotationZ == 0 && view.PanX == 0 && view.PanY == 0 && view.Zoom == 1, "горизонтальная сцена прогулки");
        if (document.WalkCamera is { } walk)
            Require(Finite(walk.Position) && float.IsFinite(walk.Yaw) && Range(walk.Pitch, -89, 89), "сохранённая прогулочная камера");
        if (document.Overview is { } overview)
        {
            Require(!overview.Walking, "сохранённый обзор");
            Validate(document with
            {
                View = overview,
                Overview = null,
                WalkCamera = null
            });
        }
        Require(document.Version >= 3 || document.Climate is null, "биомы требуют версию 3");
        if (document.Climate is { } climate)
            Require(Range(climate.Temperature, 0, 1) && Range(climate.Moisture, 0, 1), "климат");
        if (document.Water is { } water)
        {
            Require(Range(water.SeaLevel, 0, 1) && Range(water.RiverFraction, .00001f, 1) && Range(water.ChannelDepth, 0, .08f), "вода");
            Require(water.SeparateSurface || water.ChannelDepth == 0, "русла требуют отдельной воды");
            Require(document.Version >= 8 || (!water.SeparateSurface && water.ChannelDepth == 0), "геометрия воды требует версию 8");
        }
        Require(document.Version >= 4 || document.Vegetation?.BiomeAware != true, "лес по биомам требует версию 4");
        if (document.Vegetation is { } forest)
            Require(Range(forest.Density, 0, 1) && forest.MaxTrees is >= 0 and <= 2000 && forest.MaxRocks is >= 0 and <= 500, "растительность");
        Require(document.Version >= 5 || document.Shrubs is null, "кустарники требуют версию 5");
        if (document.Shrubs is { } shrubs)
            Require(Range(shrubs.Density, 0, 1) && shrubs.MaxShrubs is >= 0 and <= 1500, "кустарники");
        Require(document.Version >= 6 || document.Details is null, "детали требуют версию 6");
        if (document.Details is { } details)
            Require(Range(details.RockDensity, 0, 1) && details.MaxRocks is >= 0 and <= 1000, "осыпи");
        Require(document.Version >= 9 || document.Erosion is null, "эрозия требует версию 9");
        if (document.Erosion is { } erosion)
            Require(erosion.Iterations is >= 0 and <= 60 && Range(erosion.Strength, 0, .5f) && Range(erosion.Talus, .1f, 3), "эрозия");
        if (document.Generation is { } generation)
        {
            Require(Enum.IsDefined(generation.Kind) && generation.Size is >= 2 and <= 1025 &&
                generation.Hills is >= 0 and <= 10000 && generation.Octaves is >= 1 and <= 10 &&
                Range(generation.Frequency, 1, 32) && Range(generation.Roughness, .1f, .9f), "генератор");
            Require(generation.Size == document.Width && generation.Size == document.Height, "размер генератора и карты");
            if (generation.Kind == Generation.GeneratorKind.DiamondSquare)
                Require(((generation.Size - 1) & (generation.Size - 2)) == 0, "размер Diamond–Square");
        }
    }
}
