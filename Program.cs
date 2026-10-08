using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reporting;
using API_AMNOTE_WEB.Repositories;
using API_AMNOTE_WEB.Services;
using API_AMNOTE_WEB.Services.ActivityLog;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Services.Catalog;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using API_AMNOTE_WEB.Services.BackgroundJobs.CashExchangeRevaluation;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExchangeRevaluation;
using API_AMNOTE_WEB.Services.BackgroundJobs.InventoryValuation;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;
using API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock;
using API_AMNOTE_WEB.Services.BackgroundJobs.EInvoiceMessage;
using API_AMNOTE_WEB.Services.EInvoiceGateway;

using Dapper;
using DevExpress.AspNetCore;
using DevExpress.AspNetCore.Reporting;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer.Native.Services;
using DevExpress.XtraReports.Services;
using DevExpress.XtraReports.Web.WebDocumentViewer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using API_AMNOTE_WEB.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);
EInvoiceFtpClient.Configure(builder.Configuration);


// .NET 8 default StopHost: any BackgroundService exception kills the whole API (seen as
// persist-startup every ~9s + Excel import "API đã restart").
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var requestTimeoutSeconds = builder.Configuration.GetValue("Http:RequestTimeoutSeconds", 300);
var databaseCommandTimeoutSeconds = builder.Configuration.GetValue("Database:CommandTimeoutSeconds", 300);

builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds),
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DictionaryKeyPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(API_AMNOTE_WEB.Helpers.BackendAuditAttribute.ApplyHttpContract);
        options.JsonSerializerOptions.TypeInfoResolver = resolver;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var configOrigin = builder.Configuration["Frontend:Origin"];
if (!string.IsNullOrEmpty(configOrigin))
{
    allowedOrigins.Add(configOrigin);
}

// Production web host (http + https).
allowedOrigins.Add("https://app.amnote.com.vn");
allowedOrigins.Add("http://app.amnote.com.vn");

if (builder.Environment.IsDevelopment())
{
    allowedOrigins.Add("http://localhost:5173");
    allowedOrigins.Add("https://localhost:5173");
    allowedOrigins.Add("http://localhost:5174");
    allowedOrigins.Add("https://localhost:5174");
    allowedOrigins.Add("https://localhost:7108");
}

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("frontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => IsFrontendOriginAllowed(origin, allowedOrigins, builder.Environment.IsDevelopment()))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path;
        if (path.StartsWithSegments("/swagger") || path.StartsWithSegments("/swagger/index.html") || path.StartsWithSegments("/swagger/v1"))
        {
            return RateLimitPartition.GetNoLimiter("swagger");
        }

        var ip = ClientPublicIpResolver.ResolveFromRequest(httpContext);
        if (string.IsNullOrWhiteSpace(ip))
        {
            ip = "unknown";
        }

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300, // requests
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.AddPolicy("public-einvoice-lookup", httpContext =>
    {
        var ip = ClientPublicIpResolver.ResolveFromRequest(httpContext);
        if (string.IsNullOrWhiteSpace(ip))
        {
            ip = "unknown";
        }

        return RateLimitPartition.GetFixedWindowLimiter($"public-einvoice-lookup:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync("{\"success\":false,\"message\":\"Too many requests. Please try again later.\"}", ct);
    };
});

var connStr = builder.Configuration.GetConnectionString("AMManagerConnection");
if (string.IsNullOrWhiteSpace(connStr) || connStr == "FromGlobalData")
{
    connStr = API_AMNOTE_WEB.Helpers.Common.getManagerDBConnectStr(databaseCommandTimeoutSeconds);
}

builder.Services.AddDevExpressControls();
builder.Services.ConfigureReportingServices(configurator =>
{
    configurator.UseAsyncEngine();
    configurator.ConfigureWebDocumentViewer(viewerConfigurator =>
    {
        viewerConfigurator.UseCachedReportSourceBuilder();
    });
});

builder.Services.AddSingleton<ICompanyDatabaseResolver, CompanyDatabaseResolver>();
builder.Services.AddScoped<IDapperContext, DapperContext>();
builder.Services.AddScoped<IExistenceCheckService, ExistenceCheckService>();
builder.Services.AddScoped<IMasterInUseRepository, MasterInUseRepository>();

builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

builder.Services.AddScoped<DapperExecutor>();
builder.Services.AddScoped<ILoginRepository, LoginRepository>();

builder.Services.AddScoped<IPeriodLockRepository, PeriodLockRepository>();

builder.Services.AddSingleton<IPeriodLockJobQueue, PeriodLockJobQueue>();
builder.Services.AddHostedService<PeriodLockBackgroundService>();
builder.Services.AddSingleton<CompanyKeyLocker>();

builder.Services.AddSingleton<IExcelImportJobStore, ExcelImportJobStore>();
builder.Services.AddSingleton<IExcelImportJobQueue, ExcelImportJobQueue>();
builder.Services.AddSingleton<IExcelImportJobCancellationRegistry, ExcelImportJobCancellationRegistry>();
builder.Services.AddScoped<IExcelImportJobService, ExcelImportJobService>();
builder.Services.AddScoped<IExcelImportJobProcessor, DefaultExcelImportJobProcessor>();
builder.Services.AddHostedService<ExcelImportBackgroundService>();

builder.Services.AddSingleton<InventoryValuationJobStore>();
builder.Services.AddHostedService<InventoryValuationJobWorker>();

builder.Services.AddSingleton<CashExchangeRevaluationJobStore>();
builder.Services.AddHostedService<CashExchangeRevaluationJobWorker>();

builder.Services.AddSingleton<ExchangeRevaluationJobStore>();
builder.Services.AddHostedService<ExchangeRevaluationJobWorker>();

builder.Services.AddScoped<IExcelImportModuleHandler, OpeningBalanceAccountImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, OpeningBalanceCustomerImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, OpeningBalanceBankImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, OpeningBalanceCostObjectImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, BankInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, CustomerInfoCustomerExtImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, ManagementInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, StoreKindInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, StoreInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, DepartmentInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, ProductInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, ProductKindImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, ProductUnitImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, ChitInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, InventoryVoucherImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, InventoryOpeningImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, EInvoiceInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandler, FixedAssetInfoImportHandler>();
builder.Services.AddScoped<IExcelImportModuleHandlerRegistry, ExcelImportModuleHandlerRegistry>();

builder.Services.AddScoped<IOpeningBalanceRepository, OpeningBalanceRepository>();
builder.Services.AddScoped<IFixedAssetRepository, FixedAssetRepository>();
builder.Services.AddScoped<IFixedAssetDepreciationService, FixedAssetDepreciationService>();
builder.Services.AddScoped<IAcclistInfoRepository, AcclistInfoRepository>();
builder.Services.AddScoped<IStoreInfoRepository, StoreInfoRepository>();
builder.Services.AddScoped<IStoreKindInfoRepository, StoreKindInfoRepository>();
builder.Services.AddScoped<IDepartmentInfoRepository, DepartmentInfoRepository>();
builder.Services.AddScoped<ICompanyInfoRepository, CompanyInfoRepository>();
builder.Services.AddScoped<ICompanySignatureInfoRepository, CompanySignatureInfoRepository>();
builder.Services.AddScoped<ICompanySignatureInfoService, CompanySignatureInfoService>();
builder.Services.AddScoped<ILanguageRepository, LanguageRepository>();
builder.Services.AddScoped<IMenuRepository, MenuRepository>();

builder.Services.AddScoped<IProductInfoRepository, ProductInfoRepository>();
builder.Services.AddScoped<IProductKindRepository, ProductKindRepository>();
builder.Services.AddScoped<IProductUnitRepository, ProductUnitRepository>();

builder.Services.AddScoped<IReportConfigurationRepository, ReportConfigurationRepository>();
builder.Services.AddScoped<IReportOptionRepository, ReportOptionRepository>();
builder.Services.AddScoped<ICashFlowFormulaOptionRepository, CashFlowFormulaOptionRepository>();
builder.Services.AddScoped<IReportSignatureMappingRepository, ReportSignatureMappingRepository>();
builder.Services.AddScoped<IConfiguredReportService, ConfiguredReportService>();
builder.Services.AddScoped<TaxReductionAppendixService>();
builder.Services.AddScoped<IReportProviderAsync, CustomReportProvider>();
builder.Services.AddSingleton<IWebDocumentViewerExceptionHandler, CustomWebDocumentViewerExceptionHandler>();

builder.Services.AddScoped<IBankInfoRepository, BankInfoRepository>();
builder.Services.AddScoped<IManagementInfoRepository, ManagementInfoRepository>();
builder.Services.AddScoped<ICustomerInfoCustomerExtRepository, CustomerInfoCustomerExtRepository>();
builder.Services.AddScoped<ISysCodeSequenceRepository, SysCodeSequenceRepository>();
builder.Services.AddScoped<ChitInfoRepository>();
builder.Services.AddScoped<IChitInfoRepository>(provider => provider.GetRequiredService<ChitInfoRepository>());
builder.Services.AddScoped<IInventoryVoucherRepository, InventoryVoucherRepository>();
builder.Services.AddScoped<IInventoryVoucherWriteService, API_AMNOTE_WEB.Services.Inventory.InventoryVoucherWriteService>();
builder.Services.AddScoped<IInventoryOpeningRepository, InventoryOpeningRepository>();
builder.Services.AddScoped<IInventoryLinkRepository>(provider => provider.GetRequiredService<ChitInfoRepository>());
builder.Services.AddScoped<IInventoryValuationRepository, InventoryValuationRepository>();
builder.Services.AddScoped<IInventoryValuationService, InventoryValuationService>();
builder.Services.AddScoped<IExchangeRevaluationRepository, ExchangeRevaluationRepository>();
builder.Services.AddScoped<IExchangeRevaluationService, ExchangeRevaluationService>();
builder.Services.AddScoped<ICashExchangeRevaluationRepository, CashExchangeRevaluationRepository>();
builder.Services.AddScoped<ICashExchangeRevaluationService, CashExchangeRevaluationService>();
builder.Services.AddScoped<IChitInfoWriteService, API_AMNOTE_WEB.Services.Chit.ChitInfoWriteService>();
builder.Services.AddScoped<IEInvoiceRepository, EInvoiceRepository>();
builder.Services.AddScoped<IEInvoiceSellerRepository, EInvoiceSellerRepository>();
builder.Services.AddScoped<IEInvoiceXmlStorageService, EInvoiceXmlStorageService>();
builder.Services.AddScoped<IEInvoiceMessageRepository, EInvoiceMessageRepository>();
builder.Services.AddScoped<IEInvoiceEmailHistoryRepository, EInvoiceEmailHistoryRepository>();
builder.Services.Configure<EInvoiceGatewayOptions>(builder.Configuration.GetSection("EInvoiceGateway"));
builder.Services.AddSingleton<IEInvoiceMessageWorkSignal, EInvoiceMessageWorkSignal>();
builder.Services.AddSingleton<IViettelEInvoiceTokenProvider, ViettelEInvoiceTokenProvider>();
builder.Services.AddHttpClient(ViettelEInvoiceGatewayClient.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EInvoiceGatewayOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
        ? "https://api-vinvoice.viettel.vn"
        : options.BaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri($"{baseUrl}/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient(ViettelEInvoiceGatewayClient.InboundHttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EInvoiceGatewayOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.InboundGatewayBaseUrl)
        ? "https://vinvoice.viettel.vn/api"
        : options.InboundGatewayBaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri($"{baseUrl}/");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IViettelEInvoiceGatewayClient, ViettelEInvoiceGatewayClient>();
builder.Services.AddHostedService<EInvoiceMessagePollingBackgroundService>();
builder.Services.AddScoped<API_AMNOTE_WEB.TaxWithholding.PitRepository>();
builder.Services.AddScoped<API_AMNOTE_WEB.TaxWithholding.PitIncomePayerRepository>();
builder.Services.AddScoped<API_AMNOTE_WEB.TaxWithholding.PitIncomePayerService>();
builder.Services.AddScoped<API_AMNOTE_WEB.TaxWithholding.PitXslTemplateRepository>();
builder.Services.AddScoped<API_AMNOTE_WEB.TaxWithholding.PitXslService>();
builder.Services.AddScoped<API_AMNOTE_WEB.TaxWithholding.PitMessageOutboundService>();
builder.Services.AddScoped<API_AMNOTE_WEB.Transmission.IMessageReceiveApplyHandler, API_AMNOTE_WEB.Transmission.EInvoiceMessageReceiveApplyHandler>();
builder.Services.AddScoped<API_AMNOTE_WEB.Transmission.IMessageReceiveApplyHandler, API_AMNOTE_WEB.TaxWithholding.PitMessageReceiveApplyHandler>();
builder.Services.AddScoped<API_AMNOTE_WEB.Transmission.MessageReceiveApplyCoordinator>();
builder.Services.AddScoped<API_AMNOTE_WEB.TaxWithholding.PitService>();
builder.Services.AddHostedService<EInvoiceMttOutboxBackgroundService>();
builder.Services.AddScoped<IEInvoiceService, EInvoiceService>();
builder.Services.AddSingleton<IEInvoiceHtmlToPdfService, EInvoiceHtmlToPdfService>();
builder.Services.AddScoped<IEInvoicePrintService, EInvoicePrintService>();
builder.Services.AddScoped<IEInvoiceSettingRepository, EInvoiceSettingRepository>();
builder.Services.AddScoped<IEInvoiceSellerXslTemplateRepository, EInvoiceSellerXslTemplateRepository>();
builder.Services.AddScoped<IEInvoiceSettingService, EInvoiceSettingService>();
builder.Services.AddScoped<IEInvoiceSellerPreviewService, EInvoiceSellerPreviewService>();
builder.Services.AddScoped<IEInvoiceTransmissionPreviewService, EInvoiceTransmissionPreviewService>();
builder.Services.AddScoped<IEInvoiceTemplateRepository, EInvoiceTemplateRepository>();
builder.Services.AddScoped<IEInvoiceDeclarationRepository, EInvoiceDeclarationRepository>();
builder.Services.AddScoped<IEInvoiceDeclarationService, EInvoiceDeclarationService>();
builder.Services.AddScoped<IEInvoiceDeclarationPreviewService, EInvoiceDeclarationPreviewService>();
builder.Services.AddScoped<IEInvoiceErrorNoticeRepository, EInvoiceErrorNoticeRepository>();
builder.Services.AddScoped<IEInvoiceErrorNoticeService, EInvoiceErrorNoticeService>();
builder.Services.AddScoped<IEInvoiceErrorNoticePreviewService, EInvoiceErrorNoticePreviewService>();
builder.Services.AddScoped<IEInvoiceMinuteRepository, EInvoiceMinuteRepository>();
builder.Services.AddScoped<IEInvoiceMinuteService, EInvoiceMinuteService>();
builder.Services.AddScoped<IEInvoiceMinutePreviewService, EInvoiceMinutePreviewService>();
builder.Services.Configure<SigningPluginSetupOptions>(builder.Configuration.GetSection("SigningPlugin"));
builder.Services.AddSingleton<ISigningPluginSetupService, SigningPluginSetupService>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<ISysNotificationRepository, SysNotificationRepository>();
builder.Services.AddScoped<ISysNotificationService, SysNotificationService>();
builder.Services.AddScoped<ISysConfigRepository, SysConfigRepository>();
builder.Services.AddScoped<ISysGridColumnSettingRepository, SysGridColumnSettingRepository>();
builder.Services.AddScoped<ISysDecimalSettingRepository, SysDecimalSettingRepository>();
builder.Services.AddScoped<ISysWorkingCalendarRepository, SysWorkingCalendarRepository>();
builder.Services.AddScoped<ISysConfigService, SysConfigService>();
builder.Services.AddScoped<IUserSettingRepository, UserSettingRepository>();
builder.Services.AddScoped<IUserSettingService, UserSettingService>();
builder.Services.AddSingleton<IMasterDataCacheService, MasterDataCacheService>();

builder.Services.AddScoped<ICatalogWriteSupport, CatalogWriteSupport>();

builder.Services.AddScoped<IProductInfoService, ProductInfoService>();
builder.Services.AddScoped<IProductKindService, ProductKindService>();
builder.Services.AddScoped<IProductUnitService, ProductUnitService>();
builder.Services.AddScoped<IStoreInfoService, StoreInfoService>();
builder.Services.AddScoped<IInventoryOpeningService, InventoryOpeningService>();
builder.Services.AddScoped<IStoreKindInfoService, StoreKindInfoService>();
builder.Services.AddScoped<IDepartmentInfoService, DepartmentInfoService>();
builder.Services.AddScoped<IBankInfoService, BankInfoService>();
builder.Services.AddScoped<IManagementInfoService, ManagementInfoService>();
builder.Services.AddScoped<ICustomerInfoService, CustomerInfoService>();
builder.Services.AddScoped<IAcclistInfoService, AcclistInfoService>();

builder.Services.AddScoped<ISysDecimalSettingService, SysDecimalSettingService>();
builder.Services.AddScoped<ISysWorkingCalendarService, SysWorkingCalendarService>();
builder.Services.AddScoped<ISystemService, SystemService>();

builder.Services.AddScoped<IUserInfoRepository, UserInfoRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IExcelImportLookupRepository, ExcelImportLookupRepository>();
builder.Services.AddScoped<API_AMNOTE_WEB.Services.Lookup.ILookupService, API_AMNOTE_WEB.Services.Lookup.LookupService>();
builder.Services.Configure<API_AMNOTE_WEB.Services.TaxLookup.TaxLookupOptions>(builder.Configuration.GetSection("TaxLookup"));
builder.Services.AddHttpClient(API_AMNOTE_WEB.Services.TaxLookup.TaxLookupService.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<API_AMNOTE_WEB.Services.TaxLookup.TaxLookupOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
        ? "http://115.78.232.22:8000"
        : options.BaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri($"{baseUrl}/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<API_AMNOTE_WEB.Services.TaxLookup.ITaxLookupService, API_AMNOTE_WEB.Services.TaxLookup.TaxLookupService>();
builder.Services.AddScoped<API_AMNOTE_WEB.Interfaces.IGdtImportRepository, API_AMNOTE_WEB.Repositories.GdtImportRepository>();
builder.Services.AddScoped<API_AMNOTE_WEB.Interfaces.IGdtImportService, API_AMNOTE_WEB.Services.Tax.GdtImportService>();
builder.Services.AddScoped<RefreshTokenStore>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IPasswordCipher, PasswordCipher>();
builder.Services.AddScoped<IMailSettingRepository, MailSettingRepository>();
builder.Services.AddScoped<IMailSettingService, MailSettingService>();
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddScoped<ISystemRepository, SystemRepository>();
var jwt = builder.Configuration.GetSection("Jwt");
var issuer = jwt["Issuer"];
var audience = jwt["Audience"];
var secret = jwt["Secret"]!;
var sessionCookieMinutes = int.TryParse(builder.Configuration["Auth:SessionCookieMinutes"], out var configuredSessionCookieMinutes)
    ? configuredSessionCookieMinutes
    : int.Parse(jwt["AccessTokenMinutes"] ?? "30");
var sessionCookieName = builder.Configuration["Auth:SessionCookieName"] ?? "amnote_bff";

builder.Services
  .AddAuthentication(options =>
  {
      options.DefaultScheme = "SmartAuth";
      options.DefaultAuthenticateScheme = "SmartAuth";
      options.DefaultChallengeScheme = "SmartAuth";
      options.DefaultSignInScheme = "BffSession";
      options.DefaultSignOutScheme = "BffSession";
  })
  .AddPolicyScheme("SmartAuth", "BFF or JWT", options =>
  {
      options.ForwardDefaultSelector = context =>
      {
          var authorization = context.Request.Headers.Authorization.ToString();
          return !string.IsNullOrWhiteSpace(authorization) &&
                 authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
              ? JwtBearerDefaults.AuthenticationScheme
              : "BffSession";
      };
  })
  .AddCookie("BffSession", options =>
  {
      options.Cookie.Name = sessionCookieName;
      options.Cookie.HttpOnly = true;
      options.Cookie.Path = "/";
      options.Cookie.SameSite = SameSiteMode.None;
      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
      options.SlidingExpiration = true;
      options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionCookieMinutes);
      options.Events = new CookieAuthenticationEvents
      {
          OnRedirectToLogin = context =>
          {
              context.Response.StatusCode = StatusCodes.Status401Unauthorized;
              return Task.CompletedTask;
          },
          OnRedirectToAccessDenied = context =>
          {
              context.Response.StatusCode = StatusCodes.Status403Forbidden;
              return Task.CompletedTask;
          }
      };
  })
  .AddJwtBearer(options =>
  {
      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,

          ValidIssuer = issuer,
          ValidAudience = audience,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
          ClockSkew = TimeSpan.FromSeconds(30)
      };
  });

var app = builder.Build();

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AMnote API V1");
        c.RoutePrefix = "swagger";
    });
}

Common.ServiceProvider = app.Services;

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseMiddleware<CompanyRouteContextMiddleware>();

app.UseRouting();

app.UseCors("frontend");

app.UseRateLimiter();

app.UseRequestTimeouts();

app.UseAuthentication();
app.UseAuthorization();

app.UseDevExpressControls();
app.MapControllers();

app.MapFallback("/api/{**path}", async context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    await context.Response.WriteAsJsonAsync(ApiResponse.NotFound($"API endpoint not found: {context.Request.Path.Value}"));
});

app.Run();

static bool IsFrontendOriginAllowed(string origin, ISet<string> allowedOrigins, bool isDevelopment)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (allowedOrigins.Contains(origin))
    {
        return true;
    }

    if (!isDevelopment || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!IPAddress.TryParse(uri.Host, out var address))
    {
        return false;
    }

    if (IPAddress.IsLoopback(address))
    {
        return true;
    }

    var bytes = address.GetAddressBytes();
    return bytes.Length == 4
        && (bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168));
}
