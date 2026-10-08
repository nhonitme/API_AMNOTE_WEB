using System.Globalization;
using API_AMNOTE_WEB.Reports;

namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// Số tiền bằng chữ. ENG / KOR có bộ đọc riêng, ngôn ngữ khác (kể cả null) rơi về tiếng Việt.
    /// E-invoice không truyền reportLanguage nên TgTTTBChu luôn là tiếng Việt.
    /// </summary>
    public static class ReportCurrencyHelper
    {
        private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

        private static readonly string[] DigitWords =
        {
            "không",
            "một",
            "hai",
            "ba",
            "bốn",
            "năm",
            "sáu",
            "bảy",
            "tám",
            "chín"
        };

        private static readonly string[] EnglishDigitWords =
        {
            "zero",
            "one",
            "two",
            "three",
            "four",
            "five",
            "six",
            "seven",
            "eight",
            "nine"
        };

        private static readonly string[] EnglishUnderTwentyWords =
        {
            "zero",
            "one",
            "two",
            "three",
            "four",
            "five",
            "six",
            "seven",
            "eight",
            "nine",
            "ten",
            "eleven",
            "twelve",
            "thirteen",
            "fourteen",
            "fifteen",
            "sixteen",
            "seventeen",
            "eighteen",
            "nineteen"
        };

        private static readonly string[] EnglishTensWords =
        {
            string.Empty,
            string.Empty,
            "twenty",
            "thirty",
            "forty",
            "fifty",
            "sixty",
            "seventy",
            "eighty",
            "ninety"
        };

        private static readonly string[] EnglishScaleWords =
        {
            string.Empty,
            "thousand",
            "million",
            "billion",
            "trillion",
            "quadrillion"
        };

        private const string EnglishNegativePrefix = "Negative";
        private const string EnglishUsDollarSingular = "US dollar";
        private const string EnglishUsDollarPlural = "US dollars";
        private const string EnglishCentSingular = "cent";
        private const string EnglishCentPlural = "cents";
        private const string EnglishGroupSeparator = " ";
        private const string EnglishConjunctionSeparator = " and ";

        private static readonly string[] KoreanDigitWords =
        {
            "영",
            "일",
            "이",
            "삼",
            "사",
            "오",
            "육",
            "칠",
            "팔",
            "구"
        };

        private static readonly string[] KoreanPositionUnitWords =
        {
            string.Empty,
            "십",
            "백",
            "천"
        };

        private static readonly string[] KoreanBigUnitWords =
        {
            string.Empty,
            "만",
            "억",
            "조",
            "경"
        };

        private const string KoreanNegativePrefix = "마이너스";
        private const string KoreanZeroWord = "영";
        private const string KoreanUsDollarUnit = "미국 달러";
        private const string KoreanCentUnit = "센트";

        public static string ConvertAmountToWords(decimal amount, string? currencyCode = null, string? reportLanguage = null)
        {
            var language = ReportLanguageHelper.NormalizeLanguage(reportLanguage);
            var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);

            if (language == "ENG")
            {
                return ConvertEnglishAmountToWords(amount, normalizedCurrencyCode);
            }

            if (language == "KOR")
            {
                return ConvertKoreanAmountToWords(amount, normalizedCurrencyCode);
            }

            return normalizedCurrencyCode switch
            {
                "USD" => ConvertUsdAmountToWords(amount),
                "" or "VND" => ConvertVietnameseDongToWords(amount),
                _ => ConvertForeignCurrencyToWords(amount, normalizedCurrencyCode)
            };
        }

        public static string FormatAmount(decimal amount, string? currencyCode = null)
        {
            var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
            var formattedAmount = amount.ToString("#,##0.00", VietnameseCulture);

            return normalizedCurrencyCode.Length == 0 || normalizedCurrencyCode == "VND"
                ? formattedAmount
                : $"{formattedAmount} {normalizedCurrencyCode}";
        }

        public static string FormatExchangeRate(string? currencyCode, decimal? exchangeRate)
        {
            var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
            if (normalizedCurrencyCode.Length == 0 ||
                normalizedCurrencyCode == "VND" ||
                !exchangeRate.HasValue ||
                exchangeRate.Value <= 0m)
            {
                return ".................................................................";
            }

            return $"{normalizedCurrencyCode} x {exchangeRate.Value.ToString("#,##0.####", VietnameseCulture)}";
        }

        private static string ConvertVietnameseDongToWords(decimal amount)
        {
            var roundedAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
            if (roundedAmount == 0m)
            {
                return "Không đồng";
            }

            var isNegative = roundedAmount < 0m;
            var absoluteAmount = Math.Abs(roundedAmount);
            if (absoluteAmount > long.MaxValue)
            {
                return $"{absoluteAmount.ToString("#,##0.00", VietnameseCulture)} đồng";
            }

            var result = $"{UppercaseFirst(ReadVietnameseIntegerAmount(decimal.ToInt64(absoluteAmount)))} đồng";
            return isNegative ? $"Âm {result}" : result;
        }

        private static string ConvertUsdAmountToWords(decimal amount)
        {
            var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            var isNegative = roundedAmount < 0m;
            var absoluteAmount = Math.Abs(roundedAmount);
            if (absoluteAmount > long.MaxValue)
            {
                return FormatAmount(absoluteAmount, "USD");
            }

            var dollarAmount = decimal.ToInt64(decimal.Truncate(absoluteAmount));
            var centAmount = decimal.ToInt32(decimal.Round((absoluteAmount - dollarAmount) * 100m, 0, MidpointRounding.AwayFromZero));
            if (centAmount == 100)
            {
                dollarAmount += 1;
                centAmount = 0;
            }

            var parts = new List<string>
            {
                $"{ReadVietnameseIntegerAmount(dollarAmount)} đô la Mỹ"
            };

            if (centAmount > 0)
            {
                parts.Add($"{ReadVietnameseIntegerAmount(centAmount)} xu");
            }

            var result = UppercaseFirst(string.Join(" và ", parts));
            return isNegative ? $"Âm {result}" : result;
        }

        private static string ConvertForeignCurrencyToWords(decimal amount, string currencyCode)
        {
            var roundedAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
            if (roundedAmount == 0m)
            {
                return $"Không {currencyCode}";
            }

            var isNegative = roundedAmount < 0m;
            var absoluteAmount = Math.Abs(roundedAmount);
            if (absoluteAmount > long.MaxValue)
            {
                return FormatAmount(absoluteAmount, currencyCode);
            }

            var result = $"{UppercaseFirst(ReadVietnameseIntegerAmount(decimal.ToInt64(absoluteAmount)))} {currencyCode}";
            return isNegative ? $"Âm {result}" : result;
        }

        private static string ReadVietnameseIntegerAmount(long amount)
        {
            if (amount == 0L)
            {
                return "không";
            }

            var groups = new List<int>();
            var remainingAmount = amount;
            while (remainingAmount > 0L)
            {
                groups.Add((int)(remainingAmount % 1000L));
                remainingAmount /= 1000L;
            }

            var unitNames = new[]
            {
                string.Empty,
                "nghìn",
                "triệu",
                "tỷ",
                "nghìn tỷ",
                "triệu tỷ"
            };

            var parts = new List<string>();
            for (var index = groups.Count - 1; index >= 0; index--)
            {
                var groupValue = groups[index];
                if (groupValue == 0)
                {
                    continue;
                }

                var readFull = index < groups.Count - 1 && groupValue < 100;
                var part = ReadThreeDigits(groupValue, readFull);
                if (unitNames[index].Length > 0)
                {
                    part = $"{part} {unitNames[index]}";
                }

                parts.Add(part);
            }

            return string.Join(" ", parts).Trim();
        }

        private static string ReadThreeDigits(int number, bool readFull)
        {
            var hundreds = number / 100;
            var tens = (number % 100) / 10;
            var units = number % 10;
            var parts = new List<string>();

            if (hundreds > 0 || readFull)
            {
                parts.Add(hundreds > 0 ? $"{DigitWords[hundreds]} trăm" : "không trăm");
            }

            if (tens > 1)
            {
                parts.Add($"{DigitWords[tens]} mươi");
                if (units == 1)
                {
                    parts.Add("mốt");
                }
                else if (units == 4)
                {
                    parts.Add("tư");
                }
                else if (units == 5)
                {
                    parts.Add("lăm");
                }
                else if (units > 0)
                {
                    parts.Add(DigitWords[units]);
                }
            }
            else if (tens == 1)
            {
                parts.Add("mười");
                if (units == 5)
                {
                    parts.Add("lăm");
                }
                else if (units > 0)
                {
                    parts.Add(DigitWords[units]);
                }
            }
            else if (units > 0)
            {
                if (hundreds > 0 || readFull)
                {
                    parts.Add("lẻ");
                }

                parts.Add(units == 5 && (hundreds > 0 || readFull) ? "năm" : DigitWords[units]);
            }

            return string.Join(" ", parts).Trim();
        }

        private static string ConvertEnglishAmountToWords(decimal amount, string currencyCode)
        {
            if (currencyCode == "USD")
            {
                return ConvertEnglishUsdAmountToWords(amount);
            }

            var roundedAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
            var unitSuffix = currencyCode.Length == 0 || currencyCode == "VND" ? "VND" : currencyCode;
            if (roundedAmount == 0m)
            {
                return $"{UppercaseFirst(EnglishUnderTwentyWords[0])} {unitSuffix}";
            }

            var isNegative = roundedAmount < 0m;
            var absoluteAmount = Math.Abs(roundedAmount);
            if (absoluteAmount > long.MaxValue)
            {
                var overflow = $"{absoluteAmount.ToString("#,##0.00", VietnameseCulture)} {unitSuffix}";
                return isNegative ? $"{EnglishNegativePrefix} {overflow}" : overflow;
            }

            var result = $"{UppercaseFirst(ReadEnglishIntegerAmount(decimal.ToInt64(absoluteAmount)))} {unitSuffix}";
            return isNegative ? $"{EnglishNegativePrefix} {result}" : result;
        }

        private static string ConvertEnglishUsdAmountToWords(decimal amount)
        {
            var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            var isNegative = roundedAmount < 0m;
            var absoluteAmount = Math.Abs(roundedAmount);
            if (absoluteAmount > long.MaxValue)
            {
                var overflow = $"{absoluteAmount.ToString("#,##0.00", VietnameseCulture)} {EnglishUsDollarPlural}";
                return isNegative ? $"{EnglishNegativePrefix} {overflow}" : overflow;
            }

            var dollarAmount = decimal.ToInt64(decimal.Truncate(absoluteAmount));
            var centAmount = decimal.ToInt32(decimal.Round((absoluteAmount - dollarAmount) * 100m, 0, MidpointRounding.AwayFromZero));
            if (centAmount == 100)
            {
                dollarAmount += 1;
                centAmount = 0;
            }

            var parts = new List<string>
            {
                $"{ReadEnglishIntegerAmount(dollarAmount)} {(dollarAmount == 1L ? EnglishUsDollarSingular : EnglishUsDollarPlural)}"
            };

            if (centAmount > 0)
            {
                parts.Add($"{ReadEnglishIntegerAmount(centAmount)} {(centAmount == 1 ? EnglishCentSingular : EnglishCentPlural)}");
            }

            var result = UppercaseFirst(string.Join(EnglishConjunctionSeparator, parts));
            return isNegative ? $"{EnglishNegativePrefix} {result}" : result;
        }

        private static string ReadEnglishIntegerAmount(long amount)
        {
            if (amount == 0L)
            {
                return EnglishUnderTwentyWords[0];
            }

            var groups = new List<int>();
            var remainingAmount = amount;
            while (remainingAmount > 0L)
            {
                groups.Add((int)(remainingAmount % 1000L));
                remainingAmount /= 1000L;
            }

            var parts = new List<string>();
            for (var index = groups.Count - 1; index >= 0; index--)
            {
                var groupValue = groups[index];
                if (groupValue == 0)
                {
                    continue;
                }

                if (index >= EnglishScaleWords.Length)
                {
                    return amount.ToString("#,##0", VietnameseCulture);
                }

                var part = ReadEnglishThreeDigits(groupValue);
                if (EnglishScaleWords[index].Length > 0)
                {
                    part = $"{part} {EnglishScaleWords[index]}";
                }

                parts.Add(part);
            }

            return string.Join(EnglishGroupSeparator, parts).Trim();
        }

        private static string ReadEnglishThreeDigits(int number)
        {
            var hundreds = number / 100;
            var remainder = number % 100;
            var parts = new List<string>();

            if (hundreds > 0)
            {
                parts.Add($"{EnglishDigitWords[hundreds]} hundred");
            }

            if (remainder > 0)
            {
                if (hundreds > 0)
                {
                    parts.Add("and");
                }

                if (remainder < 20)
                {
                    parts.Add(EnglishUnderTwentyWords[remainder]);
                }
                else
                {
                    var tens = remainder / 10;
                    var units = remainder % 10;
                    parts.Add(units > 0
                        ? $"{EnglishTensWords[tens]}-{EnglishDigitWords[units]}"
                        : EnglishTensWords[tens]);
                }
            }

            return string.Join(EnglishGroupSeparator, parts).Trim();
        }

        private static string ConvertKoreanAmountToWords(decimal amount, string currencyCode)
        {
            if (currencyCode == "USD")
            {
                return ConvertKoreanUsdAmountToWords(amount);
            }

            var roundedAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
            var unitSuffix = currencyCode.Length == 0 || currencyCode == "VND" ? "VND" : currencyCode;
            if (roundedAmount == 0m)
            {
                return $"{KoreanZeroWord} {unitSuffix}";
            }

            var isNegative = roundedAmount < 0m;
            var absoluteAmount = Math.Abs(roundedAmount);
            if (absoluteAmount > long.MaxValue)
            {
                var overflow = $"{absoluteAmount.ToString("#,##0.00", VietnameseCulture)} {unitSuffix}";
                return isNegative ? $"{KoreanNegativePrefix} {overflow}" : overflow;
            }

            var result = $"{ReadKoreanIntegerAmount(decimal.ToInt64(absoluteAmount))} {unitSuffix}";
            return isNegative ? $"{KoreanNegativePrefix} {result}" : result;
        }

        private static string ConvertKoreanUsdAmountToWords(decimal amount)
        {
            var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            var isNegative = roundedAmount < 0m;
            var absoluteAmount = Math.Abs(roundedAmount);
            if (absoluteAmount > long.MaxValue)
            {
                var overflow = $"{absoluteAmount.ToString("#,##0.00", VietnameseCulture)} {KoreanUsDollarUnit}";
                return isNegative ? $"{KoreanNegativePrefix} {overflow}" : overflow;
            }

            var dollarAmount = decimal.ToInt64(decimal.Truncate(absoluteAmount));
            var centAmount = decimal.ToInt32(decimal.Round((absoluteAmount - dollarAmount) * 100m, 0, MidpointRounding.AwayFromZero));
            if (centAmount == 100)
            {
                dollarAmount += 1;
                centAmount = 0;
            }

            var parts = new List<string>
            {
                $"{ReadKoreanIntegerAmount(dollarAmount)} {KoreanUsDollarUnit}"
            };

            if (centAmount > 0)
            {
                parts.Add($"{ReadKoreanIntegerAmount(centAmount)} {KoreanCentUnit}");
            }

            var result = string.Join(" ", parts);
            return isNegative ? $"{KoreanNegativePrefix} {result}" : result;
        }

        private static string ReadKoreanIntegerAmount(long amount)
        {
            if (amount == 0L)
            {
                return KoreanZeroWord;
            }

            var groups = new List<int>();
            var remainingAmount = amount;
            while (remainingAmount > 0L)
            {
                groups.Add((int)(remainingAmount % 10000L));
                remainingAmount /= 10000L;
            }

            var parts = new List<string>();
            for (var index = groups.Count - 1; index >= 0; index--)
            {
                var groupValue = groups[index];
                if (groupValue == 0)
                {
                    continue;
                }

                if (index >= KoreanBigUnitWords.Length)
                {
                    return amount.ToString("#,##0", VietnameseCulture);
                }

                var part = ReadKoreanFourDigitGroup(groupValue);
                if (KoreanBigUnitWords[index].Length > 0)
                {
                    part = $"{part}{KoreanBigUnitWords[index]}";
                }

                parts.Add(part);
            }

            return string.Join(string.Empty, parts);
        }

        private static string ReadKoreanFourDigitGroup(int number)
        {
            var parts = new List<string>();

            for (var position = 3; position >= 0; position--)
            {
                var digit = number / Pow10(position) % 10;
                if (digit == 0)
                {
                    continue;
                }

                if (position == 0)
                {
                    parts.Add(KoreanDigitWords[digit]);
                    continue;
                }

                // 십/백/천 bỏ chữ "일" đứng trước: 15 -> 십오, 100 -> 백, 1000 -> 천.
                if (digit == 1)
                {
                    parts.Add(KoreanPositionUnitWords[position]);
                }
                else
                {
                    parts.Add($"{KoreanDigitWords[digit]}{KoreanPositionUnitWords[position]}");
                }
            }

            return string.Concat(parts);
        }

        private static int Pow10(int exponent)
        {
            return exponent switch
            {
                0 => 1,
                1 => 10,
                2 => 100,
                _ => 1000
            };
        }

        private static string NormalizeCurrencyCode(string? currencyCode)
        {
            return Common.NormalizeToken(currencyCode).ToUpperInvariant();
        }

        private static string UppercaseFirst(string value)
        {
            return value.Length == 0
                ? string.Empty
                : char.ToUpperInvariant(value[0]) + value[1..];
        }
    }
}