using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Helpers
{
    public sealed class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        private static readonly string[] Formats =
        [
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "yyyyMMdd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
            "O"
        ];

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Date value must be a string");

            var text = reader.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
                return exact;

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
                return parsed;

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                return parsed;

            throw new JsonException($"Invalid date value: {text}");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
                return;
            }

            writer.WriteNullValue();
        }
    }
}
