using API_AMNOTE_WEB.Data;

using API_AMNOTE_WEB.Interfaces.Catalog;

using Microsoft.Extensions.Logging;



namespace API_AMNOTE_WEB.Services.Catalog;



/// <summary>

/// True multi-row Excel import for catalog masters:

/// one connection + one transaction for the whole file; repository chunks VALUES (~200).

/// </summary>

public static class CatalogExcelBulkInsert

{

    public const int DeadlockRetries = 3;



    public static async Task<int> ExecuteAsync(

        ICatalogWriteSupport writeSupport,

        string companyCd,

        string? databaseName,

        Func<DapperSession, Task<int>> insertAllAsync,

        string activityModuleName,

        string activityTableName,

        ILogger? logger = null,

        int deadlockRetries = DeadlockRetries)

    {

        Exception? last = null;

        for (var attempt = 1; attempt <= deadlockRetries; attempt++)

        {

            try

            {

                return await writeSupport.ExecuteInTransactionAsync(async session =>

                {

                    var inserted = await insertAllAsync(session);

                    if (inserted > 0)

                    {

                        await writeSupport.LogInsertAsync(

                            session,

                            companyCd,

                            activityModuleName,

                            activityTableName,

                            $"BULK:{inserted}",

                            new { InsertedCount = inserted },

                            $"Excel bulk insert ({inserted} rows)");

                    }



                    return inserted;

                }, databaseName);

            }

            catch (Exception ex) when (IsMysqlDeadlock(ex))

            {

                last = ex;

                logger?.LogWarning(ex, "Catalog excel multi-row bulk deadlock attempt {Attempt}/{Max}", attempt, deadlockRetries);

                if (attempt >= deadlockRetries)

                {

                    break;

                }



                await Task.Delay(80 * attempt);

            }

        }



        throw last ?? new InvalidOperationException("Catalog excel bulk insert failed after deadlock retries.");

    }



    private static bool IsMysqlDeadlock(Exception ex)

    {

        for (Exception? e = ex; e != null; e = e.InnerException)

        {

            var msg = e.Message ?? string.Empty;

            if (msg.Contains("Deadlock", StringComparison.OrdinalIgnoreCase)

                || msg.Contains("1213", StringComparison.Ordinal))

            {

                return true;

            }

        }



        return false;

    }

}


