using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ILanguageRepository
    {
        public Task<IEnumerable<t_message_info>> getLanguageInfos(IEnumerable<string> keys);
        public Task<IEnumerable<t_message_info>> getLanguageInfoAll(IEnumerable<string> keys);
    }
}