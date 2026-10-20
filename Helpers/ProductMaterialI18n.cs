using System.Globalization;

namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>Translation keys for the product material screens and Excel handlers.</summary>
    public sealed class ProductMaterialI18n
    {
        private static readonly IReadOnlyDictionary<string, string> Fallbacks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PM_PRODUCT_CODE"] = "Mã thành phẩm",
                ["PM_API_REQUEST_REQUIRED"] = "Thiếu nội dung yêu cầu.",
                ["PM_API_PRODUCT_ID_REQUIRED"] = "PRODUCT_ID phải lớn hơn 0.",
                ["PM_API_MATERIAL_ID_REQUIRED"] = "MATERIAL_ID phải lớn hơn 0.",
                ["PM_API_MATERIAL_PRODUCT_ID_REQUIRED"] = "MATERIAL_PRODUCT_ID phải lớn hơn 0.",
                ["PM_API_SELF_MATERIAL"] = "Thành phẩm không thể là nguyên vật liệu của chính nó.",
                ["PM_API_QUANTITY_INVALID"] = "Số lượng phải lớn hơn 0 và không vượt giới hạn DECIMAL(18,6).",
                ["PM_DB_COMPANY_REQUIRED"] = "Mã công ty không được để trống.",
                ["PM_DB_PRODUCT_ID_INVALID"] = "Mã ID thành phẩm hoặc NVL không hợp lệ.",
                ["PM_DB_FINISHED_NOT_FOUND"] = "Không tìm thấy thành phẩm trong công ty.",
                ["PM_DB_MATERIAL_NOT_FOUND"] = "Không tìm thấy nguyên vật liệu trong công ty.",
                ["PM_DB_DUPLICATE"] = "Nguyên vật liệu đã được khai báo cho thành phẩm.",
                ["PM_DB_ROW_NOT_FOUND"] = "Không tìm thấy dòng nguyên vật liệu của thành phẩm trong công ty.",
                ["PM_DB_ERROR"] = "Không thể lưu dữ liệu nguyên vật liệu.",
                ["PM_IMPORT_PARENT_REQUIRED"] = "Thiếu hoặc sai productId của thành phẩm đang chọn.",
                ["PM_IMPORT_PARENT_NOT_FOUND"] = "Thành phẩm không tồn tại trong công ty hiện tại.",
                ["PM_IMPORT_MATERIAL_UNKNOWN"] = "Mã NVL không tồn tại: {0}",
                ["PM_IMPORT_PRODUCT_UNKNOWN"] = "Mã thành phẩm không tồn tại: {0}",
                ["PM_IMPORT_EMPTY_CODE"] = "(trống)",
                ["PM_IMPORT_SELF_REFERENCE"] = "Không thể chọn chính thành phẩm làm NVL.",
                ["PM_IMPORT_DUPLICATE_MATERIAL"] = "Mã NVL trùng trong file: {0}",
                ["PM_IMPORT_ALREADY_EXISTS_SINGLE"] = "NVL {0} đã khai báo cho thành phẩm.",
                ["PM_IMPORT_DUPLICATE_PAIR"] = "Trùng cặp thành phẩm / NVL trong file: {0} / {1}",
                ["PM_IMPORT_ALREADY_EXISTS_PAIR"] = "NVL đã tồn tại trong thành phẩm: {0} / {1}",
                ["PM_IMPORT_QUANTITY_INVALID"] = "Số lượng phải lớn hơn 0 và có tối đa 6 chữ số thập phân.",
                ["PM_IMPORT_PARENT_INVALID"] = "Thiếu hoặc sai productId.",
                ["PM_IMPORT_SAVE_SINGLE_START"] = "Bắt đầu lưu danh sách nguyên vật liệu...",
                ["PM_IMPORT_SAVE_ALL_START"] = "Bắt đầu lưu định mức NVL cho nhiều thành phẩm...",
                ["PM_IMPORT_ROW_MATERIAL_NOT_FOUND"] = "Mã NVL không tồn tại.",
                ["PM_IMPORT_ROW_CODES_NOT_FOUND"] = "Mã thành phẩm hoặc NVL không tồn tại.",
                ["PM_IMPORT_ROW_QUANTITY_INVALID"] = "Số lượng định mức không hợp lệ.",
                ["PM_IMPORT_ROW_DATA_INVALID"] = "Thành phẩm/NVL/số lượng không hợp lệ.",
                ["PM_IMPORT_ROW_ERROR"] = "Dòng {0}: {1}",
                ["PM_IMPORT_PROGRESS"] = "Đang lưu {0}/{1} dòng...",
                ["PM_IMPORT_SINGLE_DONE"] = "Đã nhập {0} nguyên vật liệu.",
                ["PM_IMPORT_ALL_DONE"] = "Đã nhập {0} dòng định mức.",
            };

        private static readonly IReadOnlyDictionary<string, string> StoredProcedureErrors =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["COMPANY_CD is required"] = "PM_DB_COMPANY_REQUIRED",
                ["Invalid product ID"] = "PM_DB_PRODUCT_ID_INVALID",
                ["A product cannot use itself as material"] = "PM_API_SELF_MATERIAL",
                ["QUANTITY must be greater than zero"] = "PM_API_QUANTITY_INVALID",
                ["Finished product not found in company"] = "PM_DB_FINISHED_NOT_FOUND",
                ["Material not found in company"] = "PM_DB_MATERIAL_NOT_FOUND",
                ["Material already exists for this product"] = "PM_DB_DUPLICATE",
                ["Material row not found for this product/company"] = "PM_DB_ROW_NOT_FOUND",
                ["Material row not found in company"] = "PM_DB_ROW_NOT_FOUND",
            };

        private readonly IReadOnlyDictionary<string, string> _messages;

        private ProductMaterialI18n(IReadOnlyDictionary<string, string> messages)
        {
            _messages = messages;
        }

        public static async Task<ProductMaterialI18n> LoadAsync(string? lang = null)
        {
            var resolvedLang = Common.NormalizeLanguageCode(lang ?? Common.GetCurrentLanguage());
            var messages = await Common.getLanguage(Fallbacks.Keys, resolvedLang);
            return new ProductMaterialI18n(messages);
        }

        public string T(string key, params object[] args)
        {
            var fallback = Fallbacks.TryGetValue(key, out var value) ? value : key;
            var message = _messages.TryGetValue(key, out var localized)
                && !string.IsNullOrWhiteSpace(localized)
                && !string.Equals(localized, key, StringComparison.OrdinalIgnoreCase)
                    ? localized : fallback;
            return args.Length == 0 ? message : string.Format(CultureInfo.InvariantCulture, message, args);
        }

        public string TranslateStoredProcedureError(string error)
        {
            foreach (var pair in StoredProcedureErrors)
            {
                if (error.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                    return T(pair.Value);
            }
            return T("PM_DB_ERROR");
        }
    }
}
