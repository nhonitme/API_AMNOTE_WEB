using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Helpers
{
    public static class CodeSequenceHelper
    {
        public static async Task<string> ResolveRequiredCodeAsync(
            ISysCodeSequenceRepository sequenceRepository,
            DapperSession session,
            string companyCd,
            string objectType,
            string? requestedCode,
            string fieldName,
            DateTime? baseDate = null)
        {
            var resolvedCode = await sequenceRepository.ResolveCodeAsync(
                session,
                companyCd,
                objectType,
                requestedCode,
                baseDate);

            if (string.IsNullOrWhiteSpace(resolvedCode))
            {
                throw new InvalidOperationException($"{fieldName} sequence is not configured");
            }

            return resolvedCode;
        }

        public static async Task<string> ResolveRequiredCodeByContextAsync(
            ISysCodeSequenceRepository sequenceRepository,
            DapperSession session,
            string companyCd,
            string menuCode,
            string codeField,
            string? requestedCode,
            string fieldName,
            DateTime? baseDate = null)
        {
            var resolvedCode = await sequenceRepository.ResolveCodeByContextAsync(
                session,
                companyCd,
                menuCode,
                codeField,
                requestedCode,
                baseDate);

            if (string.IsNullOrWhiteSpace(resolvedCode))
            {
                throw new InvalidOperationException($"{fieldName} sequence is not configured");
            }

            return resolvedCode;
        }

    }
}
