using System.Text.Json;

namespace API_AMNOTE_WEB.Models
{
    public sealed class DeleteActivityLogEntry
    {
        public long EntityId { get; init; }

        public string RecordId { get; init; } = string.Empty;

        public string OldData { get; init; } = string.Empty;

        public static DeleteActivityLogEntry From<T>(long entityId, string recordId, T entity)
        {
            return new DeleteActivityLogEntry
            {
                EntityId = entityId,
                RecordId = recordId ?? string.Empty,
                OldData = JsonSerializer.Serialize(entity)
            };
        }
    }
}
