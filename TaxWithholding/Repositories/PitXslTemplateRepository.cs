using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitXslTemplateRepository(DapperExecutor db)
{
    public async Task<IReadOnlyList<PitXslTemplate>> ListAsync(string companyCd, string? templateCd = null)
        => (await db.QueryAsync<PitXslTemplate>(
            Net_DB.Net_DB_Company,
            "CALL getPitXslTemplates(@company,@template_cd)",
            new { company = companyCd, template_cd = templateCd ?? "" })).ToList();

    public async Task<PitXslTemplate?> GetAsync(string companyCd, long xslId)
        => (await db.QueryAsync<PitXslTemplate>(
            Net_DB.Net_DB_Company,
            "CALL getPitXslTemplate(@company,@xsl_id)",
            new { company = companyCd, xsl_id = xslId })).FirstOrDefault();

    public async Task<long> SaveAsync(string companyCd, string userId, long xslId, PitXslTemplateSaveRequest request)
    {
        await using var session = await db.CreateSessionAsync();
        var id = await session.QuerySingleAsync<long>(
            "CALL setPitXslTemplate(@company,@xsl_id,@template_cd,@template_nm,@series,@from_doc_no,@to_doc_no,@is_default,@is_active,@xsl_content,@logo_path,@background_path,@nen_path,@user)",
            new
            {
                company = companyCd,
                xsl_id = xslId,
                template_cd = request.TEMPLATE_CD,
                template_nm = request.TEMPLATE_NM,
                series = request.SERIES,
                from_doc_no = request.FROM_DOC_NO ?? 1,
                to_doc_no = request.TO_DOC_NO,
                is_default = request.IS_DEFAULT,
                is_active = request.IS_ACTIVE,
                xsl_content = request.XSL_CONTENT,
                logo_path = request.LOGO_PATH,
                background_path = request.BACKGROUND_PATH,
                nen_path = request.NEN_PATH,
                user = userId
            });
        session.Commit();
        return id;
    }

    public async Task DeleteAsync(string companyCd, string userId, long xslId)
    {
        await using var session = await db.CreateSessionAsync();
        await session.ExecuteAsync(
            "CALL delPitXslTemplate(@company,@xsl_id,@user)",
            new { company = companyCd, xsl_id = xslId, user = userId });
        session.Commit();
    }

    public async Task SetImageAsync(string companyCd, string userId, long xslId, string kind, string path)
    {
        await using var session = await db.CreateSessionAsync();
        await session.ExecuteAsync(
            "CALL setPitXslTemplateImage(@company,@xsl_id,@kind,@path,@user)",
            new { company = companyCd, xsl_id = xslId, kind, path, user = userId });
        session.Commit();
    }
}
