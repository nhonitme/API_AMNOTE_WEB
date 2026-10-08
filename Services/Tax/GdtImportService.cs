using System.Globalization;
using System.Text.Json;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services.Tax
{
    public class GdtImportService : IGdtImportService
    {
        public const int MaxItems = 5000;

        private readonly IGdtImportRepository _repository;

        public GdtImportService(IGdtImportRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Lưu list trước (C99 SaveInvoiceList). Trả NeedDetail = !CanSkipDetailFetch.
        /// </summary>
        public async Task<GdtImportUpsertResult> UpsertListAsync(
            string companyCd,
            string userId,
            GdtImportUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            _ = companyCd;

            var type = NormalizeType(request.Type);
            var items = request.Items ?? new List<JsonElement>();
            var result = new GdtImportUpsertResult
            {
                Type = type,
                Received = items.Count,
            };

            if (items.Count > MaxItems)
            {
                throw new InvalidOperationException($"Số hóa đơn vượt giới hạn {MaxItems}.");
            }

            for (var index = 0; index < items.Count; index += 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = new GdtImportListItemState();
                try
                {
                    var row = MapItem(items[index], userId, preferSummary: true);
                    state.mhdon = row.mhdon;
                    if (string.IsNullOrWhiteSpace(row.mhdon))
                    {
                        state.Error = "thiếu mhdon/id";
                        result.Skipped += 1;
                        result.Errors.Add($"items[{index}]: thiếu mhdon/id");
                        result.Items.Add(state);
                        continue;
                    }

                    var existing = await _repository.ReadExistingAsync(type, row.mhdon, cancellationToken);
                    var statusChanged =
                        existing.HasList &&
                        !string.Equals(
                            existing.Status ?? "",
                            row.tthai ?? "",
                            StringComparison.Ordinal);

                    if (!existing.HasList)
                    {
                        await _repository.InsertListAsync(type, row, cancellationToken);
                        state.ListSaved = true;
                        state.HasList = true;
                        state.HasJson = false;
                        state.StatusChanged = false;
                        result.ListSaved += 1;
                    }
                    else if (statusChanged)
                    {
                        await _repository.InvalidateJsonAndUpdateStatusAsync(
                            type,
                            row.mhdon,
                            row.tthai ?? "",
                            cancellationToken);
                        state.ListSaved = true;
                        state.HasList = true;
                        state.HasJson = false;
                        state.StatusChanged = true;
                        result.ListSaved += 1;
                    }
                    else
                    {
                        state.HasList = true;
                        state.HasJson = existing.HasJson;
                        state.StatusChanged = false;
                    }

                    // CanSkipDetailFetch = HasList && HasJson && !StatusChanged
                    state.NeedDetail = !(state.HasList && state.HasJson && !state.StatusChanged);
                    if (state.NeedDetail)
                    {
                        result.NeedDetailCount += 1;
                    }

                    result.Upserted += 1;
                    result.Items.Add(state);
                }
                catch (Exception ex)
                {
                    state.Error = ex.Message;
                    state.NeedDetail = false;
                    result.Skipped += 1;
                    result.Errors.Add($"items[{index}]: {ex.Message}");
                    result.Items.Add(state);
                }
            }

            return result;
        }

        /// <summary>Lưu JSON chi tiết (sau khi kéo detail theo batch).</summary>
        public async Task<GdtImportUpsertResult> UpsertJsonAsync(
            string companyCd,
            string userId,
            GdtImportUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            _ = companyCd;
            _ = userId;

            var type = NormalizeType(request.Type);
            var items = request.Items ?? new List<JsonElement>();
            var result = new GdtImportUpsertResult
            {
                Type = type,
                Received = items.Count,
            };

            if (items.Count > MaxItems)
            {
                throw new InvalidOperationException($"Số hóa đơn vượt giới hạn {MaxItems}.");
            }

            for (var index = 0; index < items.Count; index += 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var row = MapItem(items[index], userId, preferSummary: false);
                    if (string.IsNullOrWhiteSpace(row.mhdon))
                    {
                        result.Skipped += 1;
                        result.Errors.Add($"items[{index}]: thiếu mhdon/id");
                        continue;
                    }

                    if (!row.HasDetailJson || string.IsNullOrWhiteSpace(row.JsonPayload))
                    {
                        result.Skipped += 1;
                        result.Errors.Add($"items[{index}]: thiếu detail");
                        continue;
                    }

                    await _repository.UpsertJsonAsync(type, row.mhdon, row.JsonPayload, cancellationToken);
                    result.JsonSaved += 1;
                    result.Upserted += 1;
                }
                catch (Exception ex)
                {
                    result.Skipped += 1;
                    result.Errors.Add($"items[{index}]: {ex.Message}");
                }
            }

            return result;
        }

        public async Task<GdtImportUpsertResult> UpsertAsync(
            string companyCd,
            string userId,
            GdtImportUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            _ = companyCd;

            var type = NormalizeType(request.Type);
            var items = request.Items ?? new List<JsonElement>();
            var result = new GdtImportUpsertResult
            {
                Type = type,
                Received = items.Count,
            };

            if (items.Count == 0)
            {
                return result;
            }

            if (items.Count > MaxItems)
            {
                throw new InvalidOperationException($"Số hóa đơn vượt giới hạn {MaxItems}.");
            }

            var rows = new List<GdtImportInvoiceRow>(items.Count);
            for (var index = 0; index < items.Count; index += 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var row = MapItem(items[index], userId);
                    if (string.IsNullOrWhiteSpace(row.mhdon))
                    {
                        result.Skipped += 1;
                        result.Errors.Add($"items[{index}]: thiếu mhdon");
                        continue;
                    }

                    rows.Add(row);
                }
                catch (Exception ex)
                {
                    result.Skipped += 1;
                    result.Errors.Add($"items[{index}]: {ex.Message}");
                }
            }

            if (rows.Count == 0)
            {
                return result;
            }

            var (headerCount, jsonCount) = await _repository.UpsertAsync(type, rows, cancellationToken);
            result.Upserted = headerCount;
            result.JsonSaved = jsonCount;
            return result;
        }

        /// <summary>Chuẩn hóa về BUY/SELL. FE gửi BUY|SELL.</summary>
        internal static string NormalizeType(string? type)
        {
            var value = (type ?? "BUY").Trim().ToUpperInvariant();
            return value switch
            {
                "SELL" or "SOLD" or "OUT" or "2" => "SELL",
                _ => "BUY",
            };
        }

        private static GdtImportInvoiceRow MapItem(JsonElement item, string userId, bool preferSummary = false)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("item phải là JSON object");
            }

            JsonElement? detail = null;
            if (TryGetProperty(item, "detail", out var detailEl)
                && detailEl.ValueKind == JsonValueKind.Object)
            {
                detail = detailEl;
            }

            string Pick(params string[] names)
            {
                foreach (var name in names)
                {
                    if (!preferSummary
                        && detail.HasValue
                        && TryGetString(detail.Value, name, out var fromDetail)
                        && fromDetail.Length > 0)
                    {
                        return fromDetail;
                    }

                    if (TryGetString(item, name, out var fromItem) && fromItem.Length > 0)
                    {
                        return fromItem;
                    }

                    if (preferSummary
                        && detail.HasValue
                        && TryGetString(detail.Value, name, out var fromDetailFallback)
                        && fromDetailFallback.Length > 0)
                    {
                        return fromDetailFallback;
                    }
                }

                return "";
            }

            double PickDouble(params string[] names)
            {
                foreach (var name in names)
                {
                    if (!preferSummary
                        && detail.HasValue
                        && TryGetDouble(detail.Value, name, out var fromDetail))
                    {
                        return fromDetail;
                    }

                    if (TryGetDouble(item, name, out var fromItem))
                    {
                        return fromItem;
                    }

                    if (preferSummary
                        && detail.HasValue
                        && TryGetDouble(detail.Value, name, out var fromDetailFallback))
                    {
                        return fromDetailFallback;
                    }
                }

                return 0;
            }

            var mhdon = Truncate(Pick("mhdon", "id"), 50);

            var row = new GdtImportInvoiceRow
            {
                mhdon = mhdon,
                tthai = Truncate(Pick("tthai"), 2),
                khmshdon = Truncate(Pick("khmshdon"), 1),
                khhdon = Truncate(Pick("khhdon"), 6),
                tdlap = Truncate(ToYmd(Pick("tdlap", "nlap")), 8),
                nky = Truncate(ToYmd(Pick("nky", "ntky", "tdky")), 8),
                shdon = Truncate(Pick("shdon"), 20),
                dvtte = Truncate(Pick("dvtte"), 3),
                mtdtchieu = Truncate(Pick("mtdtchieu"), 100),
                nbten = Truncate(Pick("nbten"), 200),
                nbdchi = Truncate(Pick("nbdchi"), 200),
                nbmst = Truncate(Pick("nbmst"), 14),
                nmten = Truncate(Pick("nmten", "nmtnmua"), 200),
                nmdchi = Truncate(Pick("nmdchi"), 200),
                nmmst = Truncate(Pick("nmmst"), 14),
                tgia = PickDouble("tgia", "TGia"),
                tgtcthue = PickDouble("tgtcthue"),
                tgtthue = PickDouble("tgtthue"),
                tgtttbso = PickDouble("tgtttbso"),
                tgtphi = PickDouble("tgtphi"),
                ttcktmai = PickDouble("ttcktmai"),
                GChu = Truncate(Pick("GChu", "gchu"), 500),
                AUTO_CHIT_CD = "",
                IS_ATTACH_FILE = "0",
                ISDEL = "",
                USERID = Truncate(userId ?? "", 20),
            };

            if (row.tgia <= 0)
            {
                row.tgia = 1;
            }

            if (string.IsNullOrWhiteSpace(row.dvtte))
            {
                row.dvtte = "VND";
            }
            else if (string.Equals(row.dvtte, "VNĐ", StringComparison.OrdinalIgnoreCase))
            {
                row.dvtte = "VND";
            }

            if (detail.HasValue)
            {
                row.HasDetailJson = true;
                row.JsonPayload = detail.Value.GetRawText();
            }
            else
            {
                row.HasDetailJson = false;
                row.JsonPayload = null;
            }

            return row;
        }

        private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
        {
            if (obj.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool TryGetString(JsonElement obj, string name, out string value)
        {
            value = "";
            if (!TryGetProperty(obj, name, out var el))
            {
                return false;
            }

            value = el.ValueKind switch
            {
                JsonValueKind.String => el.GetString()?.Trim() ?? "",
                JsonValueKind.Number => el.ToString(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                JsonValueKind.Null => "",
                _ => el.ToString(),
            };
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetDouble(JsonElement obj, string name, out double value)
        {
            value = 0;
            if (!TryGetProperty(obj, name, out var el))
            {
                return false;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out value))
            {
                return true;
            }

            if (el.ValueKind == JsonValueKind.String
                && double.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return false;
        }

        private static string ToYmd(string raw)
        {
            var text = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var digitPrefix = new string(text.TakeWhile(char.IsDigit).ToArray());
            if (digitPrefix.Length == 8)
            {
                return digitPrefix;
            }

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
            {
                if (epoch > 10_000_000_000L)
                {
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(epoch).ToLocalTime().DateTime;
                    return dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                }

                if (epoch > 1_000_000_000L)
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(epoch).ToLocalTime().DateTime;
                    return dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                }
            }

            string[] formats =
            {
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd",
                "dd/MM/yyyyTHH:mm:ss",
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy",
                "yyyy/MM/dd",
            };

            var normalized = text.Replace('T', ' ').Trim();
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
                || DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed)
                || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return parsed.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }

            var onlyDigits = new string(text.Where(char.IsDigit).ToArray());
            return onlyDigits.Length >= 8 ? onlyDigits[..8] : "";
        }

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
            {
                return value ?? "";
            }

            return value[..maxLen];
        }

        public Task<IReadOnlyList<GdtSavedLoginDto>> ListSavedLoginsAsync(
            string companyCd,
            CancellationToken cancellationToken = default)
            => _repository.ListSavedLoginsAsync(companyCd, cancellationToken);

        public async Task SaveLoginAsync(
            string companyCd,
            string username,
            string password,
            string? token = null,
            CancellationToken cancellationToken = default)
        {
            var nextUsername = Truncate((username ?? "").Trim(), 50);
            var nextPassword = Truncate(password ?? "", 50);
            var nextToken = string.IsNullOrWhiteSpace(token)
                ? null
                : Truncate(token.Trim(), 2000);
            if (string.IsNullOrWhiteSpace(nextUsername) || string.IsNullOrEmpty(nextPassword))
            {
                throw new InvalidOperationException("Thiếu tài khoản hoặc mật khẩu GDT.");
            }

            await _repository.SaveLoginAsync(
                companyCd,
                nextUsername,
                nextPassword,
                nextToken,
                cancellationToken);
        }
    }
}
