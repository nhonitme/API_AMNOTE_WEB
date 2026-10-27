using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed partial class InventoryVoucherRepository
    {
        private sealed class BomInsertIdentity { public long ID { get; set; } }

        public async Task<InventoryBomSnapshotDto> GetInventoryBomSnapshotAsync(string companyCd, long inventoryId)
        {
            // Only IO vouchers of this company may have this snapshot.
            var finished = (await _db.QueryAsync<InventoryBomFinishedDto>(
                Net_DB.Net_DB_Company,
                @"SELECT f.FINISHED_LINE_ID, f.FINISHED_PRODUCT_ID, f.FINISHED_QUANTITY, f.SORT,
                         p.PRODUCT_CD, p.PRODUCT_NM_VIET, u.UNIT_CD
                  FROM chit_inventory_bom_finished f
                  INNER JOIN chit_inventory_info v ON v.TRANSFER_ID = f.INVENTORY_ID
                    AND v.COMPANY_CD = f.COMPANY_CD AND v.CHIT_TYPE = 'IO'
                  LEFT JOIN product_info p ON p.PRODUCT_ID = f.FINISHED_PRODUCT_ID
                    AND p.COMPANY_CD = f.COMPANY_CD
                  LEFT JOIN product_unit u ON u.UNIT_ID = p.UNIT_ID AND u.COMPANY_CD = f.COMPANY_CD
                  WHERE f.COMPANY_CD = @companyCd AND f.INVENTORY_ID = @inventoryId AND f.ISDEL = '0'
                  ORDER BY f.SORT, f.FINISHED_LINE_ID",
                new { companyCd, inventoryId })).ToList();
            foreach (var row in finished)
                row.CLIENT_KEY = "FIN-" + row.FINISHED_LINE_ID;

            var materials = (await _db.QueryAsync<InventoryBomMaterialDto>(
                Net_DB.Net_DB_Company,
                @"SELECT m.BOM_MATERIAL_ID, m.FINISHED_LINE_ID, m.MATERIAL_PRODUCT_ID,
                         m.STORE_ID, m.BOM_QUANTITY, m.CALCULATED_QUANTITY, m.CONFIRMED_QUANTITY,
                         m.OUTPUT_ID, m.SORT,
                         p.PRODUCT_CD AS MATERIAL_PRODUCT_CD,
                         p.PRODUCT_NM_VIET AS MATERIAL_PRODUCT_NM, u.UNIT_CD
                  FROM chit_inventory_bom_material m
                  INNER JOIN chit_inventory_bom_finished f ON f.FINISHED_LINE_ID = m.FINISHED_LINE_ID
                    AND f.COMPANY_CD = m.COMPANY_CD AND f.INVENTORY_ID = m.INVENTORY_ID AND f.ISDEL = '0'
                  LEFT JOIN product_info p ON p.PRODUCT_ID = m.MATERIAL_PRODUCT_ID
                    AND p.COMPANY_CD = m.COMPANY_CD
                  LEFT JOIN product_unit u ON u.UNIT_ID = p.UNIT_ID AND u.COMPANY_CD = m.COMPANY_CD
                  WHERE m.COMPANY_CD = @companyCd AND m.INVENTORY_ID = @inventoryId AND m.ISDEL = '0'
                  ORDER BY m.SORT, m.BOM_MATERIAL_ID",
                new { companyCd, inventoryId })).ToList();

            foreach (var row in materials)
            {
                row.CLIENT_KEY = "MAT-" + row.BOM_MATERIAL_ID;
                row.FINISHED_CLIENT_KEY = "FIN-" + row.FINISHED_LINE_ID;
            }
            return new InventoryBomSnapshotDto { FINISHED = finished, MATERIALS = materials };
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
                    @"SELECT PRODUCT_ID FROM product_info
                      WHERE COMPANY_CD = @companyCd AND IFNULL(ISDEL, '0') = '0' AND PRODUCT_ID IN @productIds",
                    new { companyCd, productIds })).ToHashSet();
                if (productIds.Any(id => !validIds.Contains(id)))
                    throw new ArgumentException("BOM product does not belong to the current company.");
            }

            await session.ExecuteAsync(
                @"UPDATE chit_inventory_bom_material SET ISDEL = '1', UPDATE_BY = @userId
                  WHERE COMPANY_CD = @companyCd AND INVENTORY_ID = @inventoryId AND ISDEL = '0'",
                new { companyCd, inventoryId, userId });
            await session.ExecuteAsync(
                @"UPDATE chit_inventory_bom_finished SET ISDEL = '1', UPDATE_BY = @userId
                  WHERE COMPANY_CD = @companyCd AND INVENTORY_ID = @inventoryId AND ISDEL = '0'",
                new { companyCd, inventoryId, userId });

            var savedFinishedIds = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var row in snapshot.FINISHED)
            {
                await session.ExecuteAsync(
                    @"INSERT INTO chit_inventory_bom_finished
                      (COMPANY_CD, INVENTORY_ID, FINISHED_PRODUCT_ID, FINISHED_QUANTITY, SORT, CREATE_BY, UPDATE_BY)
                      VALUES (@companyCd, @inventoryId, @productId, @quantity, @sort, @userId, @userId)",
                    new { companyCd, inventoryId, productId = row.FINISHED_PRODUCT_ID,
                          quantity = row.FINISHED_QUANTITY, sort = row.SORT, userId });
                var id = await session.QuerySingleAsync<BomInsertIdentity>("SELECT LAST_INSERT_ID() AS ID");
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
                    @"INSERT INTO chit_inventory_bom_material
                      (COMPANY_CD, INVENTORY_ID, FINISHED_LINE_ID, MATERIAL_PRODUCT_ID, STORE_ID,
                       BOM_QUANTITY, CALCULATED_QUANTITY, CONFIRMED_QUANTITY, OUTPUT_ID, SORT, CREATE_BY, UPDATE_BY)
                      VALUES (@companyCd, @inventoryId, @finishedId, @materialId, @storeId,
                              @bomQuantity, @calculatedQuantity, @confirmedQuantity, @outputId, @sort, @userId, @userId)",
                    new {
                        companyCd, inventoryId,
                        finishedId = savedFinishedIds[row.FINISHED_CLIENT_KEY],
                        materialId = row.MATERIAL_PRODUCT_ID,
                        storeId = output?.STORE_ID ?? row.STORE_ID,
                        bomQuantity = row.BOM_QUANTITY,
                        calculatedQuantity = row.CALCULATED_QUANTITY,
                        confirmedQuantity = row.CONFIRMED_QUANTITY,
                        outputId = output?.OUTPUT_ID,
                        sort = row.SORT,
                        userId
                    });
            }
        }
    }
}
