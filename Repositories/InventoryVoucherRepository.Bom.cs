using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed partial class InventoryVoucherRepository
    {
        private sealed class BomInsertIdentity { public long ID { get; set; } }

        private static string JoinProductIds(IEnumerable<long> productIds)
            => string.Join(",", productIds);

        public async Task<InventoryBomSnapshotDto> GetInventoryBomSnapshotAsync(string companyCd, long inventoryId)
        {
            // Only IO vouchers of this company may have this snapshot.
            var finished = (await _db.QueryAsync<InventoryBomFinishedDto>(
                Net_DB.Net_DB_Company,
                "CALL sp_inventory_bom_finished_get(@p_COMPANY_CD, @p_INVENTORY_ID)",
                new { p_COMPANY_CD = companyCd, p_INVENTORY_ID = inventoryId })).ToList();
            foreach (var row in finished)
                row.CLIENT_KEY = "FIN-" + row.FINISHED_LINE_ID;

            var materials = (await _db.QueryAsync<InventoryBomMaterialDto>(
                Net_DB.Net_DB_Company,
                "CALL sp_inventory_bom_material_get(@p_COMPANY_CD, @p_INVENTORY_ID)",
                new { p_COMPANY_CD = companyCd, p_INVENTORY_ID = inventoryId })).ToList();

            foreach (var row in materials)
            {
                row.CLIENT_KEY = "MAT-" + row.BOM_MATERIAL_ID;
                row.FINISHED_CLIENT_KEY = "FIN-" + row.FINISHED_LINE_ID;
            }
            return new InventoryBomSnapshotDto { FINISHED = finished, MATERIALS = materials };
        }

        public async Task<IReadOnlyList<InventoryLatestInputStoreDto>> GetLatestInputStoresByProductIdsAsync(
            string companyCd, IEnumerable<long> productIds)
        {
            var ids = productIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            if (ids.Length == 0)
                return Array.Empty<InventoryLatestInputStoreDto>();

            // One query: latest receipt store, else product_info.STORE_ID.
            return (await _db.QueryAsync<InventoryLatestInputStoreDto>(
                Net_DB.Net_DB_Company,
                "CALL sp_inventory_bom_latest_input_stores_get(@p_COMPANY_CD, @p_PRODUCT_IDS)",
                new { p_COMPANY_CD = companyCd, p_PRODUCT_IDS = JoinProductIds(ids) })).ToList();
        }

        private async Task SaveBomSnapshotAsync(
            DapperSession session, string companyCd, string userId, long inventoryId,
            InventoryBomSnapshotDto snapshot, IReadOnlyList<InventoryOutput> outputs)
        {
            if (snapshot.FINISHED == null || snapshot.MATERIALS == null)
                throw new ArgumentException("Both BOM grids are required.");

            // Validate references BEFORE soft deleting the current snapshot.
            if (snapshot.FINISHED.Count > 10000 || snapshot.MATERIALS.Count > 100000)
                throw new ArgumentException("BOM snapshot exceeds supported row limits.");
            var finishedByKey = new Dictionary<string, InventoryBomFinishedDto>(StringComparer.Ordinal);
            foreach (var row in snapshot.FINISHED)
            {
                if (string.IsNullOrWhiteSpace(row.CLIENT_KEY) || row.CLIENT_KEY.Length > 100
                    || row.FINISHED_PRODUCT_ID <= 0 || row.FINISHED_QUANTITY <= 0
                    || !finishedByKey.TryAdd(row.CLIENT_KEY, row))
                    throw new ArgumentException("Invalid or duplicate BOM finished row.");
            }
            var outputByKey = new Dictionary<string, InventoryOutput>(StringComparer.Ordinal);
            var outputById = outputs.Where(o => o.OUTPUT_ID > 0)
                .ToDictionary(o => o.OUTPUT_ID!.Value);
            foreach (var output in outputs)
            {
                if (!string.IsNullOrWhiteSpace(output.CLIENT_ROW_KEY)
                    && !outputByKey.TryAdd(output.CLIENT_ROW_KEY, output))
                    throw new ArgumentException("Duplicate IO CLIENT_ROW_KEY.");
            }
            var materialKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in snapshot.MATERIALS)
            {
                if (string.IsNullOrWhiteSpace(row.CLIENT_KEY) || row.CLIENT_KEY.Length > 100
                    || !materialKeys.Add(row.CLIENT_KEY)
                    || !finishedByKey.ContainsKey(row.FINISHED_CLIENT_KEY)
                    || row.MATERIAL_PRODUCT_ID <= 0 || row.BOM_QUANTITY < 0
                    || row.CALCULATED_QUANTITY < 0 || row.CONFIRMED_QUANTITY < 0)
                    throw new ArgumentException("Invalid BOM material row.");

                InventoryOutput? output = null;
                if (!string.IsNullOrWhiteSpace(row.OUTPUT_CLIENT_KEY))
                    outputByKey.TryGetValue(row.OUTPUT_CLIENT_KEY, out output);
                if (output == null && row.OUTPUT_ID > 0)
                    outputById.TryGetValue(row.OUTPUT_ID.Value, out output);
                if (row.CONFIRMED_QUANTITY > 0 && output == null)
                    throw new ArgumentException("BOM material has no matching saved IO output line.");
                if (output != null && output.PRODUCT_ID != row.MATERIAL_PRODUCT_ID)
                    throw new ArgumentException("BOM material does not match the linked IO product.");
                if (output != null && row.STORE_ID > 0 && output.STORE_ID != row.STORE_ID)
                    throw new ArgumentException("BOM warehouse does not match the linked IO output.");
            }

            var productIds = snapshot.FINISHED.Select(x => x.FINISHED_PRODUCT_ID)
                .Concat(snapshot.MATERIALS.Select(x => x.MATERIAL_PRODUCT_ID)).Distinct().ToArray();
            if (productIds.Length > 0)
            {
                var validIds = (await session.QueryAsync<long>(
                    "CALL sp_inventory_bom_products_validate(@p_COMPANY_CD, @p_PRODUCT_IDS)",
                    new { p_COMPANY_CD = companyCd, p_PRODUCT_IDS = JoinProductIds(productIds) })).ToHashSet();
                if (productIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("BOM product does not belong to the current company.");
            }

            await session.ExecuteAsync(
                "CALL sp_inventory_bom_soft_delete(@p_COMPANY_CD, @p_INVENTORY_ID, @p_UPDATE_BY)",
                new { p_COMPANY_CD = companyCd, p_INVENTORY_ID = inventoryId, p_UPDATE_BY = userId });

            var savedFinishedIds = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var row in snapshot.FINISHED)
            {
                var id = await session.QuerySingleAsync<BomInsertIdentity>(
                    @"CALL sp_inventory_bom_finished_insert(
                        @p_COMPANY_CD, @p_INVENTORY_ID, @p_FINISHED_PRODUCT_ID,
                        @p_FINISHED_QUANTITY, @p_SORT, @p_USER_ID)",
                    new
                    {
                        p_COMPANY_CD = companyCd,
                        p_INVENTORY_ID = inventoryId,
                        p_FINISHED_PRODUCT_ID = row.FINISHED_PRODUCT_ID,
                        p_FINISHED_QUANTITY = row.FINISHED_QUANTITY,
                        p_SORT = row.SORT,
                        p_USER_ID = userId
                    });
                savedFinishedIds.Add(row.CLIENT_KEY, id.ID);
            }
            foreach (var row in snapshot.MATERIALS)
            {
                InventoryOutput? output = null;
                if (!string.IsNullOrWhiteSpace(row.OUTPUT_CLIENT_KEY))
                    outputByKey.TryGetValue(row.OUTPUT_CLIENT_KEY, out output);
                if (output == null && row.OUTPUT_ID > 0)
                    outputById.TryGetValue(row.OUTPUT_ID.Value, out output);
                await session.ExecuteAsync(
                    @"CALL sp_inventory_bom_material_insert(
                        @p_COMPANY_CD, @p_INVENTORY_ID, @p_FINISHED_LINE_ID, @p_MATERIAL_PRODUCT_ID, @p_STORE_ID,
                        @p_BOM_QUANTITY, @p_CALCULATED_QUANTITY, @p_CONFIRMED_QUANTITY, @p_OUTPUT_ID, @p_SORT, @p_USER_ID)",
                    new
                    {
                        p_COMPANY_CD = companyCd,
                        p_INVENTORY_ID = inventoryId,
                        p_FINISHED_LINE_ID = savedFinishedIds[row.FINISHED_CLIENT_KEY],
                        p_MATERIAL_PRODUCT_ID = row.MATERIAL_PRODUCT_ID,
                        p_STORE_ID = output?.STORE_ID ?? row.STORE_ID,
                        p_BOM_QUANTITY = row.BOM_QUANTITY,
                        p_CALCULATED_QUANTITY = row.CALCULATED_QUANTITY,
                        p_CONFIRMED_QUANTITY = row.CONFIRMED_QUANTITY,
                        p_OUTPUT_ID = output?.OUTPUT_ID,
                        p_SORT = row.SORT,
                        p_USER_ID = userId
                    });
            }
        }
    }
}
