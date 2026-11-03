# Nghiệm thu Giai đoạn 2C-05 — REOPEN / RECALCULATE / HISTORY

Ngày: 2026-11-03  
**Không** SQL/SOURCE · **Không** EnableWrites · **Không** apply kho · **Không** post 632 · **Không** sửa B-I · **Không** CONFIRMED toàn kỳ tự động.

---

## 0. Implemented vs skeleton vs pending DEV

| Hạng mục | Trạng thái |
|----------|------------|
| `GkStepOrder` B1→B6 + cascade ngược B6→target | **Implemented** + test |
| `ReopenRules` — lý do, quyền DryRun, phụ thuộc bước sau | **Implemented** + test |
| `RunVersionRules` — bảo toàn version cũ; chặn SUPERSEDED/CONFIRMED overwrite | **Implemented** + test |
| `RecalcRules` — bước ảnh hưởng; bind engine B2–B5; B5=`InventoryOutPriceEngine` | **Implemented** + test |
| `SessionCompare` — WIP / INPUT_ID / khóa nhóm Kho | **Implemented** + test |
| `HistoryPlan` — STABLE_KEY idempotent (không ghi trùng retry) | **Implemented** + test |
| API Preview reopen / recalc / compare | **DryRun** |
| API Confirm reopen | **Skeleton + WriteGate OFF** |
| Persist history / bump version DB | **Pending DEV** |
| Recalc thực thi engine từ DB | **Pending DEV** |

---

## 1. Nghiệp vụ đã khóa

1. Mở lại: cascade ngược các bước sau còn DONE; bắt buộc lý do; không đổi B-I.
2. Sau REOPEN: `PreservedRunVersion` giữ snapshot; `NewWorkingRunVersion = +1`; không DELETE/overwrite UNIQUE version cũ.
3. Tính lại: chặn nếu bước sau còn DONE; Preview không đụng KT/kho; B5 dùng engine Kho chung (4 PP).
4. So sánh: CONFIRMED vs PREVIEW/REOPENED — cũ/mới/Δ; dòng chỉ một phía.
5. History plan: COMPANY/RUN/VERSION/bước/hành động/trước-sau/lý do; retry cùng STABLE_KEY → Noop.

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
→ Passed: 84, Failed: 0
TEST_EXIT=0
```

Gồm: bảo toàn version cũ · mở lại sai thứ tự bị chặn · RUN_VERSION/SUPERSEDED · history retry · recalc phụ thuộc · so sánh trước/sau · Confirm WriteGate.

---

## 3. Dừng

2C-05 xong để nghiệm thu trước giai đoạn tiếp theo.
