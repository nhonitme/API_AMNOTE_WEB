namespace API_AMNOTE_WEB.Services
{
    using System.Globalization;
    public readonly record struct UsefulLifeDuration(int FullMonths, int ExtraDays);

    public readonly record struct FixedAssetDepreciationCalculationResult(
        string DepreStartYm,
        string DepreEndYm,
        decimal UsefulLifeMonth,
        int NormalMonthCount,
        decimal RemainDepreAmt,
        decimal FirstDepreAmt,
        decimal NormalDepreAmt,
        decimal LastDepreAmt);

    public static class FixedAssetDepreciationCalculator
    {
        /// <summary>
        /// DateTime.AddMonths only accepts +/-120000. Cap useful life well below that.
        /// 12000 months ≈ 1000 years — enough for fixed assets, safe for date math.
        /// </summary>
        public const int MaxUsefulLifeFullMonths = 12000;

        public static UsefulLifeDuration ParseUsefulLife(decimal usefulLifeMonth)
        {
            if (usefulLifeMonth <= 0)
            {
                throw new InvalidOperationException("Tổng số tháng khấu hao phải lớn hơn 0.");
            }

            if (usefulLifeMonth > MaxUsefulLifeFullMonths)
            {
                throw new InvalidOperationException(
                    $"Tổng số tháng khấu hao không được lớn hơn {MaxUsefulLifeFullMonths}.");
            }

            var fullMonths = (int)Math.Floor(usefulLifeMonth);
            var fraction = usefulLifeMonth - fullMonths;
            var extraDays = fraction == 0
                ? 0
                : (int)Math.Round(fraction * 100m, MidpointRounding.AwayFromZero);

            if (extraDays > 31)
            {
                throw new InvalidOperationException("Số ngày lẻ trong tổng tháng khấu hao không được lớn hơn 31.");
            }

            if (fullMonths == 0 && extraDays == 0)
            {
                throw new InvalidOperationException("Tổng số tháng khấu hao phải lớn hơn 0.");
            }

            if (fullMonths > MaxUsefulLifeFullMonths)
            {
                throw new InvalidOperationException(
                    $"Tổng số tháng khấu hao không được lớn hơn {MaxUsefulLifeFullMonths}.");
            }

            return new UsefulLifeDuration(fullMonths, extraDays);
        }

        public static DateTime ParseYmdCompact(string? ymd)
        {
            if (string.IsNullOrWhiteSpace(ymd))
            {
                throw new InvalidOperationException("Vui lòng nhập ngày bắt đầu sử dụng.");
            }

            var trimmed = ymd.Trim();
            if (trimmed.Length == 8 && trimmed.All(char.IsDigit))
            {
                var year = int.Parse(trimmed[..4], CultureInfo.InvariantCulture);
                var month = int.Parse(trimmed[4..6], CultureInfo.InvariantCulture);
                var day = int.Parse(trimmed[6..8], CultureInfo.InvariantCulture);
                return new DateTime(year, month, day);
            }

            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                || DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
            }

            throw new InvalidOperationException("Ngày bắt đầu sử dụng phải có định dạng yyyyMMdd.");
        }

        public static FixedAssetDepreciationCalculationResult Calculate(
            string useStartYmd,
            decimal usefulLifeMonth,
            decimal originalAmt,
            decimal accumDepreAmt)
        {
            return Calculate(
                ParseYmdCompact(useStartYmd),
                usefulLifeMonth,
                originalAmt,
                accumDepreAmt);
        }

        public static FixedAssetDepreciationCalculationResult Calculate(
            DateTime useStartYmd,
            decimal usefulLifeMonth,
            decimal originalAmt,
            decimal accumDepreAmt)
        {
            if (originalAmt < 0)
            {
                throw new InvalidOperationException("Nguyên giá không được nhỏ hơn 0.");
            }

            if (accumDepreAmt < 0 || accumDepreAmt > originalAmt)
            {
                throw new InvalidOperationException("Hao mòn lũy kế phải từ 0 đến nguyên giá.");
            }

            var duration = ParseUsefulLife(usefulLifeMonth);
            var depStart = useStartYmd.Date;

            DateTime depEnd;
            try
            {
                depEnd = depStart
                    .AddMonths(duration.FullMonths)
                    .AddDays(duration.ExtraDays)
                    .AddDays(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidOperationException(
                    $"Tổng số tháng khấu hao quá lớn so với ngày bắt đầu sử dụng (tối đa {MaxUsefulLifeFullMonths} tháng).");
            }

            if (depEnd < depStart)
            {
                throw new InvalidOperationException("Thời gian khấu hao không hợp lệ.");
            }

            var depreciableBase = originalAmt - accumDepreAmt;

            // Quy ước AMNote: 1 tháng = 30 ngày. Không chia theo tổng số ngày lịch thực tế.
            // NormalMonthCount = số tháng lịch đầy đủ nằm giữa kỳ đầu và kỳ cuối.
            var normalMonthCount = CountNormalMonths(depStart, depEnd);
            var normalDepreAmt = RoundMoney(depreciableBase / usefulLifeMonth);
            var dailyDepreAmt = normalDepreAmt / 30m;

            var firstDays = GetFirstPeriodDays(depStart, depEnd);
            var firstDepreAmt = RoundMoney(dailyDepreAmt * firstDays);

            // Kỳ cuối nhận toàn bộ phần còn lại để First + Normal×Count + Last = Remain.
            var lastDepreAmt = depreciableBase - firstDepreAmt - normalDepreAmt * normalMonthCount;

            if (firstDepreAmt < 0 || normalDepreAmt < 0 || lastDepreAmt < 0)
            {
                throw new InvalidOperationException("Không thể tính khấu hao với tham số hiện tại.");
            }

            return new FixedAssetDepreciationCalculationResult(
                DepreStartYm: depStart.ToString("yyyyMM"),
                DepreEndYm: depEnd.ToString("yyyyMM"),
                UsefulLifeMonth: usefulLifeMonth,
                NormalMonthCount: normalMonthCount,
                RemainDepreAmt: depreciableBase,
                FirstDepreAmt: firstDepreAmt,
                NormalDepreAmt: normalDepreAmt,
                LastDepreAmt: lastDepreAmt);
        }

        /// <summary>
        /// Đếm số tháng lịch đầy đủ (inclusive) từ tháng sau <paramref name="depStart"/>
        /// đến tháng trước <paramref name="depEnd"/>. Trả về 0 khi không còn khoảng giữa
        /// (cùng tháng, hoặc hai tháng liền kề chỉ có kỳ đầu/kỳ cuối).
        /// </summary>
        private static int CountNormalMonths(DateTime depStart, DateTime depEnd)
        {
            if (depStart.Year == depEnd.Year && depStart.Month == depEnd.Month)
            {
                return 0;
            }

            var firstNormalMonth = new DateTime(depStart.Year, depStart.Month, 1).AddMonths(1);
            var lastNormalMonth = new DateTime(depEnd.Year, depEnd.Month, 1).AddMonths(-1);

            if (firstNormalMonth > lastNormalMonth)
            {
                return 0;
            }

            return (lastNormalMonth.Year - firstNormalMonth.Year) * 12
                + (lastNormalMonth.Month - firstNormalMonth.Month)
                + 1;
        }

        /// <summary>
        /// Số ngày kỳ đầu theo quy ước AMNote (1 tháng = 30 ngày).
        /// - Bắt đầu ngày 1 và kỳ đầu là tháng đầy đủ → 30 ngày (không lấy 28/29/31 theo lịch).
        /// - Bắt đầu giữa tháng → số ngày thực inclusive từ depStart đến cuối tháng lịch đó
        ///   (hoặc đến depEnd nếu bắt đầu và kết thúc cùng tháng).
        /// </summary>
        private static int GetFirstPeriodDays(DateTime depStart, DateTime depEnd)
        {
            var sameMonth = depStart.Year == depEnd.Year && depStart.Month == depEnd.Month;
            var lastDayOfStartMonth = DateTime.DaysInMonth(depStart.Year, depStart.Month);

            if (depStart.Day == 1)
            {
                // Tháng đầu đầy đủ theo AMNote (vd. 01/09 → tính 30 ngày, không lấy 31/28/29).
                if (!sameMonth || depEnd.Day == lastDayOfStartMonth)
                {
                    return 30;
                }

                // Trường hợp hiếm: bắt đầu ngày 1 nhưng kết thúc giữa cùng tháng.
                return depEnd.Day;
            }

            if (sameMonth)
            {
                return (depEnd - depStart).Days + 1;
            }

            return lastDayOfStartMonth - depStart.Day + 1;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }
    }
}
