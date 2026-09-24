using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace XV.Core;

/// <summary>
/// Application-layer encryption used for every call after pairing (see docs/PROTOCOL.md).
/// AES-256-GCM with the 32-byte per-device key issued at enrollment.
///   request AAD  = "XVGK1|req|{deviceId}|{ts}|{nonce}"
///   response AAD = "XVGK1|res|{deviceId}|{nonce}"
/// Wire format: {"iv":"base64(12 bytes)","ct":"base64(ciphertext||16-byte tag)"}.
/// Requests older/newer than 10 minutes or with a repeated nonce are rejected (replay protection).
/// </summary>
public static class Envelope
{
    public const int MaxSkewMs = 10 * 60 * 1000;
    static readonly ConcurrentDictionary<string, long> SeenNonces = new();

    public static string RequestAad(string deviceId, long ts, string nonce) => $"XVGK1|req|{deviceId}|{ts}|{nonce}";
    public static string ResponseAad(string deviceId, string nonce) => $"XVGK1|res|{deviceId}|{nonce}";

    public static (string iv, string ct) Seal(byte[] key, string plaintext, string aad)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(iv, pt, ct, tag, Encoding.UTF8.GetBytes(aad));
        return (Convert.ToBase64String(iv), Convert.ToBase64String(ct.Concat(tag).ToArray()));
    }

    public static string Open(byte[] key, string ivB64, string ctB64, string aad)
    {
        var iv = Convert.FromBase64String(ivB64);
        var all = Convert.FromBase64String(ctB64);
        if (iv.Length != 12 || all.Length < 16) throw new CryptographicException("Malformed envelope");
        var ct = all.AsSpan(0, all.Length - 16);
        var tag = all.AsSpan(all.Length - 16);
        var pt = new byte[ct.Length];
        using var gcm = new AesGcm(key, 16);
        gcm.Decrypt(iv, ct, tag, pt, Encoding.UTF8.GetBytes(aad));
        return Encoding.UTF8.GetString(pt);
    }

    /// <summary>Cheap pre-check before any decryption: timestamp inside the window and a well-formed nonce.</summary>
    public static bool InWindow(long ts, string nonce) =>
        Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ts) <= MaxSkewMs && nonce.Length is >= 16 and <= 64;

    /// <summary>
    /// True when the timestamp is fresh and the nonce has not been used before. Call only after the request
    /// has been authenticated (decrypted), so unauthenticated traffic cannot fill the nonce cache.
    /// </summary>
    public static bool AcceptFresh(string deviceId, long ts, string nonce)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (Math.Abs(now - ts) > MaxSkewMs || nonce.Length < 16 || nonce.Length > 64) return false;
        if (SeenNonces.Count > 50_000)
            foreach (var kv in SeenNonces)
                if (now - kv.Value > 2 * MaxSkewMs) SeenNonces.TryRemove(kv.Key, out _);
        return SeenNonces.TryAdd(deviceId + "|" + nonce, now);
    }
}
