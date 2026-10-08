using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Services.Lookup;
using API_AMNOTE_WEB.Services.TaxLookup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Authorize]
    public class LookupController : BaseApiController
    {
        private readonly ILookupService _lookupService;
        private readonly ITaxLookupService _taxLookupService;

        public LookupController(ILookupService lookupService, ITaxLookupService taxLookupService)
        {
            _lookupService = lookupService;
            _taxLookupService = taxLookupService;
        }

        [HttpGet("banks")]
        public async Task<IActionResult> GetBanks()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetBanksAsync(companyCd);
            return Success(data);
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetCustomersAsync(companyCd);
            return Success(data);
        }

        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetDepartmentsAsync(companyCd);
            return Success(data);
        }

        [HttpGet("management")]
        public async Task<IActionResult> GetManagement()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetManagementsAsync(companyCd);
            return Success(data);
        }

        [HttpGet("stores")]
        public async Task<IActionResult> GetStores()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetStoresAsync(companyCd);
            return Success(data);
        }

        [HttpGet("store-kinds")]
        public async Task<IActionResult> GetStoreKinds()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetStoreKindsAsync(companyCd);
            return Success(data);
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetProductsAsync(companyCd);
            return Success(data);
        }

        [HttpGet("product-kinds")]
        public async Task<IActionResult> GetProductKinds()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetProductKindsAsync(companyCd);
            return Success(data);
        }

        [HttpGet("units")]
        public async Task<IActionResult> GetUnits()
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _lookupService.GetUnitsAsync(companyCd);
            return Success(data);
        }

        [HttpGet("countries")]
        public async Task<IActionResult> GetCountries()
        {
            var data = await _lookupService.GetCountriesAsync();
            return Success(data);
        }

        [HttpGet("tax-info")]
        public async Task<IActionResult> GetTaxInfo([FromQuery] string mst, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mst))
            {
                return BadRequest("MST is required.");
            }

            var data = await _taxLookupService.LookupAsync(mst, cancellationToken);
            if (data == null)
            {
                return NotFound("Tax information was not found.");
            }

            return Success(data);
        }

        [HttpGet("check-exists")]
        public async Task<IActionResult> CheckExists(
            [FromQuery] string type,
            [FromQuery] string code,
            [FromQuery] long? currentId = null)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(code))
            {
                return Success(false);
            }

            var companyCd = Common.GetCompanyCode();
            var exists = await _lookupService.CheckExistsAsync(type.Trim().ToLowerInvariant(), code.Trim(), currentId, companyCd);
            return Success(exists);
        }
    }
}
