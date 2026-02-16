using System.Security.Cryptography;
using System.Text;

namespace Tracker.Infrastructure;

public static class HashUtility
{
    /// <summary>
    /// Computes SHA-256 hash of the input text for caching purposes.
    /// Returns lowercase hex string.
    /// </summary>
    public static string ComputeHash(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
            
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    
    /// <summary>
    /// Computes SHA-256 hash with normalization (trims whitespace, normalizes line endings)
    /// </summary>
    public static string ComputeNormalizedHash(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
            
        var normalized = text.Trim().Replace("\r\n", "\n").Replace("\r", "\n");
        return ComputeHash(normalized);
    }
}