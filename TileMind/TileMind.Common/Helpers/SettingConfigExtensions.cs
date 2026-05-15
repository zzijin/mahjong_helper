using System.Text.Json;
using System.Text.Json.Serialization;
using TileMind.Common.Config;

namespace TileMind.Common.Helpers;

public static class SettingConfigExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new OpenCvPointConverter() }
    };

    /// <summary>从 JSON 文件加载配置对象。</summary>
    public static T? Load<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath)) return default;
        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var prop = root.EnumerateObject().FirstOrDefault();
        if (prop.Name == null) return default;
        return JsonSerializer.Deserialize<T>(prop.Value.GetRawText(), _jsonOptions);
    }

    extension(FrameFusionOptions options)
    {
        public void Save(string filePath = FrameFusionOptions.SettingFilePath)
        {
            WriteJson(filePath, "FrameFusion", options);
        }
    }
    extension(ScreenCaptureOptions options)
    {
        public void Save(string filePath = ScreenCaptureOptions.SettingFilePath)
        {
            WriteJson(filePath, "ScreenCapture", options);
        }
    }
    extension(YoloOptions options)
    {
        public void Save(string filePath = YoloOptions.SettingFilePath)
        {
            WriteJson(filePath, "Yolo", options);
        }
    }

    private static void WriteJson<T>(string filePath, string sectionName, T value)
    {
        using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WritePropertyName(sectionName);
        JsonSerializer.Serialize(writer, value, _jsonOptions);
        writer.WriteEndObject();
        writer.Flush();
    }
}

/// <summary>
/// OpenCvSharp.Point 的 X/Y 是字段，System.Text.Json 默认不序列化字段。
/// 此 Converter 将 Point 序列化为 {"X":n,"Y":n}。
/// </summary>
public class OpenCvPointConverter : JsonConverter<OpenCvSharp.Point>
{
    public override OpenCvSharp.Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        int x = 0, y = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var prop = reader.GetString();
                reader.Read();
                if (prop == "X") x = reader.GetInt32();
                else if (prop == "Y") y = reader.GetInt32();
            }
        }
        return new OpenCvSharp.Point(x, y);
    }

    public override void Write(Utf8JsonWriter writer, OpenCvSharp.Point value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }
}
