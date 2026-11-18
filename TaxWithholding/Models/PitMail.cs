using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitSendMailRequest
{
    [Required, MaxLength(1000), JsonPropertyName("toEmail")]
    public string ToEmail { get; set; } = "";
}

public sealed record PitSendMailResult(bool Sent, bool HistorySaved, string? Warning);
