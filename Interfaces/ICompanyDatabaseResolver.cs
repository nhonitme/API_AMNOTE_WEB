namespace API_AMNOTE_WEB.Interfaces
{
    public interface ICompanyDatabaseResolver
    {
        string ResolveDatabaseName(string companyCd);
        Task<string> ResolveDatabaseNameAsync(string companyCd);
    }
}
