namespace XV.Core;

/// <summary>
/// Keeps the last several snapshots of the encrypted database and its key in a folder that sits <b>beside</b> the
/// live data directory, not inside it -- so deleting or corrupting the live data folder does not touch them. If the
/// database is missing or fails to open at startup, the newest snapshot is restored automatically before the
/// Command Center opens it, so reinstalling the software (or recovering from a corrupted or accidentally deleted
/// database) picks the previous data back up on its own, with nothing for the operator to do.
///
/// A snapshot is exactly the same DPAPI-protected files the live store already uses (machine-bound, not user-bound,
/// same as today), so it only ever opens on this machine -- the same trust boundary the live data already has.
/// </summary>
public static class AutoBackup
{
    const int KeepSnapshots = 14;
    // The registry database and its key, the Comms (messages/calls) database and its key, the admin password and
    // every other setting, the tamper-evident audit chain anchor, and the TLS certificate -- so a recovered Command
    // Center still presents the same certificate paired phones already trust, instead of forcing every terminal to
    // be paired again.
    static readonly string[] Files =
        ["xv-access-control.db", "database.key.bin", "xv-comms.db", "comms.key.bin", "settings.json", "server-cert.pfx.bin", "audit-head.bin"];

    /// <summary>A sibling of the live data folder (e.g. ...\XVAccessControl-Recovery next to ...\XVAccessControl),
    /// so removing the live folder -- the "server deleted" case -- leaves this one alone.</summary>
    static string RecoveryDir
    {
        get
        {
            var live = Paths.DataDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(live);
            var dir = string.IsNullOrEmpty(parent) ? Path.Combine(live, "Recovery") : Path.Combine(parent, Path.GetFileName(live) + "-Recovery");
            return Directory.CreateDirectory(dir).FullName;
        }
    }

    /// <summary>Checkpoints the live database and copies it, and its key, into a new timestamped snapshot. Safe to
    /// call often (e.g. once a day and once at startup) while the store is open and healthy.</summary>
    public static void Snapshot(Store store)
    {
        try
        {
            store.Checkpoint();
            var main = Paths.File(Files[0]);
            if (!File.Exists(main)) return;
            var dir = Directory.CreateDirectory(Path.Combine(RecoveryDir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))).FullName;
            foreach (var f in Files)
            {
                var src = Paths.File(f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(dir, f), overwrite: true);
            }
            Prune();
        }
        catch (Exception ex) { Diag.Error("Automatic backup snapshot failed", ex); }
    }

    static void Prune()
    {
        try
        {
            foreach (var d in new DirectoryInfo(RecoveryDir).GetDirectories().OrderByDescending(d => d.Name).Skip(KeepSnapshots))
                d.Delete(true);
        }
        catch (Exception ex) { Diag.Error("Automatic backup cleanup failed", ex); }
    }

    static DirectoryInfo? Newest() =>
        Directory.Exists(RecoveryDir) ? new DirectoryInfo(RecoveryDir).GetDirectories().OrderByDescending(d => d.Name).FirstOrDefault() : null;

    /// <summary>Called before the store is first opened. If the live database file is simply missing (deleted, or a
    /// fresh reinstall pointed at a data folder whose live file never made it back), restores the newest snapshot
    /// into place. Returns the snapshot's timestamp when a restore happened, otherwise null.</summary>
    public static string? RestoreIfMissing() => File.Exists(Paths.File(Files[0])) ? null : Restore(Newest());

    /// <summary>Unconditionally restores the newest snapshot over whatever is currently in the data folder. Used
    /// after the live database failed to open (corruption) and <see cref="Quarantine"/> has moved the bad files
    /// aside, so this never overwrites anything that could still matter.</summary>
    public static string? ForceRestoreLatest() => Restore(Newest());

    static string? Restore(DirectoryInfo? snapshot)
    {
        if (snapshot == null) return null;
        try
        {
            foreach (var f in Files)
            {
                var src = Path.Combine(snapshot.FullName, f);
                if (File.Exists(src)) File.Copy(src, Paths.File(f), overwrite: true);
            }
            Diag.Info($"Recovered the database from the automatic backup taken {snapshot.Name}");
            return snapshot.Name;
        }
        catch (Exception ex) { Diag.Error("Automatic recovery failed", ex); return null; }
    }

    /// <summary>Moves a database file that failed to open out of the way (renamed, never deleted) so a restored
    /// snapshot does not collide with it and the damaged file stays available for inspection.</summary>
    public static void Quarantine()
    {
        try
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            foreach (var f in Files)
            {
                var src = Paths.File(f);
                if (File.Exists(src)) File.Move(src, src + $".corrupted-{stamp}");
            }
        }
        catch (Exception ex) { Diag.Error("Could not quarantine the damaged database", ex); }
    }

    /// <summary>True once at least one snapshot exists, so the UI can show whether auto-recovery is actually armed.</summary>
    public static bool HasSnapshot => Newest() != null;
}
