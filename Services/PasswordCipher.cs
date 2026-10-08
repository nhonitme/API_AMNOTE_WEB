using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Services
{
    public class PasswordCipher : IPasswordCipher
    {
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            var ariaProvider = new AriaSecurity.AriaProvider();
            return ariaProvider.EncryptToString(plainText) ?? string.Empty;
        }

        public string Decrypt(string? encryptedText)
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
            {
                return string.Empty;
            }

            var ariaProvider = new AriaSecurity.AriaProvider();
            return ariaProvider.DecryptFromString(encryptedText)?.Replace("\0", string.Empty).Trim() ?? string.Empty;
        }

        public bool Matches(string plainText, string? encryptedText)
        {
            if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(encryptedText))
            {
                return false;
            }

            var encryptedInput = Encrypt(plainText);
            return !string.IsNullOrEmpty(encryptedInput) && encryptedInput == encryptedText;
        }
    }
}
