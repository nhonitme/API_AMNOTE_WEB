using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public class SysGridColumnSettingRepository : ISysGridColumnSettingRepository
    {
        private const string CacheScope = "sys-grid-column-setting";
        private const string GetAllQuery = "CALL getallsys_grid_column_bundle(@p_COMPANY_CD, @p_USER_ID)";
        private const string GetSliceQuery = "CALL getsys_grid_column_slice(@p_COMPANY_CD, @p_USER_ID, @p_GRID_ID)";
        private const string GetSettingQuery = "CALL getsys_grid_column_setting(@p_TEMPLATE_ID, @p_GRID_ID)";
        private const string SetTemplateQuery =
            "CALL setsys_grid_template(@p_COMPANY_CD, @p_USER_ID, @p_GRID_ID, @p_TEMPLATE_ID, @p_TEMPLATE_NAME, @p_IS_DEFAULT_TEMPLATE, @p_UPDATE_BY)";
        private const string SetColumnQuery =
            @"CALL setsys_grid_column_setting(@p_TEMPLATE_ID, @p_GRID_ID, @p_FIELD_NAME, @p_LABEL_TEXT, @p_CAPTION, @p_IS_VISIBLE, @p_VISIBLE_INDEX, @p_COLUMN_WIDTH, @p_IS_FIXED, @p_FIXED_POSITION, @p_ALLOW_HIDING, @p_SORT_ORDER, @p_SORT_INDEX, @p_UPDATE_BY)";
        private const string ResetQuery = "CALL resetsys_grid_column_setting(@p_COMPANY_CD, @p_USER_ID, @p_GRID_ID)";

        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;
        private readonly IMasterDataCacheService _cacheService;

        public SysGridColumnSettingRepository(
            DapperExecutor db,
            IActivityLogService activityLogService,
            IMasterDataCacheService cacheService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        public async Task<SysGridColumnBundleDto> GetAllAsync(string companyCd, string userId)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedUserId = Common.NormalizeRequiredText(userId);

            return await _cacheService.GetOrCreateAsync(
                CacheScope,
                normalizedCompanyCd,
                $"userId={normalizedUserId}",
                () => QueryBundleAsync(normalizedCompanyCd, normalizedUserId));
        }

        public async Task<SysGridColumnBundleDto> SaveAsync(
            string companyCd,
            string userId,
            string gridId,
            long templateId,
            string? templateName,
            string? isDefaultTemplate,
            IEnumerable<SysGridColumnSettingSaveItemRequest> columns,
            string actor)
        {
            ArgumentNullException.ThrowIfNull(columns);

            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedUserId = Common.NormalizeRequiredText(userId);
            var normalizedGridId = Common.NormalizeRequiredText(gridId);
            var normalizedColumns = columns.ToList();

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                var template = (await session.QueryAsync<SysGridColumnTemplate>(SetTemplateQuery, new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_USER_ID = normalizedUserId,
                    p_GRID_ID = normalizedGridId,
                    p_TEMPLATE_ID = templateId,
                    p_TEMPLATE_NAME = templateName,
                    p_IS_DEFAULT_TEMPLATE = Common.NormalizeFlagString(isDefaultTemplate, "0"),
                    p_UPDATE_BY = actor
                })).FirstOrDefault();

                if (template == null || template.TEMPLATE_ID <= 0)
                {
                    throw new InvalidOperationException("Failed to save grid template.");
                }

                var existing = (await session.QueryAsync<SysGridColumnSettingDto>(GetSettingQuery, new
                {
                    p_TEMPLATE_ID = template.TEMPLATE_ID,
                    p_GRID_ID = normalizedGridId
                })).ToList();

                var existingByField = existing.ToDictionary(
                    item => Common.NormalizeRequiredText(item.FIELD_NAME),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var patch in normalizedColumns)
                {
                    var fieldName = Common.NormalizeRequiredText(patch.FIELD_NAME);
                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        continue;
                    }

                    existingByField.TryGetValue(fieldName, out var current);
                    var merged = MergePatch(current, patch, template.TEMPLATE_ID, normalizedGridId, fieldName);
                    await session.ExecuteAsync(SetColumnQuery, CreateSetColumnParameters(merged, actor));
                }

                var slice = await QueryGridSliceAsync(session, normalizedCompanyCd, normalizedUserId, normalizedGridId);

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    normalizedCompanyCd,
                    templateId > 0 ? "UPDATE" : "INSERT",
                    "SysGridColumnSetting",
                    "sys_grid_column_setting",
                    $"{normalizedUserId}:{normalizedGridId}:{template.TEMPLATE_ID}",
                    JsonSerializer.Serialize(existing),
                    JsonSerializer.Serialize(slice.SETTINGS),
                    $"Save grid column setting {normalizedGridId}:{template.TEMPLATE_ID}");

                session.Commit();
                await _cacheService.ClearAsync(CacheScope, normalizedCompanyCd);
                return slice;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<SysGridColumnBundleDto> ResetAsync(string companyCd, string userId, string gridId, string actor)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedUserId = Common.NormalizeRequiredText(userId);
            var normalizedGridId = Common.NormalizeRequiredText(gridId);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                await session.ExecuteAsync(ResetQuery, new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_USER_ID = normalizedUserId,
                    p_GRID_ID = normalizedGridId
                });

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    normalizedCompanyCd,
                    "DELETE",
                    "SysGridColumnSetting",
                    "sys_grid_column_setting",
                    $"{normalizedUserId}:{normalizedGridId}",
                    gridId,
                    string.Empty,
                    $"Reset grid column setting {normalizedGridId}");

                session.Commit();
                await _cacheService.ClearAsync(CacheScope, normalizedCompanyCd);
                return new SysGridColumnBundleDto
                {
                    COLUMNS = new List<SysGridColumnDto>(),
                    TEMPLATES = new List<SysGridColumnTemplateDto>(),
                    SETTINGS = new List<SysGridColumnSettingDto>()
                };
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IReadOnlyList<SysGridColumn>> GetMergedLayoutAsync(
            string companyCd,
            string userId,
            string gridId,
            long? templateId = null)
        {
            var bundle = await GetAllAsync(companyCd, userId);
            var normalizedGridId = Common.NormalizeRequiredText(gridId);
            var columns = bundle.COLUMNS
                .Where(item => string.Equals(item.GRID_ID, normalizedGridId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var templates = bundle.TEMPLATES
                .Where(item => string.Equals(item.GRID_ID, normalizedGridId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var selectedTemplateId = templateId.GetValueOrDefault();
            if (selectedTemplateId <= 0)
            {
                selectedTemplateId = templates
                    .Where(item => item.IS_DEFAULT_TEMPLATE == "1")
                    .Select(item => item.TEMPLATE_ID)
                    .FirstOrDefault();
            }

            var settings = selectedTemplateId <= 0
                ? new List<SysGridColumnSettingDto>()
                : bundle.SETTINGS
                    .Where(item =>
                        string.Equals(item.GRID_ID, normalizedGridId, StringComparison.OrdinalIgnoreCase) &&
                        item.TEMPLATE_ID == selectedTemplateId)
                    .ToList();

            return MergeLayout(columns, settings);
        }

        private async Task<SysGridColumnBundleDto> QueryBundleAsync(string companyCd, string userId)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            using var grid = await session.QueryMultipleAsync(GetAllQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_USER_ID = userId
            });

            var columns = (await grid.ReadAsync<SysGridColumnDto>()).ToList();
            var templates = grid.IsConsumed
                ? new List<SysGridColumnTemplateDto>()
                : (await grid.ReadAsync<SysGridColumnTemplateDto>()).ToList();
            var settings = grid.IsConsumed
                ? new List<SysGridColumnSettingDto>()
                : (await grid.ReadAsync<SysGridColumnSettingDto>()).ToList();

            return new SysGridColumnBundleDto
            {
                COLUMNS = columns,
                TEMPLATES = templates,
                SETTINGS = settings
            };
        }

        private static async Task<SysGridColumnBundleDto> QueryGridSliceAsync(
            DapperSession session,
            string companyCd,
            string userId,
            string gridId)
        {
            using var grid = await session.QueryMultipleAsync(GetSliceQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_USER_ID = userId,
                p_GRID_ID = gridId
            });

            var templates = (await grid.ReadAsync<SysGridColumnTemplateDto>()).ToList();
            var settings = grid.IsConsumed
                ? new List<SysGridColumnSettingDto>()
                : (await grid.ReadAsync<SysGridColumnSettingDto>()).ToList();

            return new SysGridColumnBundleDto
            {
                COLUMNS = new List<SysGridColumnDto>(),
                TEMPLATES = templates,
                SETTINGS = settings
            };
        }

        private static SysGridColumnSetting MergePatch(
            SysGridColumnSettingDto? current,
            SysGridColumnSettingSaveItemRequest patch,
            long templateId,
            string gridId,
            string fieldName)
        {
            return new SysGridColumnSetting
            {
                TEMPLATE_ID = templateId,
                GRID_ID = gridId,
                FIELD_NAME = fieldName,
                LABEL_TEXT = patch.LABEL_TEXT ?? current?.LABEL_TEXT,
                CAPTION = patch.CAPTION ?? current?.CAPTION,
                IS_VISIBLE = patch.IS_VISIBLE ?? current?.IS_VISIBLE,
                VISIBLE_INDEX = patch.VISIBLE_INDEX ?? current?.VISIBLE_INDEX,
                COLUMN_WIDTH = patch.COLUMN_WIDTH ?? current?.COLUMN_WIDTH,
                IS_FIXED = patch.IS_FIXED ?? current?.IS_FIXED,
                FIXED_POSITION = patch.FIXED_POSITION ?? current?.FIXED_POSITION,
                ALLOW_HIDING = patch.ALLOW_HIDING ?? current?.ALLOW_HIDING,
                SORT_ORDER = patch.SORT_ORDER ?? current?.SORT_ORDER,
                SORT_INDEX = patch.SORT_INDEX ?? current?.SORT_INDEX
            };
        }

        private static object CreateSetColumnParameters(SysGridColumnSetting item, string actor)
        {
            return new
            {
                p_TEMPLATE_ID = item.TEMPLATE_ID,
                p_GRID_ID = item.GRID_ID,
                p_FIELD_NAME = item.FIELD_NAME,
                p_LABEL_TEXT = item.LABEL_TEXT,
                p_CAPTION = item.CAPTION,
                p_IS_VISIBLE = item.IS_VISIBLE,
                p_VISIBLE_INDEX = item.VISIBLE_INDEX,
                p_COLUMN_WIDTH = item.COLUMN_WIDTH,
                p_IS_FIXED = item.IS_FIXED,
                p_FIXED_POSITION = item.FIXED_POSITION,
                p_ALLOW_HIDING = item.ALLOW_HIDING,
                p_SORT_ORDER = item.SORT_ORDER,
                p_SORT_INDEX = item.SORT_INDEX,
                p_UPDATE_BY = actor
            };
        }

        private static List<SysGridColumn> MergeLayout(
            IReadOnlyList<SysGridColumnDto> columns,
            IReadOnlyList<SysGridColumnSettingDto> settings)
        {
            var settingsByField = settings
                .Where(item => !string.IsNullOrWhiteSpace(item.FIELD_NAME))
                .GroupBy(item => item.FIELD_NAME, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var merged = new List<SysGridColumn>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                settingsByField.TryGetValue(column.FIELD_NAME, out var setting);
                merged.Add(Coalesce(column, setting));
                seen.Add(column.FIELD_NAME);
            }

            foreach (var setting in settings)
            {
                if (seen.Contains(setting.FIELD_NAME))
                {
                    continue;
                }

                merged.Add(FromSettingOnly(setting));
            }

            return merged
                .OrderBy(item => item.VISIBLE_INDEX ?? int.MaxValue)
                .ThenBy(item => item.FIELD_NAME, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static SysGridColumn Coalesce(SysGridColumnDto column, SysGridColumnSettingDto? setting)
        {
            var fieldName = column.FIELD_NAME;
            var settingLabel = Common.NormalizeNullableText(setting?.LABEL_TEXT);
            var columnLabel = Common.NormalizeNullableText(column.LABEL_TEXT);
            var columnCaption = Common.NormalizeNullableText(column.CAPTION);

            return new SysGridColumn
            {
                ID = column.ID,
                GRID_ID = column.GRID_ID,
                FIELD_NAME = fieldName,
                // Display resolve: translate(setting.LABEL_TEXT) → translate(column.LABEL_TEXT)
                // → translate(FIELD_NAME) → column.CAPTION. Never use setting.CAPTION.
                LABEL_TEXT = settingLabel ?? columnLabel,
                CAPTION = columnCaption ?? string.Empty,
                IS_VISIBLE = setting?.IS_VISIBLE ?? column.IS_VISIBLE,
                VISIBLE_INDEX = setting?.VISIBLE_INDEX ?? column.VISIBLE_INDEX,
                COLUMN_WIDTH = setting?.COLUMN_WIDTH ?? column.COLUMN_WIDTH,
                IS_FIXED = setting?.IS_FIXED ?? column.IS_FIXED,
                FIXED_POSITION = setting?.FIXED_POSITION ?? column.FIXED_POSITION,
                ALLOW_HIDING = setting?.ALLOW_HIDING ?? column.ALLOW_HIDING,
                ALIGN = column.ALIGN,
                FORMAT_TYPE = column.FORMAT_TYPE,
                SORT_ORDER = setting?.SORT_ORDER ?? column.SORT_ORDER,
                SORT_INDEX = setting?.SORT_INDEX ?? column.SORT_INDEX
            };
        }

        private static SysGridColumn FromSettingOnly(SysGridColumnSettingDto setting)
        {
            return new SysGridColumn
            {
                GRID_ID = setting.GRID_ID,
                FIELD_NAME = setting.FIELD_NAME,
                LABEL_TEXT = setting.LABEL_TEXT,
                CAPTION = string.Empty,
                IS_VISIBLE = setting.IS_VISIBLE ?? "1",
                VISIBLE_INDEX = setting.VISIBLE_INDEX,
                COLUMN_WIDTH = setting.COLUMN_WIDTH,
                IS_FIXED = setting.IS_FIXED ?? "0",
                FIXED_POSITION = setting.FIXED_POSITION,
                ALLOW_HIDING = setting.ALLOW_HIDING ?? "1",
                SORT_ORDER = setting.SORT_ORDER,
                SORT_INDEX = setting.SORT_INDEX
            };
        }

    }
}
