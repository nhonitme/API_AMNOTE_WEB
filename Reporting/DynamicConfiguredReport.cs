using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reports;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using Microsoft.AspNetCore.Hosting;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;

namespace API_AMNOTE_WEB.Reporting
{
    public sealed class DynamicConfiguredReport : XtraReport
    {
        private const string DefaultFontFamily = "Arial";
        private const float DefaultBaseFontSize = 8.5f;
        private const float DefaultTitleFontSize = 16f;
        private const float DefaultInfoFontSize = 9f;
        private const float DefaultHeaderFontSize = 8.5f;
        private const float DefaultDetailFontSize = 8.5f;
        private const float DefaultFooterFontSize = 8f;
        private const int DefaultMarginLeft = 40;
        private const int DefaultMarginRight = 40;
        private const int DefaultMarginTop = 25;
        private const int DefaultMarginBottom = 25;
        private const float DefaultHeaderHeight = 28f;
        private const float DefaultDetailHeight = 24f;

        private readonly IReadOnlyList<ReportColumnDefinition> _columns;
        private readonly ReportTableLayout? _tableLayout;
        private readonly ReportLayoutSettings _layout;
        private readonly string _reportLanguage;
        private readonly string _reportCode;
        private readonly bool _showDetailTable;
        private readonly bool _useEInvoiceSalesAppendixStyle;
        private readonly bool _hasBalanceSheetPeriodCaptions;
        private readonly IWebHostEnvironment? _environment;
        private readonly IReadOnlyDictionary<string, object?> _namedValues;
        private TopMarginBand topMarginBand1;
        private DetailBand detailBand1;
        private BottomMarginBand bottomMarginBand1;
        private readonly IReadOnlyDictionary<string, object?> _normalizedValues;

        public DynamicConfiguredReport(
            ReportConfigurationInfo configuration,
            DataTable table,
            ReportCompanyContext companyContext,
            string? reportLanguage,
            IWebHostEnvironment? environment = null,
            IReadOnlyDictionary<string, object?>? namedValues = null,
            IReadOnlyDictionary<string, object?>? normalizedValues = null)
        {
            _environment = environment;
            _reportLanguage = ReportLanguageHelper.NormalizeLanguage(reportLanguage);
            _reportCode = Common.NormalizeNullableText(configuration.REPORT_CODE) ?? string.Empty;
            _namedValues = namedValues ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            _normalizedValues = normalizedValues ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            var sourceTable = EnsureRenderableTable(table, configuration.REPORT_CODE, _reportLanguage);
            var tableLayout = ResolveTableLayout(sourceTable, _reportLanguage);
            var columns = tableLayout?.DetailColumns ?? BuildColumns(sourceTable, _reportLanguage);
            (_tableLayout, _columns, _hasBalanceSheetPeriodCaptions) = ApplyBalanceSheetPeriodCaptions(tableLayout, columns);
            _showDetailTable = _tableLayout?.DetailColumns.Count > 0;
            _useEInvoiceSalesAppendixStyle = IsEInvoiceSalesAppendixReport(configuration)
                && HasColumn(sourceTable, "__ROW_TYPE")
                && HasColumn(sourceTable, "SUMMARY_LABEL");
            _layout = ResolveLayout(configuration, _columns.Count);

            DataSource = sourceTable;
            DataMember = sourceTable.TableName;
            DisplayName = ResolveReportTitle(configuration);

            Font = CreateFont(_layout.FontFamily, _layout.BaseFontSize);
            PaperKind = _layout.PaperKind;
            Landscape = _layout.Landscape;
            Margins = new Margins(_layout.MarginLeft, _layout.MarginRight, _layout.MarginTop, _layout.MarginBottom);
            RequestParameters = false;
            BuildElementConfiguredReport(configuration, companyContext, sourceTable);
        }

        private void BuildElementConfiguredReport(
            ReportConfigurationInfo configuration,
            ReportCompanyContext companyContext,
            DataTable table)
        {
            var reportHeaderElements = GetReportElements(configuration, "REPORT_HEADER");
            var pageHeaderElements = GetReportElements(configuration, "PAGE_HEADER");
            var detailElements = GetReportElements(configuration, "DETAIL");
            var reportFooterElements = GetReportElements(configuration, "REPORT_FOOTER");
            var pageFooterElements = GetReportElements(configuration, "PAGE_FOOTER");

            var tableGroupField = ResolveTableGroupField(table);
            var useGroupedChitPages = _showDetailTable
                && tableGroupField != null
                && (reportHeaderElements.Count > 0 || pageHeaderElements.Count > 0 || reportFooterElements.Count > 0 || HasSignatures(companyContext));
            var useDocumentDetailPages = !_showDetailTable && detailElements.Count > 0;

            if (useGroupedChitPages)
            {
                var groupedBands = new List<Band>
                {
                    BuildTopMarginBand(),
                    BuildElementTableGroupHeaderBand(reportHeaderElements, pageHeaderElements, detailElements, companyContext, table, tableGroupField!),
                    BuildConfiguredTableDetailBand(),
                    BuildElementTableGroupFooterBand(reportFooterElements, companyContext, table),
                    BuildBottomMarginBand()
                };

                if (pageFooterElements.Count > 0)
                {
                    groupedBands.Insert(groupedBands.Count - 1, BuildElementPageFooterBand(pageFooterElements, companyContext, table));
                }

                Bands.AddRange(groupedBands.ToArray());
                return;
            }

            if (useDocumentDetailPages)
            {
                Bands.AddRange(new Band[]
                {
                    BuildTopMarginBand(),
                    BuildElementDocumentDetailBand(reportHeaderElements, pageHeaderElements, detailElements, reportFooterElements, companyContext, table),
                    BuildBottomMarginBand()
                });
                return;
            }

            var bands = new List<Band>
            {
                BuildTopMarginBand()
            };

            if (reportHeaderElements.Count > 0)
            {
                bands.Add(BuildElementReportHeaderBand(reportHeaderElements, companyContext, table));
            }

            if (pageHeaderElements.Count > 0)
            {
                bands.Add(BuildElementPageHeaderBand(pageHeaderElements, companyContext, table));
            }

            if (_showDetailTable)
            {
                bands.Add(BuildConfiguredTableHeaderBand());
                bands.Add(BuildConfiguredTableDetailBand(detailElements, companyContext, table));
            }

            if (reportFooterElements.Count > 0 || HasSignatures(companyContext))
            {
                bands.Add(BuildElementReportFooterBand(reportFooterElements, companyContext, table));
            }

            if (pageFooterElements.Count > 0)
            {
                bands.Add(BuildElementPageFooterBand(pageFooterElements, companyContext, table));
            }

            bands.Add(BuildBottomMarginBand());
            Bands.AddRange(bands.ToArray());
        }

        private ReportHeaderBand BuildElementReportHeaderBand(
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table)
        {
            var band = new ReportHeaderBand();
            var height = AddElementBlock(band.Controls, elements, companyContext, table, 0f, false);
            band.HeightF = Math.Max(1f, height);
            return band;
        }

        private PageHeaderBand BuildElementPageHeaderBand(
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table)
        {
            var band = new PageHeaderBand();
            var height = AddElementBlock(band.Controls, elements, companyContext, table, 0f, true);
            band.HeightF = Math.Max(1f, height);
            return band;
        }

        private ReportFooterBand BuildElementReportFooterBand(
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table)
        {
            var band = new ReportFooterBand
            {
                KeepTogether = true
            };
            var top = AddReportFooterBlock(band.Controls, elements, companyContext, table, 0f);
            band.HeightF = Math.Max(1f, top);
            return band;
        }

        private PageFooterBand BuildElementPageFooterBand(
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table)
        {
            var band = new PageFooterBand();
            var height = AddElementBlock(band.Controls, elements, companyContext, table, 0f, false);
            band.HeightF = Math.Max(1f, height);
            return band;
        }

        private DetailBand BuildElementDocumentDetailBand(
            IReadOnlyList<CompanyReportElementInfo> reportHeaderElements,
            IReadOnlyList<CompanyReportElementInfo> pageHeaderElements,
            IReadOnlyList<CompanyReportElementInfo> detailElements,
            IReadOnlyList<CompanyReportElementInfo> reportFooterElements,
            ReportCompanyContext companyContext,
            DataTable table)
        {
            var band = new DetailBand
            {
                KeepTogether = true,
                PageBreak = PageBreak.AfterBandExceptLastEntry
            };
            var top = 0f;
            top = AddElementBlock(band.Controls, reportHeaderElements, companyContext, table, top, true);
            top = AddElementBlock(band.Controls, pageHeaderElements, companyContext, table, top, true);
            top = AddElementBlock(band.Controls, detailElements, companyContext, table, top, true);
            top = AddReportFooterBlock(band.Controls, reportFooterElements, companyContext, table, top);
            band.HeightF = Math.Max(1f, top);
            return band;
        }

        private GroupHeaderBand BuildElementTableGroupHeaderBand(
            IReadOnlyList<CompanyReportElementInfo> reportHeaderElements,
            IReadOnlyList<CompanyReportElementInfo> pageHeaderElements,
            IReadOnlyList<CompanyReportElementInfo> detailElements,
            ReportCompanyContext companyContext,
            DataTable table,
            string groupField)
        {
            var band = new GroupHeaderBand
            {
                RepeatEveryPage = true,
                KeepTogether = true,
                PageBreak = PageBreak.BeforeBandExceptFirstEntry
            };
            band.GroupFields.Add(new GroupField(groupField));
            var top = 0f;
            top = AddElementBlock(band.Controls, reportHeaderElements, companyContext, table, top, true);
            top = AddElementBlock(band.Controls, pageHeaderElements, companyContext, table, top, true);
            top = AddElementBlock(band.Controls, detailElements, companyContext, table, top, true);
            band.Controls.Add(CreateConfiguredTableHeader(top));
            band.HeightF = Math.Max(1f, top + GetConfiguredTableHeaderHeight());
            return band;
        }

        private GroupFooterBand BuildElementTableGroupFooterBand(
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table)
        {
            var band = new GroupFooterBand
            {
                KeepTogether = true,
                PageBreak = PageBreak.AfterBandExceptLastEntry
            };
            var top = AddReportFooterBlock(band.Controls, elements, companyContext, table, 0f);
            band.HeightF = Math.Max(1f, top);
            return band;
        }

        private float AddElementBlock(
            XRControlCollection controls,
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table,
            float startTop,
            bool bindDataFields)
        {
            if (elements.Count == 0)
            {
                return startTop;
            }

            return AddElementRows(controls, elements.Where(IsContentElement).ToList(), companyContext, table, startTop, bindDataFields);
        }

        private float AddReportFooterBlock(
            XRControlCollection controls,
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table,
            float startTop)
        {
            var top = AddElementBlock(controls, elements, companyContext, table, startTop, true);
            if (!HasSignatures(companyContext))
            {
                return top;
            }

            var signatureTop = top + (top > startTop ? 8f : 0f);
            controls.Add(CreateSignatureTable(companyContext.Signatures, signatureTop));
            return signatureTop + GetSignatureSectionHeight(companyContext.Signatures);
        }

        private float AddElementRows(
            XRControlCollection controls,
            IReadOnlyList<CompanyReportElementInfo> elements,
            ReportCompanyContext companyContext,
            DataTable table,
            float startTop,
            bool bindDataFields)
        {
            if (elements.Count == 0)
            {
                return startTop;
            }

            var contentWidth = GetContentWidth();
            var leftTop = startTop;
            var rightTop = startTop;
            var fullTop = startTop;
            var previousRowNo = 0;
            var spacerUnit = Math.Max(18f, _layout.InfoFontSize + 8f);

            foreach (var rowGroup in elements
                .Where(IsRenderableElement)
                .OrderBy(element => element.ROW_NO)
                .ThenBy(element => element.COL_NO)
                .ThenBy(element => element.SORT_ORDER)
                .GroupBy(element => Math.Max(1, element.ROW_NO)))
            {
                var rowNo = rowGroup.Key;
                if (previousRowNo > 0 && rowNo > previousRowNo + 1)
                {
                    var gap = (rowNo - previousRowNo - 1) * spacerUnit;
                    leftTop += gap;
                    rightTop += gap;
                    fullTop += gap;
                }

                var rowElements = rowGroup.ToList();
                var fullWidthElements = rowElements.Where(IsFullWidthElement).ToList();
                var leftElements = rowElements.Where(IsLeftAreaElement).ToList();
                var rightElements = rowElements.Where(IsRightAreaElement).ToList();
                var centerElements = rowElements.Where(IsCenterAreaElement).ToList();

                if (fullWidthElements.Count > 0 || centerElements.Count > 0)
                {
                    fullTop = Math.Max(fullTop, Math.Max(leftTop, rightTop));
                    var renderElements = fullWidthElements.Concat(centerElements);
                    if (centerElements.Count > 0)
                    {
                        // Render left/right siblings with centered elements on the same configured row.
                        renderElements = renderElements.Concat(leftElements).Concat(rightElements);
                    }

                    fullTop = AddMeasuredElementGroup(
                        controls,
                        renderElements.ToList(),
                        rowElements,
                        companyContext,
                        table,
                        contentWidth,
                        fullTop,
                        bindDataFields);
                    leftTop = fullTop;
                    rightTop = fullTop;
                }
                else
                {
                    if (leftElements.Count > 0)
                    {
                        leftTop = AddMeasuredElementGroup(
                            controls,
                            leftElements,
                            rowElements,
                            companyContext,
                            table,
                            contentWidth,
                            leftTop,
                            bindDataFields);
                    }

                    if (rightElements.Count > 0)
                    {
                        rightTop = AddMeasuredElementGroup(
                            controls,
                            rightElements,
                            rowElements,
                            companyContext,
                            table,
                            contentWidth,
                            rightTop,
                            bindDataFields);
                    }
                }

                previousRowNo = rowNo;
            }

            return Math.Max(fullTop, Math.Max(leftTop, rightTop)) + 4f;
        }

        private float AddMeasuredElementGroup(
            XRControlCollection controls,
            IReadOnlyList<CompanyReportElementInfo> elements,
            IReadOnlyList<CompanyReportElementInfo> rowElements,
            ReportCompanyContext companyContext,
            DataTable table,
            float contentWidth,
            float startTop,
            bool bindDataFields)
        {
            if (elements.Count == 0)
            {
                return startTop;
            }

            var measuredElements = elements
                .Select(element =>
                {
                    var text = ResolveElementDisplayText(element, companyContext, table, null);
                    var bounds = ResolveElementBounds(element, rowElements, contentWidth, startTop, 0f);
                    var font = CreateElementFont(element);
                    var neededWidth = MeasureGdiTextWidthHundredths(text, font, typographic: true) + 4f;
                    var width = ResolveWidthWithoutSiblingOverlap(
                        element,
                        rowElements,
                        contentWidth,
                        bounds.Left,
                        bounds.Width,
                        neededWidth);
                    var height = ResolveElementHeight(element, text, width);
                    return new
                    {
                        Element = element,
                        Text = text,
                        Font = font,
                        Left = bounds.Left,
                        Width = width,
                        Height = height
                    };
                })
                .ToList();
            var groupHeight = measuredElements
                .Select(item => item.Height)
                .DefaultIfEmpty(Math.Max(18f, _layout.InfoFontSize + 8f))
                .Max();

            foreach (var item in measuredElements)
            {
                var label = CreateLabel(
                    item.Left,
                    startTop,
                    item.Width,
                    item.Height,
                    item.Text,
                    item.Font,
                    ResolveElementAlignment(item.Element.ALIGN, item.Element.AREA_CODE));
                label.Multiline = true;
                label.WordWrap = true;
                label.CanGrow = true;
                if (bindDataFields && IsDataBoundElement(item.Element))
                {
                    label.BeforePrint += (_, _) =>
                    {
                        var currentRow = ResolveCurrentDataRow();
                        label.Text = ResolveElementDisplayText(item.Element, companyContext, table, currentRow);
                    };
                }

                controls.Add(label);
            }

            return startTop + groupHeight;
        }

        private RectangleF ResolveElementBounds(
            CompanyReportElementInfo element,
            IReadOnlyList<CompanyReportElementInfo> rowElements,
            float contentWidth,
            float top,
            float rowHeight)
        {
            var areaCode = Common.NormalizeToken(element.AREA_CODE);
            var widthPercent = element.WIDTH_PERCENT.HasValue && element.WIDTH_PERCENT.Value > 0
                ? (float)element.WIDTH_PERCENT.Value
                : 0f;
            var absoluteWidth = element.WIDTH.HasValue && element.WIDTH.Value > 0
                ? (float)element.WIDTH.Value
                : 0f;

            if ((areaCode == "full" || areaCode == "table") && rowElements.Count(item => IsFullWidthArea(item.AREA_CODE)) > 1)
            {
                var colCount = rowElements
                    .Where(item => IsFullWidthArea(item.AREA_CODE))
                    .Select(item => Math.Max(1, item.COL_NO) + Math.Max(1, item.COL_SPAN) - 1)
                    .DefaultIfEmpty(1)
                    .Max();
                var unitWidth = contentWidth / Math.Max(1, colCount);
                return new RectangleF(
                    unitWidth * (Math.Max(1, element.COL_NO) - 1),
                    top,
                    unitWidth * Math.Max(1, element.COL_SPAN),
                    rowHeight);
            }

            if (widthPercent > 0f)
            {
                var width = contentWidth * widthPercent / 100f;
                var left = areaCode switch
                {
                    "center" => (contentWidth - width) / 2f,
                    "right" => contentWidth - width,
                    _ => 0f
                };
                return new RectangleF(left, top, width, rowHeight);
            }

            if (absoluteWidth > 0f)
            {
                var width = Math.Min(contentWidth, absoluteWidth);
                var left = areaCode switch
                {
                    "center" => (contentWidth - width) / 2f,
                    "right" => contentWidth - width,
                    _ => 0f
                };
                return new RectangleF(left, top, width, rowHeight);
            }

            return areaCode switch
            {
                "center" => new RectangleF(contentWidth * 0.24f, top, contentWidth * 0.52f, rowHeight),
                "right" => new RectangleF(contentWidth * 0.58f, top, contentWidth * 0.42f, rowHeight),
                "full" or "table" => new RectangleF(0f, top, contentWidth, rowHeight),
                _ => new RectangleF(0f, top, contentWidth * 0.42f, rowHeight)
            };
        }

        private float ResolveWidthWithoutSiblingOverlap(
            CompanyReportElementInfo element,
            IReadOnlyList<CompanyReportElementInfo> rowElements,
            float contentWidth,
            float left,
            float width,
            float neededWidth)
        {
            if (!IsLeftAreaElement(element) || neededWidth <= width + 0.5f)
            {
                return width;
            }

            var rightLeft = rowElements
                .Where(IsRightAreaElement)
                .Select(item => ResolveElementBounds(item, rowElements, contentWidth, 0f, 0f).Left)
                .DefaultIfEmpty(contentWidth)
                .Min();
            var maxWidth = Math.Max(width, rightLeft - left - 4f);
            // Only widen when the whole string still fits on one line. If it must wrap,
            // keep the configured width so extra lines stay in the left column.
            return neededWidth <= maxWidth + 0.5f ? neededWidth : width;
        }

        private float ResolveElementHeight(
            CompanyReportElementInfo element,
            string text,
            float availableWidth)
        {
            var font = CreateElementFont(element);
            var singleLine = Math.Max(20f, font.Size + 10f);
            if (string.IsNullOrWhiteSpace(text))
            {
                return singleLine;
            }

            var usableWidth = Math.Max(1f, availableWidth - 4f);
            var unconstrained = MeasureGdiTextWidthHundredths(text, font, typographic: false);
            if (unconstrained <= usableWidth + 0.5f)
            {
                return singleLine;
            }

            var wrapped = MeasureGdiTextSizeHundredths(text, font, usableWidth, typographic: false);
            return Math.Max(singleLine * 2f, wrapped.Height + 2f);
        }

        private DXFont CreateElementFont(CompanyReportElementInfo element)
        {
            var style = DXFontStyle.Regular;
            if (IsFlagEnabled(element.IS_BOLD))
            {
                style |= DXFontStyle.Bold;
            }

            if (IsFlagEnabled(element.IS_ITALIC))
            {
                style |= DXFontStyle.Italic;
            }

            return CreateFont(_layout.FontFamily, ResolveFontSize(element.FONT_SIZE, _layout.InfoFontSize), style);
        }

        private string ResolveElementDisplayText(
            CompanyReportElementInfo element,
            ReportCompanyContext companyContext,
            DataTable table,
            DataRow? row)
        {
            var label = ResolveElementLabelText(element);
            if (IsStaticFixedText(element))
            {
                return label ?? string.Empty;
            }

            var value = ResolveElementValueText(element, companyContext, table, row);
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value) && !string.Equals(label, value, StringComparison.Ordinal))
            {
                return $"{label} {value}";
            }

            return FirstNonEmpty(value, ResolveElementStaticText(element.CAPTION), label) ?? string.Empty;
        }

        private static bool IsStaticFixedText(CompanyReportElementInfo element)
        {
            if (IsAccountNumberElement(element))
            {
                return false;
            }

            var source = Common.NormalizeToken(element.VALUE_SOURCE);
            return source is "fixedtext" or "fixed";
        }

        private string? ResolveElementValueText(
            CompanyReportElementInfo element,
            ReportCompanyContext companyContext,
            DataTable table,
            DataRow? row)
        {
            // CASH_BOOK header "Số tài khoản" was seeded as FIXED_TEXT without VALUE_FIELD.
            // Always bind account filter / data account when this item is rendered.
            if (IsAccountNumberElement(element))
            {
                return ResolveParamElementValue(
                        Common.NormalizeNullableText(element.VALUE_FIELD) ?? "accountCd",
                        element)
                    ?? ResolveParamElementValue("accountCd", element)
                    ?? ResolveParamElementValue("ACCOUNT_CD", element)
                    ?? ResolveDataElementValue(table, row, "ACCOUNT_CD", element);
            }

            var source = Common.NormalizeToken(element.VALUE_SOURCE);
            var field = Common.NormalizeNullableText(element.VALUE_FIELD);
            return source switch
            {
                "company" => ResolveCompanyElementValue(companyContext, field),
                "param" => ResolveParamElementValue(field, element)
                    ?? ResolveDataElementValue(table, row, field, element),
                "data" => ResolveDataElementValue(table, row, field, element),
                "system" => ResolveSystemElementValue(companyContext, field),
                _ => ResolveElementStaticText(element.CAPTION) ?? ResolveElementLabelText(element)
            };
        }

        private static bool IsAccountNumberElement(CompanyReportElementInfo element)
        {
            var itemKey = Common.NormalizeToken(element.ITEM_KEY);
            var labelKey = Common.NormalizeToken(element.LABEL_TEXT);
            return itemKey is "accountnumber" or "accountno" or "accountcd"
                || labelKey is "accountnumber" or "accountno";
        }

        private string? ResolveParamElementValue(string? field, CompanyReportElementInfo element)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return null;
            }

            foreach (var key in EnumerateParamLookupKeys(field))
            {
                if (_namedValues.TryGetValue(key, out var namedValue))
                {
                    var text = FormatParamRawValue(namedValue);
                    if (text != null)
                    {
                        return FormatElementValue(namedValue, element);
                    }
                }

                var token = Common.NormalizeToken(key);
                if (token.Length > 0 && _normalizedValues.TryGetValue(token, out var normalizedValue))
                {
                    var text = FormatParamRawValue(normalizedValue);
                    if (text != null)
                    {
                        return FormatElementValue(normalizedValue, element);
                    }
                }
            }

            return null;
        }

        private static string? FormatParamRawValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return Common.NormalizeNullableText(value.ToString());
        }

        private static IEnumerable<string> EnumerateParamLookupKeys(string field)
        {
            var normalized = Common.NormalizeNullableText(field);
            if (normalized == null)
            {
                yield break;
            }

            yield return normalized;

            var sqlName = Common.NormalizeSqlParameterName(normalized);
            if (!sqlName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return sqlName;
            }

            if (normalized.StartsWith("p_", StringComparison.OrdinalIgnoreCase) && normalized.Length > 2)
            {
                yield return normalized[2..];
            }

            var token = Common.NormalizeToken(normalized);
            if (token.Length > 0)
            {
                yield return token;
                if (token.StartsWith("p", StringComparison.Ordinal) && token.Length > 1)
                {
                    yield return token[1..];
                }
            }
        }

        private static string? ResolveCompanyElementValue(ReportCompanyContext companyContext, string? field)
        {
            var token = Common.NormalizeToken(field);
            return token switch
            {
                "companyname" or "companynm" or "companynmviet" or "name" => companyContext.CompanyNameText,
                "companydisplay" => companyContext.CompanyDisplayText,
                "companycd" => companyContext.CompanyInfo.COMPANY_CD,
                "address" or "companyaddress" => companyContext.AddressDisplayText,
                "taxcd" => companyContext.CompanyInfo.TAX_CD,
                _ => ResolveObjectPropertyText(companyContext.CompanyInfo, field)
            };
        }

        private string? ResolveDataElementValue(DataTable table, DataRow? row, string? field, CompanyReportElementInfo element)
        {
            if (field == null || (row == null && table.Rows.Count == 0))
            {
                return ResolveElementLabelText(element);
            }

            var columnName = ResolveColumn(table, field);
            if (columnName == null)
            {
                return ResolveElementLabelText(element);
            }

            return FormatElementValue((row ?? table.Rows[0])[columnName], element);
        }

        private static string? ResolveSystemElementValue(ReportCompanyContext companyContext, string? field)
        {
            var token = Common.NormalizeToken(field);
            return token switch
            {
                "today" or "now" or "printdate" => companyContext.PrintDateText,
                _ => null
            };
        }

        private static string? ResolveObjectPropertyText(object source, string? field)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return null;
            }

            var property = source.GetType().GetProperties()
                .FirstOrDefault(item => item.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
            return Common.NormalizeNullableText(property?.GetValue(source)?.ToString());
        }

        private string? FormatElementValue(object? value, CompanyReportElementInfo element)
        {
            if (value == null || value == DBNull.Value)
            {
                return ResolveElementLabelText(element);
            }

            var formatString = Common.NormalizeNullableText(element.FORMAT_STRING);
            if (value is DateTime dateTime)
            {
                return dateTime.ToString(formatString ?? "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (value is IFormattable formattable && IsElementNumeric(element.DATA_TYPE))
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    formatString ?? "{0:n2}",
                    formattable);
            }

            return Common.NormalizeNullableText(value.ToString()) ?? ResolveElementLabelText(element);
        }

        private string? ResolveElementLabelText(CompanyReportElementInfo element)
        {
            var value = Common.NormalizeNullableText(element.LABEL_TEXT);
            if (value == null)
            {
                return null;
            }

            var translated = ReportLanguageHelper.LocalizeLabel(value, _reportLanguage);
            return ResolveElementStaticText(translated);
        }

        private static string? ResolveElementStaticText(string? value)
        {
            return Common.NormalizeNullableText(value)?.Replace("\\n", "\n", StringComparison.Ordinal);
        }

        private TextAlignment ResolveElementAlignment(string? alignment, string? areaCode = null)
        {
            var token = Common.NormalizeToken(alignment);
            var area = Common.NormalizeToken(areaCode);
            return token switch
            {
                "center" or "middlecenter" => TextAlignment.MiddleCenter,
                "right" or "middleright" => area == "right" ? TextAlignment.TopRight : TextAlignment.MiddleRight,
                "left" or "middleleft" => area == "right" ? TextAlignment.MiddleLeft : TextAlignment.TopLeft,
                "topleft" => TextAlignment.TopLeft,
                "topcenter" => TextAlignment.TopCenter,
                "topright" => TextAlignment.TopRight,
                "bottomleft" => TextAlignment.BottomLeft,
                "bottomcenter" => TextAlignment.BottomCenter,
                "bottomright" => TextAlignment.BottomRight,
                _ => area switch
                {
                    "right" => TextAlignment.TopRight,
                    "center" => TextAlignment.TopCenter,
                    _ => TextAlignment.TopLeft
                }
            };
        }

        private static IReadOnlyList<CompanyReportElementInfo> GetReportElements(ReportConfigurationInfo configuration, string sectionType)
        {
            var normalizedSectionType = Common.NormalizeToken(sectionType);
            return configuration.ELEMENTS
                .Where(IsRenderableElement)
                .Where(element => Common.NormalizeToken(element.SECTION_TYPE) == normalizedSectionType)
                .OrderBy(element => element.ROW_NO)
                .ThenBy(element => element.COL_NO)
                .ThenBy(element => element.SORT_ORDER)
                .ThenBy(element => element.ID)
                .ToList();
        }

        private DataRow? ResolveCurrentDataRow()
        {
            var currentRow = GetCurrentRow();
            return currentRow switch
            {
                DataRow row => row,
                DataRowView rowView => rowView.Row,
                _ => null
            };
        }

        private static string? ResolveTableGroupField(DataTable table)
        {
            // Explicit group key wins when present.
            if (HasColumn(table, "REPORT_GROUP_KEY"))
            {
                return ResolveColumn(table, "REPORT_GROUP_KEY");
            }

            // Ledger / outline reports (OPENING/DETAIL/CLOSING...) must print continuously.
            // Do not page-break per CHIT_ID — that creates one-page-per-voucher spam.
            if (HasColumn(table, "__ROW_TYPE"))
            {
                return null;
            }

            // Document-batch prints may still group by bare CHIT_ID.
            // System link aliases (__CHIT_ID) never drive page breaks.
            var chitIdColumn = ResolveColumn(table, "CHIT_ID");
            if (chitIdColumn != null &&
                !chitIdColumn.StartsWith(ReportSystemFields.Prefix, StringComparison.Ordinal))
            {
                return chitIdColumn;
            }

            return null;
        }

        private static bool IsRenderableElement(CompanyReportElementInfo element)
        {
            return IsFlagEnabled(element.IS_VISIBLE)
                && !IsFlagEnabled(element.ISDEL)
                && Common.NormalizeToken(element.ITEM_KEY) != "none";
        }

        private static bool IsContentElement(CompanyReportElementInfo element)
        {
            return Common.NormalizeToken(element.ELEMENT_TYPE) != "signature";
        }

        private static bool HasSignatures(ReportCompanyContext companyContext)
        {
            return companyContext.Signatures.Count > 0;
        }

        private static bool IsDataBoundElement(CompanyReportElementInfo element)
        {
            return Common.NormalizeToken(element.VALUE_SOURCE) == "data";
        }

        private static bool IsElementNumeric(string? dataType)
        {
            return Common.NormalizeToken(dataType) is "number" or "money" or "quantity" or "decimal" or "amount";
        }

        private static bool IsFullWidthArea(string? areaCode)
        {
            return Common.NormalizeToken(areaCode) is "full" or "table";
        }

        private static bool IsFullWidthElement(CompanyReportElementInfo element)
        {
            return IsFullWidthArea(element.AREA_CODE);
        }

        private static bool IsLeftAreaElement(CompanyReportElementInfo element)
        {
            var area = Common.NormalizeToken(element.AREA_CODE);
            return area is "" or "left";
        }

        private static bool IsRightAreaElement(CompanyReportElementInfo element)
        {
            return Common.NormalizeToken(element.AREA_CODE) == "right";
        }

        private static bool IsCenterAreaElement(CompanyReportElementInfo element)
        {
            return Common.NormalizeToken(element.AREA_CODE) == "center";
        }

        private static bool IsFlagEnabled(string? value)
        {
            return Common.NormalizeToken(value) is "1" or "y" or "yes" or "true";
        }

        private static DXFontStyle ResolveDataRowFontStyle(DataRow? row)
        {
            var style = DXFontStyle.Regular;
            if (IsFlagEnabled(ReadDataRowText(row, "FONT_BOLD"))
                || IsFlagEnabled(ReadDataRowText(row, "__FONT_BOLD")))
            {
                style |= DXFontStyle.Bold;
            }

            if (IsFlagEnabled(ReadDataRowText(row, "FONT_ITALIC"))
                || IsFlagEnabled(ReadDataRowText(row, "__FONT_ITALIC")))
            {
                style |= DXFontStyle.Italic;
            }

            return style;
        }

        private static bool IsEInvoiceSalesAppendixReport(ReportConfigurationInfo configuration)
        {
            return Common.NormalizeToken(configuration.REPORT_CODE) == "einvreport";
        }

        private GroupHeaderBand BuildConfiguredTableHeaderBand()
        {
            var headerHeight = GetConfiguredTableHeaderHeight();
            var band = new GroupHeaderBand
            {
                HeightF = headerHeight,
                RepeatEveryPage = true,
                KeepTogether = true
            };

            if (DataSource is DataTable table && HasColumn(table, "REPORT_GROUP_KEY"))
            {
                band.GroupFields.Add(new GroupField("REPORT_GROUP_KEY"));
            }
            var header = CreateConfiguredTableHeader(0f);
            band.Controls.Add(header);
            return band;
        }

        private DetailBand BuildConfiguredTableDetailBand(
            IReadOnlyList<CompanyReportElementInfo>? detailElements = null,
            ReportCompanyContext? companyContext = null,
            DataTable? dataTable = null)
        {
            var rowHeight = Math.Max(27f, _layout.DetailFontSize + 18f);
            var columns = GetConfiguredTableColumns();
            var band = new DetailBand
            {
                KeepTogether = true
            };
            var top = detailElements != null && companyContext != null && dataTable != null
                ? AddElementBlock(band.Controls, detailElements, companyContext, dataTable, 0f, true)
                : 0f;
            var table = new XRTable
            {
                BoundsF = new RectangleF(0f, top, GetContentWidth(), rowHeight),
                Font = CreateFont(_layout.FontFamily, _layout.DetailFontSize),
                Borders = BorderSide.None
            };
            var row = new XRTableRow
            {
                HeightF = rowHeight,
                CanGrow = true,
            };

            for (var index = 0; index < columns.Count; index++)
            {
                row.Cells.Add(CreateConfiguredTableDataCell(columns[index], index, columns));
            }

            table.Rows.Add(row);
            band.Controls.Add(table);
            band.HeightF = Math.Max(1f, top + rowHeight);
            return band;
        }

        private XRControl CreateConfiguredTableHeader(float top)
        {
            var layout = _tableLayout;
            if (layout == null)
            {
                return CreateSimpleConfiguredTableHeader(top);
            }

            var rowHeight = GetConfiguredTableHeaderRowHeight();
            var headerHeight = rowHeight * layout.HeaderRowCount;
            var occupied = new bool[layout.HeaderRowCount, layout.ColumnCount];
            var contentWidth = GetContentWidth();
            var columnWidths = ResolveTableColumnWidths(layout, contentWidth);
            var columnLefts = ResolveTableColumnLefts(columnWidths);
            var panel = new XRPanel
            {
                BoundsF = new RectangleF(0f, top, contentWidth, headerHeight),
                Borders = BorderSide.None,
                BackColor = GetConfiguredTableHeaderBackColor()
            };

            foreach (var cell in layout.HeaderCells.OrderBy(item => item.RowIndex).ThenBy(item => item.ColIndex).ThenBy(item => item.SortOrder))
            {
                var rowIndex = ClampTableIndex(cell.RowIndex, layout.HeaderRowCount);
                var colIndex = ClampTableIndex(cell.ColIndex, layout.ColumnCount);
                var rowSpan = Math.Min(Math.Max(1, cell.RowSpan), layout.HeaderRowCount - rowIndex);
                var colSpan = Math.Min(Math.Max(1, cell.ColSpan), layout.ColumnCount - colIndex);
                if (IsHeaderSpanOccupied(occupied, rowIndex, colIndex, rowSpan, colSpan))
                {
                    continue;
                }

                MarkHeaderSpanOccupied(occupied, rowIndex, colIndex, rowSpan, colSpan);
                panel.Controls.Add(CreateConfiguredTableHeaderLabel(
                    cell.Caption,
                    columnLefts[colIndex],
                    rowIndex * rowHeight,
                    SumTableWidths(columnWidths, colIndex, colSpan),
                    rowSpan * rowHeight));
            }

            return panel;
        }

        private XRTable CreateSimpleConfiguredTableHeader(float top)
        {
            var headerHeight = GetConfiguredTableHeaderHeight();
            var table = new XRTable
            {
                BoundsF = new RectangleF(0f, top, GetContentWidth(), headerHeight),
                Font = CreateFont(_layout.FontFamily, _layout.HeaderFontSize, DXFontStyle.Bold),
                Borders = BorderSide.None,
                BackColor = GetConfiguredTableHeaderBackColor()
            };
            var row = new XRTableRow { HeightF = headerHeight };

            foreach (var column in _columns)
            {
                row.Cells.Add(CreateConfiguredTableHeaderCell(column.Caption, column.Weight, headerHeight));
            }

            table.Rows.Add(row);
            return table;
        }

        private XRTableCell CreateConfiguredTableHeaderCell(string text, float weight, float height)
        {
            return new XRTableCell
            {
                Text = text,
                Weight = weight,
                HeightF = height,
                Font = CreateFont(_layout.FontFamily, _layout.HeaderFontSize, DXFontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter,
                Borders = BorderSide.All,
                BorderWidth = 0.8f,
                Padding = new PaddingInfo(2, 2, 1, 1),
                CanGrow = false,
                Multiline = true,
                BackColor = GetConfiguredTableHeaderBackColor()
            };
        }

        private XRLabel CreateConfiguredTableHeaderLabel(string text, float left, float top, float width, float height)
        {
            var label = new XRLabel
            {
                BoundsF = new RectangleF(left, top, width, height),
                Text = text,
                Font = CreateFont(_layout.FontFamily, _layout.HeaderFontSize, DXFontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter,
                Borders = BorderSide.All,
                BorderWidth = 0.8f,
                Padding = new PaddingInfo(2, 2, 1, 1),
                CanGrow = false,
                Multiline = true,
                BackColor = GetConfiguredTableHeaderBackColor()
            };
            return label;
        }

        private Color GetConfiguredTableHeaderBackColor()
        {
            return _useEInvoiceSalesAppendixStyle
                ? Color.FromArgb(221, 219, 241)
                : Color.White;
        }

        private XRTableCell CreateConfiguredTableDataCell(
            ReportColumnDefinition column,
            int columnIndex,
            IReadOnlyList<ReportColumnDefinition> columns)
        {
            var originalWeight = (double)column.Weight;
            var totalWeight = columns.Sum(item => (double)item.Weight);
            var summaryLabelSpanWeight = columns
                .Take(Math.Min(6, columns.Count))
                .Sum(item => (double)item.Weight);
            var cell = new XRTableCell
            {
                Weight = column.Weight,
                Borders = BorderSide.All,
                BorderWidth = 0.8f,
                Padding = new PaddingInfo(3, 3, 1, 1),
                TextAlignment = ResolveWrappedDataCellAlignment(column.Alignment),
                CanGrow = true,
                WordWrap = true,
                Multiline = true
            };

            cell.BeforePrint += (_, _) =>
            {
                ConfigureConfiguredTableDataCell(
                    cell,
                    column,
                    columnIndex,
                    originalWeight,
                    summaryLabelSpanWeight,
                    totalWeight);
            };

            return cell;
        }

        private void ConfigureConfiguredTableDataCell(
            XRTableCell cell,
            ReportColumnDefinition column,
            int columnIndex,
            double originalWeight,
            double summaryLabelSpanWeight,
            double totalWeight)
        {
            var currentRow = ResolveCurrentDataRow();
            cell.Visible = true;
            cell.Weight = originalWeight;
            cell.TextAlignment = ResolveWrappedDataCellAlignment(column.Alignment);
            var detailFontStyle = ResolveDataRowFontStyle(currentRow);
            cell.Font = CreateFont(_layout.FontFamily, _layout.DetailFontSize, detailFontStyle);
            cell.BackColor = Color.Transparent;
            cell.Text = ResolveConfiguredTableCellDisplayText(currentRow, column);

            if (!_useEInvoiceSalesAppendixStyle)
            {
                return;
            }

            var rowType = Common.NormalizeToken(ReadDataRowText(currentRow, "__ROW_TYPE")).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(rowType) || rowType == "DETAIL")
            {
                return;
            }

            var summaryLabel = FirstNonEmpty(
                ReadDataRowText(currentRow, "SUMMARY_LABEL"),
                ReadDataRowText(currentRow, "STT")) ?? string.Empty;

            if (rowType == "SECTION")
            {
                if (columnIndex == 0)
                {
                    cell.Weight = totalWeight;
                    cell.Text = summaryLabel;
                    cell.TextAlignment = TextAlignment.MiddleLeft;
                    cell.Font = CreateFont(_layout.FontFamily, _layout.DetailFontSize, DXFontStyle.Bold);
                }
                else
                {
                    cell.Visible = false;
                    cell.Text = string.Empty;
                }

                return;
            }

            if (rowType is "SUBTOTAL" or "GRANDTOTAL")
            {
                const int summaryLabelColumnSpan = 6;
                if (columnIndex == 0)
                {
                    cell.Weight = summaryLabelSpanWeight;
                    cell.Text = summaryLabel;
                    cell.TextAlignment = TextAlignment.MiddleRight;
                    cell.Font = CreateFont(_layout.FontFamily, _layout.DetailFontSize, DXFontStyle.Bold);
                    cell.BackColor = rowType == "GRANDTOTAL"
                        ? GetConfiguredTableHeaderBackColor()
                        : Color.Transparent;
                    return;
                }

                if (columnIndex < summaryLabelColumnSpan)
                {
                    cell.Visible = false;
                    cell.Text = string.Empty;
                    return;
                }
            }
        }

        private string ResolveConfiguredTableCellDisplayText(DataRow? currentRow, ReportColumnDefinition column)
        {
            if (IsReportItemNameColumn(column.Name))
            {
                return ResolveReportItemDisplayName(
                    ReadDataRowText(currentRow, "LABEL_TEXT"),
                    ReadDataRowText(currentRow, "CAPTION"),
                    ReadDataRowText(currentRow, column.Name));
            }

            return FormatConfiguredTableCellValue(ReadDataRowValue(currentRow, column.Name), column.FormatString);
        }

        private static bool IsReportItemNameColumn(string? columnName)
        {
            var token = Common.NormalizeToken(columnName);
            return token is "itemname" or "reportitemname" or "itemtext" or "linename" or "sectionname";
        }

        private string ResolveReportItemDisplayName(string? labelText, string? caption, string? itemName)
        {
            var rawItemName = itemName ?? string.Empty;
            var indentLength = 0;
            while (indentLength < rawItemName.Length && char.IsWhiteSpace(rawItemName[indentLength]))
            {
                indentLength++;
            }

            var indent = indentLength > 0 ? rawItemName[..indentLength] : string.Empty;
            var itemNameTrimmed = Common.NormalizeNullableText(rawItemName);
            var fallback = Common.NormalizeNullableText(caption) ?? itemNameTrimmed ?? string.Empty;
            var localized = ReportLanguageHelper.LocalizeLabelOrFallback(labelText, fallback, _reportLanguage);

            if (string.IsNullOrEmpty(indent))
            {
                return string.IsNullOrEmpty(localized) ? rawItemName : localized;
            }

            if (string.Equals(localized, rawItemName, StringComparison.Ordinal)
                || string.Equals(localized, itemNameTrimmed, StringComparison.Ordinal))
            {
                return rawItemName;
            }

            return indent + localized;
        }

        private static object? ReadDataRowValue(DataRow? row, string columnName)
        {
            if (row == null)
            {
                return null;
            }

            var resolvedColumn = ResolveColumn(row.Table, columnName);
            return resolvedColumn == null ? null : row[resolvedColumn];
        }

        private static string? ReadDataRowText(DataRow? row, string columnName)
        {
            var value = ReadDataRowValue(row, columnName);
            return value == null || value == DBNull.Value
                ? null
                : Common.NormalizeNullableText(value.ToString());
        }

        private static string FormatConfiguredTableCellValue(object? value, string? formatString)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            var format = Common.NormalizeNullableText(formatString);
            if (LooksLikeDateFormat(format) || value is DateTime or DateTimeOffset)
            {
                var dateTime = TryCoerceToDateTime(value);
                if (dateTime.HasValue)
                {
                    return format == null
                        ? dateTime.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
                        : string.Format(System.Globalization.CultureInfo.InvariantCulture, format, dateTime.Value);
                }
            }

            // string implements IFormattable in some runtimes but date masks do not apply to raw YMD text —
            // skip IFormattable path for string so we do not silently ignore FORMAT_TYPE=date.
            if (format != null && value is IFormattable && value is not string)
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, value);
            }

            return value is IConvertible
                ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
                : value.ToString() ?? string.Empty;
        }

        private static bool LooksLikeDateFormat(string? format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return false;
            }

            return format.Contains("yyyy", StringComparison.OrdinalIgnoreCase)
                   || format.Contains("yy", StringComparison.OrdinalIgnoreCase)
                   || format.Contains("MM", StringComparison.Ordinal)
                   || format.Contains("dd", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime? TryCoerceToDateTime(object value)
        {
            switch (value)
            {
                case DateTime dateTime:
                    return dateTime;
                case DateTimeOffset dateTimeOffset:
                    return dateTimeOffset.DateTime;
            }

            var text = Common.NormalizeNullableText(
                Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            if (text == null)
            {
                return null;
            }

            if (text.Length == 8
                && text.All(char.IsDigit)
                && DateTime.TryParseExact(
                    text,
                    "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var ymd))
            {
                return ymd;
            }

            if (text.Length == 6
                && text.All(char.IsDigit)
                && DateTime.TryParseExact(
                    text + "01",
                    "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var ym))
            {
                return ym;
            }

            string[] patterns =
            [
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "dd/MM/yyyy",
                "dd-MM-yyyy",
                "yyyy-MM",
                "yyyy/MM"
            ];

            return DateTime.TryParseExact(
                text,
                patterns,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed)
                ? parsed
                : null;
        }

        private float GetConfiguredTableHeaderHeight()
        {
            return _tableLayout == null
                ? GetSimpleConfiguredTableHeaderHeight()
                : GetConfiguredTableHeaderRowHeight() * _tableLayout.HeaderRowCount;
        }

        private float GetSimpleConfiguredTableHeaderHeight()
        {
            var maxCaptionLength = _columns.Count == 0 ? 0 : _columns.Max(column => column.Caption.Length);
            var factor = maxCaptionLength > 36 ? 4.2f : maxCaptionLength > 18 ? 3.3f : 2.6f;
            var height = Math.Max(34f, _layout.HeaderFontSize * factor);
            return _hasBalanceSheetPeriodCaptions
                ? Math.Max(height, _layout.HeaderFontSize * 4.0f)
                : height;
        }

        private float GetConfiguredTableHeaderRowHeight()
        {
            var height = Math.Max(22f, _layout.HeaderFontSize * 2.3f);
            return _hasBalanceSheetPeriodCaptions
                ? Math.Max(height, _layout.HeaderFontSize * 3.8f)
                : height;
        }

        private (
            ReportTableLayout? Layout,
            IReadOnlyList<ReportColumnDefinition> Columns,
            bool HasPeriodCaptions)
            ApplyBalanceSheetPeriodCaptions(
                ReportTableLayout? layout,
                IReadOnlyList<ReportColumnDefinition> columns)
        {
            var isBalanceSheet = IsBalanceSheetReportCode(_reportCode);
            var useComparativeYearPeriod = IsComparativeYearPeriodReportCode(_reportCode);
            if (!isBalanceSheet && !useComparativeYearPeriod)
            {
                return (layout, columns, false);
            }

            var fromDate = ResolveReportPeriodDate("FROM_DATE", "fromYmd", "p_FROM_DATE", "p_FROM_YMD");
            var toDate = ResolveReportPeriodDate("TO_DATE", "toYmd", "p_TO_DATE", "p_TO_YMD");
            var hasPeriodCaptions = false;

            if (layout != null)
            {
                var headerCells = layout.HeaderCells
                    .Select(cell =>
                    {
                        var period = ResolveFinancialStatementHeaderPeriod(
                            cell.FieldName ?? cell.ColumnKey,
                            fromDate,
                            toDate,
                            useComparativeYearPeriod);
                        if (period == null)
                        {
                            return cell;
                        }

                        hasPeriodCaptions = true;
                        return cell with { Caption = AppendBalanceSheetPeriodCaption(cell.Caption, period) };
                    })
                    .ToList();

                var detailColumns = layout.DetailColumns
                    .Select(column =>
                    {
                        var next = EnhanceBalanceSheetColumnCaption(
                            column,
                            fromDate,
                            toDate,
                            useComparativeYearPeriod,
                            out var applied);
                        hasPeriodCaptions |= applied;
                        return next;
                    })
                    .ToList();

                return (
                    layout with { HeaderCells = headerCells, DetailColumns = detailColumns },
                    detailColumns,
                    hasPeriodCaptions);
            }

            var nextColumns = columns
                .Select(column =>
                {
                    var next = EnhanceBalanceSheetColumnCaption(
                        column,
                        fromDate,
                        toDate,
                        useComparativeYearPeriod,
                        out var applied);
                    hasPeriodCaptions |= applied;
                    return next;
                })
                .ToList();

            return (null, nextColumns, hasPeriodCaptions);
        }

        private static ReportColumnDefinition EnhanceBalanceSheetColumnCaption(
            ReportColumnDefinition column,
            DateTime? fromDate,
            DateTime? toDate,
            bool useComparativeYearPeriod,
            out bool applied)
        {
            var period = ResolveFinancialStatementHeaderPeriod(
                column.Name,
                fromDate,
                toDate,
                useComparativeYearPeriod);
            if (period == null)
            {
                applied = false;
                return column;
            }

            applied = true;
            return column with { Caption = AppendBalanceSheetPeriodCaption(column.Caption, period) };
        }

        private static string AppendBalanceSheetPeriodCaption(string caption, string period)
        {
            var title = Common.NormalizeNullableText(caption) ?? string.Empty;
            if (title.Length == 0)
            {
                return period;
            }

            if (title.Contains(period, StringComparison.Ordinal))
            {
                return title;
            }

            return $"{title}\n{period}";
        }

        private static bool IsBalanceSheetReportCode(string? reportCode)
        {
            var normalized = Common.NormalizeToken(reportCode);
            return normalized is "glbalancesheetb01dn" or "b01dn";
        }

        private static bool IsProfitLossReportCode(string? reportCode)
        {
            var normalized = Common.NormalizeToken(reportCode);
            return normalized is "glprofitlossb02dn" or "b02dn";
        }

        private static bool IsCashflowReportCode(string? reportCode)
        {
            var normalized = Common.NormalizeToken(reportCode);
            return normalized is "glcashflowb03dntt" or "glcashflowb03dngt" or "b03dntt" or "b03dngt";
        }

        private static bool IsComparativeYearPeriodReportCode(string? reportCode)
        {
            return IsProfitLossReportCode(reportCode) || IsCashflowReportCode(reportCode);
        }

        private static string? ResolveFinancialStatementHeaderPeriod(
            string? fieldName,
            DateTime? fromDate,
            DateTime? toDate,
            bool useComparativeYearPeriod)
        {
            return useComparativeYearPeriod
                ? ResolveComparativeYearHeaderPeriod(fieldName, fromDate, toDate)
                : ResolveBalanceSheetHeaderPeriod(fieldName, fromDate, toDate);
        }

        private static string? FormatHeaderDateRange(DateTime? fromDate, DateTime? toDate)
        {
            var fromText = FormatBalanceSheetHeaderDate(fromDate);
            var toText = FormatBalanceSheetHeaderDate(toDate);
            return fromText != null && toText != null ? $"{fromText} - {toText}" : null;
        }

        private static string? ResolveBalanceSheetHeaderPeriod(
            string? fieldName,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var fieldKey = Common.NormalizeToken(fieldName);
            if (fieldKey.Length == 0)
            {
                return null;
            }

            if (fieldKey is "endyear" or "thisyearmoney" or "currentyear")
            {
                return FormatHeaderDateRange(fromDate, toDate);
            }

            if (fieldKey is "beginyear" or "lastyearmoney" or "previousyear")
            {
                if (fromDate == null)
                {
                    return null;
                }

                return FormatBalanceSheetHeaderDate(fromDate.Value.Date.AddDays(-1));
            }

            return null;
        }

        private static string? ResolveComparativeYearHeaderPeriod(
            string? fieldName,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var fieldKey = Common.NormalizeToken(fieldName);
            if (fieldKey.Length == 0)
            {
                return null;
            }

            if (fieldKey is "currentyear" or "thisyearmoney" or "endyear")
            {
                return FormatHeaderDateRange(fromDate, toDate);
            }

            if (fieldKey is "previousyear" or "lastyearmoney" or "beginyear")
            {
                if (fromDate == null)
                {
                    return null;
                }

                return FormatBalanceSheetHeaderDate(fromDate.Value.Date.AddDays(-1));
            }

            return null;
        }

        private static string? FormatBalanceSheetHeaderDate(DateTime? value)
        {
            return value?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        private DateTime? ResolveReportPeriodDate(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (_namedValues.TryGetValue(key, out var namedValue) && namedValue != null)
                {
                    var parsed = TryCoerceToDateTime(namedValue);
                    if (parsed != null)
                    {
                        return parsed;
                    }
                }

                var token = Common.NormalizeToken(key);
                if (token.Length > 0
                    && _normalizedValues.TryGetValue(token, out var normalizedValue)
                    && normalizedValue != null)
                {
                    var parsed = TryCoerceToDateTime(normalizedValue);
                    if (parsed != null)
                    {
                        return parsed;
                    }
                }
            }

            return null;
        }

        private IReadOnlyList<ReportColumnDefinition> GetConfiguredTableColumns()
        {
            return _tableLayout?.DetailColumns ?? _columns;
        }

        private static int ClampTableIndex(int index, int count)
        {
            return Math.Min(Math.Max(0, index), Math.Max(0, count - 1));
        }

        private static bool IsHeaderSpanOccupied(bool[,] occupied, int rowIndex, int colIndex, int rowSpan, int colSpan)
        {
            var rowLimit = Math.Min(occupied.GetLength(0), rowIndex + Math.Max(1, rowSpan));
            var colLimit = Math.Min(occupied.GetLength(1), colIndex + Math.Max(1, colSpan));
            for (var row = rowIndex; row < rowLimit; row++)
            {
                for (var column = colIndex; column < colLimit; column++)
                {
                    if (occupied[row, column])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void MarkHeaderSpanOccupied(bool[,] occupied, int rowIndex, int colIndex, int rowSpan, int colSpan)
        {
            var rowLimit = Math.Min(occupied.GetLength(0), rowIndex + Math.Max(1, rowSpan));
            var colLimit = Math.Min(occupied.GetLength(1), colIndex + Math.Max(1, colSpan));
            for (var row = rowIndex; row < rowLimit; row++)
            {
                for (var column = colIndex; column < colLimit; column++)
                {
                    occupied[row, column] = true;
                }
            }
        }

        private static IReadOnlyList<float> ResolveTableColumnWidths(ReportTableLayout layout, float contentWidth)
        {
            var totalWeight = layout.ColumnWeights.Sum();
            if (totalWeight <= 0f)
            {
                return Enumerable.Repeat(contentWidth / Math.Max(1, layout.ColumnCount), layout.ColumnCount).ToList();
            }

            return layout.ColumnWeights
                .Select(weight => contentWidth * weight / totalWeight)
                .ToList();
        }

        private static IReadOnlyList<float> ResolveTableColumnLefts(IReadOnlyList<float> columnWidths)
        {
            var lefts = new float[columnWidths.Count];
            var left = 0f;
            for (var index = 0; index < columnWidths.Count; index++)
            {
                lefts[index] = left;
                left += columnWidths[index];
            }

            return lefts;
        }

        private static float SumTableWidths(IReadOnlyList<float> columnWidths, int colIndex, int colSpan)
        {
            var width = 0f;
            var colLimit = Math.Min(columnWidths.Count, colIndex + Math.Max(1, colSpan));
            for (var index = colIndex; index < colLimit; index++)
            {
                width += columnWidths[index];
            }

            return width;
        }

        private static float ResolveTableCellWeight(ReportTableLayoutCell cell)
        {
            return cell.Width > 0f ? cell.Width : Math.Max(1f, cell.ColSpan);
        }

        private TopMarginBand BuildTopMarginBand()
        {
            return new TopMarginBand
            {
                HeightF = _layout.MarginTop
            };
        }

        private BottomMarginBand BuildBottomMarginBand()
        {
            return new BottomMarginBand
            {
                HeightF = _layout.MarginBottom
            };
        }

        private XRTable CreateSignatureTable(
            IReadOnlyList<CompanyReportSignatureInfo> signatures,
            float top)
        {
            var slotCount = signatures.Count;
            var slotWidth = slotCount > 0 ? GetContentWidth() / slotCount : 0f;
            var nameRowHeight = GetSignatureNameRowHeight(signatures, slotWidth);
            var sectionHeight = slotCount > 0 ? 96f + nameRowHeight : 0f;
            var table = new XRTable
            {
                BoundsF = new RectangleF(0f, top, GetContentWidth(), sectionHeight),
                Font = CreateFont(_layout.FontFamily, _layout.FooterFontSize),
                BackColor = Color.Transparent,
                Borders = BorderSide.None
            };

            var captionRow = new XRTableRow { HeightF = 24f }; // VD: NGUOI LAP BIEU
            var titleRow = new XRTableRow { HeightF = 20f }; // (KY, HO TEN)
            var spacerRow = new XRTableRow { HeightF = 52f }; // SPACE (anh chu ky)
            var nameRow = new XRTableRow { HeightF = nameRowHeight }; // TEN NGUOI KY

            foreach (var signature in signatures)
            {
                captionRow.Cells.Add(CreateSignatureCell(
                    ResolveSignatureDisplayLabel(signature),
                    DXFontStyle.Bold,
                    TextAlignment.MiddleCenter,
                    BorderSide.None,
                    horizontalPadding: 0));

                titleRow.Cells.Add(CreateSignatureCell(
                    ResolveSignatureTitle(signature),
                    DXFontStyle.Regular,
                    TextAlignment.BottomCenter,
                    BorderSide.None));

                spacerRow.Cells.Add(CreateSignatureImageCell(signature.SIGN_IMAGE_URL));

                nameRow.Cells.Add(CreateSignatureCell(
                    signature.SIGN_NAME ?? string.Empty,
                    DXFontStyle.Bold,
                    TextAlignment.TopCenter,
                    BorderSide.None,
                    canGrow: true));
            }

            table.Rows.AddRange(new[] { captionRow, titleRow, spacerRow, nameRow });
            return table;
        }

        private static XRLabel CreateLabel(float left, float top, float width, float height, string text, DXFont font, TextAlignment alignment)
        {
            return new XRLabel
            {
                BoundsF = new RectangleF(left, top, width, height),
                Text = text,
                Font = font,
                TextAlignment = alignment,
                Padding = new PaddingInfo(2, 2, 0, 0)
            };
        }

        private XRTableCell CreateSignatureImageCell(string? imageUrl)
        {
            var cell = CreateSignatureCell(
                string.Empty,
                DXFontStyle.Regular,
                TextAlignment.MiddleCenter,
                BorderSide.None);

            try
            {
                if (_environment == null)
                {
                    return cell;
                }

                if (!CompanySignatureImageStorage.TryGetImageBytes(
                        _environment,
                        imageUrl,
                        out var bytes,
                        out _))
                {
                    return cell;
                }

                using var stream = new MemoryStream(bytes, writable: false);
                using var bitmap = new Bitmap(stream);
                var embedded = (Image)bitmap.Clone();

                var picture = new XRPictureBox
                {
                    LocationF = PointF.Empty,
                    // Temporary size; BeforePrint stretches to the table cell.
                    SizeF = new SizeF(80f, 56f),
                    UseImageResolution = false,
                    ImageSource = new ImageSource(embedded),
                    Sizing = ImageSizeMode.ZoomImage,
                    ImageAlignment = ImageAlignment.MiddleCenter,
                    Borders = BorderSide.None,
                    Padding = new PaddingInfo(4, 4, 2, 2)
                };

                cell.BeforePrint += (_, _) =>
                {
                    var width = Math.Max(1f, cell.WidthF - 8f);
                    var height = Math.Max(1f, cell.HeightF - 4f);
                    picture.BoundsF = new RectangleF(4f, 2f, width, height);
                };

                cell.Controls.Add(picture);
            }
            catch
            {
                // Keep empty cell if image cannot be embedded.
            }

            return cell;
        }

        private XRTableCell CreateSignatureCell(
            string text,
            DXFontStyle fontStyle,
            TextAlignment alignment,
            BorderSide borders,
            bool canGrow = false,
            int horizontalPadding = 4)
        {
            return new XRTableCell
            {
                Text = text,
                Weight = 1d,
                Font = CreateFont(_layout.FontFamily, _layout.FooterFontSize, fontStyle),
                TextAlignment = alignment,
                Borders = borders,
                BorderWidth = 0f,
                Padding = new PaddingInfo(horizontalPadding, horizontalPadding, 2, 2),
                CanGrow = canGrow,
                Multiline = true,
                WordWrap = canGrow
            };
        }

        private float GetContentWidth()
        {
            return PageWidth - Margins.Left - Margins.Right;
        }

        private float GetSignatureSectionHeight(IReadOnlyList<CompanyReportSignatureInfo> signatures)
        {
            if (signatures.Count == 0)
            {
                return 0f;
            }

            var slotWidth = GetContentWidth() / signatures.Count;
            return 96f + GetSignatureNameRowHeight(signatures, slotWidth);
        }

        private float GetSignatureNameRowHeight(
            IReadOnlyList<CompanyReportSignatureInfo> signatures,
            float slotWidth)
        {
            if (signatures.Count == 0)
            {
                return 0f;
            }

            var font = CreateFont(_layout.FontFamily, _layout.FooterFontSize, DXFontStyle.Bold);
            var maxTextHeight = signatures
                .Select(signature => MeasureGdiTextSizeHundredths(
                    signature.SIGN_NAME ?? string.Empty,
                    font,
                    Math.Max(1f, slotWidth - 8f),
                    typographic: false).Height)
                .DefaultIfEmpty(0f)
                .Max();

            // Keep enough room for a single line and cell padding; grow for wrapped names.
            return Math.Max(18f, maxTextHeight + 4f);
        }

        private static DataTable EnsureRenderableTable(DataTable table, string? reportName, string reportLanguage)
        {
            if (table.Columns.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(table.TableName))
                {
                    table.TableName = string.IsNullOrWhiteSpace(reportName) ? "ReportData" : reportName.Trim().Replace(" ", string.Empty);
                }

                return table;
            }

            var fallback = new DataTable(string.IsNullOrWhiteSpace(reportName) ? "ReportData" : reportName.Trim().Replace(" ", string.Empty));
            fallback.Columns.Add("MESSAGE", typeof(string));
            fallback.Rows.Add(ReportLanguageHelper.LocalizeLabel("NO_DATA", reportLanguage));
            return fallback;
        }

        private static IReadOnlyList<ReportColumnDefinition> BuildColumns(DataTable table, string reportLanguage)
        {
            return table.Columns.Cast<DataColumn>()
                .Where(column => !string.IsNullOrWhiteSpace(column.ColumnName))
                .Where(column => !ReportSystemFields.IsSystemField(column.ColumnName))
                .Select(column => CreateColumn(column, reportLanguage))
                .ToList();
        }

        private static ReportTableLayout? ResolveTableLayout(DataTable table, string reportLanguage)
        {
            if (!HasDetailTable(table) ||
                !table.ExtendedProperties.Contains(ReportDataTableProperties.TableLayoutMetadata) ||
                table.ExtendedProperties[ReportDataTableProperties.TableLayoutMetadata] is not DataTable metadataTable)
            {
                return null;
            }

            var cells = metadataTable.Rows.Cast<DataRow>()
                .Select(row => CreateTableLayoutCell(row, reportLanguage))
                .Where(cell => cell != null)
                .Cast<ReportTableLayoutCell>()
                .OrderBy(cell => cell.RowIndex)
                .ThenBy(cell => cell.ColIndex)
                .ThenBy(cell => cell.SortOrder)
                .ToList();
            var detailCells = cells
                .Where(cell => cell.FieldName != null)
                .OrderBy(cell => cell.ColIndex)
                .ThenBy(cell => cell.RowIndex)
                .ThenBy(cell => cell.SortOrder)
                .ToList();

            if (detailCells.Count == 0)
            {
                return null;
            }

            ReportFieldCoalesceHelper.MaterializeFromLayout(table, metadataTable);

            var missingFields = detailCells
                .Select(cell => cell.FieldName!)
                .Where(fieldName => !HasColumn(table, fieldName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(fieldName => fieldName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missingFields.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Report column layout FIELD_NAME missing from data: {string.Join(", ", missingFields)}.");
            }

            var headerRowCount = Math.Max(1, cells.Max(cell => cell.RowIndex + cell.RowSpan));
            var columnCount = Math.Max(1, cells.Max(cell => cell.ColIndex + cell.ColSpan));
            var columnWeights = ResolveTableColumnWeights(cells, columnCount);
            var detailColumns = detailCells
                .Select(cell => CreateColumn(table.Columns[ResolveColumn(table, cell.FieldName!)!]!, cell))
                .ToList();

            return new ReportTableLayout(cells, detailColumns, headerRowCount, columnCount, columnWeights);
        }

        private static bool HasDetailTable(DataTable table)
        {
            return table.ExtendedProperties.Contains(ReportDataTableProperties.HasDetailTable) &&
                   table.ExtendedProperties[ReportDataTableProperties.HasDetailTable] is true;
        }

        private static ReportColumnDefinition CreateColumn(DataColumn column, string reportLanguage)
        {
            var dataType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
            var isNumeric = IsNumericType(dataType);
            var isDate = dataType == typeof(DateTime);
            var caption = ResolveColumnCaption(column, reportLanguage);

            return new ReportColumnDefinition(
                column.ColumnName,
                caption,
                CalculateWeight(column.ColumnName, dataType),
                isNumeric ? TextAlignment.MiddleRight : isDate ? TextAlignment.MiddleCenter : TextAlignment.MiddleLeft,
                isNumeric && !column.ColumnName.EndsWith("LV", StringComparison.OrdinalIgnoreCase),
                ResolveFormatString(dataType));
        }

        private static ReportColumnDefinition CreateColumn(DataColumn column, ReportTableLayoutCell cell)
        {
            var dataType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
            var alignment = ResolveTextAlignment(cell.Alignment, dataType);

            return new ReportColumnDefinition(
                column.ColumnName,
                cell.Caption,
                ResolveTableCellWeight(cell),
                alignment,
                false,
                ResolveFormatString(cell.FormatType, dataType));
        }

        private static IReadOnlyList<float> ResolveTableColumnWeights(IReadOnlyList<ReportTableLayoutCell> cells, int columnCount)
        {
            var weights = new float[columnCount];
            foreach (var cell in cells.Where(item => item.ColSpan == 1 && item.Width > 0f))
            {
                if (cell.ColIndex >= 0 && cell.ColIndex < columnCount)
                {
                    weights[cell.ColIndex] = Math.Max(weights[cell.ColIndex], cell.Width);
                }
            }

            foreach (var cell in cells.Where(item => item.ColSpan > 1 && item.Width > 0f))
            {
                var start = Math.Max(0, cell.ColIndex);
                var end = Math.Min(columnCount, cell.ColIndex + cell.ColSpan);
                if (start >= end)
                {
                    continue;
                }

                var knownWeight = 0f;
                var missingIndexes = new List<int>();
                for (var index = start; index < end; index++)
                {
                    if (weights[index] > 0f)
                    {
                        knownWeight += weights[index];
                    }
                    else
                    {
                        missingIndexes.Add(index);
                    }
                }

                if (missingIndexes.Count == end - start)
                {
                    var distributedWeight = cell.Width / Math.Max(1, end - start);
                    foreach (var index in missingIndexes)
                    {
                        weights[index] = distributedWeight;
                    }
                    continue;
                }

                if (missingIndexes.Count > 0)
                {
                    var distributedWeight = Math.Max(0f, cell.Width - knownWeight) / missingIndexes.Count;
                    foreach (var index in missingIndexes)
                    {
                        weights[index] = distributedWeight > 0f ? distributedWeight : cell.Width / Math.Max(1, end - start);
                    }
                }
            }

            for (var index = 0; index < weights.Length; index++)
            {
                if (weights[index] <= 0f)
                {
                    weights[index] = 1f;
                }
            }

            return weights;
        }

        private static ReportTableLayoutCell? CreateTableLayoutCell(DataRow row, string reportLanguage)
        {
            var columnKey = ReadText(row, "COLUMN_KEY");
            if (columnKey == null)
            {
                return null;
            }

            var labelText = ReadText(row, "LABEL_TEXT");
            var fieldName = ReadText(row, "FIELD_NAME");

            return new ReportTableLayoutCell(
                columnKey,
                ReadText(row, "PARENT_KEY"),
                fieldName,
                ReportLanguageHelper.ResolveDisplayLabel(labelText, reportLanguage),
                ReadInt(row, "ROW_INDEX", 0),
                ReadInt(row, "COL_INDEX", 0),
                Math.Max(1, ReadInt(row, "COL_SPAN", 1)),
                Math.Max(1, ReadInt(row, "ROW_SPAN", 1)),
                ReadFloat(row, "WIDTH", 0f),
                ReadText(row, "ALIGN"),
                ReadText(row, "FORMAT_TYPE"),
                ReadInt(row, "SORT_ORDER", 0));
        }

        private static string? ReadText(DataRow row, string columnName)
        {
            var resolvedColumn = ResolveColumn(row.Table, columnName);
            return resolvedColumn == null ? null : Common.NormalizeNullableText(row[resolvedColumn]?.ToString());
        }

        private static int ReadInt(DataRow row, string columnName, int fallback)
        {
            var value = ReadText(row, columnName);
            return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : fallback;
        }

        private static float ReadFloat(DataRow row, string columnName, float fallback)
        {
            var value = ReadText(row, columnName);
            return float.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : fallback;
        }

        private static bool HasColumn(DataTable table, string columnName)
        {
            return ResolveColumn(table, columnName) != null;
        }

        private static string? ResolveColumn(DataTable table, string columnName)
        {
            var exact = table.Columns.Cast<DataColumn>()
                .FirstOrDefault(column => column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                ?.ColumnName;
            if (exact != null)
            {
                return exact;
            }

            var token = Common.NormalizeToken(columnName);
            if (token.Length == 0)
            {
                return null;
            }

            var candidates = new List<string> { token };
            if (token.StartsWith("p", StringComparison.Ordinal) && token.Length > 1)
            {
                candidates.Add(token[1..]);
            }

            foreach (var candidate in candidates)
            {
                var match = table.Columns.Cast<DataColumn>()
                    .FirstOrDefault(column => Common.NormalizeToken(column.ColumnName) == candidate);
                if (match != null)
                {
                    return match.ColumnName;
                }
            }

            return null;
        }

        private static float CalculateWeight(string columnName, Type dataType)
        {
            var upper = columnName.ToUpperInvariant();
            if (dataType == typeof(DateTime))
            {
                return 1.2f;
            }

            if (IsNumericType(dataType))
            {
                return 1f;
            }

            if (upper.Contains("ADDRESS") || upper.Contains("NOTE") || upper.Contains("REMARK") || upper.Contains("SUMMARY"))
            {
                return 2f;
            }

            if (upper.Contains("NAME") || upper.Contains("_NM") || upper.Contains("DESC"))
            {
                return 1.6f;
            }

            if (columnName.Length > 36)
            {
                return 3f;
            }

            if (columnName.Length > 24)
            {
                return 2.2f;
            }

            if (columnName.Length > 16)
            {
                return 1.6f;
            }

            return 1.2f;
        }

        private static string? ResolveFormatString(Type dataType)
        {
            if (dataType == typeof(DateTime))
            {
                return "{0:yyyy-MM-dd HH:mm}";
            }

            if (dataType == typeof(decimal) ||
                dataType == typeof(double) ||
                dataType == typeof(float))
            {
                return "{0:n2}";
            }

            if (dataType == typeof(int) ||
                dataType == typeof(long) ||
                dataType == typeof(short) ||
                dataType == typeof(byte))
            {
                return "{0:n0}";
            }

            return null;
        }

        private static string? ResolveFormatString(string? formatType, Type dataType)
        {
            return Common.NormalizeToken(formatType) switch
            {
                "number0" or "integer" => "{0:n0}",
                "number1" => "{0:n1}",
                "number2" or "amount" or "quantity" or "unitprice" => "{0:n2}",
                "number3" => "{0:n3}",
                "number4" => "{0:n4}",
                "date" => "{0:dd/MM/yyyy}",
                "datetime" => "{0:dd/MM/yyyy HH:mm}",
                _ => ResolveFormatString(dataType)
            };
        }

        private static TextAlignment ResolveWrappedDataCellAlignment(TextAlignment alignment)
        {
            return alignment switch
            {
                TextAlignment.TopLeft or TextAlignment.MiddleLeft or TextAlignment.BottomLeft => TextAlignment.MiddleLeft,
                TextAlignment.TopCenter or TextAlignment.MiddleCenter or TextAlignment.BottomCenter => TextAlignment.MiddleCenter,
                TextAlignment.TopRight or TextAlignment.MiddleRight or TextAlignment.BottomRight => TextAlignment.MiddleRight,
                _ => TextAlignment.MiddleLeft
            };
        }

        private static TextAlignment ResolveTextAlignment(string? alignment, Type dataType)
        {
            return Common.NormalizeToken(alignment) switch
            {
                "left" or "middleleft" => TextAlignment.MiddleLeft,
                "center" or "middlecenter" => TextAlignment.MiddleCenter,
                "right" or "middleright" => TextAlignment.MiddleRight,
                "topleft" => TextAlignment.TopLeft,
                "topcenter" => TextAlignment.TopCenter,
                "topright" => TextAlignment.TopRight,
                "bottomleft" => TextAlignment.BottomLeft,
                "bottomcenter" => TextAlignment.BottomCenter,
                "bottomright" => TextAlignment.BottomRight,
                _ => IsNumericType(dataType) ? TextAlignment.MiddleRight : dataType == typeof(DateTime) ? TextAlignment.MiddleCenter : TextAlignment.MiddleLeft
            };
        }

        private static bool IsNumericType(Type dataType)
        {
            return dataType == typeof(decimal) ||
                   dataType == typeof(double) ||
                   dataType == typeof(float) ||
                   dataType == typeof(int) ||
                   dataType == typeof(long) ||
                   dataType == typeof(short) ||
                   dataType == typeof(byte);
        }

        private string ResolveReportTitle(ReportConfigurationInfo configuration)
        {
            return ReportLanguageHelper.ResolveDisplayLabel(configuration.LABEL_TEXT, _reportLanguage);
        }

        private static string ResolveColumnCaption(DataColumn column, string reportLanguage)
        {
            var configuredCaption = Common.NormalizeNullableText(column.Caption);
            if (configuredCaption != null && !configuredCaption.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))
            {
                return ReportLanguageHelper.LocalizeLabel(configuredCaption, reportLanguage);
            }

            var translationKey = ReportLanguageHelper.RemoveLanguageSuffix(column.ColumnName);
            return ReportLanguageHelper.LocalizeLabel(translationKey.Length == 0 ? column.ColumnName : translationKey, reportLanguage);
        }

        private string ResolveSignatureDisplayLabel(CompanyReportSignatureInfo signature)
        {
            var displayLabel = Common.NormalizeNullableText(signature.DISPLAY_LABEL);
            if (displayLabel != null)
            {
                var translatedLabel = ReportLanguageHelper.LocalizeLabel(displayLabel, _reportLanguage);
                if (!string.Equals(translatedLabel, displayLabel, StringComparison.OrdinalIgnoreCase))
                {
                    return translatedLabel;
                }
            }

            return FirstNonEmpty(signature.DISPLAY_LABEL, signature.SIGN_CODE) ?? string.Empty;
        }

        private string ResolveSignatureTitle(CompanyReportSignatureInfo signature)
        {
            var signTitle = Common.NormalizeNullableText(signature.SIGN_TITLE);
            if (signTitle == null)
            {
                return string.Empty;
            }

            var translatedTitle = ReportLanguageHelper.LocalizeLabel(signTitle, _reportLanguage);
            return string.IsNullOrWhiteSpace(translatedTitle) ? signTitle : translatedTitle;
        }

        private static ReportLayoutSettings ResolveLayout(ReportConfigurationInfo configuration, int columnCount)
        {
            var fontFamily = Common.NormalizeNullableText(configuration.FONT_FAMILY) ?? DefaultFontFamily;
            var baseFontSize = ResolveFontSize(configuration.FONT_SIZE, DefaultBaseFontSize);

            return new ReportLayoutSettings(
                fontFamily,
                baseFontSize,
                ResolveFontSize(configuration.TITLE_FONT_SIZE, DefaultTitleFontSize),
                ResolveFontSize(configuration.INFO_FONT_SIZE, DefaultInfoFontSize),
                ResolveFontSize(configuration.HEADER_FONT_SIZE, baseFontSize),
                ResolveFontSize(configuration.DETAIL_FONT_SIZE, baseFontSize),
                ResolveFontSize(configuration.FOOTER_FONT_SIZE, DefaultFooterFontSize),
                ResolveMargin(configuration.MARGIN_LEFT, DefaultMarginLeft),
                ResolveMargin(configuration.MARGIN_RIGHT, DefaultMarginRight),
                ResolveMargin(configuration.MARGIN_TOP, DefaultMarginTop),
                ResolveMargin(configuration.MARGIN_BOTTOM, DefaultMarginBottom),
                ResolvePaperKind(configuration.PAPER_KIND, columnCount),
                ResolveLandscape(configuration.PAGE_ORIENTATION, columnCount));
        }

        private static float ResolveFontSize(decimal? value, float fallback)
        {
            if (!value.HasValue)
            {
                return fallback;
            }

            var resolved = (float)value.Value;
            return resolved > 0f ? resolved : fallback;
        }

        private static int ResolveMargin(int? value, int fallback)
        {
            return value.HasValue && value.Value >= 0 ? value.Value : fallback;
        }

        private static bool ResolveLandscape(string? value, int columnCount)
        {
            return Common.NormalizeToken(value) switch
            {
                "landscape" or "horizontal" or "ngang" => true,
                "portrait" or "vertical" or "doc" or "dung" => false,
                _ => columnCount > 8
            };
        }

        private static DXPaperKind ResolvePaperKind(string? value, int columnCount)
        {
            var normalized = Common.NormalizeToken(value);
            if (normalized.Length == 0 || normalized == "auto")
            {
                return columnCount > 8 ? DXPaperKind.A3 : DXPaperKind.A4;
            }

            foreach (var paperKind in Enum.GetValues<DXPaperKind>())
            {
                if (Common.NormalizeToken(paperKind.ToString()) == normalized)
                {
                    return paperKind;
                }
            }

            return columnCount > 8 ? DXPaperKind.A3 : DXPaperKind.A4;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                var normalized = Common.NormalizeNullableText(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        private static SizeF MeasureGdiTextSizeHundredths(string text, DXFont font, float maxWidthHundredths, bool typographic)
        {
            if (string.IsNullOrEmpty(text))
            {
                return SizeF.Empty;
            }

            try
            {
                var style = FontStyle.Regular;
                if (font.Style.HasFlag(DXFontStyle.Bold))
                {
                    style |= FontStyle.Bold;
                }

                if (font.Style.HasFlag(DXFontStyle.Italic))
                {
                    style |= FontStyle.Italic;
                }

                using var gdiFont = new Font(font.Name, font.Size, style, GraphicsUnit.Point);
                using var bmp = new Bitmap(1, 1);
                using var g = Graphics.FromImage(bmp);
                g.PageUnit = GraphicsUnit.Point;
                var format = typographic ? StringFormat.GenericTypographic : StringFormat.GenericDefault;
                var maxWidthPoints = Math.Max(1f, maxWidthHundredths * 72f / 100f);
                var size = g.MeasureString(text, gdiFont, new SizeF(maxWidthPoints, 8000f), format);
                return new SizeF(size.Width * 100f / 72f, size.Height * 100f / 72f);
            }
            catch
            {
                return SizeF.Empty;
            }
        }

        private static float MeasureGdiTextWidthHundredths(string text, DXFont font, bool typographic)
        {
            return MeasureGdiTextSizeHundredths(text, font, 10000f, typographic).Width;
        }

        private static DXFont CreateFont(string fontFamily, float fontSize, DXFontStyle style = DXFontStyle.Regular)
        {
            try
            {
                return new DXFont(fontFamily, fontSize, style);
            }
            catch
            {
                return new DXFont(DefaultFontFamily, fontSize, style);
            }
        }

        private void InitializeComponent()
        {
            this.topMarginBand1 = new DevExpress.XtraReports.UI.TopMarginBand();
            this.detailBand1 = new DevExpress.XtraReports.UI.DetailBand();
            this.bottomMarginBand1 = new DevExpress.XtraReports.UI.BottomMarginBand();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // topMarginBand1
            // 
            this.topMarginBand1.Name = "topMarginBand1";
            // 
            // detailBand1
            // 
            this.detailBand1.Name = "detailBand1";
            // 
            // bottomMarginBand1
            // 
            this.bottomMarginBand1.Name = "bottomMarginBand1";
            // 
            // DynamicConfiguredReport
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.topMarginBand1,
            this.detailBand1,
            this.bottomMarginBand1});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();

        }

        private sealed record ReportColumnDefinition(string Name, string Caption, float Weight, TextAlignment Alignment, bool AllowSummary, string? FormatString);
        private sealed record ReportTableLayout(IReadOnlyList<ReportTableLayoutCell> HeaderCells, IReadOnlyList<ReportColumnDefinition> DetailColumns, int HeaderRowCount, int ColumnCount, IReadOnlyList<float> ColumnWeights);
        private sealed record ReportTableLayoutCell(string ColumnKey, string? ParentKey, string? FieldName, string Caption, int RowIndex, int ColIndex, int ColSpan, int RowSpan, float Width, string? Alignment, string? FormatType, int SortOrder);
        private sealed record ReportLayoutSettings(string FontFamily, float BaseFontSize, float TitleFontSize, float InfoFontSize, float HeaderFontSize, float DetailFontSize, float FooterFontSize, int MarginLeft, int MarginRight, int MarginTop, int MarginBottom, DXPaperKind PaperKind, bool Landscape);
    }
}
