// LocalJsonPersistence.cs
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
// optional for PID writing (diagnostics)
using System.Diagnostics;

public class LocalJsonPersistence: IResourcePersistence
{
    // --- P/Invoke for Windows (optional fallback) ---
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool ReplaceFileW(string lpReplacedFileName, string lpReplacementFileName, string lpBackupFileName, int dwReplaceFlags, IntPtr lpExclude, IntPtr lpReserved);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, int dwFlags);

    const int REPLACEFILE_WRITE_THROUGH = 0x1;
    const int REPLACEFILE_IGNORE_MERGE_ERRORS = 0x2;

    const int MOVEFILE_REPLACE_EXISTING = 0x1;
    const int MOVEFILE_WRITE_THROUGH = 0x8;

    // --- P/Invoke for POSIX rename (optional fallback) ---
    [DllImport("libc", SetLastError = true)]
    static extern int rename(string oldpath, string newpath);

    // configuration resolved at construction time
    readonly TextAsset _fallbackAsset;   // spawnJsonAsset from ResourceManager (optional)
    readonly string _targetPath;        // resolved absolute path to save/load file
    readonly bool _verbose;

    // lock parameters
    readonly int _lockMaxAttempts = 6;
    readonly int _lockBaseWaitMs = 50;

    // temporary file cleanup policy
    readonly TimeSpan _tmpFileRetain = TimeSpan.FromHours(24);

    public LocalJsonPersistence(TextAsset spawnJsonAsset, string spawnJsonPath, bool verboseLogs)
    {
        _fallbackAsset = spawnJsonAsset;
        _targetPath = ResolvePath(spawnJsonPath);
        _verbose = verboseLogs;
        if (_verbose) UnityEngine.Debug.Log($"[LocalJsonPersistence] Using path: {_targetPath}");
    }

    ///// PUBLIC API used by ResourceManager /////

    /// <summary>
    /// Persist the container to disk using atomic-swap semantics where possible.
    /// Throws on fatal failure.
    /// </summary>
    public void Save(SpawnRecordCollection container)
    {
        if (container == null) throw new ArgumentNullException(nameof(container));

        string resolvedPath = _targetPath;
        if (string.IsNullOrEmpty(resolvedPath)) throw new InvalidOperationException("Save path not resolved.");

        if (_verbose) UnityEngine.Debug.Log($"[LocalJsonPersistence] Save -> {resolvedPath}");

        string json = JsonUtility.ToJson(container, true);
        var utf8NoBom = new UTF8Encoding(false);

        var dir = Path.GetDirectoryName(resolvedPath);
        if (string.IsNullOrEmpty(dir))
            throw new InvalidOperationException($"Invalid directory derived from path '{resolvedPath}'");

        Directory.CreateDirectory(dir);

        // Write tmp file in same directory (same FS -> rename/replace is usually atomic)
        string tmp = Path.Combine(dir, $"{Path.GetFileName(resolvedPath)}.{Guid.NewGuid():N}.tmp");
        string lockPath = resolvedPath + ".lock";

        // Create tmp file (exclusive) and flush to disk
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs, utf8NoBom))
            {
                sw.Write(json);
                sw.Flush();

                // Try flush-to-disk if available; fall back gracefully otherwise
                try
                {
                    // Some runtimes support Flush(true), older Mono may not.
                    fs.Flush(true);
                }
                catch (MissingMethodException)
                {
                    try { fs.Flush(); } catch { /* best-effort */ }
                }
                catch (NotSupportedException)
                {
                    try { fs.Flush(); } catch { /* best-effort */ }
                }
                catch (Exception)
                {
                    // best-effort final flush
                    try { fs.Flush(); } catch { /* ignore */ }
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[LocalJsonPersistence] Failed to write tmp file '{tmp}': {ex.GetType().Name}: {ex.Message}");
            // ensure tmp left for diagnostics; rethrow for caller to handle
            throw;
        }

        // Acquire inter-process lock before attempting swaps (best-effort)
        FileStream lockFs = null;
        try
        {
            lockFs = AcquireLock(lockPath, _lockMaxAttempts, _lockBaseWaitMs);
            if (lockFs == null)
            {
                UnityEngine.Debug.LogWarning("[LocalJsonPersistence] Could not acquire lock; proceeding anyway (risk of races).");
            }

            bool swapped = false;
            Exception lastSwapEx = null;

            // Try managed atomic APIs first (preferred)
            try
            {
                if (File.Exists(resolvedPath))
                {
                    // File.Replace is atomic-ish on many runtimes and creates a backup file (.bak)
                    File.Replace(tmp, resolvedPath, resolvedPath + ".bak", true);
                }
                else
                {
                    // Move new file into place
                    File.Move(tmp, resolvedPath);
                }
                swapped = true;
                if (_verbose) UnityEngine.Debug.Log("[LocalJsonPersistence] Saved using managed File.Replace/File.Move.");
            }
            catch (Exception managedEx)
            {
                lastSwapEx = managedEx;
                if (_verbose) UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] Managed replace/move failed: {managedEx.GetType().Name}: {managedEx.Message}");
            }

            // If managed approach failed, try platform-specific native operations
            if (!swapped)
            {
                if (IsWindows())
                {
                    try
                    {
                        if (File.Exists(resolvedPath))
                        {
                            bool ok = ReplaceFileW(resolvedPath, tmp, resolvedPath + ".bak", REPLACEFILE_WRITE_THROUGH | REPLACEFILE_IGNORE_MERGE_ERRORS, IntPtr.Zero, IntPtr.Zero);
                            if (!ok)
                            {
                                int err = Marshal.GetLastWin32Error();
                                throw new IOException($"ReplaceFileW failed (win32err={err})");
                            }
                        }
                        else
                        {
                            bool moved = MoveFileExW(tmp, resolvedPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH);
                            if (!moved)
                            {
                                int err = Marshal.GetLastWin32Error();
                                throw new IOException($"MoveFileExW failed (win32err={err})");
                            }
                        }
                        swapped = true;
                        if (_verbose) UnityEngine.Debug.Log("[LocalJsonPersistence] Saved using ReplaceFileW/MoveFileExW.");
                    }
                    catch (Exception ex)
                    {
                        lastSwapEx = ex;
                        UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] Windows atomic replace failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        int r = rename(tmp, resolvedPath);
                        if (r != 0)
                        {
                            int errno = Marshal.GetLastWin32Error();
                            throw new IOException($"rename failed errno={errno}");
                        }
                        swapped = true;
                        if (_verbose) UnityEngine.Debug.Log("[LocalJsonPersistence] Saved using POSIX rename.");
                    }
                    catch (Exception ex)
                    {
                        lastSwapEx = ex;
                        UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] POSIX rename failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            // Fallback loop: delete dest + move
            if (!swapped)
            {
                int tries = 0;
                while (!swapped && tries < 4)
                {
                    tries++;
                    try
                    {
                        if (File.Exists(resolvedPath)) File.Delete(resolvedPath);
                        File.Move(tmp, resolvedPath);
                        swapped = true;
                        if (_verbose) UnityEngine.Debug.Log($"[LocalJsonPersistence] Saved via fallback move attempt {tries}.");
                    }
                    catch (Exception ex)
                    {
                        lastSwapEx = ex;
                        UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] Fallback move attempt {tries} failed: {ex.GetType().Name}: {ex.Message}");
                        Thread.Sleep(50 * (1 << tries));
                    }
                }
            }

            // Final fallback: non-atomic overwrite
            if (!swapped)
            {
                try
                {
                    File.WriteAllText(resolvedPath, json, utf8NoBom);
                    if (File.Exists(tmp)) File.Delete(tmp);
                    swapped = true;
                    UnityEngine.Debug.LogWarning("[LocalJsonPersistence] Saved via non-atomic fallback. Consider diagnosing underlying replace errors.");
                }
                catch (Exception ex)
                {
                    lastSwapEx = ex;
                    UnityEngine.Debug.LogError($"[LocalJsonPersistence] Final fallback failed: {ex.GetType().Name}: {ex.Message}");
                    // leave tmp for diagnostics and rethrow
                    throw;
                }
            }

            // If we reached here and swapped==true, consider cleaning old tmp files
            if (swapped)
            {
                TryCleanupStaleTempFiles(dir, Path.GetFileName(resolvedPath));
            }
        }
        finally
        {
            // release lock (dispose FileStream)
            try { lockFs?.Dispose(); } catch { }
#if UNITY_EDITOR
            // Refresh asset DB if saving inside project Assets
            if (!string.IsNullOrEmpty(_targetPath) && _targetPath.StartsWith(Application.dataPath, StringComparison.Ordinal))
            {
                try { AssetDatabase.Refresh(); } catch { }
            }
#endif
            if (_verbose) UnityEngine.Debug.Log("[LocalJsonPersistence] Save complete (attempted atomic swap + fallbacks).");
        }
    }

    /// <summary>
    /// Loads the SpawnRecordCollection. Tries main file, then backup (.bak), then recent tmp files, then fallback TextAsset, then empty container.
    /// </summary>
    public SpawnRecordCollection Load()
    {
        string resolvedPath = _targetPath;
        if (_verbose) UnityEngine.Debug.Log($"[LocalJsonPersistence] Load from {resolvedPath}");

        // 1) try main file
        if (File.Exists(resolvedPath))
        {
            var loaded = TryReadJsonFile(resolvedPath);
            if (loaded != null)
            {
                if (_verbose) UnityEngine.Debug.Log("[LocalJsonPersistence] Loaded main snapshot file.");
                return loaded;
            }
            UnityEngine.Debug.LogWarning("[LocalJsonPersistence] Main snapshot exists but parsing failed; trying alternatives.");
        }

        // 2) try backup
        string bak = resolvedPath + ".bak";
        if (File.Exists(bak))
        {
            var loadedBak = TryReadJsonFile(bak);
            if (loadedBak != null)
            {
                UnityEngine.Debug.LogWarning("[LocalJsonPersistence] Loaded snapshot from .bak (fallback).");
                return loadedBak;
            }
        }

        // 3) try newest tmp in same directory that matches pattern
        var dir = Path.GetDirectoryName(resolvedPath);
        if (Directory.Exists(dir))
        {
            try
            {
                var files = Directory.GetFiles(dir, Path.GetFileName(resolvedPath) + ".*.tmp");
                string newest = null;
                DateTime newestTime = DateTime.MinValue;
                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    if (fi.CreationTimeUtc > newestTime)
                    {
                        newestTime = fi.CreationTimeUtc;
                        newest = f;
                    }
                }
                if (!string.IsNullOrEmpty(newest))
                {
                    var loadedTmp = TryReadJsonFile(newest);
                    if (loadedTmp != null)
                    {
                        UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] Loaded snapshot from tmp '{Path.GetFileName(newest)}' (fallback).");
                        return loadedTmp;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] Tmp fallback check failed: {ex.Message}");
            }
        }

        // 4) fallback embedded TextAsset (seed data)
        if (_fallbackAsset != null && !string.IsNullOrEmpty(_fallbackAsset.text))
        {
            try
            {
                var seed = JsonUtility.FromJson<SpawnRecordCollection>(_fallbackAsset.text);
                if (seed != null)
                {
                    if (_verbose) UnityEngine.Debug.Log("[LocalJsonPersistence] Loaded fallback TextAsset snapshot.");
                    return seed;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] Fallback TextAsset parse failed: {ex.Message}");
            }
        }

        // 5) last resort: empty snapshot
        if (_verbose) UnityEngine.Debug.Log("[LocalJsonPersistence] No valid snapshot found; returning empty container.");
        return new SpawnRecordCollection();
    }

    ///// INTERNAL HELPERS /////

    // Resolve spawnJsonPath provided by ResourceManager:
    // - if null/empty -> Application.persistentDataPath/spawnrecords.json
    // - if starts with "Assets/" -> maps to Application.dataPath + remainder
    // - otherwise uses provided path as-is (absolute or relative)
    private string ResolvePath(string spawnJsonPath)
    {
        if (!string.IsNullOrEmpty(spawnJsonPath))
        {
            // If path starts with "Assets/" map to project Assets folder
            if (spawnJsonPath.StartsWith("Assets/") && Application.isEditor)
            {
                // map to absolute file path inside project (editor only)
                string relative = spawnJsonPath.Substring("Assets/".Length);
                return Path.Combine(Application.dataPath, relative).Replace('\\', '/');
            }

            // If path is absolute, keep it; if relative, make it relative to persistentDataPath
            if (Path.IsPathRooted(spawnJsonPath))
            {
                return spawnJsonPath;
            }
            else
            {
                return Path.Combine(Application.persistentDataPath, spawnJsonPath);
            }
        }

        // default
        return Path.Combine(Application.persistentDataPath, "spawnrecords.json");
    }

    // Simple attempt to read and parse JSON file; returns null on error
    private SpawnRecordCollection TryReadJsonFile(string path)
    {
        try
        {
            string txt = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrEmpty(txt)) return null;
            var parsed = JsonUtility.FromJson<SpawnRecordCollection>(txt);
            return parsed;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[LocalJsonPersistence] Failed to read/parse '{path}': {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // Acquire exclusive lock file with retries (returns FileStream if successful; caller must Dispose it)
    private FileStream AcquireLock(string lockPath, int maxAttempts, int baseWaitMs)
    {
        int attempt = 0;
        while (attempt < maxAttempts)
        {
            try
            {
                // Open/create lock file with exclusive access
                var fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                // Optionally write PID to file for diagnostics (best-effort)
                try
                {
                    fs.SetLength(0);
                    using (var sw = new StreamWriter(fs, new UTF8Encoding(false), 1024, leaveOpen: true))
                    {
                        sw.Write($"pid:{Process.GetCurrentProcess().Id}\ncreated:{DateTime.UtcNow:o}");
                        sw.Flush();
                        fs.Flush(true);
                    }
                    fs.Position = 0; // reset for any future inspection
                }
                catch { /* ignore write errors to lock file */ }
                return fs;
            }
            catch (IOException)
            {
                // contested; exponential backoff
                int wait = baseWaitMs * (1 << attempt);
                Thread.Sleep(wait);
                attempt++;
            }
            catch (Exception)
            {
                // unexpected; bail
                break;
            }
        }

        return null;
    }

    private void TryCleanupStaleTempFiles(string dir, string baseFileName)
    {
        try
        {
            var files = Directory.GetFiles(dir, baseFileName + ".*.tmp");
            DateTime now = DateTime.UtcNow;
            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    if ((now - fi.CreationTimeUtc) > _tmpFileRetain)
                        File.Delete(f);
                }
                catch { /* ignore individual cleanup errors */ }
            }
        }
        catch { /* ignore cleanup errors */ }
    }

    private bool IsWindows()
    {
        var p = Application.platform;
        return p == RuntimePlatform.WindowsPlayer || p == RuntimePlatform.WindowsEditor;
    }
}
