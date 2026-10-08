using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysCodeSequenceRepository
    {
        Task<IEnumerable<SysCodeSequence>> GetSequencesAsync(string companyCd, string? objectType = null);
        Task<SysCodeSequence?> GetSequenceAsync(string companyCd, long id);
        Task<int> UpsertSequenceAsync(string companyCd, SysCodeSequenceRequest request);
        Task<int> DeleteSequenceAsync(string companyCd, long id);
        Task<SysCodeSequencePreview?> PreviewAsync(string companyCd, string objectType, DateTime? baseDate = null);
        Task<SysCodeSequencePreview?> PreviewByContextAsync(string companyCd, string menuCode, string codeField, DateTime? baseDate = null);
        Task<IReadOnlyList<SysCodeSequencePreview>> PreviewManyAsync(string companyCd, IEnumerable<string> objectTypes, DateTime? baseDate = null);
        Task<string?> ResolveCodeAsync(DapperSession session, string companyCd, string objectType, string? requestedCode, DateTime? baseDate = null);
        Task<string?> ResolveCodeByContextAsync(DapperSession session, string companyCd, string menuCode, string codeField, string? requestedCode, DateTime? baseDate = null);

        int ClearAllCache();
    }
}
