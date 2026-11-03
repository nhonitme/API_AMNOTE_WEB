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
/// Idempotency history G-K — nguồn tin cậy = DB (OperationId + StableKey + fingerprint trong COMPANY_CD).
/// Không tin client hint.
/// </summary>
public static class HistoryPlan
{
    public const int OperationIdMinLength = 8;
    public const int OperationIdMaxLength = 64;

    /// <summary>
    /// VARCHAR(190) — UNIQUE (COMPANY_CD 20 + STABLE_KEY 190) * 3 utf8 = 630 ≤ 767 (MySQL 5.6 InnoDB,
    /// không cần innodb_large_prefix). Không dùng prefix unique.
    /// </summary>
    public const int StableKeyMaxLength = 190;

    public const int CompanyCdMaxLength = 20;
    public const int Mysql56InnoDbUtf8UniqueMaxBytes = 767;
    public const int ContentFingerprintMaxLength = 64;
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

    /// <summary>Bytes ước lượng index utf8 (3 byte/char) — kiểm thử tương thích MySQL 5.6.</summary>
    public static int EstimateUtf8IndexBytes(params int[] charLengths)
    {
        if (charLengths == null || charLengths.Length == 0)
            return 0;
        checked
        {
            var sum = 0;
            foreach (var n in charLengths)
            {
                if (n < 0)
                    throw new ArgumentOutOfRangeException(nameof(charLengths));
                sum += n * 3;
            }
            return sum;
        }
    }

    public static bool FitsMysql56Utf8UniqueIndex(params int[] charLengths) =>
        EstimateUtf8IndexBytes(charLengths) <= Mysql56InnoDbUtf8UniqueMaxBytes;

    /// <summary>
    /// Khóa history theo OperationId + RUN/STEP/ACTION.
    /// Canonical &gt; 190 → SHA-256 hex đầy đủ với prefix (73 ký tự) — không cắt prefix unique.
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
        if (companyCd.Trim().Length > CompanyCdMaxLength)
            throw new ArgumentException($"COMPANY_CD tối đa {CompanyCdMaxLength} ký tự.");
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

    public static string? NormalizeContentFingerprint(string? fingerprint, bool required)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            if (required)
                throw new ArgumentException("CONTENT_FINGERPRINT bắt buộc cho Confirm / ghi history có kiểm tra nội dung.");
            return null;
        }

        var fp = fingerprint.Trim();
        if (fp.Length > ContentFingerprintMaxLength)
        {
            throw new ArgumentException(
                $"CONTENT_FINGERPRINT tối đa {ContentFingerprintMaxLength} ký tự (VARCHAR(64)).");
        }

        return fp;
    }

    public static void EnsureStableKeyLength(string? stableKey)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("History STABLE_KEY bắt buộc.");
        if (stableKey.Trim().Length > StableKeyMaxLength)
        {
            throw new ArgumentException(
                $"STABLE_KEY vượt VARCHAR({StableKeyMaxLength}) — dùng hash canonical PC_HIST_H:.");
        }
    }

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
    /// Retry cùng OperationId + StableKey + fingerprint → Noop.
    /// Khác StableKey / fingerprint → Conflict.
    /// <paramref name="requireContentFingerprint"/> (Confirm): thiếu fingerprint một phía ≠ khớp.
    /// </summary>
    public static WritePlan PlanWrite(
        string stableKey,
        string operationId,
        IReadOnlyCollection<PersistedHistoryRef>? persistedFromDb,
        string? contentFingerprint = null,
        bool requireContentFingerprint = false)
    {
        EnsureStableKeyLength(stableKey);
        var op = RequireOperationId(operationId);
        var key = stableKey.Trim();
        var fp = NormalizeContentFingerprint(contentFingerprint, required: requireContentFingerprint);

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
            if (requireContentFingerprint)
            {
                throw new InvalidOperationException(
                    $"OperationId '{op}' đã persist nhưng thiếu STABLE_KEY/fingerprint từ DB — Conflict (Confirm).");
            }

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

        AssertFingerprintsCompatible(op, fp, exact.ContentFingerprint, requireContentFingerprint);

        return new WritePlan
        {
            Action = WriteAction.NoopAlreadyWritten,
            StableKey = key,
            OperationId = op,
            ContentFingerprint = fp ?? exact.ContentFingerprint
        };
    }

    /// <summary>
    /// Soft dry-run: cả hai null → OK; Confirm/require: thiếu một phía → Conflict; khác → Conflict.
    /// </summary>
    public static void AssertFingerprintsCompatible(
        string operationId,
        string? requestedFingerprint,
        string? persistedFingerprint,
        bool requireContentFingerprint)
    {
        var req = string.IsNullOrWhiteSpace(requestedFingerprint) ? null : requestedFingerprint.Trim();
        var got = string.IsNullOrWhiteSpace(persistedFingerprint) ? null : persistedFingerprint.Trim();

        if (!requireContentFingerprint && req == null && got == null)
            return;

        if (requireContentFingerprint)
        {
            if (req == null)
                throw new ArgumentException("CONTENT_FINGERPRINT bắt buộc khi Confirm yêu cầu fingerprint.");
            if (got == null)
            {
                throw new InvalidOperationException(
                    $"OperationId '{operationId}' trùng STABLE_KEY nhưng DB thiếu CONTENT_FINGERPRINT — Conflict (không coi thiếu là khớp).");
            }
        }
        else
        {
            // Soft: chỉ Conflict khi cả hai có và khác nhau
            if (req == null || got == null)
                return;
        }

        if (!string.Equals(req, got, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OperationId '{operationId}' trùng STABLE_KEY nhưng khác CONTENT_FINGERPRINT. Conflict.");
        }
    }

    public static WritePlan PlanWrite(
        string stableKey,
        string operationId,
        IReadOnlyCollection<string>? persistedOperationIdsFromDb,
        string? contentFingerprint = null,
        bool requireContentFingerprint = false) =>
        PlanWrite(
            stableKey,
            operationId,
            NormalizePersisted(null, persistedOperationIdsFromDb),
            contentFingerprint,
            requireContentFingerprint);

    /// <summary>
    /// Sau UNIQUE 1062 (request song song): đọc lại hàng đã commit → Noop/Conflict.
    /// </summary>
    public static WritePlan ResolveAfterDuplicateKey(
        string attemptedStableKey,
        string operationId,
        PersistedHistoryRef? existingRowByOperationId,
        string? contentFingerprint = null,
        bool requireContentFingerprint = false)
    {
        if (existingRowByOperationId == null
            || string.IsNullOrWhiteSpace(existingRowByOperationId.OperationId))
        {
            throw new InvalidOperationException(
                "Duplicate key history nhưng không đọc lại được OperationId từ DB sau race — abort.");
        }

        return PlanWrite(
            attemptedStableKey,
            operationId,
            new[] { existingRowByOperationId },
            contentFingerprint,
            requireContentFingerprint);
    }

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
                : "History idempotent — Noop (cùng OperationId + StableKey + fingerprint)."
        };
}
