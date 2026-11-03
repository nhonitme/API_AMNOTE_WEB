using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

public static class HistoryActionCode
{
    public const string Reopen = "REOPEN";
    public const string Preview = "PREVIEW";
    public const string Recalculate = "RECALCULATE";
    public const string Confirm = "CONFIRM";
    public const string Compare = "COMPARE";
    public const string Error = "ERROR";
}

/// <summary>
/// Idempotency history G-K — nguồn tin cậy = DB (OperationId + StableKey trong COMPANY_CD).
/// Không tin client hint.
/// </summary>
public static class HistoryPlan
{
    public const int OperationIdMinLength = 8;
    public const int OperationIdMaxLength = 64;
    /// <summary>Khớp VARCHAR(255) + UNIQUE utf8 InnoDB (MySQL 5.6/8.0).</summary>
    public const int StableKeyMaxLength = 255;
    public const string HashedStableKeyPrefix = "PC_HIST_H:";

    public enum WriteAction
    {
        InsertOnce,
        NoopAlreadyWritten
    }

    public sealed class WritePlan
    {
        public WriteAction Action { get; init; }
        public string StableKey { get; init; } = string.Empty;
        public string OperationId { get; init; } = string.Empty;
        public string? ContentFingerprint { get; init; }
        public bool WritesHistory => Action == WriteAction.InsertOnce;
    }

    /// <summary>
    /// Khóa history theo OperationId + RUN/STEP/ACTION.
    /// Nếu canonical vượt giới hạn cột → hash SHA-256 ổn định (PC_HIST_H:…).
    /// </summary>
    public static string StableKey(
        string companyCd,
        long runId,
        int runVersion,
        string stepCode,
        string actionCode,
        string operationId)
    {
        var op = RequireOperationId(operationId);
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new ArgumentException("COMPANY_CD bắt buộc cho STABLE_KEY.");
        if (runId <= 0 || runVersion <= 0)
            throw new ArgumentException("RUN_ID / RUN_VERSION phải > 0 cho STABLE_KEY.");
        if (string.IsNullOrWhiteSpace(stepCode))
            throw new ArgumentException("STEP_CODE bắt buộc cho STABLE_KEY.");
        if (string.IsNullOrWhiteSpace(actionCode))
            throw new ArgumentException("ACTION_CODE bắt buộc cho STABLE_KEY.");

        var canonical =
            $"PC_HIST:{companyCd.Trim()}:{runId}:v{runVersion}:{stepCode.Trim()}:{actionCode.Trim()}:op:{op}";

        if (canonical.Length <= StableKeyMaxLength)
            return canonical;

        // Hash fallback — collision cực hiếm; UNIQUE (COMPANY_CD, OPERATION_ID) vẫn là chốt race.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        var hashed = HashedStableKeyPrefix + hash;
        EnsureStableKeyLength(hashed);
        return hashed;
    }

    public static string RequireOperationId(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException(
                $"OperationId bắt buộc ({OperationIdMinLength}–{OperationIdMaxLength} ký tự).");

        var op = operationId.Trim();
        if (op.Length < OperationIdMinLength || op.Length > OperationIdMaxLength)
        {
            throw new ArgumentException(
                $"OperationId phải từ {OperationIdMinLength} đến {OperationIdMaxLength} ký tự (VARCHAR(64)).");
        }

        return op;
    }

    public static void EnsureStableKeyLength(string? stableKey)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("History STABLE_KEY bắt buộc.");
        if (stableKey.Trim().Length > StableKeyMaxLength)
        {
            throw new ArgumentException(
                $"STABLE_KEY vượt VARCHAR({StableKeyMaxLength}) — rút gọn hoặc dùng hash canonical.");
        }
    }

    /// <summary>
    /// Chuẩn hóa persisted từ DB. OpId-only (legacy dry-run) → StableKey=null (Noop khi trùng OpId).
    /// Confirm thật phải truyền đủ StableKey từ DB.
    /// </summary>
    public static IReadOnlyList<PersistedHistoryRef> NormalizePersisted(
        IReadOnlyCollection<PersistedHistoryRef>? persistedHistoryFromDb,
        IReadOnlyCollection<string>? persistedOperationIdsFromDb)
    {
        if (persistedHistoryFromDb is { Count: > 0 })
        {
            return persistedHistoryFromDb
                .Where(p => !string.IsNullOrWhiteSpace(p.OperationId))
                .Select(p => new PersistedHistoryRef
                {
                    OperationId = p.OperationId.Trim(),
                    StableKey = string.IsNullOrWhiteSpace(p.StableKey) ? null : p.StableKey.Trim(),
                    ContentFingerprint = string.IsNullOrWhiteSpace(p.ContentFingerprint)
                        ? null
                        : p.ContentFingerprint.Trim()
                })
                .ToList();
        }

        if (persistedOperationIdsFromDb is { Count: > 0 })
        {
            return persistedOperationIdsFromDb
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => new PersistedHistoryRef
                {
                    OperationId = id.Trim(),
                    StableKey = null
                })
                .ToList();
        }

        return Array.Empty<PersistedHistoryRef>();
    }

    /// <summary>
    /// Retry cùng OperationId + cùng StableKey (+ cùng fingerprint nếu có) → Noop.
    /// Cùng OperationId nhưng khác StableKey / fingerprint → Conflict.
    /// </summary>
    public static WritePlan PlanWrite(
        string stableKey,
        string operationId,
        IReadOnlyCollection<PersistedHistoryRef>? persistedFromDb,
        string? contentFingerprint = null)
    {
        EnsureStableKeyLength(stableKey);
        var op = RequireOperationId(operationId);
        var key = stableKey.Trim();
        var fp = string.IsNullOrWhiteSpace(contentFingerprint) ? null : contentFingerprint.Trim();

        var known = persistedFromDb ?? Array.Empty<PersistedHistoryRef>();
        var sameOp = known
            .Where(p => string.Equals(
                (p.OperationId ?? string.Empty).Trim(), op, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sameOp.Count == 0)
        {
            return new WritePlan
            {
                Action = WriteAction.InsertOnce,
                StableKey = key,
                OperationId = op,
                ContentFingerprint = fp
            };
        }

        var withKey = sameOp
            .Where(p => !string.IsNullOrWhiteSpace(p.StableKey))
            .ToList();

        if (withKey.Count == 0)
        {
            // Legacy OpId-only (dry-run) — coi là retry cùng nội dung.
            return new WritePlan
            {
                Action = WriteAction.NoopAlreadyWritten,
                StableKey = key,
                OperationId = op,
                ContentFingerprint = fp
            };
        }

        var exact = withKey.FirstOrDefault(p =>
            string.Equals(p.StableKey!.Trim(), key, StringComparison.OrdinalIgnoreCase));

        if (exact == null)
        {
            throw new InvalidOperationException(
                $"OperationId '{op}' đã dùng cho thao tác khác (STABLE_KEY khác RUN/STEP/ACTION). Conflict — không Noop.");
        }

        if (!string.IsNullOrWhiteSpace(fp)
            && !string.IsNullOrWhiteSpace(exact.ContentFingerprint)
            && !string.Equals(exact.ContentFingerprint.Trim(), fp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OperationId '{op}' trùng STABLE_KEY nhưng khác nội dung nghiệp vụ (fingerprint). Conflict.");
        }

        return new WritePlan
        {
            Action = WriteAction.NoopAlreadyWritten,
            StableKey = key,
            OperationId = op,
            ContentFingerprint = fp ?? exact.ContentFingerprint
        };
    }

    /// <summary>Overload dry-run / legacy — chỉ danh sách OperationId.</summary>
    public static WritePlan PlanWrite(
        string stableKey,
        string operationId,
        IReadOnlyCollection<string>? persistedOperationIdsFromDb,
        string? contentFingerprint = null) =>
        PlanWrite(
            stableKey,
            operationId,
            NormalizePersisted(null, persistedOperationIdsFromDb),
            contentFingerprint);

    /// <summary>
    /// Sau lỗi UNIQUE race (1062): đọc lại hàng DB theo OperationId → Noop hoặc Conflict.
    /// Không INSERT lại.
    /// </summary>
    public static WritePlan ResolveAfterDuplicateKey(
        string attemptedStableKey,
        string operationId,
        PersistedHistoryRef? existingRowByOperationId,
        string? contentFingerprint = null)
    {
        if (existingRowByOperationId == null
            || string.IsNullOrWhiteSpace(existingRowByOperationId.OperationId))
        {
            throw new InvalidOperationException(
                "Duplicate key history nhưng không đọc lại được OperationId từ DB — abort.");
        }

        return PlanWrite(
            attemptedStableKey,
            operationId,
            new[] { existingRowByOperationId },
            contentFingerprint);
    }

    /// <summary>
    /// Nhận diện lỗi UNIQUE (MySQL 1062) không phụ thuộc package — dùng message/type name.
    /// </summary>
    public static bool IsDuplicateKeyException(Exception? ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            var typeName = cur.GetType().FullName ?? cur.GetType().Name;
            var msg = cur.Message ?? string.Empty;
            if (typeName.Contains("MySqlException", StringComparison.OrdinalIgnoreCase)
                && (msg.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("1062", StringComparison.Ordinal)))
                return true;

            if (msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static ProductionCostHistoryWriteResult ToWriteResult(WritePlan plan, long? historyId = null) =>
        new()
        {
            Inserted = plan.WritesHistory,
            IdempotencyAction = plan.Action.ToString(),
            HistoryId = historyId,
            OperationId = plan.OperationId,
            StableKey = plan.StableKey,
            Message = plan.WritesHistory
                ? "History inserted."
                : "History idempotent — Noop (cùng OperationId + StableKey)."
        };
}
