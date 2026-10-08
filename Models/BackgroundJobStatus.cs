namespace API_AMNOTE_WEB.Models;

public static class BackgroundJobStatus
{
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Done = "DONE";
    public const string Error = "ERROR";
    public const string Cancelled = "CANCELLED";

    public static bool IsFinished(string status)
    {
        return status == Done || status == Error || status == Cancelled;
    }
}
