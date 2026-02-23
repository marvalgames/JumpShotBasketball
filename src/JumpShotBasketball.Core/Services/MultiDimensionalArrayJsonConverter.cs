using System.Text.Json;
using System.Text.Json.Serialization;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// Custom JsonConverter for int[,,] since System.Text.Json doesn't support
/// multidimensional arrays natively. Serializes as { dimensions, data }.
/// Used for DraftBoard.DraftChart.
/// </summary>
public class MultiDimensionalArrayJsonConverter : JsonConverter<int[,,]>
{
    public override int[,,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var dims = root.GetProperty("dimensions");
        int d0 = dims[0].GetInt32();
        int d1 = dims[1].GetInt32();
        int d2 = dims[2].GetInt32();

        var result = new int[d0, d1, d2];
        var data = root.GetProperty("data");

        int index = 0;
        for (int i = 0; i < d0; i++)
            for (int j = 0; j < d1; j++)
                for (int k = 0; k < d2; k++)
                    result[i, j, k] = data[index++].GetInt32();

        return result;
    }

    public override void Write(Utf8JsonWriter writer, int[,,] value, JsonSerializerOptions options)
    {
        int d0 = value.GetLength(0);
        int d1 = value.GetLength(1);
        int d2 = value.GetLength(2);

        writer.WriteStartObject();

        writer.WritePropertyName("dimensions");
        writer.WriteStartArray();
        writer.WriteNumberValue(d0);
        writer.WriteNumberValue(d1);
        writer.WriteNumberValue(d2);
        writer.WriteEndArray();

        writer.WritePropertyName("data");
        writer.WriteStartArray();
        for (int i = 0; i < d0; i++)
            for (int j = 0; j < d1; j++)
                for (int k = 0; k < d2; k++)
                    writer.WriteNumberValue(value[i, j, k]);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
