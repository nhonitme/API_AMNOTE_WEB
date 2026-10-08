using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services.Lookup
{
    public class LookupService : ILookupService
    {
        private const string GetCountriesQuery = @"
            SELECT COUNTRY_ID, COUNTRY_CD, COUNTRY_NM
            FROM country_info
            ORDER BY COUNTRY_NM, COUNTRY_CD";

        private readonly IBankInfoService _bankService;
        private readonly ICustomerInfoService _customerService;
        private readonly IDepartmentInfoService _departmentService;
        private readonly IManagementInfoService _managementService;
        private readonly IStoreInfoService _storeService;
        private readonly IStoreKindInfoService _storeKindService;
        private readonly IProductInfoService _productService;
        private readonly IProductKindService _productKindService;
        private readonly IProductUnitService _productUnitService;
        private readonly IAcclistInfoService _acclistService;
        private readonly DapperExecutor _db;
        private readonly ILogger<LookupService> _logger;


        public LookupService(
            IBankInfoService bankService,
            ICustomerInfoService customerService,
            IDepartmentInfoService departmentService,
            IManagementInfoService managementService,
            IStoreInfoService storeService,
            IStoreKindInfoService storeKindService,
            IProductInfoService productService,
            IProductKindService productKindService,
            IProductUnitService productUnitService,
            IAcclistInfoService acclistService,
            DapperExecutor db,
            ILogger<LookupService> logger)
        {
            _bankService = bankService;
            _customerService = customerService;
            _departmentService = departmentService;
            _managementService = managementService;
            _storeService = storeService;
            _storeKindService = storeKindService;
            _productService = productService;
            _productKindService = productKindService;
            _productUnitService = productUnitService;
            _acclistService = acclistService;
            _db = db;
            _logger = logger;
        }
        public async Task<IEnumerable<BankLookupDto>> GetBanksAsync(string companyCd)
        {
            var rows = await _bankService.GetListAsync(companyCd);
            return rows.Select(x => new BankLookupDto
            {
                BANK_ID = x.BANK_ID,
                BANK_CD = x.BANK_CD ?? string.Empty,
                BANK_NM = x.BANK_NM ?? string.Empty,
                ACC_CD = x.ACC_CD ?? string.Empty,
                PASSBOOK_NM = x.PASSBOOK_NM ?? string.Empty,
                ACCOUNT_NUM = x.ACCOUNT_NUM ?? string.Empty,
                CITAD_CODE = x.CITAD_CODE ?? string.Empty,
            });
        }

        public async Task<IEnumerable<CustomerLookupDto>> GetCustomersAsync(string companyCd)
        {
            var rows = await _customerService.GetListAsync(companyCd);
            return rows.Select(x => new CustomerLookupDto
            {
                CUSTOMER_ID = x.CUSTOMER_ID,
                CUSTOMER_CD = x.CUSTOMER_CD ?? string.Empty,
                CUSTOMER_NM_VIET = x.CUSTOMER_NM_VIET ?? string.Empty,
                CUSTOMER_NM_ENG = x.CUSTOMER_NM_ENG ?? string.Empty,
                CUSTOMER_NM_KOR = x.CUSTOMER_NM_KOR ?? string.Empty,
                CUSTOMER_NM_CHINA = x.CUSTOMER_NM_CHINA ?? string.Empty,
                TAX_CD = x.TAX_CD ?? string.Empty,
                ADDRESS = x.ADDRESS ?? string.Empty,
            });
        }

        public async Task<IEnumerable<DepartmentLookupDto>> GetDepartmentsAsync(string companyCd)
        {
            var rows = await _departmentService.GetListAsync(companyCd);
            return rows.Select(x => new DepartmentLookupDto
            {
                DEPARTMENT_ID = x.DEPARTMENT_ID,
                DEPARTMENT_CD = x.DEPARTMENT_CD ?? string.Empty,
                DEP_NAME_VIET = x.DEP_NAME_VIET ?? string.Empty,
                DEP_NAME_ENG = x.DEP_NAME_ENG ?? string.Empty,
                DEP_NAME_KOR = x.DEP_NAME_KOR ?? string.Empty,
                DEP_NAME_CHINA = x.DEP_NAME_CHINA ?? string.Empty,
            });
        }

        public async Task<IEnumerable<ManagementLookupDto>> GetManagementsAsync(string companyCd)
        {
            var rows = await _managementService.GetListAsync(companyCd);
            return rows.Select(x => new ManagementLookupDto
            {
                MG_ID = x.MG_ID,
                MG_CD = x.MG_CD ?? string.Empty,
                MG_DESC_VIET = x.MG_DESC_VIET ?? string.Empty,
                MG_DESC_ENG = x.MG_DESC_ENG ?? string.Empty,
                MG_DESC_KOR = x.MG_DESC_KOR ?? string.Empty,
                MG_CD_ROOT = x.MG_CD_ROOT ?? string.Empty,
            });
        }

        public async Task<IEnumerable<StoreLookupDto>> GetStoresAsync(string companyCd)
        {
            var rows = await _storeService.GetListAsync(companyCd);
            return rows.Select(x => new StoreLookupDto
            {
                STORE_ID = x.STORE_ID,
                STORE_CD = x.STORE_CD ?? string.Empty,
                STORE_NM_VIET = x.STORE_NM_VIET ?? string.Empty,
                STORE_NM_ENG = x.STORE_NM_ENG ?? string.Empty,
                STORE_NM_KOR = x.STORE_NM_KOR ?? string.Empty,
                STORE_KIND_ID = x.STORE_KIND_ID,
                STORE_KIND_CD = x.STORE_KIND_CD ?? string.Empty,
                STORE_KIND_NM_VIET = x.STORE_KIND_NM_VIET ?? string.Empty,
            });
        }

        public async Task<IEnumerable<StoreKindLookupDto>> GetStoreKindsAsync(string companyCd)
        {
            var rows = await _storeKindService.GetListAsync(companyCd);
            return rows.Select(x => new StoreKindLookupDto
            {
                STORE_KIND_ID = x.STORE_KIND_ID,
                STORE_KIND_CD = x.STORE_KIND_CD ?? string.Empty,
                STORE_KIND_NM_VIET = x.STORE_KIND_NM_VIET ?? string.Empty,
                STORE_KIND_NM_ENG = x.STORE_KIND_NM_ENG ?? string.Empty,
                STORE_KIND_NM_KOR = x.STORE_KIND_NM_KOR ?? string.Empty,
            });
        }

        public async Task<IEnumerable<ProductLookupDto>> GetProductsAsync(string companyCd)
        {
            var rows = await _productService.GetListAsync(companyCd);
            return rows.Select(x => new ProductLookupDto
            {
                PRODUCT_ID = x.PRODUCT_ID,
                PRODUCT_CD = x.PRODUCT_CD ?? string.Empty,
                PRODUCT_NM_VIET = x.PRODUCT_NM_VIET ?? string.Empty,
                PRODUCT_NM_ENG = x.PRODUCT_NM_ENG ?? string.Empty,
                PRODUCT_NM_KOR = x.PRODUCT_NM_KOR ?? string.Empty,
                PRODUCT_NM_CHINA = x.PRODUCT_NM_CHINA ?? string.Empty,
                PRODUCT_KIND_ID = x.PRODUCT_KIND_ID ?? 0,
                PRODUCT_KIND_CD = x.PRODUCT_KIND_CD ?? string.Empty,
                PRODUCTKIND_NM_VIET = x.PRODUCTKIND_NM_VIET ?? string.Empty,
                UNIT_ID = x.UNIT_ID ?? 0,
                UNIT_CD = x.UNIT_CD ?? string.Empty,
                UNIT_NM = x.UNIT_NM ?? string.Empty,
                STORE_ID = x.STORE_ID ?? 0,
                STORE_CD = x.STORE_CD ?? string.Empty,
                STORE_NM_VIET = x.STORE_NM_VIET ?? string.Empty,
                DIVISION = x.DIVISION ?? string.Empty,
            });
        }

        public async Task<IEnumerable<ProductKindLookupDto>> GetProductKindsAsync(string companyCd)
        {
            var rows = await _productKindService.GetListAsync(companyCd);
            return rows.Select(x => new ProductKindLookupDto
            {
                PRODUCT_KIND_ID = x.PRODUCT_KIND_ID,
                PRODUCT_KIND_CD = x.PRODUCT_KIND_CD ?? string.Empty,
                PRODUCTKIND_NM_VIET = x.PRODUCTKIND_NM_VIET ?? string.Empty,
                PRODUCTKIND_NM_ENG = x.PRODUCTKIND_NM_ENG ?? string.Empty,
                PRODUCTKIND_NM_KOR = x.PRODUCTKIND_NM_KOR ?? string.Empty,
                PRODUCTKIND_NM_CHINA = x.PRODUCTKIND_NM_CHINA ?? string.Empty,
            });
        }

        public async Task<IEnumerable<UnitLookupDto>> GetUnitsAsync(string companyCd)
        {
            var rows = await _productUnitService.GetListAsync(companyCd);
            return rows.Select(x => new UnitLookupDto
            {
                UNIT_ID = x.UNIT_ID,
                UNIT_CD = x.UNIT_CD ?? string.Empty,
                UNIT_NM = x.UNIT_NM ?? string.Empty,
            });
        }

        public async Task<IEnumerable<CountryLookupDto>> GetCountriesAsync()
        {
            var rows = await _db.QueryAsync<CountryLookupDto>(Net_DB.Net_DB_Manager, GetCountriesQuery);
            return rows.Select(x => new CountryLookupDto
            {
                COUNTRY_ID = x.COUNTRY_ID,
                COUNTRY_CD = (x.COUNTRY_CD ?? string.Empty).Trim().ToUpperInvariant(),
                COUNTRY_NM = (x.COUNTRY_NM ?? string.Empty).Trim(),
            });
        }

        public async Task<bool> CheckExistsAsync(string type, string code, long? currentId, string companyCd)
        {
            var intId = currentId.HasValue ? (int?)((int)currentId.Value) : null;
            return type.ToLowerInvariant() switch
            {
                "bank" => await _bankService.CodeExistsAsync(companyCd, code, currentId),
                "customer" => await _customerService.CodeExistsAsync(companyCd, code, currentId),
                "department" => await _departmentService.CodeExistsAsync(companyCd, code, currentId),
                "management" => await _managementService.CodeExistsAsync(companyCd, code, currentId),
                "account" => await _acclistService.CodeExistsAsync(companyCd, code, intId),
                "product" => await _productService.CodeExistsAsync(companyCd, code, intId),
                "product-kind" => await _productKindService.CodeExistsAsync(companyCd, code, intId),
                "unit" => await _productUnitService.CodeExistsAsync(companyCd, code, intId),
                "store" => await _storeService.CodeExistsAsync(companyCd, code, intId),
                "store-kind" => await _storeKindService.CodeExistsAsync(companyCd, code, intId),
                _ => throw new ArgumentException($"Unsupported lookup type: {type}")
            };
        }
    }
}
