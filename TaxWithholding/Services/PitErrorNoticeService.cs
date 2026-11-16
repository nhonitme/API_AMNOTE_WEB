using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using System.Globalization;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitErrorNoticeService(DapperExecutor db, IPitErrorNoticeRepository repository, IPitRepository documents) : IPitErrorNoticeService
{
    public async Task<long> SaveAsync(string company, string user, long id, int version, PitErrorNoticeDto data)
    {
        PitErrorNoticeValidator.Validate(data);
        foreach (var item in data.Items.Where(item => item.REF_CTU_ID > 0))
        {
            var certificate = await documents.Get(company, PitKinds.Certificate, item.REF_CTU_ID!.Value);
            if (certificate.IS_SIGNED != 1 || certificate.TAX_CD != data.MST || item.KHCTU != certificate.SERIES
                || item.SCTU != certificate.DOC_NO?.ToString(CultureInfo.InvariantCulture)
                || item.NLAP.Date != certificate.DOC_DATE?.Date || item.KHMSCTU != "03/TNCN" || item.LCTDT != "8")
                throw new ArgumentException("Chứng từ được chọn không khớp thông tin đã ký hoặc MST tổ chức.");
        }
        await using var session = await db.CreateSessionAsync();
        var savedId = await repository.SaveAsync(session, company, user, id, version, data);
        session.Commit();
        return savedId;
    }
}
