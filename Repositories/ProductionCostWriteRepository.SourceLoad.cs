using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Valuation;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;
using MySqlConnector;

namespace API_AMNOTE_WEB.Repositories;

public sealed partial class ProductionCostWriteRepository
{
    public async Task<AllocationDryRunPayload> LoadAllocationDryRunFromDbAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        ProductionCostRunDto lockedRun,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lockedRun);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);

        var config = AllocationRunConfig.FromLockedRun(lockedRun);
        var bounds = await RequirePeriodBoundsAsync(session, companyCd, config.PeriodYm);
        var objects = await LoadSelectedObjectsAsync(session, companyCd, runId);
        if (objects.Count == 0)
        {
            throw new InvalidOperationException(
                "Chưa có đối tượng tập hợp chi phí (production_cost_object IS_SELECTED=1, PRODUCT_ID>0). " +
                "Đồng bộ từ phiếu nhập hoặc chọn đối tượng trước khi phân bổ.");
        }

        var mutable = IsMutableRun(lockedRun.Status);
        IReadOnlyList<AllocationPoolInput> pool;
        if (mutable)
        {
            var saved = await ReadPoolSnapshotsAsync(session, companyCd, runId, runVersion);
            await CallAsync(
                session,
                "CALL sp_pc_alloc_load_pool(@CompanyCd, @RunId, @RunVersion, @FromYmd, @ToYmd, @Is6212, @ByDept, @UserId)",
                new
                {
                    CompanyCd = companyCd.Trim(),
                    RunId = runId,
                    RunVersion = runVersion,
                    bounds.FromYmd,
                    bounds.ToYmd,
                    Is6212 = config.Include6212 ? "1" : "0",
                    ByDept = config.ByDepartment ? "1" : "0",
                    UserId = userId ?? string.Empty
                },
                "sp_pc_alloc_load_pool");
            pool = OverlayReductions(await ReadPoolAsync(session, companyCd, runId, runVersion), saved);
        }
        else
        {
            pool = await ReadPoolAsync(session, companyCd, runId, runVersion);
        }

        if (pool.Count == 0)
        {
            throw new InvalidOperationException(
                "Không có phát sinh 622/627 (và 6212 nếu bật) trong kỳ trên sổ cái. " +
                "Không Confirm phân bổ với pool rỗng.");
        }

        IReadOnlyList<AllocationProductInput> products;
        if (config.AllocateType == 0)
        {
            if (mutable)
            {
                await CallAsync(
                    session,
                    "CALL sp_pc_nvl_basis_build(@CompanyCd, @RunId, @RunVersion, @FromYmd, @ToYmd, @UserId)",
                    new
                    {
                        CompanyCd = companyCd.Trim(),
                        RunId = runId,
                        RunVersion = runVersion,
                        bounds.FromYmd,
                        bounds.ToYmd,
                        UserId = userId ?? string.Empty
                    },
                    "sp_pc_nvl_basis_build");
            }

            var basis = await ReadNvlBasisAsync(session, companyCd, runId, runVersion);
            if (basis.Count == 0)
            {
                throw new InvalidOperationException(
                    "sp_pc_nvl_basis_build không tạo tiêu thức NVL cho RUN này " +
                    "(không có N621/C152 gắn BOM, hoặc thiếu procedure). Không dùng số liệu client.");
            }

            products = objects.Select(o =>
            {
                basis.TryGetValue(o.ProductId, out var amount);
                return new AllocationProductInput
                {
                    ProductId = o.ProductId,
                    ProductCd = o.ProductCd,
                    DepartmentId = o.DepartmentId,
                    DepartmentCd = o.DepartmentCd,
                    AmountQuantity = amount,
                    Quantity = 0
                };
            }).ToList();
        }
        else
        {
            Dictionary<long, decimal> qtyByProduct;
            if (mutable)
            {
                var rows = await QueryAsync<BasisQtyRow>(
                    session,
                    "CALL sp_pc_alloc_load_basis_qty(@CompanyCd, @RunId, @RunVersion, @FromYmd, @ToYmd, @UserId)",
                    new
                    {
                        CompanyCd = companyCd.Trim(),
                        RunId = runId,
                        RunVersion = runVersion,
                        bounds.FromYmd,
                        bounds.ToYmd,
                        UserId = userId ?? string.Empty
                    },
                    "sp_pc_alloc_load_basis_qty");
                qtyByProduct = rows
                    .GroupBy(r => r.ProductId)
                    .ToDictionary(g => g.Key, g => g.Max(x => x.Quantity));
            }
            else
            {
                qtyByProduct = await ReadFinishedQtyAsync(session, companyCd, bounds, objects.Select(o => o.ProductId).Distinct().ToArray());
            }

            products = objects.Select(o =>
            {
                qtyByProduct.TryGetValue(o.ProductId, out var qty);
                return new AllocationProductInput
                {
                    ProductId = o.ProductId,
                    ProductCd = o.ProductCd,
                    DepartmentId = o.DepartmentId,
                    DepartmentCd = o.DepartmentCd,
                    Quantity = qty
                };
            }).ToList();
        }

        return new AllocationDryRunPayload
        {
            AllocateType = config.AllocateType,
            ByDepartment = config.ByDepartment,
            NumberDigit = config.NumberDigit,
            Include6212 = config.Include6212,
            Pool = pool,
            Products = products
        };
    }

    public async Task<WipDryRunPayload> LoadWipDryRunFromDbAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        ProductionCostRunDto lockedRun,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lockedRun);
        _ = userId;
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);

        var wipConfig = WipRunConfig.FromLockedRun(lockedRun);
        var allocConfig = AllocationRunConfig.FromLockedRun(lockedRun);
        var bounds = await RequirePeriodBoundsAsync(session, companyCd, wipConfig.PeriodYm);
        var objects = await LoadSelectedObjectsAsync(session, companyCd, runId);
        if (objects.Count == 0)
        {
            throw new InvalidOperationException(
                "Không load được đối tượng tính giá thành từ production_cost_object.");
        }

        var productIds = objects.Select(o => o.ProductId).Distinct().ToArray();
        var opening = await ReadOpeningWipAsync(session, companyCd, PreviousPeriod(wipConfig.PeriodYm));
        var saved = await ReadWipAdjustmentsAsync(session, companyCd, runId, runVersion);
        var alloc = await ReadAllocSumsAsync(session, companyCd, runId, runVersion);
        var direct154 = await ReadDirect154Async(session, companyCd, bounds);
        var nvl = await ReadNvlBasisAsync(session, companyCd, runId, runVersion);
        var qty = await ReadFinishedQtyAsync(session, companyCd, bounds, productIds);
        var amount6212 = allocConfig.Include6212
            ? await ReadAccountDebitAsync(session, companyCd, bounds, "6212%")
            : 0m;

        var deptCount = objects.GroupBy(o => o.ProductId).ToDictionary(g => g.Key, g => g.Count());
        var products = new List<WipCalcInput>();
        var sources = new List<CostSourceLine>();

        if (amount6212 != 0)
        {
            sources.Add(new CostSourceLine
            {
                ProductId = 0,
                DepartmentId = 0,
                SourceKind = ProductionCostSourceKind.Src6212,
                Amount = amount6212
            });
        }

        foreach (var obj in objects)
        {
            var key = (obj.ProductId, obj.DepartmentId);
            opening.TryGetValue(key, out var open);
            saved.TryGetValue(key, out var adj);
            alloc.TryGetValue(key, out var allocated);
            direct154.TryGetValue(key, out var directOnDept);
            var amount622 = allocated?.Amount622 ?? 0m;
            var amount627 = allocated?.Amount627 ?? 0m;
            nvl.TryGetValue(obj.ProductId, out var nvlAmount);
            qty.TryGetValue(obj.ProductId, out var finished);
            var depts = deptCount[obj.ProductId];
            var share = depts <= 1 ? finished : decimal.Round(finished / depts, 6, MidpointRounding.AwayFromZero);

            products.Add(new WipCalcInput
            {
                ProductId = obj.ProductId,
                ProductCd = obj.ProductCd,
                DepartmentId = obj.DepartmentId,
                DepartmentCd = obj.DepartmentCd,
                IncompleteQtyOpening = adj?.IncompleteQtyOpening ?? open?.ClosingQty ?? 0,
                BeforeAmountResult621 = open?.Amount621 ?? adj?.Before621 ?? 0,
                BeforeAmountResult62N = open?.Amount62N ?? adj?.Before62N ?? 0,
                Quantity = share,
                PercentCompleted = adj?.PercentCompleted ?? 100m,
                ClosingIncompleteQty = adj?.ClosingQty ?? 0,
                ProductReduce = adj?.ProductReduce ?? 0
            });

            if (nvlAmount != 0 && obj.DepartmentId == objects.First(x => x.ProductId == obj.ProductId).DepartmentId)
            {
                sources.Add(new CostSourceLine
                {
                    ProductId = obj.ProductId,
                    DepartmentId = 0,
                    SourceKind = ProductionCostSourceKind.Src621,
                    Amount = nvlAmount
                });
            }

            if (amount622 != 0)
            {
                sources.Add(new CostSourceLine
                {
                    ProductId = obj.ProductId,
                    DepartmentId = obj.DepartmentId,
                    SourceKind = ProductionCostSourceKind.Src622Alloc,
                    Amount = amount622
                });
            }

            if (amount627 != 0)
            {
                sources.Add(new CostSourceLine
                {
                    ProductId = obj.ProductId,
                    DepartmentId = obj.DepartmentId,
                    SourceKind = ProductionCostSourceKind.Src627Alloc,
                    Amount = amount627
                });
            }

            if (directOnDept != 0)
            {
                sources.Add(new CostSourceLine
                {
                    ProductId = obj.ProductId,
                    DepartmentId = obj.DepartmentId,
                    SourceKind = ProductionCostSourceKind.Src154Direct,
                    Amount = directOnDept
                });
            }
        }

        if (direct154.TryGetValue((0, 0), out var shared154) && shared154 != 0)
        {
            sources.Add(new CostSourceLine
            {
                ProductId = 0,
                DepartmentId = 0,
                SourceKind = ProductionCostSourceKind.Src154Direct,
                Amount = shared154
            });
        }

        if (!sources.Any(s => CostSourceRules.IsIncurredKind(s.SourceKind)))
        {
            sources.Add(new CostSourceLine
            {
                ProductId = objects[0].ProductId,
                DepartmentId = 0,
                SourceKind = ProductionCostSourceKind.Src621,
                Amount = 0
            });
        }

        CostSourceRules.AssertNoTransferInIncurred(sources);

        var receipts = await ReadFinishedReceiptsAsync(session, companyCd, bounds, productIds, objects);

        return new WipDryRunPayload
        {
            WipMethod = wipConfig.WipMethod,
            Config = wipConfig.ToEngineConfig(),
            Products = products,
            Sources = sources,
            Receipts = receipts
        };
    }

    public async Task<ValuationDryRunPayload> LoadValuationDryRunFromDbAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        ProductionCostRunDto lockedRun,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lockedRun);
        _ = userId;
        ProductionCostWriteContext.EnsureCompany(companyCd);
        var periodYm = lockedRun.PeriodYm.Trim();
        var bounds = await RequirePeriodBoundsAsync(session, companyCd, periodYm);

        var wipRows = (await QueryAsync<WipStoredRow>(
            session,
            @"SELECT PRODUCT_ID AS ProductId, DEPARTMENT_ID AS DepartmentId, PRODUCT_CD AS ProductCd,
                     DEPARTMENT_CD AS DepartmentCd, QUANTITY AS Quantity, UNIT_COST AS UnitCost,
                     TOTAL_COST_PRICE AS TotalCostPrice, AMOUNT_621 AS Amount621, AMOUNT_6212 AS Amount6212,
                     AMOUNT_622 AS Amount622, AMOUNT_627 AS Amount627, AMOUNT_154_DIRECT AS Amount154Direct,
                     TOTAL_AMOUNT_INCURRED AS TotalAmountIncurred,
                     CLOSING_INCOMPLETE_AMOUNT_154 AS ClosingIncompleteAmount154,
                     PERCENT_COMPLETED AS PercentCompleted,
                     CLOSING_INCOMPLETE_QUANTITY AS ClosingIncompleteQty
              FROM production_cost_wip
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion },
            "đọc production_cost_wip")).ToList();

        if (wipRows.Count == 0)
        {
            throw new InvalidOperationException(
                "Chưa có snapshot production_cost_wip của RUN_VERSION này. Confirm B4 trước khi định giá.");
        }

        var receiptRows = (await QueryAsync<ReceiptStoredRow>(
            session,
            @"SELECT PRODUCT_ID AS ProductId, DEPARTMENT_ID AS DepartmentId, INPUT_ID AS InputId,
                     INVENTORY_ID AS InventoryId, PRODUCT_CD AS ProductCd, INVENTORY_NO AS InventoryNo,
                     QTY AS Qty, OLD_UNIT_PRICE AS OldUnitPrice, OLD_AMOUNT AS OldAmount
              FROM production_cost_receipt
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion },
            "đọc production_cost_receipt")).ToList();

        if (receiptRows.Count == 0)
        {
            throw new InvalidOperationException(
                "Chưa có production_cost_receipt theo INPUT_ID cho RUN_VERSION này.");
        }

        var productIds = wipRows.Select(w => w.ProductId).Distinct().ToArray();
        var movements = await ReadValuationMovementsAsync(session, companyCd, bounds, productIds);
        if (movements.Count == 0)
        {
            throw new InvalidOperationException(
                "Không đọc được chuyển động kho (nhập/xuất/tồn đầu) của thành phẩm trong kỳ.");
        }

        var stableKey = ValuationApplyPlan.StableKey(companyCd, periodYm);
        var existing = await QueryFirstOrDefaultAsync<ExistingCogsRow>(
            session,
            @"SELECT v.CHIT_ID AS ChitId, v.STABLE_KEY AS StableKey, c.COMPANY_CD AS CompanyCd
              FROM production_cost_voucher_ref v
              LEFT JOIN chitinfo c
                ON c.CHIT_ID = v.CHIT_ID AND c.COMPANY_CD = v.COMPANY_CD AND c.ISDEL = '0'
              WHERE v.COMPANY_CD=@CompanyCd AND v.STABLE_KEY=@StableKey AND v.ISDEL='0'
              LIMIT 1",
            new { CompanyCd = companyCd.Trim(), StableKey = stableKey },
            "đọc VALUATION_COGS");

        var method = await ReadValuationMethodAsync(session, companyCd, runId);

        return new ValuationDryRunPayload
        {
            ValuationMethod = method,
            ConfirmedWip = wipRows.Select(w => new WipCalcResult
            {
                ProductId = w.ProductId,
                DepartmentId = w.DepartmentId,
                ProductCd = w.ProductCd,
                DepartmentCd = w.DepartmentCd,
                Quantity = w.Quantity,
                UnitCost = w.UnitCost,
                TotalCostPrice = w.TotalCostPrice,
                Amount621 = w.Amount621,
                Amount6212 = w.Amount6212,
                Amount622 = w.Amount622,
                Amount627 = w.Amount627,
                Amount154Direct = w.Amount154Direct,
                TotalAmountIncurred = w.TotalAmountIncurred,
                ClosingIncompleteAmount154 = w.ClosingIncompleteAmount154,
                PercentCompleted = w.PercentCompleted,
                ClosingIncompleteQty = w.ClosingIncompleteQty
            }).ToList(),
            Receipts = receiptRows.Select(r => new ReceiptPricePrepInput
            {
                ProductId = r.ProductId,
                DepartmentId = r.DepartmentId,
                InputId = r.InputId,
                InventoryId = r.InventoryId,
                ProductCd = r.ProductCd,
                InventoryNo = r.InventoryNo,
                Qty = r.Qty,
                OldUnitPrice = r.OldUnitPrice,
                OldAmount = r.OldAmount
            }).ToList(),
            Movements = movements,
            ExistingValuationChitId = existing?.ChitId,
            ExistingValuationChitCompanyCd = existing?.CompanyCd,
            ExistingValuationStableKey = existing?.StableKey
        };
    }

    private async Task<PeriodBounds> RequirePeriodBoundsAsync(DapperSession session, string companyCd, string periodYm)
    {
        var row = await QueryFirstOrDefaultAsync<PeriodBounds>(
            session,
            @"SELECT FROM_YMD AS FromYmd, TO_YMD AS ToYmd
              FROM production_cost_period
              WHERE COMPANY_CD=@CompanyCd AND PERIOD_YM=@PeriodYm AND ISDEL='0'
              LIMIT 1",
            new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm },
            "đọc production_cost_period");
        if (row == null || string.IsNullOrWhiteSpace(row.FromYmd) || string.IsNullOrWhiteSpace(row.ToYmd))
        {
            throw new InvalidOperationException(
                $"Kỳ {periodYm} chưa có FROM_YMD/TO_YMD trên production_cost_period.");
        }

        return row;
    }

    private async Task<List<ObjectRow>> LoadSelectedObjectsAsync(DapperSession session, string companyCd, long runId)
    {
        var rows = await QueryAsync<ObjectRow>(
            session,
            @"SELECT PRODUCT_ID AS ProductId, DEPARTMENT_ID AS DepartmentId,
                     PRODUCT_CD AS ProductCd, DEPARTMENT_CD AS DepartmentCd
              FROM production_cost_object
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND IS_SELECTED='1' AND ISDEL='0' AND PRODUCT_ID > 0
              ORDER BY PRODUCT_ID, DEPARTMENT_ID",
            new { CompanyCd = companyCd.Trim(), RunId = runId },
            "đọc production_cost_object");
        return rows.ToList();
    }

    private async Task<List<AllocationPoolInput>> ReadPoolAsync(
        DapperSession session, string companyCd, long runId, int runVersion)
    {
        var rows = await QueryAsync<AllocationPoolInput>(
            session,
            @"SELECT ACCOUNT_CD AS AccountCd, DEPARTMENT_ID AS DepartmentId, DEPARTMENT_CD AS DepartmentCd,
                     TOTAL_AMOUNT AS TotalAmount, CREDIT_AMOUNT AS CreditAmount,
                     DEBIT_AMOUNT_154 AS DebitAmount154, REDUCTION AS Reduction,
                     ALLOCATION_COSTS AS AllocationCosts
              FROM production_cost_alloc_pool
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion },
            "đọc production_cost_alloc_pool");
        return rows.ToList();
    }

    private async Task<List<PoolSnapshotRow>> ReadPoolSnapshotsAsync(
        DapperSession session, string companyCd, long runId, int runVersion)
    {
        var rows = await QueryAsync<PoolSnapshotRow>(
            session,
            @"SELECT ACCOUNT_CD AS AccountCd, DEPARTMENT_ID AS DepartmentId,
                     TOTAL_AMOUNT AS TotalAmount, REDUCTION AS Reduction
              FROM production_cost_alloc_pool
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion },
            "đọc reduction pool");
        return rows.ToList();
    }

    private static List<AllocationPoolInput> OverlayReductions(
        List<AllocationPoolInput> pool,
        List<PoolSnapshotRow> saved)
    {
        if (saved.Count == 0)
            return pool;

        return pool.Select(row =>
        {
            var hit = saved.FirstOrDefault(s =>
                string.Equals(s.AccountCd, row.AccountCd, StringComparison.OrdinalIgnoreCase)
                && s.DepartmentId == row.DepartmentId
                && Math.Abs(s.TotalAmount - row.TotalAmount) <= 0.01m
                && Math.Abs(s.Reduction - row.Reduction) > 0.01m);
            if (hit == null)
                return row;
            return new AllocationPoolInput
            {
                AccountCd = row.AccountCd,
                DepartmentId = row.DepartmentId,
                DepartmentCd = row.DepartmentCd,
                TotalAmount = row.TotalAmount,
                CreditAmount = row.CreditAmount,
                DebitAmount154 = row.DebitAmount154,
                Reduction = hit.Reduction,
                AllocationCosts = AllocationMath.ComputeAllocationCosts(row.TotalAmount, hit.Reduction)
            };
        }).ToList();
    }

    private async Task<Dictionary<long, decimal>> ReadNvlBasisAsync(
        DapperSession session, string companyCd, long runId, int runVersion)
    {
        var rows = await QueryAsync<BasisAmountRow>(
            session,
            @"SELECT PRODUCT_ID AS ProductId, AMOUNT_QUANTITY AS Amount
              FROM production_cost_nvl_basis
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion },
            "đọc production_cost_nvl_basis");
        return rows.GroupBy(r => r.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
    }

    private async Task<Dictionary<long, decimal>> ReadFinishedQtyAsync(
        DapperSession session, string companyCd, PeriodBounds bounds, long[] productIds)
    {
        if (productIds.Length == 0)
            return new Dictionary<long, decimal>();

        var rows = await QueryAsync<BasisQtyRow>(
            session,
            @"SELECT i.PRODUCT_ID AS ProductId, ROUND(SUM(i.QUANTITY), 6) AS Quantity
              FROM chit_inventory_input i
              INNER JOIN chitdetailinfo d
                ON d.COMPANY_CD = i.COMPANY_CD AND d.CHITDETAIL_ID = i.CHITDETAIL_ID AND d.ISDEL = '0'
              INNER JOIN chitinfo c
                ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
              WHERE i.COMPANY_CD = @CompanyCd AND i.ISDEL = '0'
                AND c.CHIT_YMD >= @FromYmd AND c.CHIT_YMD <= @ToYmd
                AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
                AND TRIM(d.DEBIT) LIKE '155%'
                AND TRIM(d.CREDIT) LIKE '154%'
                AND i.PRODUCT_ID IN @ProductIds
              GROUP BY i.PRODUCT_ID",
            new { CompanyCd = companyCd.Trim(), bounds.FromYmd, bounds.ToYmd, ProductIds = productIds },
            "đọc số lượng thành phẩm N155/C154");
        return rows.ToDictionary(r => r.ProductId, r => r.Quantity);
    }

    private async Task<Dictionary<(long ProductId, long DepartmentId), OpeningWipRow>> ReadOpeningWipAsync(
        DapperSession session, string companyCd, string previousPeriodYm)
    {
        var rows = await QueryAsync<OpeningWipRow>(
            session,
            @"SELECT w.PRODUCT_ID AS ProductId, w.DEPARTMENT_ID AS DepartmentId,
                     w.CLOSING_INCOMPLETE_QUANTITY AS ClosingQty,
                     w.AMOUNT_RESULT_621 AS Amount621, w.AMOUNT_RESULT_62N AS Amount62N
              FROM production_cost_wip w
              INNER JOIN production_cost_run r
                ON r.RUN_ID = w.RUN_ID AND r.COMPANY_CD = w.COMPANY_CD AND r.ISDEL = '0'
              INNER JOIN production_cost_period p
                ON p.CURRENT_RUN_ID = r.RUN_ID AND p.COMPANY_CD = r.COMPANY_CD AND p.ISDEL = '0'
              WHERE p.COMPANY_CD = @CompanyCd AND p.PERIOD_YM = @PeriodYm
                AND w.RUN_VERSION = r.RUN_VERSION AND w.ISDEL = '0'",
            new { CompanyCd = companyCd.Trim(), PeriodYm = previousPeriodYm },
            "đọc dở dang đầu kỳ");
        return rows.ToDictionary(r => (r.ProductId, r.DepartmentId));
    }

    private async Task<Dictionary<(long, long), WipAdjRow>> ReadWipAdjustmentsAsync(
        DapperSession session, string companyCd, long runId, int runVersion)
    {
        var rows = await QueryAsync<WipAdjRow>(
            session,
            @"SELECT PRODUCT_ID AS ProductId, DEPARTMENT_ID AS DepartmentId,
                     PERCENT_COMPLETED AS PercentCompleted,
                     CLOSING_INCOMPLETE_QUANTITY AS ClosingQty,
                     PRODUCT_REDUCE AS ProductReduce,
                     INCOMPLETE_QTY_OPENING AS IncompleteQtyOpening,
                     BEFORE_AMOUNT_RESULT_621 AS Before621,
                     BEFORE_AMOUNT_RESULT_62N AS Before62N
              FROM production_cost_wip
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion },
            "đọc điều chỉnh WIP");
        return rows.ToDictionary(r => (r.ProductId, r.DepartmentId));
    }

    private async Task<Dictionary<(long, long), AllocSumRow>> ReadAllocSumsAsync(
        DapperSession session, string companyCd, long runId, int runVersion)
    {
        var rows = await QueryAsync<AllocSumRow>(
            session,
            @"SELECT PRODUCT_ID AS ProductId, DEPARTMENT_ID AS DepartmentId,
                     ROUND(SUM(CASE WHEN ACCOUNT_CD LIKE '622%' THEN ALLOCATED_AMOUNT ELSE 0 END), 2) AS Amount622,
                     ROUND(SUM(CASE WHEN ACCOUNT_CD LIKE '627%' THEN ALLOCATED_AMOUNT ELSE 0 END), 2) AS Amount627
              FROM production_cost_alloc_detail
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'
              GROUP BY PRODUCT_ID, DEPARTMENT_ID",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion },
            "đọc phân bổ 622/627");
        return rows.ToDictionary(r => (r.ProductId, r.DepartmentId));
    }

    private async Task<Dictionary<(long, long), decimal>> ReadDirect154Async(
        DapperSession session, string companyCd, PeriodBounds bounds)
    {
        var rows = await QueryAsync<Direct154Row>(
            session,
            @"SELECT IFNULL(src.ProductId, 0) AS ProductId,
                     IFNULL(src.DepartmentId, 0) AS DepartmentId,
                     ROUND(SUM(src.Amount), 2) AS Amount
              FROM (
                SELECT d.CHITDETAIL_ID,
                       d.AMOUNT AS Amount,
                       IFNULL(ext.DEPARTMENT_ID, 0) AS DepartmentId,
                       (SELECT i.PRODUCT_ID FROM chit_inventory_input i
                         WHERE i.COMPANY_CD = d.COMPANY_CD AND i.CHITDETAIL_ID = d.CHITDETAIL_ID AND i.ISDEL = '0'
                         ORDER BY i.INPUT_ID LIMIT 1) AS ProductId
                FROM chitdetailinfo d
                INNER JOIN chitinfo c
                  ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
                LEFT JOIN chitdetailinfo_ext ext
                  ON ext.COMPANY_CD = d.COMPANY_CD AND ext.CHITDETAIL_ID = d.CHITDETAIL_ID AND ext.ISDEL = '0'
                WHERE d.COMPANY_CD = @CompanyCd AND d.ISDEL = '0'
                  AND c.CHIT_YMD >= @FromYmd AND c.CHIT_YMD <= @ToYmd
                  AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
                  AND TRIM(d.DEBIT) LIKE '154%'
                  AND NOT EXISTS (
                    SELECT 1 FROM production_cost_voucher_ref v
                    WHERE v.COMPANY_CD = c.COMPANY_CD AND v.CHIT_ID = c.CHIT_ID AND v.ISDEL = '0'
                      AND v.REF_ROLE IN ('EXP_TO_154_ONLY', 'VALUATION_COGS')
                  )
              ) src
              GROUP BY IFNULL(src.ProductId, 0), IFNULL(src.DepartmentId, 0)",
            new { CompanyCd = companyCd.Trim(), bounds.FromYmd, bounds.ToYmd },
            "đọc 154 trực tiếp");
        return rows.ToDictionary(r => (r.ProductId, r.DepartmentId), r => r.Amount);
    }

    private async Task<decimal> ReadAccountDebitAsync(
        DapperSession session, string companyCd, PeriodBounds bounds, string debitLike)
    {
        var amount = await QueryFirstOrDefaultAsync<decimal?>(
            session,
            @"SELECT ROUND(SUM(d.AMOUNT), 2)
              FROM chitdetailinfo d
              INNER JOIN chitinfo c
                ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
              WHERE d.COMPANY_CD = @CompanyCd AND d.ISDEL = '0'
                AND c.CHIT_YMD >= @FromYmd AND c.CHIT_YMD <= @ToYmd
                AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
                AND TRIM(d.DEBIT) LIKE @DebitLike",
            new { CompanyCd = companyCd.Trim(), bounds.FromYmd, bounds.ToYmd, DebitLike = debitLike },
            "đọc phát sinh nợ tài khoản");
        return amount ?? 0m;
    }

    private async Task<List<ReceiptPricePrepInput>> ReadFinishedReceiptsAsync(
        DapperSession session,
        string companyCd,
        PeriodBounds bounds,
        long[] productIds,
        List<ObjectRow> objects)
    {
        if (productIds.Length == 0)
            return new List<ReceiptPricePrepInput>();

        var deptByProduct = objects.GroupBy(o => o.ProductId).ToDictionary(g => g.Key, g => g.Select(x => x.DepartmentId).Distinct().ToList());
        var rows = await QueryAsync<ReceiptInputRow>(
            session,
            @"SELECT i.INPUT_ID AS InputId, i.PRODUCT_ID AS ProductId, i.INVENTORY_ID AS InventoryId,
                     i.PRODUCT_CD AS ProductCd, i.INVENTORY_CD AS InventoryNo,
                     CAST(i.QUANTITY AS DECIMAL(18,6)) AS Qty,
                     CAST(i.UNIT_PRICE_CC AS DECIMAL(18,6)) AS OldUnitPrice,
                     CAST(i.AMOUNT_CC AS DECIMAL(18,2)) AS OldAmount
              FROM chit_inventory_input i
              INNER JOIN chitdetailinfo d
                ON d.COMPANY_CD = i.COMPANY_CD AND d.CHITDETAIL_ID = i.CHITDETAIL_ID AND d.ISDEL = '0'
              INNER JOIN chitinfo c
                ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
              WHERE i.COMPANY_CD = @CompanyCd AND i.ISDEL = '0'
                AND c.CHIT_YMD >= @FromYmd AND c.CHIT_YMD <= @ToYmd
                AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
                AND TRIM(d.DEBIT) LIKE '155%' AND TRIM(d.CREDIT) LIKE '154%'
                AND i.PRODUCT_ID IN @ProductIds",
            new { CompanyCd = companyCd.Trim(), bounds.FromYmd, bounds.ToYmd, ProductIds = productIds },
            "đọc phiếu nhập thành phẩm");

        var list = new List<ReceiptPricePrepInput>();
        foreach (var row in rows)
        {
            if (!deptByProduct.TryGetValue(row.ProductId, out var depts) || depts.Count != 1)
            {
                throw new InvalidOperationException(
                    $"PRODUCT_ID={row.ProductId} có {depts?.Count ?? 0} phòng ban đã chọn nhưng phiếu INPUT_ID={row.InputId} không mang DEPARTMENT_ID. " +
                    "Không gán đơn giá tùy ý.");
            }

            list.Add(new ReceiptPricePrepInput
            {
                ProductId = row.ProductId,
                DepartmentId = depts[0],
                InputId = row.InputId,
                InventoryId = row.InventoryId,
                ProductCd = row.ProductCd,
                InventoryNo = row.InventoryNo,
                Qty = row.Qty,
                OldUnitPrice = row.OldUnitPrice,
                OldAmount = row.OldAmount
            });
        }

        return list;
    }

    private async Task<List<ValuationMovementInput>> ReadValuationMovementsAsync(
        DapperSession session, string companyCd, PeriodBounds bounds, long[] productIds)
    {
        var opening = await QueryAsync<MovementRow>(
            session,
            @"SELECT 'OPEN' AS FlowType, m.ProductId AS ProductId, m.ProductCd AS ProductCd, m.ProductName AS ProductName,
                     m.StoreId AS StoreId, m.StoreCd AS StoreCd, m.StoreName AS StoreName, m.ChitType AS ChitType,
                     NULL AS InputId, NULL AS InventoryId, NULL AS ChitdetailId, @FromYmd AS TxnYmd,
                     SUM(m.Quantity) AS Quantity, SUM(m.AmountCc) AS AmountCc
              FROM (
                SELECT ii.PRODUCT_ID AS ProductId,
                       COALESCE(NULLIF(ii.PRODUCT_CD, ''), p.PRODUCT_CD, '') AS ProductCd,
                       COALESCE(NULLIF(p.PRODUCT_NM_VIET, ''), '') AS ProductName,
                       IFNULL(ii.STORE_ID, 0) AS StoreId,
                       COALESCE(NULLIF(ii.STORE_CD, ''), s.STORE_CD, '') AS StoreCd,
                       COALESCE(NULLIF(s.STORE_NM_VIET, ''), '') AS StoreName,
                       IFNULL(ii.CHIT_TYPE, '') AS ChitType,
                       CAST(ii.QUANTITY AS DECIMAL(18,6)) AS Quantity,
                       CAST(IFNULL(ii.AMOUNT_CC, ii.QUANTITY * ii.UNIT_PRICE_CC) AS DECIMAL(18,2)) AS AmountCc
                FROM chit_inventory_input ii
                LEFT JOIN product_info p ON p.COMPANY_CD=ii.COMPANY_CD AND p.PRODUCT_ID=ii.PRODUCT_ID AND IFNULL(p.ISDEL,'0')='0'
                LEFT JOIN store_info s ON s.COMPANY_CD=ii.COMPANY_CD AND s.STORE_ID=ii.STORE_ID AND IFNULL(s.ISDEL,'0')='0'
                LEFT JOIN chit_inventory_info h ON h.COMPANY_CD=ii.COMPANY_CD AND h.INVENTORY_ID=ii.INVENTORY_ID AND IFNULL(h.ISDEL,'0')='0'
                WHERE ii.COMPANY_CD=@CompanyCd AND IFNULL(ii.ISDEL,'0')='0' AND ii.PRODUCT_ID IN @ProductIds
                  AND COALESCE(DATE_FORMAT(ii.INVENTORY_YMD, '%Y%m%d'), NULLIF(TRIM(h.INVENTORY_YMD), '')) < @FromYmd
                UNION ALL
                SELECT io.PRODUCT_ID,
                       COALESCE(NULLIF(io.PRODUCT_CD, ''), p.PRODUCT_CD, ''),
                       COALESCE(NULLIF(p.PRODUCT_NM_VIET, ''), ''),
                       IFNULL(io.STORE_ID, 0),
                       COALESCE(NULLIF(io.STORE_CD, ''), s.STORE_CD, ''),
                       COALESCE(NULLIF(s.STORE_NM_VIET, ''), ''),
                       IFNULL(io.CHIT_TYPE, ''),
                       CAST(-io.QUANTITY AS DECIMAL(18,6)),
                       CAST(-IFNULL(io.AMOUNT_CC, io.QUANTITY * io.UNIT_PRICE_CC) AS DECIMAL(18,2))
                FROM chit_inventory_output io
                LEFT JOIN product_info p ON p.COMPANY_CD=io.COMPANY_CD AND p.PRODUCT_ID=io.PRODUCT_ID AND IFNULL(p.ISDEL,'0')='0'
                LEFT JOIN store_info s ON s.COMPANY_CD=io.COMPANY_CD AND s.STORE_ID=io.STORE_ID AND IFNULL(s.ISDEL,'0')='0'
                LEFT JOIN chit_inventory_info h ON h.COMPANY_CD=io.COMPANY_CD AND h.INVENTORY_ID=io.INVENTORY_ID AND IFNULL(h.ISDEL,'0')='0'
                WHERE io.COMPANY_CD=@CompanyCd AND IFNULL(io.ISDEL,'0')='0' AND io.PRODUCT_ID IN @ProductIds
                  AND COALESCE(DATE_FORMAT(io.INVENTORY_YMD, '%Y%m%d'), NULLIF(TRIM(h.INVENTORY_YMD), '')) < @FromYmd
              ) m
              GROUP BY m.ProductId, m.ProductCd, m.ProductName, m.StoreId, m.StoreCd, m.StoreName, m.ChitType
              HAVING SUM(m.Quantity) <> 0 OR SUM(m.AmountCc) <> 0",
            new { CompanyCd = companyCd.Trim(), bounds.FromYmd, ProductIds = productIds },
            "đọc tồn đầu kỳ cho định giá");

        var rows = await QueryAsync<MovementRow>(
            session,
            @"SELECT 'INPUT' AS FlowType, ii.PRODUCT_ID AS ProductId,
                     COALESCE(NULLIF(ii.PRODUCT_CD, ''), p.PRODUCT_CD, '') AS ProductCd,
                     COALESCE(NULLIF(p.PRODUCT_NM_VIET, ''), '') AS ProductName,
                     IFNULL(ii.STORE_ID, 0) AS StoreId,
                     COALESCE(NULLIF(ii.STORE_CD, ''), s.STORE_CD, '') AS StoreCd,
                     COALESCE(NULLIF(s.STORE_NM_VIET, ''), '') AS StoreName,
                     IFNULL(ii.CHIT_TYPE, '') AS ChitType,
                     ii.INPUT_ID AS InputId, ii.INVENTORY_ID AS InventoryId, ii.CHITDETAIL_ID AS ChitdetailId,
                     COALESCE(DATE_FORMAT(ii.INVENTORY_YMD, '%Y%m%d'), NULLIF(TRIM(h.INVENTORY_YMD), '')) AS TxnYmd,
                     CAST(ii.QUANTITY AS DECIMAL(18,6)) AS Quantity,
                     CAST(IFNULL(ii.AMOUNT_CC, ii.QUANTITY * ii.UNIT_PRICE_CC) AS DECIMAL(18,2)) AS AmountCc
              FROM chit_inventory_input ii
              LEFT JOIN chit_inventory_info h
                ON h.COMPANY_CD = ii.COMPANY_CD AND h.INVENTORY_ID = ii.INVENTORY_ID AND IFNULL(h.ISDEL,'0')='0'
              LEFT JOIN product_info p
                ON p.COMPANY_CD = ii.COMPANY_CD AND p.PRODUCT_ID = ii.PRODUCT_ID AND IFNULL(p.ISDEL,'0')='0'
              LEFT JOIN store_info s
                ON s.COMPANY_CD = ii.COMPANY_CD AND s.STORE_ID = ii.STORE_ID AND IFNULL(s.ISDEL,'0')='0'
              WHERE ii.COMPANY_CD = @CompanyCd AND IFNULL(ii.ISDEL,'0')='0'
                AND ii.PRODUCT_ID IN @ProductIds
                AND COALESCE(DATE_FORMAT(ii.INVENTORY_YMD, '%Y%m%d'), NULLIF(TRIM(h.INVENTORY_YMD), '')) BETWEEN @FromYmd AND @ToYmd
                AND NOT EXISTS (
                  SELECT 1 FROM chitdetailinfo d
                  INNER JOIN chitinfo c ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
                  WHERE d.COMPANY_CD = ii.COMPANY_CD AND d.CHITDETAIL_ID = ii.CHITDETAIL_ID AND d.ISDEL = '0'
                    AND IFNULL(c.INPUT_TYPE,'') = 'LOCK'
                )
              UNION ALL
              SELECT 'OUTPUT', io.PRODUCT_ID,
                     COALESCE(NULLIF(io.PRODUCT_CD, ''), p.PRODUCT_CD, ''),
                     COALESCE(NULLIF(p.PRODUCT_NM_VIET, ''), ''),
                     IFNULL(io.STORE_ID, 0),
                     COALESCE(NULLIF(io.STORE_CD, ''), s.STORE_CD, ''),
                     COALESCE(NULLIF(s.STORE_NM_VIET, ''), ''),
                     IFNULL(io.CHIT_TYPE, ''),
                     NULL, io.INVENTORY_ID, io.CHITDETAIL_ID,
                     COALESCE(DATE_FORMAT(io.INVENTORY_YMD, '%Y%m%d'), NULLIF(TRIM(h.INVENTORY_YMD), '')),
                     CAST(io.QUANTITY AS DECIMAL(18,6)),
                     CAST(IFNULL(io.AMOUNT_CC, io.QUANTITY * io.UNIT_PRICE_CC) AS DECIMAL(18,2))
              FROM chit_inventory_output io
              LEFT JOIN chit_inventory_info h
                ON h.COMPANY_CD = io.COMPANY_CD AND h.INVENTORY_ID = io.INVENTORY_ID AND IFNULL(h.ISDEL,'0')='0'
              LEFT JOIN product_info p
                ON p.COMPANY_CD = io.COMPANY_CD AND p.PRODUCT_ID = io.PRODUCT_ID AND IFNULL(p.ISDEL,'0')='0'
              LEFT JOIN store_info s
                ON s.COMPANY_CD = io.COMPANY_CD AND s.STORE_ID = io.STORE_ID AND IFNULL(s.ISDEL,'0')='0'
              WHERE io.COMPANY_CD = @CompanyCd AND IFNULL(io.ISDEL,'0')='0'
                AND io.PRODUCT_ID IN @ProductIds
                AND COALESCE(DATE_FORMAT(io.INVENTORY_YMD, '%Y%m%d'), NULLIF(TRIM(h.INVENTORY_YMD), '')) BETWEEN @FromYmd AND @ToYmd
                AND NOT EXISTS (
                  SELECT 1 FROM chitdetailinfo d
                  INNER JOIN chitinfo c ON c.COMPANY_CD = d.COMPANY_CD AND c.CHIT_ID = d.CHIT_ID AND c.ISDEL = '0'
                  WHERE d.COMPANY_CD = io.COMPANY_CD AND d.CHITDETAIL_ID = io.CHITDETAIL_ID AND d.ISDEL = '0'
                    AND IFNULL(c.INPUT_TYPE,'') = 'LOCK'
                )",
            new { CompanyCd = companyCd.Trim(), bounds.FromYmd, bounds.ToYmd, ProductIds = productIds },
            "đọc chuyển động kho");

        return opening.Concat(rows).Select(r => new ValuationMovementInput
        {
            FlowType = r.FlowType,
            ProductId = r.ProductId,
            ProductCd = r.ProductCd,
            ProductName = r.ProductName,
            StoreId = r.StoreId,
            StoreCd = r.StoreCd,
            StoreName = r.StoreName,
            ChitType = r.ChitType,
            InputId = r.InputId,
            InventoryId = r.InventoryId,
            ChitdetailId = r.ChitdetailId,
            TxnYmd = r.TxnYmd,
            Quantity = r.Quantity,
            AmountCc = r.AmountCc,
            IsExpTo154Only = false,
            CompanyCd = companyCd.Trim()
        }).ToList();
    }

    private async Task<string> ReadValuationMethodAsync(DapperSession session, string companyCd, long runId)
    {
        var json = await QueryFirstOrDefaultAsync<string>(
            session,
            @"SELECT OPTIONS_JSON FROM production_cost_run
              WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND ISDEL='0' LIMIT 1",
            new { CompanyCd = companyCd.Trim(), RunId = runId },
            "đọc OPTIONS_JSON");
        if (string.IsNullOrWhiteSpace(json))
            return GkValuationMethod.PeriodEndAverage;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("valuationMethod", out var prop)
                && prop.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return InventoryValuationMethod.Normalize(prop.GetString());
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException("OPTIONS_JSON của RUN không phải JSON hợp lệ.", ex);
        }

        return GkValuationMethod.PeriodEndAverage;
    }

    private static bool IsMutableRun(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || status.Equals(GkRunStatus.Draft, StringComparison.OrdinalIgnoreCase)
        || status.Equals(GkRunStatus.Previewed, StringComparison.OrdinalIgnoreCase)
        || status.Equals(GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase);

    private static string PreviousPeriod(string periodYm)
    {
        var year = int.Parse(periodYm[..4]);
        var month = int.Parse(periodYm.Substring(4, 2));
        if (month == 1)
        {
            year--;
            month = 12;
        }
        else
        {
            month--;
        }

        return $"{year}{month:00}";
    }

    private static async Task CallAsync(DapperSession session, string sql, object param, string operation)
    {
        try
        {
            await session.ExecuteAsync(sql, param, commandTimeout: 180);
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"{operation}: {ex.Message}", ex);
        }
    }

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(
        DapperSession session, string sql, object param, string operation)
    {
        try
        {
            var rows = await session.QueryAsync<T>(sql, param, commandTimeout: 180);
            return rows.ToList();
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"{operation}: {ex.Message}", ex);
        }
    }

    private static async Task<T?> QueryFirstOrDefaultAsync<T>(
        DapperSession session, string sql, object param, string operation)
    {
        try
        {
            return await session.QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: 180);
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"{operation}: {ex.Message}", ex);
        }
    }

    private sealed class PeriodBounds
    {
        public string FromYmd { get; set; } = string.Empty;
        public string ToYmd { get; set; } = string.Empty;
    }

    private sealed class ObjectRow
    {
        public long ProductId { get; set; }
        public long DepartmentId { get; set; }
        public string? ProductCd { get; set; }
        public string? DepartmentCd { get; set; }
    }

    private sealed class PoolSnapshotRow
    {
        public string AccountCd { get; set; } = string.Empty;
        public long DepartmentId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Reduction { get; set; }
    }

    private sealed class BasisQtyRow
    {
        public long ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    private sealed class BasisAmountRow
    {
        public long ProductId { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class OpeningWipRow
    {
        public long ProductId { get; set; }
        public long DepartmentId { get; set; }
        public decimal ClosingQty { get; set; }
        public decimal Amount621 { get; set; }
        public decimal Amount62N { get; set; }
    }

    private sealed class WipAdjRow
    {
        public long ProductId { get; set; }
        public long DepartmentId { get; set; }
        public decimal PercentCompleted { get; set; }
        public decimal ClosingQty { get; set; }
        public decimal ProductReduce { get; set; }
        public decimal IncompleteQtyOpening { get; set; }
        public decimal Before621 { get; set; }
        public decimal Before62N { get; set; }
    }

    private sealed class AllocSumRow
    {
        public long ProductId { get; set; }
        public long DepartmentId { get; set; }
        public decimal Amount622 { get; set; }
        public decimal Amount627 { get; set; }
    }

    private sealed class Direct154Row
    {
        public long ProductId { get; set; }
        public long DepartmentId { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class ReceiptInputRow
    {
        public long InputId { get; set; }
        public long ProductId { get; set; }
        public long? InventoryId { get; set; }
        public string? ProductCd { get; set; }
        public string? InventoryNo { get; set; }
        public decimal Qty { get; set; }
        public decimal OldUnitPrice { get; set; }
        public decimal OldAmount { get; set; }
    }

    private sealed class WipStoredRow
    {
        public long ProductId { get; set; }
        public long DepartmentId { get; set; }
        public string? ProductCd { get; set; }
        public string? DepartmentCd { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCostPrice { get; set; }
        public decimal Amount621 { get; set; }
        public decimal Amount6212 { get; set; }
        public decimal Amount622 { get; set; }
        public decimal Amount627 { get; set; }
        public decimal Amount154Direct { get; set; }
        public decimal TotalAmountIncurred { get; set; }
        public decimal ClosingIncompleteAmount154 { get; set; }
        public decimal PercentCompleted { get; set; }
        public decimal ClosingIncompleteQty { get; set; }
    }

    private sealed class ReceiptStoredRow
    {
        public long ProductId { get; set; }
        public long DepartmentId { get; set; }
        public long InputId { get; set; }
        public long? InventoryId { get; set; }
        public string? ProductCd { get; set; }
        public string? InventoryNo { get; set; }
        public decimal Qty { get; set; }
        public decimal OldUnitPrice { get; set; }
        public decimal OldAmount { get; set; }
    }

    private sealed class MovementRow
    {
        public string FlowType { get; set; } = string.Empty;
        public long ProductId { get; set; }
        public string? ProductCd { get; set; }
        public string? ProductName { get; set; }
        public long StoreId { get; set; }
        public string? StoreCd { get; set; }
        public string? StoreName { get; set; }
        public string? ChitType { get; set; }
        public long? InputId { get; set; }
        public long? InventoryId { get; set; }
        public long? ChitdetailId { get; set; }
        public string? TxnYmd { get; set; }
        public decimal Quantity { get; set; }
        public decimal AmountCc { get; set; }
    }

    private sealed class ExistingCogsRow
    {
        public long? ChitId { get; set; }
        public string? StableKey { get; set; }
        public string? CompanyCd { get; set; }
    }
}
