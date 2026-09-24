using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using XV.Core;

namespace XV.CommandCenter;

/// <summary>
/// The data folder holds the encrypted database, its DPAPI-protected key and the settings. The key is protected for this
/// machine, so any local Windows user who can read the folder could decrypt it. On first run the folder is therefore
/// restricted to SYSTEM, Administrators and the Windows account that set the Command Center up.
/// </summary>
public static class DataFolderSecurity
{
    public static void Apply()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var dir = new DirectoryInfo(Paths.DataDir);
            var current = dir.GetAccessControl();
            if (current.AreAccessRulesProtected) return; // already restricted — never remove the original owner
            var me = WindowsIdentity.GetCurrent().User!;
            var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            var sec = new DirectorySecurity();
            sec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (var sid in new[] { new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), me })
                sec.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            dir.SetAccessControl(sec);
            // Existing files and sub-folders: drop explicit rules (e.g. from the installer) and inherit the restricted ones.
            foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var fs = new FileSecurity(); fs.SetAccessRuleProtection(false, false); f.SetAccessControl(fs);
            }
            foreach (var d in dir.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                var ds = new DirectorySecurity(); ds.SetAccessRuleProtection(false, false); d.SetAccessControl(ds);
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(Paths.File("error.log"), $"{DateTime.Now:u} Could not restrict the data folder: {ex.Message}\n");
        }
    }
}
