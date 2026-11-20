using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost;
using API_AMNOTE_WEB.Services.ProductionCost.Prerequisite;

namespace API_AMNOTE_WEB.Repositories;

public sealed partial class ProductionCostWriteRepository
{
    /// <inheritdoc />
    public async Task AssertPrerequisiteReadyAsync(
        DapperSession session,
        string companyCd,
        long runId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (runId <= 0)
            throw new InvalidOperationException("RUN_ID không hợp lệ khi kiểm tra dữ liệu đầu vào.");

        var objects = await QueryAsync<ProductionCostObjectDto>(
            session,
            @"SELECT
    o.OBJECT_SEL_ID AS ObjectSelId,
    o.RUN_ID AS RunId,
    o.DEPARTMENT_ID AS DepartmentId,
    o.PRODUCT_ID AS ProductId,
    o.DEPARTMENT_CD AS DepartmentCd,
    o.PRODUCT_CD AS ProductCd,
    o.IS_SELECTED AS IsSelected,
    o.WARNING_TEXT AS WarningText
FROM production_cost_object o
WHERE o.COMPANY_CD = @CompanyCd
  AND o.RUN_ID = @RunId
  AND o.ISDEL = '0'
ORDER BY o.DEPARTMENT_ID, o.PRODUCT_ID",
            new { CompanyCd = companyCd.Trim(), RunId = runId },
            "đọc production_cost_object (Bước 00)");

        // Same N155/C154 filter as read-side GetSourceReceiptLinesAsync.
        var receipts = await QueryAsync<ProductionCostSourceReceiptLineDto>(
            session,
            @"SELECT
    i.INPUT_ID AS InputId,
    i.INVENTORY_ID AS InventoryId,
    i.PRODUCT_ID AS ProductId,
    IFNULL(ext.DEPARTMENT_ID, 0) AS DepartmentId,
    MAX(i.QUANTITY) AS Quantity
FROM chit_inventory_input i
INNER JOIN chitdetailinfo d
  ON d.COMPANY_CD = i.COMPANY_CD
 AND d.CHITDETAIL_ID = i.CHITDETAIL_ID
 AND d.ISDEL = '0'
INNER JOIN chitinfo c
  ON c.COMPANY_CD = d.COMPANY_CD
 AND c.CHIT_ID = d.CHIT_ID
 AND c.ISDEL = '0'
INNER JOIN production_cost_run r
  ON r.COMPANY_CD = i.COMPANY_CD
 AND r.RUN_ID = @RunId
 AND r.ISDEL = '0'
INNER JOIN production_cost_period per
  ON per.COMPANY_CD = r.COMPANY_CD
 AND per.PERIOD_COST_ID = r.PERIOD_COST_ID
 AND per.ISDEL = '0'
LEFT JOIN chitdetailinfo_ext ext
  ON ext.COMPANY_CD = d.COMPANY_CD
 AND ext.CHITDETAIL_ID = d.CHITDETAIL_ID
 AND ext.ISDEL = '0'
WHERE i.COMPANY_CD = @CompanyCd
  AND i.ISDEL = '0'
  AND i.PRODUCT_ID > 0
  AND c.CHIT_YMD BETWEEN per.FROM_YMD AND per.TO_YMD
  AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
  AND TRIM(d.DEBIT) LIKE '155%'
  AND TRIM(d.CREDIT) LIKE '154%'
GROUP BY i.INPUT_ID, i.INVENTORY_ID, i.PRODUCT_ID, IFNULL(ext.DEPARTMENT_ID, 0)",
            new { CompanyCd = companyCd.Trim(), RunId = runId },
            "đọc phiếu nhập N155/C154 (Bước 00)");

        var receiptList = receipts.ToList();
        var distinctReceipts = receiptList
            .Where(x => x.InventoryId.GetValueOrDefault() > 0)
            .Select(x => x.InventoryId!.Value)
            .Distinct()
            .Count();

        var enriched = PrerequisiteValidation.EnrichFinishedQty(objects.ToList(), receiptList);
        var validation = PrerequisiteValidation.Evaluate(
            periodExists: true,
            runId: runId,
            objects: enriched,
            receipts: receiptList,
            distinctSourceReceiptCount: distinctReceipts);
        PrerequisiteValidation.AssertReady(validation);
    }
}
