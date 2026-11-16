using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.TaxWithholding;

// Document dates have no timezone/time component; never shift them via UTC serialization.
public sealed class PitDateJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type type) => type == typeof(DateTime) || type == typeof(DateTime?);
    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        => type == typeof(DateTime) ? new DateConverter() : new NullableDateConverter();

    private static DateTime ReadDate(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String || !DateTime.TryParseExact(reader.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new JsonException("Ngày phải có định dạng yyyy-MM-dd.");
        return date;
    }
    private sealed class DateConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => ReadDate(ref reader);
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
    private sealed class NullableDateConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? null : ReadDate(ref reader);
        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            else writer.WriteNullValue();
        }
    }
}
