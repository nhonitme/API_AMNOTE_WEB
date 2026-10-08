using System.Xml.Linq;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Transmission;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitMessageOutboundService(DapperExecutor db, IEInvoiceMessageRepository messages, PitRepository repository)
{
    public async Task DispatchAsync(string dbName, string companyCd)
    {
        var pending = await db.QueryAsync<PitOutboxItem>(
            Net_DB.Net_DB_Company,
            "CALL getPitOutbox(@company)",
            new { company = companyCd },
            sDBName: dbName);

        foreach (var item in pending)
        {
            var kind = PitKinds.KindFromTarget(item.TARGET_TYPE);
            var row = await repository.Get(companyCd, kind, item.TARGET_ID, dbName);

            if (row.QUEUED == 0)
            {
                if (!await messages.SendExistsAsync(companyCd, row.MTDIEP!))
                {
                    var envelopeTaxCode = row.TAX_CD;
                    if (string.IsNullOrWhiteSpace(envelopeTaxCode))
                        envelopeTaxCode = await db.QuerySingleAsync<string>(
                            Net_DB.Net_DB_Company,
                            "SELECT MST FROM pit_tkhai_info WHERE COMPANY_CD=@company AND CQT_STATUS=2 AND ISDEL=0 ORDER BY UPDATE_AT DESC LIMIT 1",
                            new { company = companyCd },
                            sDBName: dbName);
                    var packaged = MessageEnvelopeBuilder.Build(
                        new[] { XElement.Parse(row.SIGNED_XML!, LoadOptions.PreserveWhitespace) },
                        PitKinds.Message(row.KIND),
                        row.MTDIEP!,
                        envelopeTaxCode,
                        version: "2.1.1",
                        includeXmlDeclaration: false);

                    await messages.EnqueueOutboundAsync(
                        companyCd,
                        row.UPDATE_BY ?? "SYSTEM",
                        item.TARGET_TYPE,
                        row.DOCUMENT_ID,
                        packaged);
                }

                await db.ExecuteAsync(
                    Net_DB.Net_DB_Company,
                    "CALL setPitQueued(@company,@target,@id)",
                    new { company = companyCd, target = item.TARGET_TYPE, id = row.DOCUMENT_ID },
                    sDBName: dbName);
            }
        }
    }
}
