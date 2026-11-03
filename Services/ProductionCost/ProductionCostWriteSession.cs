using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Mở <see cref="DapperSession"/> (BEGIN) cho ghi G-K — C# sở hữu Commit/Rollback.
/// SP không START/COMMIT nội bộ.
/// </summary>
public static class ProductionCostWriteSession
{
    public static async Task<T> ExecuteAsync<T>(
        IProductionCostWriteRepository writeRepository,
        string databaseName,
        Func<DapperSession, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writeRepository);
        ArgumentNullException.ThrowIfNull(work);
        ProductionCostWriteContext.EnsureDatabase(databaseName);

        await using var session = await writeRepository.BeginWriteSessionAsync(databaseName);
        return await ProductionCostWriteTransaction.CompleteAsync(
            commit: () => session.Commit(),
            rollback: () => session.Rollback(),
            work: ct => work(session, ct),
            cancellationToken);
    }

    public static Task ExecuteAsync(
        IProductionCostWriteRepository writeRepository,
        string databaseName,
        Func<DapperSession, CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(writeRepository, databaseName, async (session, ct) =>
        {
            await work(session, ct);
            return true;
        }, cancellationToken);
}
