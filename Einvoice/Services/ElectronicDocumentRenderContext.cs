namespace API_AMNOTE_WEB.Services;

/// <summary>One pinned template/asset manifest for a document render; never selected by the client.</summary>
public sealed class ElectronicDocumentRenderContext
{
    public ElectronicDocumentTemplateSnapshot? Template { get; set; }
}

public sealed record ElectronicDocumentTemplateSnapshot(
    string XslPath, string? Logo = null, string? Background = null, string? Nen = null, string? Border = null,
    string? SettingsHash = null);
