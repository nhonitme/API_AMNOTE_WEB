using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Prerequisite;

/// <summary>
/// Bước 00 — đánh giá dữ liệu đầu vào G-K từ đối tượng RUN + phiếu nhập N155/C154.
/// Pure function: không ghi DB; Confirm gọi lại trước khi ghi bước phụ thuộc.
/// </summary>
public static class PrerequisiteValidation
{
    public static class Status
    {
        public const string NotChecked = "NOT_CHECKED";
        public const string Success = "SUCCESS";
        public const string Warning = "WARNING";
        public const string Error = "ERROR";
    }

    public static class Level
    {
        public const string Error = "ERROR";
        public const string Warning = "WARNING";
    }

    public static class Code
    {
        public const string PeriodNotReady = "PERIOD_NOT_READY";
        public const string NoSelectedProduct = "NO_SELECTED_PRODUCT";
        public const string NoSourceReceipt = "NO_SOURCE_RECEIPT";
        public const string MissingProductId = "MISSING_PRODUCT_ID";
        public const string MissingDepartmentId = "MISSING_DEPARTMENT_ID";
        public const string DuplicateMapping = "DUPLICATE_MAPPING";
        public const string InvalidQuantity = "INVALID_QUANTITY";
        public const string MissingAccountingLink = "MISSING_ACCOUNTING_LINK";
        public const string DeselectedSourceProduct = "DESELECTED_SOURCE_PRODUCT";
    }

    /// <summary>
    /// Đánh giá trạng thái tiền kiểm. Ưu tiên: NOT_CHECKED → ERROR → WARNING → SUCCESS.
    /// </summary>
    public static ProductionCostPrerequisiteValidationDto Evaluate(
        bool periodExists,
        long? runId,
        IReadOnlyList<ProductionCostObjectDto> objects,
        IReadOnlyList<ProductionCostSourceReceiptLineDto> receipts,
        int distinctSourceReceiptCount)
    {
        if (!periodExists || runId is null or <= 0)
        {
            return new ProductionCostPrerequisiteValidationDto
            {
                Status = Status.NotChecked,
                ErrorCount = 0,
                WarningCount = 0,
                Issues =
                [
                    new ProductionCostPrerequisiteIssueDto
                    {
                        Level = Level.Warning,
                        Code = Code.PeriodNotReady,
                        Message = "Kỳ G-K chưa được khởi tạo hoặc chưa tải được RUN. Hãy khởi tạo kỳ trước khi kiểm tra đầu vào."
                    }
                ]
            };
        }

        var issues = new List<ProductionCostPrerequisiteIssueDto>();
        var objectList = objects ?? Array.Empty<ProductionCostObjectDto>();
        var receiptList = receipts ?? Array.Empty<ProductionCostSourceReceiptLineDto>();

        // Enrich qty/receipt counts the same way GetObjectsAsync does so Confirm
        // and GET /objects share one rule set.
        var enriched = EnrichFinishedQty(objectList, receiptList);

        var selected = enriched
            .Where(x => string.Equals(x.IsSelected, "1", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (distinctSourceReceiptCount <= 0)
        {
            issues.Add(new ProductionCostPrerequisiteIssueDto
            {
                Level = Level.Error,
                Code = Code.NoSourceReceipt,
                Message =
                    "Không có chứng từ nhập thành phẩm nguồn hợp lệ trong kỳ (Nợ 155% / Có 154%). " +
                    "Đồng bộ đối tượng từ nhập kho hoặc kiểm tra hạch toán phiếu nhập."
            });
        }

        if (selected.Count == 0)
        {
            issues.Add(new ProductionCostPrerequisiteIssueDto
            {
                Level = Level.Error,
                Code = Code.NoSelectedProduct,
                Message =
                    "Chưa có thành phẩm được chọn để tính giá thành. " +
                    "Chọn đối tượng hợp lệ trên Bước 00 trước khi ghi các bước G-K."
            });
        }

        // Duplicate logical mapping: same PRODUCT_ID + DEPARTMENT_CD → multiple DEPARTMENT_ID
        var duplicateLogical = enriched
            .Where(x => x.ProductId > 0 && !string.IsNullOrWhiteSpace(x.DepartmentCd))
            .GroupBy(x => (x.ProductId, DepartmentCd: x.DepartmentCd!.Trim().ToUpperInvariant()))
            .Where(g => g.Select(x => x.DepartmentId).Distinct().Count() > 1);

        foreach (var group in duplicateLogical)
        {
            var ids = string.Join(", ", group.Select(x => x.DepartmentId).Distinct().OrderBy(x => x));
            foreach (var row in group)
            {
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.DuplicateMapping,
                    Message =
                        $"Mapping đối tượng/thành phẩm trùng hoặc mâu thuẫn: PRODUCT_ID={row.ProductId}, " +
                        $"DEPARTMENT_CD={group.Key.DepartmentCd}, DEPARTMENT_ID={ids}. " +
                        "Đối chiếu master đối tượng trước khi khóa giá thành.",
                    ProductId = row.ProductId,
                    DepartmentId = row.DepartmentId
                });
            }
        }

        // Source-bearing rows missing required IDs (orphan / incomplete mapping)
        foreach (var row in enriched.Where(x => x.ProductId <= 0 || x.DepartmentId <= 0))
        {
            if (row.ProductId <= 0)
            {
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.MissingProductId,
                    Message =
                        $"Thành phẩm nguồn thiếu PRODUCT_ID bắt buộc" +
                        (string.IsNullOrWhiteSpace(row.ProductCd) ? "." : $" (mã {row.ProductCd}).") +
                        " Không thể tính giá thành cho dòng này.",
                    ProductId = row.ProductId,
                    DepartmentId = row.DepartmentId
                });
            }

            if (row.DepartmentId <= 0)
            {
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.MissingDepartmentId,
                    Message =
                        $"Thành phẩm phát sinh nguồn thiếu DEPARTMENT_ID hợp lệ" +
                        (string.IsNullOrWhiteSpace(row.ProductCd) ? "." : $" (TP {row.ProductCd}).") +
                        " Gán đối tượng tập hợp chi phí trên danh mục sản phẩm rồi đồng bộ lại.",
                    ProductId = row.ProductId > 0 ? row.ProductId : null,
                    DepartmentId = row.DepartmentId
                });
            }
        }

        foreach (var row in selected)
        {
            if (row.ProductId <= 0)
            {
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.MissingProductId,
                    Message = "Thành phẩm được chọn thiếu PRODUCT_ID hợp lệ.",
                    ProductId = row.ProductId,
                    DepartmentId = row.DepartmentId
                });
                continue;
            }

            if (row.DepartmentId <= 0)
            {
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.MissingDepartmentId,
                    Message =
                        $"Thành phẩm được chọn thiếu DEPARTMENT_ID liên kết đối tượng tập hợp chi phí" +
                        (string.IsNullOrWhiteSpace(row.ProductCd) ? "." : $" (TP {row.ProductCd})."),
                    ProductId = row.ProductId,
                    DepartmentId = row.DepartmentId
                });
                continue;
            }

            if (row.FinishedQty <= 0)
            {
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.InvalidQuantity,
                    Message =
                        $"Thành phẩm được chọn có số lượng không hợp lệ (SL={row.FinishedQty})" +
                        (string.IsNullOrWhiteSpace(row.ProductCd) ? "." : $" — TP {row.ProductCd}.") +
                        " Kiểm tra phiếu nhập Nợ 155% / Có 154% trong kỳ.",
                    ProductId = row.ProductId,
                    DepartmentId = row.DepartmentId
                });

                // SL=0 trên đối tượng đã chọn → không có dòng nguồn N155/C154 khớp.
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.MissingAccountingLink,
                    Message =
                        $"Không liên kết được chứng từ nhập kho với hạch toán Nợ 155% / Có 154% cho thành phẩm được chọn" +
                        (string.IsNullOrWhiteSpace(row.ProductCd) ? "." : $" (TP {row.ProductCd})."),
                    ProductId = row.ProductId,
                    DepartmentId = row.DepartmentId
                });
            }
        }

        // Selected total qty must be > 0 (aggregate)
        if (selected.Count > 0 && selected.Sum(x => x.FinishedQty) <= 0)
        {
            if (!issues.Any(x => x.Code == Code.InvalidQuantity))
            {
                issues.Add(new ProductionCostPrerequisiteIssueDto
                {
                    Level = Level.Error,
                    Code = Code.InvalidQuantity,
                    Message = "Tổng số lượng thành phẩm được chọn phải lớn hơn 0."
                });
            }
        }

        // Warning: valid source FG exists but user deselected it
        var selectedKeys = selected
            .Where(x => x.ProductId > 0 && x.DepartmentId > 0)
            .Select(x => (x.ProductId, x.DepartmentId))
            .ToHashSet();

        foreach (var row in enriched.Where(x =>
                     !string.Equals(x.IsSelected, "1", StringComparison.OrdinalIgnoreCase)
                     && x.ProductId > 0
                     && x.DepartmentId > 0
                     && x.FinishedQty > 0
                     && x.ReceiptCount > 0))
        {
            if (selectedKeys.Contains((row.ProductId, row.DepartmentId)))
                continue;

            issues.Add(new ProductionCostPrerequisiteIssueDto
            {
                Level = Level.Warning,
                Code = Code.DeselectedSourceProduct,
                Message =
                    $"Thành phẩm có nhập kho hợp lệ trong kỳ nhưng chưa được chọn tính giá thành" +
                    (string.IsNullOrWhiteSpace(row.ProductCd) ? "." : $" (TP {row.ProductCd}).") +
                    " Bỏ qua nếu cố ý loại khỏi phạm vi tính.",
                ProductId = row.ProductId,
                DepartmentId = row.DepartmentId
            });
        }

        // Deduplicate identical code+product+dept messages
        issues = issues
            .GroupBy(x => (x.Level, x.Code, x.ProductId, x.DepartmentId, x.Message))
            .Select(g => g.First())
            .ToList();

        var errorCount = issues.Count(x => x.Level == Level.Error);
        var warningCount = issues.Count(x => x.Level == Level.Warning);

        string status;
        if (errorCount > 0)
            status = Status.Error;
        else if (warningCount > 0)
            status = Status.Warning;
        else
            status = Status.Success;

        return new ProductionCostPrerequisiteValidationDto
        {
            Status = status,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            Issues = issues
        };
    }

    public static void AssertReady(ProductionCostPrerequisiteValidationDto result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.Equals(result.Status, Status.Success, StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Status, Status.Warning, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var firstError = result.Issues.FirstOrDefault(x =>
            string.Equals(x.Level, Level.Error, StringComparison.OrdinalIgnoreCase));
        var detail = firstError?.Message
                     ?? (string.Equals(result.Status, Status.NotChecked, StringComparison.OrdinalIgnoreCase)
                         ? "Chưa kiểm tra được dữ liệu đầu vào. Hãy khởi tạo kỳ và mở Bước 00."
                         : "Dữ liệu đầu vào chưa đạt. Khắc phục lỗi trên Bước 00 trước khi ghi nhận giá thành.");

        throw new InvalidOperationException(
            $"Bước 00 — lỗi chặn dữ liệu đầu vào ({result.ErrorCount} lỗi). {detail}");
    }

    /// <summary>
    /// Gán FinishedQty / ReceiptCount theo PRODUCT_ID (+ DEPARTMENT_ID khi product đa đối tượng).
    /// Không nhân số lượng khi mapping nhiều DEPARTMENT_ID cho cùng PRODUCT_ID.
    /// </summary>
    public static IReadOnlyList<ProductionCostObjectDto> EnrichFinishedQty(
        IReadOnlyList<ProductionCostObjectDto> objects,
        IReadOnlyList<ProductionCostSourceReceiptLineDto> receipts)
    {
        var list = objects.Select(CloneObject).ToList();
        var objectsByProduct = list
            .Where(x => x.ProductId > 0)
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Count());
        var receiptsByProduct = receipts.ToLookup(x => x.ProductId);

        foreach (var item in list)
        {
            if (!objectsByProduct.TryGetValue(item.ProductId, out var count))
                continue;

            var matching = receiptsByProduct[item.ProductId]
                .Where(x => count == 1 || x.DepartmentId == item.DepartmentId)
                .GroupBy(x => x.InputId)
                .Select(g => g.First())
                .ToList();

            item.FinishedQty = matching.Sum(x => x.Quantity);
            item.ReceiptCount = matching
                .Where(x => x.InventoryId.GetValueOrDefault() > 0)
                .Select(x => x.InventoryId!.Value)
                .Distinct()
                .Count();
        }

        return list;
    }

    private static ProductionCostObjectDto CloneObject(ProductionCostObjectDto src) => new()
    {
        ObjectSelId = src.ObjectSelId,
        RunId = src.RunId,
        DepartmentId = src.DepartmentId,
        ProductId = src.ProductId,
        DepartmentCd = src.DepartmentCd,
        DepartmentName = src.DepartmentName,
        ProductCd = src.ProductCd,
        ProductName = src.ProductName,
        FinishedQty = src.FinishedQty,
        ReceiptCount = src.ReceiptCount,
        IsSelected = src.IsSelected,
        WarningText = src.WarningText
    };
}
