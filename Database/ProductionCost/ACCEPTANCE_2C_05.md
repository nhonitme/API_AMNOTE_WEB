# Nghiệm thu Giai đoạn 2C-05 — REOPEN / RECALCULATE / HISTORY (final safety)

Ngày: 2026-11-03  
**Không** SQL · **Không** EnableWrites · **Không** apply kho · **Không** post 632 · **Không** đổi engine Kho / B-I.

---

## 0. Implemented vs skeleton vs pending DEV

| Hạng mục | Trạng thái |
|----------|------------|
| REOPEN: lần đầu (+1 version) / đã REOPENED (giữ version) / retry OperationId | **Implemented** |
| SUPERSEDED chỉ khi `CreateNewWorkingVersion` | **Implemented** |
| History `OperationId` + STABLE_KEY; retry Noop; thao tác mới cùng version ghi riêng | **Implemented** |
| Confirm không tin client history hint (`ClientHistoryHintIsNotAuthoritative`) | **Implemented** (plan) |
| B2 vs `AllowsPoolRecalc`; B3/B4/B5 vẫn chạy khi ALLOC_CONFIRMED | **Implemented** |
| Đồng bộ RUN_ID/VERSION request ↔ DryRun payload | **Implemented** |
| API Preview reopen/recalc/compare | **DryRun** |
| Confirm reopen DB | **Skeleton + WriteGate OFF** |
| Persist history / bump version | **Pending DEV** |

---

## 1. Quy tắc version (final)

| Tình huống | Version | SUPERSEDED version cũ |
|------------|---------|------------------------|
| REOPEN lần đầu từ CONFIRMED / ALLOC_CONFIRMED | `current + 1` | Có (chuyển hợp lệ) |
| Đã REOPENED — mở thêm bước / cascade trong phiên | Giữ `current` | Không |
| Retry cùng `OperationId` | Giữ `current` | Không |
| Snapshot CONFIRMED cũ | Bảo toàn — không DELETE/overwrite | — |

---

## 2. Kiểm thử (báo cáo tách)

### Host build

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c05-check
→ 0 Error(s)
HOST_EXIT=0
```

### Unit test

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 85, Failed: 0
TEST_EXIT=0
```

Gồm: reopen liên tiếp giữ version · retry OperationId · recalculate nhiều lần cùng version · ALLOC_CONFIRMED chặn B2 · request sai version · Confirm WriteGate.


---

## 3. Dừng

Final lifecycle safety 2C-05 xong — chờ nghiệm thu trước giai đoạn tiếp theo.
