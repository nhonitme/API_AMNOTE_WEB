using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using System.Globalization;
namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitDeclarationRepository(DapperExecutor db) : IPitDeclarationRepository
{
    public Task<(IReadOnlyList<PitDeclaration> First, IReadOnlyList<PitCountRow> Second)> SearchAsync(object parameters, string? dbName)
        => db.QueryMultipleAsync<PitDeclaration, PitCountRow>(Net_DB.Net_DB_Company,
            "CALL getPitTkhai(@company,@id,@from,@to,@keyword,@signed,@cqtStatus,@offset,@pageSize)", parameters, sDBName: dbName);

    public async Task<long> SaveAsync(DapperSession s, string company, string user, long id, int version, PitDeclarationDto d)
    {
        var result = await s.QuerySingleAsync<long>("CALL setPitTkhai(@company,@id,@version,@method,@name,@tax,@office,@officeCd,@contact,@address,@email,@phone,@location,@date,@user)",
            new
            {
                company,
                id,
                version,
                method = d.HTHUC,
                name = d.TNNT,
                tax = d.MST,
                office = d.CQTQLY,
                officeCd = d.MCQTQLY,
                contact = d.NLHE,
                address = d.DCLHE,
                email = d.DCTDTU,
                phone = d.DTLHE,
                location = d.DDANH,
                date = d.NLAP,
                user,
            });
        await s.ExecuteAsync("CALL delPitTkhaiCts(@company,@id)", new { company, id = result });
        for (var i = 0; i < d.Certificates.Count; i++)
        {
            var c = d.Certificates[i]; await s.ExecuteAsync("CALL setPitTkhaiCts(@company,@id,@stt,@issuer,@serial,@from,@to,@method,@user)",
            new
            {
                company,
                id = result,
                stt = i + 1,
                issuer = c.TTCHUC,
                serial = c.SERI,
                from = c.TNGAY.ToOffset(TimeSpan.FromHours(7)).DateTime,
                to = c.DNGAY.ToOffset(TimeSpan.FromHours(7)).DateTime,
                method = c.HTHUC,
                user,
            });
        }
        return result;
    }
}
