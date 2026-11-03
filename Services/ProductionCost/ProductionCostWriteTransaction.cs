namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Transaction ownership thuần — Commit khi work thành công, Rollback khi lỗi.
/// Unit-testable không cần MySQL. Confirm 2C-06B+ dùng kèm <see cref="ProductionCostWriteSession"/>.
/// </summary>
public static class ProductionCostWriteTransaction
{
    public static async Task<T> CompleteAsync<T>(
        Action commit,
        Action rollback,
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(rollback);
        ArgumentNullException.ThrowIfNull(work);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await work(cancellationToken);
            commit();
            return result;
        }
        catch
        {
            try { rollback(); }
            catch { /* session may already be closed */ }
            throw;
        }
    }

    public static Task CompleteAsync(
        Action commit,
        Action rollback,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(commit, rollback, async ct =>
        {
            await work(ct);
            return true;
        }, cancellationToken);
}
