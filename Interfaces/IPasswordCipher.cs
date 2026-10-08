namespace API_AMNOTE_WEB.Interfaces
{
    public interface IPasswordCipher
    {
        string Encrypt(string plainText);
        string Decrypt(string? encryptedText);
        bool Matches(string plainText, string? encryptedText);
    }
}
