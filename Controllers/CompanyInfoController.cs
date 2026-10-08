using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Threading.Tasks;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Controllers
{
    public class CompanyInfoController : BaseApiController
    {
        private const string MenuCode = "MD_COMPANY";
        private readonly ICompanyInfoRepository _repository;
        private readonly ILogger<CompanyInfoController> _logger;

        public CompanyInfoController(ICompanyInfoRepository repository, ILogger<CompanyInfoController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        [HttpGet("Get")]
        [Authorize]
        public async Task<IActionResult> GetCompanyInfo([FromQuery] string? COMPANY_CD = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var companyCd = string.IsNullOrWhiteSpace(COMPANY_CD) ? GetCompanyCode() : COMPANY_CD.Trim();
            var companyInfo = await _repository.GetCompanyInfoAsync(companyCd);

            return Success(new
            {
                data = companyInfo == null
                    ? CreateEmptyCompanyInfoDto(companyCd)
                    : MapCompanyInfoDto(companyInfo),
                exists = companyInfo != null
            });
        }

        [HttpPut]
        [HttpPut("Update")]
        [Authorize]
        public async Task<IActionResult> UpdateCompanyInfo([FromBody] CompanyInfoRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (request == null)
            {
                return ValidationError("Request is required");
            }

            var companyCd = string.IsNullOrWhiteSpace(request.COMPANY_CD) ? GetCompanyCode() : request.COMPANY_CD.Trim();
            var existing = await _repository.GetCompanyInfoAsync(companyCd);
            var upsertRequest = BuildUpsertRequest(existing, request, companyCd);

            if (!string.IsNullOrWhiteSpace(upsertRequest.CARRYFORWARD_YMD))
            {
                if (!DateTime.TryParseExact(upsertRequest.CARRYFORWARD_YMD.Trim(),
                    new[] { "yyyyMMdd", "yyyy-MM-dd" }, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var carryforwardDate))
                {
                    return ValidationError("CARRYFORWARD_YMD must be a valid date in yyyyMMdd format");
                }

                upsertRequest.CARRYFORWARD_YMD = carryforwardDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(upsertRequest.OPEN_YMD))
            {
                if (!DateTime.TryParseExact(upsertRequest.OPEN_YMD.Trim(),
                    new[] { "yyyyMMdd", "yyyy-MM-dd" }, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var openDate))
                {
                    return ValidationError("OPEN_YMD must be a valid date in yyyyMMdd format");
                }

                upsertRequest.OPEN_YMD = openDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }

            if (string.IsNullOrWhiteSpace(upsertRequest.COMPANY_NM))
            {
                return ValidationError("COMPANY_NM is required");
            }

            var result = await _repository.UpsertCompanyInfoAsync(companyCd, upsertRequest, existing);
            if (result < 0)
            {
                return ServerError("Company info update failed");
            }

            var updated = await _repository.GetCompanyInfoAsync(companyCd);
            if (updated == null)
            {
                return ServerError("Company info update failed");
            }

            var mapped = MapCompanyInfoDto(updated);
            if (existing == null)
            {
                return Created(mapped, "Company info created successfully");
            }

            return Updated(mapped, "Company info updated successfully");
        }

        private static CompanyInfoDto MapCompanyInfoDto(CompanyInfo companyInfo)
        {
            return new CompanyInfoDto
            {
                COMPANY_CD = companyInfo.COMPANY_CD,
                COMPANY_NM = companyInfo.COMPANY_NM,
                COMPANY_NM_EN = companyInfo.COMPANY_NM_EN,
                COMPANY_NM_KOR = companyInfo.COMPANY_NM_KOR,
                COMPANY_TYPE = companyInfo.COMPANY_TYPE,
                COMPANY_KIND = companyInfo.COMPANY_KIND,
                DE_COMPANY_CD = companyInfo.DE_COMPANY_CD,
                COMPANY_LV = companyInfo.COMPANY_LV,
                ACCDATE_CD = companyInfo.ACCDATE_CD,
                TAX_CD = companyInfo.TAX_CD,
                CCCDan = companyInfo.CCCDan,
                TCQTQLy = companyInfo.TCQTQLy,
                MCQTQLy = companyInfo.MCQTQLy,
                BRN = companyInfo.BRN,
                CRN = companyInfo.CRN,
                OWNER_NM = companyInfo.OWNER_NM,
                ZIP_CODE = companyInfo.ZIP_CODE,
                ADDRESS_DO = companyInfo.ADDRESS_DO,
                ADDRESS = companyInfo.ADDRESS,
                ADDRESS_ENG = companyInfo.ADDRESS_ENG,
                ADDRESS_KOR = companyInfo.ADDRESS_KOR,
                CARRYFORWARD_YMD = companyInfo.CARRYFORWARD_YMD,
                SIDO = companyInfo.SIDO,
                GUMYUN = companyInfo.GUMYUN,
                BUSINESS_TYPE = companyInfo.BUSINESS_TYPE,
                KIND_BUSINESS = companyInfo.KIND_BUSINESS,
                TEL = companyInfo.TEL,
                EMAIL = companyInfo.EMAIL,
                WEBSITE = companyInfo.WEBSITE,
                FAX = companyInfo.FAX,
                STOCKCALC_TYPE = companyInfo.STOCKCALC_TYPE,
                OPEN_YMD = companyInfo.OPEN_YMD,
                DECISION = companyInfo.DECISION,
                NOTE = companyInfo.NOTE
            };
        }

        private static CompanyInfoDto CreateEmptyCompanyInfoDto(string companyCd)
        {
            return new CompanyInfoDto
            {
                COMPANY_CD = companyCd
            };
        }

        private static CompanyInfoRequest BuildUpsertRequest(CompanyInfo? existing, CompanyInfoRequest request, string companyCd)
        {
            return new CompanyInfoRequest
            {
                COMPANY_CD = companyCd,
                DB_GROUP_ID = request.DB_GROUP_ID ?? existing?.DB_GROUP_ID,
                COMPANY_NM = request.COMPANY_NM == null ? existing?.COMPANY_NM : NormalizeRequiredText(request.COMPANY_NM),
                COMPANY_NM_EN = request.COMPANY_NM_EN == null ? existing?.COMPANY_NM_EN : NormalizeNullableText(request.COMPANY_NM_EN),
                COMPANY_NM_KOR = request.COMPANY_NM_KOR == null ? existing?.COMPANY_NM_KOR : NormalizeNullableText(request.COMPANY_NM_KOR),
                COMPANY_TYPE = request.COMPANY_TYPE ?? existing?.COMPANY_TYPE,
                COMPANY_KIND = request.COMPANY_KIND ?? existing?.COMPANY_KIND,
                DE_COMPANY_CD = request.DE_COMPANY_CD == null ? existing?.DE_COMPANY_CD : NormalizeNullableText(request.DE_COMPANY_CD),
                COMPANY_LV = request.COMPANY_LV == null ? existing?.COMPANY_LV : NormalizeNullableText(request.COMPANY_LV),
                ACCDATE_CD = request.ACCDATE_CD == null ? existing?.ACCDATE_CD : NormalizeNullableText(request.ACCDATE_CD),
                TAX_CD = request.TAX_CD == null ? existing?.TAX_CD : NormalizeNullableText(request.TAX_CD),
                CCCDan = request.CCCDan == null ? existing?.CCCDan : NormalizeNullableText(request.CCCDan),
                TCQTQLy = request.TCQTQLy == null ? existing?.TCQTQLy : NormalizeNullableText(request.TCQTQLy),
                MCQTQLy = request.MCQTQLy == null ? existing?.MCQTQLy : NormalizeNullableText(request.MCQTQLy),
                BRN = request.BRN == null ? existing?.BRN : NormalizeNullableText(request.BRN),
                CRN = request.CRN == null ? existing?.CRN : NormalizeNullableText(request.CRN),
                OWNER_NM = request.OWNER_NM == null ? existing?.OWNER_NM : NormalizeNullableText(request.OWNER_NM),
                ZIP_CODE = request.ZIP_CODE == null ? existing?.ZIP_CODE : NormalizeNullableText(request.ZIP_CODE),
                ADDRESS_DO = request.ADDRESS_DO == null ? existing?.ADDRESS_DO : NormalizeNullableText(request.ADDRESS_DO),
                ADDRESS = request.ADDRESS == null ? existing?.ADDRESS : NormalizeNullableText(request.ADDRESS),
                ADDRESS_ENG = request.ADDRESS_ENG == null ? existing?.ADDRESS_ENG : NormalizeNullableText(request.ADDRESS_ENG),
                ADDRESS_KOR = request.ADDRESS_KOR == null ? existing?.ADDRESS_KOR : NormalizeNullableText(request.ADDRESS_KOR),
                CARRYFORWARD_YMD = request.CARRYFORWARD_YMD == null ? existing?.CARRYFORWARD_YMD : NormalizeNullableText(request.CARRYFORWARD_YMD),
                SIDO = request.SIDO == null ? existing?.SIDO : NormalizeNullableText(request.SIDO),
                GUMYUN = request.GUMYUN == null ? existing?.GUMYUN : NormalizeNullableText(request.GUMYUN),
                BUSINESS_TYPE = request.BUSINESS_TYPE == null ? existing?.BUSINESS_TYPE : NormalizeNullableText(request.BUSINESS_TYPE),
                KIND_BUSINESS = request.KIND_BUSINESS == null ? existing?.KIND_BUSINESS : NormalizeNullableText(request.KIND_BUSINESS),
                TEL = request.TEL == null ? existing?.TEL : NormalizeNullableText(request.TEL),
                EMAIL = request.EMAIL == null ? existing?.EMAIL : NormalizeNullableText(request.EMAIL),
                WEBSITE = request.WEBSITE == null ? existing?.WEBSITE : NormalizeNullableText(request.WEBSITE),
                FAX = request.FAX == null ? existing?.FAX : NormalizeNullableText(request.FAX),
                STOCKCALC_TYPE = request.STOCKCALC_TYPE == null ? existing?.STOCKCALC_TYPE : NormalizeNullableText(request.STOCKCALC_TYPE),
                OPEN_YMD = request.OPEN_YMD == null ? existing?.OPEN_YMD : NormalizeNullableText(request.OPEN_YMD),
                DECISION = request.DECISION == null ? existing?.DECISION : NormalizeNullableText(request.DECISION),
                REG_YMD = request.REG_YMD ?? existing?.REG_YMD,
                ISDEL = request.ISDEL == null ? existing?.ISDEL ?? "0" : NormalizeFlagString(request.ISDEL, "0"),
                NOTE = request.NOTE == null ? existing?.NOTE : NormalizeNullableText(request.NOTE)
            };
        }
    }
}
