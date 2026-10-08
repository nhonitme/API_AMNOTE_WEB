using API_AMNOTE_WEB.Interfaces;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace API_AMNOTE_WEB.Services
{
    public sealed class EInvoiceHtmlToPdfService : IEInvoiceHtmlToPdfService, IAsyncDisposable
    {
        private readonly SemaphoreSlim _browserGate = new(1, 1);
        private IBrowser? _browser;
        private bool _fetcherInitialized;

        public EInvoiceHtmlToPdfService(IHostApplicationLifetime lifetime)
        {
            lifetime.ApplicationStopping.Register(() =>
            {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }

        public async Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new InvalidOperationException("Invoice HTML is empty.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var browser = await GetBrowserAsync(cancellationToken);
            await using var page = await browser.NewPageAsync();

            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                Timeout = 120_000,
            });

            await WaitForInvoiceLayoutAsync(page, cancellationToken);
            await WaitForImagesAsync(page, cancellationToken);

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
                await page.WaitForFunctionAsync(
                    @"() => {
                        if (window.__invoicePaginated === true) return true;
                        if (document.querySelector('.invoice-page, .invoice-root')) return true;
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
                await Task.Delay(500, cancellationToken);
            }
        }

        private static async Task WaitForImagesAsync(IPage page, CancellationToken cancellationToken)
        {
            try
            {
                await page.EvaluateFunctionAsync(@"async () => {
                    const images = Array.from(document.images ?? []);
                    await Promise.all(images.map((image) => {
                        if (image.complete) {
                            return Promise.resolve();
                        }

                        return new Promise((resolve) => {
                            image.addEventListener('load', resolve, { once: true });
                            image.addEventListener('error', resolve, { once: true });
                        });
                    }));
                }");
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(300, cancellationToken);
            }
        }

        private static bool IsValidPdf(byte[] pdf)
        {
            return pdf.Length > 512
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

                if (!_fetcherInitialized)
                {
                    var fetcher = new BrowserFetcher();
                    await fetcher.DownloadAsync();
                    _fetcherInitialized = true;
                }

                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
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
