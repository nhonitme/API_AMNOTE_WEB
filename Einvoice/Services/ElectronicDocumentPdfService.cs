using System.Data;
using System.Text;
using System.Text.Json;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Services;

/// <summary>
/// All document PDF entry points share this persistent artifact store. A connection-scoped
/// MySQL lock serializes producers across processes without holding a business transaction.
/// </summary>
public sealed class ElectronicDocumentPdfService(
    DapperExecutor db, ElectronicDocumentStorage files, ElectronicDocumentRenderContext renderContext,
    IEInvoiceHtmlToPdfService converter, ICompanyDatabaseResolver databases)
{
    public sealed record Source(string Table, string Id, string? TemplateCode = null);
    public static readonly IReadOnlyDictionary<string, Source> Sources = new Dictionary<string, Source>
    {
        ["invoice"] = new("einvoice_info", "INVOICE_ID"),
        ["minute"] = new("einvoice_bban_info", "BBAN_ID", "BBDCTT"),
        ["declaration"] = new("einvoice_tkhai_info", "TKHAI_ID", "01/ĐKTĐ-HĐĐT"),
        ["error-notice"] = new("einvoice_tbao_info", "TBAO_ID", "04/SS-HĐĐT"),
        ["pit-certificate"] = new("pit_ctu_info", "CTU_ID"),
        ["pit-declaration"] = new("pit_tkhai_info", "TKHAI_ID", "01/ĐKTĐ-CTĐT"),
        ["pit-error-notice"] = new("pit_tbao_info", "TBAO_ID", "04/SS-CTĐT"),
        ["transmission"] = new("einvoice_message_receive", "RECEIVE_ID")
    };

    private sealed class Revision
    {
        public long PDF_REVISION { get; set; }
        public int IS_SIGNED { get; set; }
        public string? PDF_TEMPLATE { get; set; }
    }
    private sealed class Artifact
    {
        public string? PDF_PATH { get; set; }
        public string? HTML_PATH { get; set; }
        public string? TEMPLATE_JSON { get; set; }
    }

    // Called inside the signature transaction for documents whose XSL is in manager DB.
    // This pins metadata only; FTP and Chromium never run while business locks are held.
    public async Task PinSharedTemplateAsync(DapperSession session, string company, string kind, long id)
    {
        var source = Sources[kind];
        if (source.TemplateCode == null) throw new ArgumentException("A shared template document is required.");
        var templateCode = kind is "declaration" or "error-notice"
            ? await session.QuerySingleAsync<string>($"SELECT MSO FROM {source.Table} WHERE COMPANY_CD=@company AND {source.Id}=@id", new { company, id })
            : source.TemplateCode;
        var path = await db.QueryFirstOrDefaultAsync<string>(Net_DB.Net_DB_Manager,
            "SELECT XSL_FTP_PATH FROM einvoice_template WHERE TEMPLATE_TYPE='XSL' AND TEMPLATE_CD=@templateCode AND IS_ACTIVE=1 AND ISDEL=0 ORDER BY TEMPLATE_ID DESC LIMIT 1", new { templateCode });
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Shared XSL FTP reference is not configured.");
        var template = JsonSerializer.Serialize(new ElectronicDocumentTemplateSnapshot(path));
        await session.ExecuteAsync($"UPDATE {source.Table} SET PDF_TEMPLATE=@template WHERE COMPANY_CD=@company AND {source.Id}=@id AND IS_SIGNED=1 AND PDF_TEMPLATE IS NULL", new { template, company, id });
    }

    public async Task<byte[]> GetAsync(string company, string kind, long id, object? options,
        Func<CancellationToken, Task<string>> buildHtml, CancellationToken cancellationToken = default)
    {
        if (!Sources.TryGetValue(kind, out var source) || id <= 0 || string.IsNullOrWhiteSpace(company))
            throw new ArgumentException("Invalid PDF document identity.");
        var database = await databases.ResolveDatabaseNameAsync(company);
        var sourceDatabase = kind == "transmission" ? Net_DB.Net_DB_Manager : Net_DB.Net_DB_Company;
        var activePredicate = kind == "transmission" ? "" : " AND ISDEL=0";
        var signedExpression = kind == "transmission" ? "1 AS IS_SIGNED" : "IS_SIGNED";
        async Task<Revision> ReadRevision() => await db.QueryFirstOrDefaultAsync<Revision>(sourceDatabase,
            $"SELECT PDF_REVISION, {signedExpression}, PDF_TEMPLATE FROM {source.Table} WHERE COMPANY_CD=@company AND {source.Id}=@id{activePredicate}",
            new { company, id }, sDBName: database) ?? throw new KeyNotFoundException("Document not found.");

        // Only small metadata is fetched on a cache hit, never XML, XSL or detail rows.
        var revision = await ReadRevision();
        var template = revision.PDF_TEMPLATE;
        if (revision.IS_SIGNED == 1 && kind != "transmission" && string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException("Signed document template snapshot is missing. Complete the explicit cutover pin step; the current template will not be substituted.");
        if (string.IsNullOrWhiteSpace(template))
            template = await ReadTemplateAsync(company, kind, id, database, source.TemplateCode);
        if (revision.IS_SIGNED == 1 && string.IsNullOrWhiteSpace(revision.PDF_TEMPLATE))
        {
            await db.ExecuteAsync(sourceDatabase,
                $"UPDATE {source.Table} SET PDF_TEMPLATE=@template WHERE COMPANY_CD=@company AND {source.Id}=@id AND PDF_REVISION=@revision AND PDF_TEMPLATE IS NULL" + (kind == "transmission" ? "" : " AND IS_SIGNED=1"),
                new { template, company, id, revision = revision.PDF_REVISION }, sDBName: database);
            var pinned = await ReadRevision();
            if (pinned.PDF_REVISION != revision.PDF_REVISION || string.IsNullOrWhiteSpace(pinned.PDF_TEMPLATE))
                throw new InvalidOperationException("Document changed while pinning its PDF template.");
            template = pinned.PDF_TEMPLATE;
        }
        var optionsJson = JsonSerializer.Serialize(options);
        var key = ElectronicDocumentStorage.Hash(JsonSerializer.Serialize(new
        {
            database, company, kind, id, revision = revision.PDF_REVISION,
            // Signed documents retain their pinned template. Drafts follow explicit template changes.
            template, optionsJson, renderer = "chromium-v1"
        }));
        var parameters = new { key, company, kind, id, revision = revision.PDF_REVISION, template, optionsJson };
        using var connection = db.GetOpenConnection(Net_DB.Net_DB_Company, database);
        var lockName = "pdf:" + key[..60];
        var locked = await connection.ExecuteScalarAsync<int>("SELECT GET_LOCK(@name,120)", new { name = lockName });
        if (locked != 1) throw new InvalidOperationException("PDF đang được tạo. Vui lòng thử lại sau.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((await ReadRevision()).PDF_REVISION != revision.PDF_REVISION)
                throw new InvalidOperationException("Document changed while waiting for PDF. Open the latest revision.");
            if (revision.IS_SIGNED == 0 && await ReadTemplateAsync(company, kind, id, database, source.TemplateCode) != template)
                throw new InvalidOperationException("Document template/settings changed. Open the latest PDF.");
            await connection.ExecuteAsync("""
                INSERT IGNORE INTO electronic_document_pdf
                    (ARTIFACT_KEY,COMPANY_CD,DOCUMENT_KIND,DOCUMENT_ID,SOURCE_REVISION,TEMPLATE_JSON,OPTIONS_JSON,STATE)
                VALUES (@key,@company,@kind,@id,@revision,@template,@optionsJson,'PENDING')
                """, parameters);
            var artifact = await connection.QuerySingleAsync<Artifact>(
                "SELECT PDF_PATH,HTML_PATH,TEMPLATE_JSON FROM electronic_document_pdf WHERE ARTIFACT_KEY=@key", parameters);
            if (!string.IsNullOrWhiteSpace(artifact.PDF_PATH))
            {
                var cached = files.Read(artifact.PDF_PATH);
                // Blank Chromium captures (~1.1KB) were cached after printing before JS pagination finished.
                if (cached.Length >= 4096)
                    return cached;

                await connection.ExecuteAsync(
                    "UPDATE electronic_document_pdf SET PDF_PATH=NULL,PDF_SHA256=NULL,PDF_SIZE=NULL,STATE='PENDING',ERROR_CODE=NULL WHERE ARTIFACT_KEY=@key AND IS_USED_LOCK(@lockName)=CONNECTION_ID()",
                    new { key, lockName });
                artifact.PDF_PATH = null;
            }

            await connection.ExecuteAsync("UPDATE electronic_document_pdf SET STATE='GENERATING',ERROR_CODE=NULL WHERE ARTIFACT_KEY=@key", parameters);
            string html;
            if (!string.IsNullOrWhiteSpace(artifact.HTML_PATH))
            {
                // Retry uses the exact immutable render snapshot, never a newer template/source.
                html = Encoding.UTF8.GetString(files.Read(artifact.HTML_PATH));
            }
            else
            {
                var previousTemplate = renderContext.Template;
                try
                {
                    renderContext.Template = JsonSerializer.Deserialize<ElectronicDocumentTemplateSnapshot>(artifact.TEMPLATE_JSON!)
                        ?? throw new InvalidOperationException("PDF template snapshot is missing.");
                    if ((await ReadRevision()).PDF_REVISION != revision.PDF_REVISION)
                        throw new InvalidOperationException("Document changed before PDF rendering. Open the latest revision.");
                    html = await buildHtml(cancellationToken);
                    if ((await ReadRevision()).PDF_REVISION != revision.PDF_REVISION)
                        throw new InvalidOperationException("Document changed during PDF rendering. Open the latest revision.");
                    if (revision.IS_SIGNED == 0 && await ReadTemplateAsync(company, kind, id, database, source.TemplateCode) != template)
                        throw new InvalidOperationException("Document template/settings changed during rendering.");
                }
                finally { renderContext.Template = previousTemplate; }
                var htmlPath = files.Write(await files.NamespaceAsync(company), "render", Encoding.UTF8.GetBytes(html), "html");
                await connection.ExecuteAsync("UPDATE electronic_document_pdf SET HTML_PATH=@htmlPath WHERE ARTIFACT_KEY=@key AND IS_USED_LOCK(@lockName)=CONNECTION_ID()",
                    new { htmlPath, key, lockName });
            }
            var pdf = await converter.ConvertAsync(html, cancellationToken);
            if ((await ReadRevision()).PDF_REVISION != revision.PDF_REVISION)
                throw new InvalidOperationException("Document changed during PDF conversion. Open the latest revision.");
            var path = files.Write(await files.NamespaceAsync(company), "pdf", pdf, "pdf");
            // No 'current PDF' pointer: consumers always resolve the current revision first.
            var published = await connection.ExecuteAsync("""
                UPDATE electronic_document_pdf SET PDF_PATH=@path,PDF_SHA256=@hash,PDF_SIZE=@size,
                    STATE='READY',ERROR_CODE=NULL,UPDATE_AT=UTC_TIMESTAMP(6)
                WHERE ARTIFACT_KEY=@key AND IS_USED_LOCK(@lockName)=CONNECTION_ID()
                """, new { path, hash = ElectronicDocumentStorage.Hash(pdf), size = pdf.Length, key, lockName });
            if (published != 1) throw new InvalidOperationException("PDF producer lost its distributed lock.");
            return pdf;
        }
        catch
        {
            await connection.ExecuteAsync("UPDATE electronic_document_pdf SET STATE='FAILED',ERROR_CODE='PDF_GENERATION_FAILED' WHERE ARTIFACT_KEY=@key AND PDF_PATH IS NULL", parameters);
            throw;
        }
        finally { await connection.ExecuteAsync("SELECT RELEASE_LOCK(@name)", new { name = lockName }); }
    }

    private async Task<string> ReadTemplateAsync(string company, string kind, long id, string database, string? templateCode)
    {
        if (kind == "transmission")
        {
            templateCode = await db.QuerySingleAsync<string>(Net_DB.Net_DB_Manager,
                "SELECT MLTDIEP FROM einvoice_message_receive WHERE COMPANY_CD=@company AND RECEIVE_ID=@id", new { company, id });
            if (EInvoiceMessageTypeCodes.ShouldPreviewWithSellerXsl(templateCode))
            {
                var invoiceId = await db.QueryFirstOrDefaultAsync<long?>(Net_DB.Net_DB_Manager, """
                    SELECT s.INVOICE_ID FROM einvoice_message_receive r JOIN einvoice_message_send s
                        ON s.COMPANY_CD=r.COMPANY_CD AND (s.MTDIEP=r.MTDTCHIEU OR s.MTDIEP=r.MTRA_CUU)
                    WHERE r.COMPANY_CD=@company AND r.RECEIVE_ID=@id AND s.INVOICE_ID IS NOT NULL
                    ORDER BY s.SEND_ID DESC LIMIT 1
                    """, new { company, id });
                if (invoiceId is not > 0) throw new InvalidOperationException("Transmission does not identify an invoice.");
                var pinned = await db.QueryFirstOrDefaultAsync<string>(Net_DB.Net_DB_Company,
                    "SELECT PDF_TEMPLATE FROM einvoice_info WHERE COMPANY_CD=@company AND INVOICE_ID=@invoiceId AND ISDEL=0",
                    new { company, invoiceId }, sDBName: database);
                return pinned ?? await ReadTemplateAsync(company, "invoice", invoiceId.Value, database, null);
            }
        }
        if (kind is "declaration" or "error-notice")
        {
            var source = Sources[kind];
            templateCode = await db.QuerySingleAsync<string>(Net_DB.Net_DB_Company,
                $"SELECT MSO FROM {source.Table} WHERE COMPANY_CD=@company AND {source.Id}=@id AND ISDEL=0", new { company, id }, sDBName: database);
            if (string.IsNullOrWhiteSpace(templateCode)) throw new InvalidOperationException("Document template code is missing.");
        }
        if (templateCode != null)
        {
            var path = await db.QueryFirstOrDefaultAsync<string>(Net_DB.Net_DB_Manager,
                "SELECT XSL_FTP_PATH FROM einvoice_template WHERE TEMPLATE_TYPE='XSL' AND TEMPLATE_CD=@templateCode AND IS_ACTIVE=1 AND ISDEL=0 ORDER BY TEMPLATE_ID DESC LIMIT 1", new { templateCode });
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Shared XSL FTP reference is not configured.");
            return JsonSerializer.Serialize(new ElectronicDocumentTemplateSnapshot(path));
        }
        var json = kind == "invoice"
            ? await db.QueryFirstOrDefaultAsync<string>(Net_DB.Net_DB_Company, """
                SELECT JSON_OBJECT('XslPath',c.XSL_FTP_PATH,'Logo',d.LOGO_PATH,'Background',d.BACKGROUND_PATH,
                    'Nen',d.INVOICE_BACKGROUND_PATH,'Border',d.INVOICE_BORDER_PATH,'SettingsHash',SHA2(CONCAT(COALESCE((SELECT CAST(JSON_ARRAYAGG(JSON_OBJECT('SETTING_ID',ds.`SETTING_ID`,'COMPANY_CD',ds.`COMPANY_CD`,'XSL_ID',ds.`XSL_ID`,'APPLY_TARGET',ds.`APPLY_TARGET`,'FIELD_SCOPE',ds.`FIELD_SCOPE`,'FIELD_NAME',ds.`FIELD_NAME`,'CAPTION',ds.`CAPTION`,'LABEL_TEXT',ds.`LABEL_TEXT`,'CURRENCY_SCOPE',ds.`CURRENCY_SCOPE`,'DECIMAL_SCALE',ds.`DECIMAL_SCALE`,'ROUND_MODE',ds.`ROUND_MODE`,'IS_ACTIVE',ds.`IS_ACTIVE`,'SORT_ORDER',ds.`SORT_ORDER`,'NOTE',ds.`NOTE`)) OVER (ORDER BY ds.SETTING_ID ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS CHAR) FROM einvoice_decimal_setting ds WHERE ds.COMPANY_CD IN (i.COMPANY_CD,'') AND ds.XSL_ID IN (0,i.XSL_ID) LIMIT 1),''),COALESCE((SELECT CAST(JSON_OBJECT('Name',SELLER_NM,'Tax',SELLER_TAX_CD,'Address',SELLER_ADDRESS,'Phone',SDTHOAI,'Email',DCTDTU,'Account',STKNHANG,'Bank',TNHANG) AS CHAR) FROM einvoice_seller_info WHERE SELLER_ID=i.SELLER_ID AND COMPANY_CD=i.COMPANY_CD),'')),256))
                FROM einvoice_info i JOIN einvoice_seller_xsl_template_content c ON c.XSL_ID=i.XSL_ID
                LEFT JOIN einvoice_seller_xsl_template_design d ON d.DESIGN_ID=c.CURRENT_DESIGN_ID
                WHERE i.COMPANY_CD=@company AND i.INVOICE_ID=@id AND i.ISDEL=0
                """, new { company, id }, sDBName: database)
            : await db.QueryFirstOrDefaultAsync<string>(Net_DB.Net_DB_Company, """
                SELECT JSON_OBJECT('XslPath',c.XSL_FTP_PATH,'Logo',c.LOGO_PATH,'Background',c.BACKGROUND_PATH,'Nen',c.NEN_PATH)
                FROM pit_ctu_info i JOIN pit_xsl_template_content c ON c.XSL_ID=i.XSL_ID
                WHERE i.COMPANY_CD=@company AND i.CTU_ID=@id AND i.ISDEL=0
                """, new { company, id }, sDBName: database);
        var snapshot = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ElectronicDocumentTemplateSnapshot>(json);
        if (string.IsNullOrWhiteSpace(snapshot?.XslPath)) throw new InvalidOperationException("Document XSL FTP reference is not configured.");
        return JsonSerializer.Serialize(snapshot);
    }
}
