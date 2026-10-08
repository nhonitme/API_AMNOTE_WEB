namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class ExcelImportModuleHandlerRegistry : IExcelImportModuleHandlerRegistry
    {
        private readonly Dictionary<string, IExcelImportModuleHandler> _handlers;

        public ExcelImportModuleHandlerRegistry(IEnumerable<IExcelImportModuleHandler> handlers)
        {
            _handlers = new Dictionary<string, IExcelImportModuleHandler>(StringComparer.OrdinalIgnoreCase);

            foreach (var handler in handlers)
            {
                var moduleCds = handler is IExcelImportModuleAliasProvider aliasProvider
                    ? aliasProvider.ModuleCds
                    : new[] { handler.ModuleCd };

                foreach (var moduleCd in moduleCds.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    _handlers[moduleCd] = handler;
                }
            }
        }

        public IExcelImportModuleHandler GetHandler(string moduleCd)
        {
            if (string.IsNullOrWhiteSpace(moduleCd))
            {
                throw new ArgumentException("ModuleCd is required.");
            }

            if (!_handlers.TryGetValue(moduleCd, out var handler))
            {
                throw new InvalidOperationException($"Không tìm thấy Excel import handler cho ModuleCd: {moduleCd}");
            }

            return handler;
        }
    }
}