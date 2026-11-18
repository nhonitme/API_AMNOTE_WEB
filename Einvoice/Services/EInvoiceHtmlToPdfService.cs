using API_AMNOTE_WEB.Interfaces;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace API_AMNOTE_WEB.Services
{
    public sealed class EInvoiceHtmlToPdfService : IEInvoiceHtmlToPdfService, IAsyncDisposable
    {
        private readonly SemaphoreSlim _browserGate = new(1, 1);
        private readonly SemaphoreSlim _renderGate = new(2, 2);
        private IBrowser? _browser;
        private readonly string _executablePath;

        public EInvoiceHtmlToPdfService(IHostApplicationLifetime lifetime, IConfiguration configuration)
        {
            _executablePath = configuration["ElectronicDocuments:ChromiumExecutablePath"] ?? "";
            lifetime.ApplicationStopping.Register(() =>
            {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }

        public async Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
        {
            await _renderGate.WaitAsync(cancellationToken);
            try { return await ConvertCoreAsync(html, cancellationToken); }
            finally { _renderGate.Release(); }
        }

        private async Task<byte[]> ConvertCoreAsync(string html, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new InvalidOperationException("Invoice HTML is empty.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var browser = await GetBrowserAsync(cancellationToken);
            await using var page = await browser.NewPageAsync();
            using var cancellation = cancellationToken.Register(() => { _ = page.CloseAsync(); });
            var deniedResource = 0;
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (_, args) =>
            {
                try
                {
                    var url = args.Request.Url;
                    if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || url == "about:blank")
                        await args.Request.ContinueAsync();
                    else
                    {
                        Interlocked.Exchange(ref deniedResource, 1);
                        await args.Request.AbortAsync();
                    }
                }
                catch { Interlocked.Exchange(ref deniedResource, 1); }
            };

            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                Timeout = 120_000,
            });

            await WaitForInvoiceLayoutAsync(page, cancellationToken);
            await WaitForImagesAsync(page, cancellationToken);
            if (deniedResource != 0) throw new InvalidOperationException("PDF contains unversioned external resources. Embed images/fonts before publishing the template.");

            cancellationToken.ThrowIfCancellationRequested();

            var pdf = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = false,
                MarginOptions = new MarginOptions
                {
                    Top = "0mm",
                    Right = "0mm",
                    Bottom = "0mm",
                    Left = "0mm",
                },
            });

            if (!IsValidPdf(pdf))
            {
                throw new InvalidOperationException("Generated PDF is empty.");
            }

            return pdf;
        }

        private static async Task WaitForInvoiceLayoutAsync(IPage page, CancellationToken cancellationToken)
        {
            try
            {
                // invoice-source is visibility:hidden until paginateInvoice fills #invoice-pages.
                // Waiting only for .invoice-root returns too early and yields a blank ~1KB PDF.
                await page.WaitForFunctionAsync(
                    @"() => {
                        const needsPagination = !!(
                            document.getElementById('invoice-source')
                            || document.getElementById('invoice-pages')
                        );
                        if (needsPagination) {
                            return window.__invoicePaginated === true
                                && !!document.querySelector('#invoice-pages .invoice-page');
                        }
                        if (window.__invoicePaginated === true) {
                            return !!document.querySelector('.invoice-page');
                        }
                        const body = document.body;
                        return !!body && body.innerText.trim().length > 0;
                    }",
                    new WaitForFunctionOptions
                    {
                        Timeout = 30_000,
                        PollingInterval = 100,
                    });
            }
            catch (WaitTaskTimeoutException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
        }

        private static async Task WaitForImagesAsync(IPage page, CancellationToken cancellationToken)
        {
            try
            {
                await page.EvaluateFunctionAsync(@"async () => {
                    const images = Array.from(document.images ?? []);
                    await Promise.all(images.map((image) => {
                        if (image.complete && image.naturalWidth > 0) {
                            return Promise.resolve();
                        }
                        if (image.complete) throw new Error('A document image failed to load');

                        return new Promise((resolve, reject) => {
                            image.addEventListener('load', resolve, { once: true });
                            image.addEventListener('error', reject, { once: true });
                        });
                    }));
                }");
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
        }

        // Blank Chromium pages land around 1.1–1.5KB; real invoice pages are far larger.
        private const int MinUsefulPdfBytes = 4096;

        private static bool IsValidPdf(byte[] pdf)
        {
            return pdf.Length >= MinUsefulPdfBytes
                && pdf[0] == (byte)'%'
                && pdf[1] == (byte)'P'
                && pdf[2] == (byte)'D'
                && pdf[3] == (byte)'F';
        }

        private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
        {
            if (_browser is { IsConnected: true })
            {
                return _browser;
            }

            await _browserGate.WaitAsync(cancellationToken);
            try
            {
                if (_browser is { IsConnected: true })
                {
                    return _browser;
                }

                if (_browser != null)
                {
                    await _browser.CloseAsync();
                    _browser.Dispose();
                    _browser = null;
                }

                if (string.IsNullOrWhiteSpace(_executablePath) || !Path.IsPathFullyQualified(_executablePath) || !File.Exists(_executablePath))
                    throw new InvalidOperationException("Configure ElectronicDocuments:ChromiumExecutablePath with an installed Chromium executable. Runtime browser installation is disabled.");

                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = _executablePath,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" },
                });

                return _browser;
            }
            finally
            {
                _browserGate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_browser == null)
            {
                return;
            }

            await _browserGate.WaitAsync();
            try
            {
                if (_browser != null)
                {
                    await _browser.CloseAsync();
                    _browser.Dispose();
                    _browser = null;
                }
            }
            finally
            {
                _browserGate.Release();
                _browserGate.Dispose();
            }
        }
    }
}
