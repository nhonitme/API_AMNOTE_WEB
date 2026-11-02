namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Chặn mọi ghi G-K (2C-02) cho đến khi migration 2B nghiệm thu DEV/TEST
/// và cấu hình <c>ProductionCost:EnableWrites=true</c>.
/// Mặc định OFF — không kích hoạt trên production.
/// </summary>
public interface IProductionCostWriteGate
{
    bool IsWriteEnabled { get; }
    string BlockReason { get; }
    void EnsureWritable();
}

public sealed class ProductionCostWriteGate : IProductionCostWriteGate
{
    public const string ConfigKey = "ProductionCost:EnableWrites";
    public const string DefaultBlockReason =
        "Ghi G-K 2C-02 bị chặn đến khi migration 2B nghiệm thu trên DEV/TEST " +
        "và bật ProductionCost:EnableWrites=true (không áp dụng production).";

    private readonly bool _enabled;

    public ProductionCostWriteGate(bool enabled = false)
    {
        _enabled = enabled;
    }

    public bool IsWriteEnabled => _enabled;

    public string BlockReason => DefaultBlockReason;

    public void EnsureWritable()
    {
        if (!_enabled)
            throw new InvalidOperationException(BlockReason);
    }
}
