using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services
{
    public class FixedAssetDepreciationService : IFixedAssetDepreciationService
    {
        public FixedAssetDepreciationPreviewResponse Preview(FixedAssetDepreciationPreviewRequest request)
        {
            if (request is null)
            {
                throw new InvalidOperationException("Thiếu thông tin tính khấu hao.");
            }

            if (string.IsNullOrWhiteSpace(request.USE_START_YMD))
            {
                throw new InvalidOperationException("Vui lòng nhập ngày bắt đầu sử dụng.");
            }

            var result = FixedAssetDepreciationCalculator.Calculate(
                request.USE_START_YMD,
                request.USEFUL_LIFE_MONTH,
                request.ORIGINAL_AMT,
                request.ACCUM_DEPRE_AMT);

            return new FixedAssetDepreciationPreviewResponse
            {
                DEPRE_START_YM = result.DepreStartYm,
                DEPRE_END_YM = result.DepreEndYm,
                USEFUL_LIFE_MONTH = result.UsefulLifeMonth,
                NORMAL_MONTH_COUNT = result.NormalMonthCount,
                REMAIN_DEPRE_AMT = result.RemainDepreAmt,
                FIRST_DEPRE_AMT = result.FirstDepreAmt,
                NORMAL_DEPRE_AMT = result.NormalDepreAmt,
                LAST_DEPRE_AMT = result.LastDepreAmt,
            };
        }
    }
}
