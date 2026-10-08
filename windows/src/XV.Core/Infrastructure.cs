using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XV.Core;

/// <summary>Where the Command Center keeps its data. Override with XV_DATA_DIR (used by tests).</summary>
public static class Paths
{
    public static string DataDir { get; private set; } = Init();

    static string Init()
    {
        var env = Environment.GetEnvironmentVariable("XV_DATA_DIR");
        var dir = !string.IsNullOrWhiteSpace(env)
            ? env
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "XVAccessControl");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string File(string name) => Path.Combine(DataDir, name);
}

/// <summary>Protects secrets at rest. Windows: DPAPI bound to this machine. Elsewhere (tests only): plain bytes.</summary>
public static class Protector
{
    static readonly byte[] Entropy = "XV-DIGITAL-ACCESS-CONTROL-v1"u8.ToArray();

    public static byte[] Protect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine) : data;

    public static byte[] Unprotect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Unprotect(data, Entropy, DataProtectionScope.LocalMachine) : data;

    /// <summary>Loads a protected secret, creating it with <paramref name="create"/> on first use.</summary>
    public static byte[] LoadOrCreate(string fileName, Func<byte[]> create)
    {
        var path = Paths.File(fileName);
        if (System.IO.File.Exists(path)) return Unprotect(System.IO.File.ReadAllBytes(path));
        var value = create();
        System.IO.File.WriteAllBytes(path, Protect(value));
        return value;
    }
}

public sealed class Settings
{
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = Environment.MachineName;
    public int Port { get; set; } = 8443;
    public int DiscoveryPort { get; set; } = 47913;

    // Internet / cloud linkage
    public bool InternetEnabled { get; set; }
    public string CloudMode { get; set; } = "PORT_FORWARD"; // PORT_FORWARD | VPN | TUNNEL | RELAY
    public string PublicHost { get; set; } = "";
    public int PublicPort { get; set; } = 8443;
    public string CloudUrl { get; set; } = "";              // full https URL when a tunnel/relay provides one
    public bool CloudUsesPublicCertificate { get; set; }    // tunnel terminates TLS with a CA certificate

    /// <summary>When not empty, an internet-origin connection is accepted only from one of these CIDR ranges (e.g.
    /// "203.0.113.0/24" for a known office, or a single address as "203.0.113.7"). Empty = allow any address, same
    /// as before. LAN/VPN-private addresses are never affected by this list.</summary>
    public List<string> AllowedInternetCidrs { get; set; } = [];

    public bool RequireApproval { get; set; } = true;

    // Comms engine (messages, alerts, calls) — separate encrypted listener
    public int CommsPort { get; set; } = 8444;
    public int CommsPublicPort { get; set; } = 8444;
    public string CommsCloudUrl { get; set; } = "";         // full https URL when a tunnel publishes the comms port

    [JsonIgnore]
    public string CommsPublicUrl =>
        CommsCloudUrl.Trim().Length > 0 ? CommsCloudUrl.Trim().TrimEnd('/') :
        PublicHost.Trim().Length > 0 ? $"https://{PublicHost.Trim()}:{CommsPublicPort}" : "";

    /// <summary>FULL | MINIMAL | RECEIVE_ONLY — how much registry data terminals receive (see Store.Bootstrap).</summary>
    public string DataSharing { get; set; } = "MINIMAL";

    /// <summary>Administrator password (PBKDF2) protecting exports and data-protection settings. Empty until first set.</summary>
    public string AdminSalt { get; set; } = "";
    public string AdminHash { get; set; } = "";

    /// <summary>When true a Windows Firewall rule blocks this program from opening connections to public internet addresses.</summary>
    public bool BlockOutbound { get; set; } = true;

    [JsonIgnore]
    public bool HasAdminPassword => AdminHash.Length > 0;

    public void SetAdminPassword(string password)
    {
        if (password.Length < 8) throw new InvalidOperationException("The administrator password must be at least 8 characters.");
        AdminSalt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        AdminHash = HashAdmin(password, AdminSalt);
    }

    public bool CheckAdminPassword(string password) =>
        HasAdminPassword && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(HashAdmin(password, AdminSalt)), Convert.FromHexString(AdminHash));

    static string HashAdmin(string password, string saltHex) =>
        Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromHexString(saltHex), 120_000, HashAlgorithmName.SHA256, 32));

    /// <summary>Extra personnel fields defined by the administrator (shown in forms, cards and exports).</summary>
    public List<string> CustomFields { get; set; } = [];

    /// <summary>Standard personnel fields visible in "Add Soldier" dialog. Empty list means all default fields visible.
    /// Examples: "rank", "company", "platoon", "section", "mobile", "notes". Any field not listed here is hidden from the UI
    /// but still stored in the database.</summary>
    public List<string> VisiblePersonnelFields { get; set; } = ["rank", "company", "platoon", "section", "mobile", "notes"];

    /// <summary>History record types besides ENTRY / EXIT that can be added to a person's history from the PC.</summary>
    /// <summary>Reasons offered on the terminal when recording an entry / exit (the operator can also type a custom reason).</summary>
    public List<string> MovementReasons { get; set; } = ["TD", "Proceeding on Leave", "Rejoining from Leave", "Posting Out", "Posting In", "Local Work"];

    /// <summary>Locations whose terminals also receive the "has not returned" alert. The PC always shows it; nobody else gets it unless chosen here.</summary>
    public List<string> AbsenceAlertLocations { get; set; } = [];

    /// <summary>Operators (RPs) whose terminals also receive the "has not returned" alert, chosen separately from the locations.</summary>
    public List<string> AbsenceAlertOperators { get; set; } = [];

    /// <summary>Absences already sent to terminals ("person|expected return"), so restarting the Command Center does not send them again.</summary>
    public List<string> AbsenceAlertsSent { get; set; } = [];

    /// <summary>Exit reasons for which the terminal asks the expected return date (tracked for overdue alerts).</summary>
    public List<string> ReturnDateReasons { get; set; } = ["TD", "Proceeding on Leave", "Local Work"];

    /// <summary>Lock the Command Center after this many idle minutes (0 = never); unlocked with the administrator password.</summary>
    public int AutoLockMinutes { get; set; } = 0;

    /// <summary>Heading printed at the top of an Excel/PDF export, with {Scope} replaced by what the export covers
    /// (e.g. "Capt John Doe" for one person, "Alpha Company" for a company, "2 Platoon" for a platoon). Editable in
    /// Settings so a unit can phrase it however their paperwork expects.</summary>
    public string ExportHeadingTemplate { get; set; } = "Movement History of {Scope}";

    /// <summary>Card Studio design (theme, header texts, instructions, CO details), stored as JSON by the Card Studio.</summary>
    public string CardDesignJson { get; set; } = "";

    public List<string> EventTypes { get; set; } = ["Leave", "Returned from Leave", "Duty", "Course", "Medical", "Guard Duty", "Out Pass", "Temporary Duty"];
    public int TokenHours { get; set; } = 8;
    public int OfflineGraceHours { get; set; } = 12;
    public bool StartWithWindows { get; set; } = true;

    /// <summary>Break a long stay into months once it passes 30 days ("1mo 5d") instead of just piling up days.</summary>
    public bool ShowMonthsInDuration { get; set; }

    // ============================================================ Security settings (Phase 1-4)
    /// <summary>Maximum pairing attempts per code before lockout (Phase 3 security).</summary>
    public int MaxPairingAttempts { get; set; } = 5;

    /// <summary>Certificate validity period in years (Phase 4 security).</summary>
    public int CertificateValidityYears { get; set; } = 2;

    /// <summary>Days before certificate expiry to trigger rotation (Phase 4 security).</summary>
    public int CertificateRotationDays { get; set; } = 30;

    /// <summary>When true, certificates never expire (no automatic rotation); when false, rotate after CertificateValidityYears (Phase 4).</summary>
    public bool DisableCertificateExpiry { get; set; } = false;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static string FilePath => Paths.File("settings.json");

    public static Settings Load()
    {
        Settings s;
        try { s = System.IO.File.Exists(FilePath) ? JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { s = new(); }
        if (string.IsNullOrWhiteSpace(s.ServerId)) s.ServerId = "XV-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        s.Save();
        return s;
    }

    public void Save() => System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));

    /// <summary>The address phones should use over the internet, or empty when not configured.</summary>
    [JsonIgnore]
    public string PublicUrl =>
        !string.IsNullOrWhiteSpace(CloudUrl) ? CloudUrl.Trim().TrimEnd('/')
        : !string.IsNullOrWhiteSpace(PublicHost) ? $"https://{PublicHost.Trim()}:{PublicPort}" : "";
}

/// <summary>Self-signed TLS certificate that phones pin by SHA-256 fingerprint (set during pairing).</summary>
public static class CertManager
{
    public static X509Certificate2 LoadOrCreate(Settings settings)
    {
        // Check if certificate needs rotation (Phase 4 security: certificate rotation)
        var certPath = Paths.File("server-cert.pfx.bin");
        if (File.Exists(certPath))
        {
            try
            {
                var pfx = Protector.Unprotect(File.ReadAllBytes(certPath));
#pragma warning disable SYSLIB0057
                var cert = new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
#pragma warning restore SYSLIB0057
                // Rotate certificate if expiring within CertificateRotationDays and expiry is not disabled
                if (!settings.DisableCertificateExpiry && cert.NotAfter < DateTimeOffset.UtcNow.AddDays(settings.CertificateRotationDays))
                    RotateCertificate(settings);
                else
                    return cert;
            }
            catch { /* Fall through to regenerate on any error */ }
        }

        var newPfx = Protector.LoadOrCreate("server-cert.pfx.bin", () => GenerateCertificate(settings));
#pragma warning disable SYSLIB0057
        return new X509Certificate2(newPfx, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
#pragma warning restore SYSLIB0057
    }

    static byte[] GenerateCertificate(Settings settings)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN=XV Access Control {settings.ServerId}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddDnsName("xv-access-control.local");
        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
        // Certificate validity: configurable via settings.CertificateValidityYears (Phase 4 security)
        var validityYears = settings.DisableCertificateExpiry ? 100 : Math.Max(1, settings.CertificateValidityYears);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(validityYears));
        return cert.Export(X509ContentType.Pfx);
    }

    static void RotateCertificate(Settings settings)
    {
        try
        {
            // Archive old certificate before generating new one
            var certPath = Paths.File("server-cert.pfx.bin");
            var archivePath = Paths.File($"server-cert-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pfx.bin.bak");
            if (File.Exists(certPath)) File.Copy(certPath, archivePath, overwrite: false);
            // Regenerate certificate (overwriting existing)
            Protector.LoadOrCreate("server-cert.pfx.bin", () => GenerateCertificate(settings));
        }
        catch { /* Certificate rotation failure is non-fatal; server continues with old cert */ }
    }

    public static string Fingerprint(X509Certificate2 cert) => Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLowerInvariant();
}

public static class NetUtil
{
    /// <summary>
    /// IPv4 addresses phones can use to reach this PC, best first: real Wi-Fi / Ethernet adapters that have a gateway
    /// (the router the phones are on). Virtual adapters (Hyper-V / WSL vEthernet, VirtualBox, VMware, VPN / TAP,
    /// Bluetooth) and "no network" 169.254.x.x addresses are left out — a phone cannot reach them, and trying them
    /// first made pairing slow or fail.
    /// </summary>
    public static List<string> LanAddresses()
    {
        var found = new List<(string ip, int rank)>();
        string[] virtualNames = ["virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "wsl", "tap", "tun", "vpn", "bluetooth", "loopback", "docker", "npcap", "zerotier", "hamachi"];
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var name = (ni.Name + " " + ni.Description).ToLowerInvariant();
                var isVirtual = virtualNames.Any(name.Contains);
                var props = ni.GetIPProperties();
                var hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                foreach (var ua in props.UnicastAddresses)
                {
                    var a = ua.Address;
                    if (a.AddressFamily != AddressFamily.InterNetwork || !IsPrivate(a) || IPAddress.IsLoopback(a)) continue;
                    var b = a.GetAddressBytes();
                    if (b[0] == 169 && b[1] == 254) continue; // no DHCP answer: not on any network
                    // Tailscale / other overlay networks (100.64/10) stay usable but come after the real LAN.
                    var rank = isVirtual ? 3 : hasGateway ? 0 : (b[0] == 100 ? 2 : 1);
                    if (isVirtual && !(b[0] == 100)) continue; // Hyper-V, VirtualBox, VMware … are never reachable from a phone
                    found.Add((a.ToString(), rank));
                }
            }
        }
        catch { /* adapters unavailable */ }
        return found.OrderBy(f => f.rank).ThenBy(f => f.ip.StartsWith("192.168.") ? 0 : f.ip.StartsWith("10.") ? 1 : 2)
            .Select(f => f.ip).Distinct().ToList();
    }

    public static bool IsPrivate(IPAddress? ip)
    {
        if (ip == null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6SiteLocal;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127); // CGNAT range used by Tailscale
    }

    /// <summary>True if <paramref name="ip"/> falls inside <paramref name="cidr"/> (e.g. "203.0.113.0/24"); a bare
    /// address with no "/" is treated as a single host. Used to optionally restrict internet-mode connections to
    /// known networks. Malformed entries never match (fail closed, not open).</summary>
    public static bool InCidr(IPAddress ip, string cidr)
    {
        var parts = cidr.Trim().Split('/');
        if (!IPAddress.TryParse(parts[0], out var baseIp) || baseIp.AddressFamily != ip.AddressFamily) return false;
        var maxBits = ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var prefix = parts.Length > 1 && int.TryParse(parts[1], out var p) && p is >= 0 && p <= maxBits ? p : maxBits;
        var ipBytes = ip.GetAddressBytes(); var baseBytes = baseIp.GetAddressBytes();
        var fullBytes = prefix / 8; var remBits = prefix % 8;
        for (var i = 0; i < fullBytes; i++) if (ipBytes[i] != baseBytes[i]) return false;
        if (remBits == 0) return true;
        var mask = (byte)(0xFF << (8 - remBits));
        return (ipBytes[fullBytes] & mask) == (baseBytes[fullBytes] & mask);
    }
}
