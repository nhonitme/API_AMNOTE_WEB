using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Dapper;

namespace API_AMNOTE_WEB.Repositories;

public sealed class VatInOutRepository : IVatInOutRepository
{
    private readonly DapperExecutor _db;
    private readonly ICompanyDatabaseResolver _databaseResolver;
    public VatInOutRepository(DapperExecutor db, ICompanyDatabaseResolver databaseResolver)
    {
        _db = db;
        _databaseResolver = databaseResolver;
    }

    private sealed class TotalRecordResult
    {
        public int TOTAL_RECORDS { get; set; }
    }

    public async Task<(IReadOnlyList<VatInOutInvoice> Items, int TotalRecords)> GetPagedAsync(
        string companyCd, VatInOutQuery filter, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = "CALL rpt_vat_inout_list(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD, @p_TYPE, @p_STATUS, @p_SEARCH_TEXT, @p_LANGUAGE, @p_PAGE_NUMBER, @p_PAGE_SIZE)";
        var parameters = new
        {
            p_COMPANY_CD = companyCd, p_FROM_YMD = filter.FROM_YMD, p_TO_YMD = filter.TO_YMD,
            p_TYPE = filter.TYPE, p_STATUS = filter.STATUS ?? string.Empty,
            p_SEARCH_TEXT = filter.SEARCH_TEXT ?? string.Empty, p_LANGUAGE = filter.LANGUAGE,
            p_PAGE_NUMBER = pageNumber, p_PAGE_SIZE = pageSize
        };
        var databaseName = await _databaseResolver.ResolveDatabaseNameAsync(companyCd);
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _db.GetOpenConnection(Net_DB.Net_DB_Company, databaseName);
        Common.LogQuery(sql, parameters);
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            sql, parameters, commandTimeout: 60, cancellationToken: cancellationToken));
        var items = (await results.ReadAsync<VatInOutInvoice>()).ToList();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.COMPANY_CD))
            {
                item.COMPANY_CD = companyCd;
            }
        }
        var totals = (await results.ReadAsync<TotalRecordResult>()).Single();
        return (items, totals.TOTAL_RECORDS);
    }
}

