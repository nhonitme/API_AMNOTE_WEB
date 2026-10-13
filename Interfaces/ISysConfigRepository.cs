using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysConfigRepository
    {
        Task<IEnumerable<SysConfig>> GetRuntimeSysConfigsAsync(string companyCd);
    }
}
