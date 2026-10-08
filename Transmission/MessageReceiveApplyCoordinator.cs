using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Transmission;

public sealed class MessageReceiveApplyCoordinator(
    IEnumerable<IMessageReceiveApplyHandler> handlers)
{
    private readonly IReadOnlyList<IMessageReceiveApplyHandler> _handlers = handlers.ToList();

    public Task ApplyAsync(
        EInvoiceMessageWorkQueueItem item,
        IReadOnlyList<ViettelEInvoiceMessageLookupResult> received,
        bool preferSuccessResult)
    {
        var targetType = EInvoiceMessageTargetTypes.Normalize(item.TARGET_TYPE);
        var matchingHandlers = _handlers
            .Where(handler => handler.CanHandle(targetType))
            .ToList();

        return matchingHandlers.Count switch
        {
            1 => matchingHandlers[0].ApplyAsync(item, received, preferSuccessResult),
            0 => throw new InvalidOperationException(
                $"No receive apply handler is registered for target type '{targetType}'."),
            _ => throw new InvalidOperationException(
                $"Multiple receive apply handlers are registered for target type '{targetType}'.")
        };
    }
}
