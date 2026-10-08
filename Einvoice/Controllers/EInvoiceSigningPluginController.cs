using API_AMNOTE_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EInvoiceSigningPluginController : BaseApiController
{
    private readonly ISigningPluginSetupService setupService;

    public EInvoiceSigningPluginController(ISigningPluginSetupService setupService)
    {
        this.setupService = setupService;
    }

    [AllowAnonymous]
    [HttpGet("setup-info")]
    public IActionResult GetSetupInfo()
    {
        return Success(setupService.GetSetupInfo());
    }

    [AllowAnonymous]
    [HttpGet("setup")]
    public IActionResult DownloadSetup()
    {
        var setupFile = setupService.ResolveSetupFileForDownload();
        if (setupFile == null)
            return NotFound("Signing plugin setup file is not available on the server.");

        return new PhysicalFileResult(setupFile.Value.FullPath, setupFile.Value.ContentType)
        {
            FileDownloadName = setupFile.Value.FileName,
            EnableRangeProcessing = true,
        };
    }
}
