using System;
using System.IO;
using System.Security;
using Gaffer.Application.Serialization;
using Gaffer.Common;

namespace Gaffer.UserData
{
    /// <summary>
    /// Reads and writes a save payload to a file. FORMAT-AGNOSTIC despite the name — it hands the injected
    /// <see cref="ISerializer"/> a stream and knows nothing about what goes on it; the shipped codec is
    /// <see cref="SaveSerializer"/> (compact binary out, binary or legacy JSON in), and the name is kept
    /// only because it is what the editor tool windows construct. Save serializes and writes; Load reads,
    /// deserializes, and
    /// migrates to the current schema — each fallible step returns a <see cref="Result"/>, so a missing or
    /// corrupt file is an expected failure the caller handles, never a crash. Synchronous: a single small
    /// save file for the run. The async I/O boundary (TDD §10) matters once saves grow or move off the main
    /// thread; this keeps the adapter honest and the editor demo simple until then.
    /// <para>
    /// The write follows UNITY.md §7's save protocol, because the OS can kill a backgrounding app mid-write
    /// and a plain write to the real path truncates it first — an interruption there does not lose the last
    /// save, it destroys the whole run. See <see cref="Save"/> for the protocol and
    /// <see cref="Load"/> for the fallback that makes it recoverable.
    /// </para>
    /// </summary>
    public sealed class JsonSaveStore
    {
        /// <summary>The half-written file. It sits in the SAME directory as the destination on purpose:
        /// <see cref="File.Replace(string, string, string)"/> throws across volumes, and a system temp
        /// directory is a different volume on every platform this ships to (UNITY.md §7).</summary>
        private const string TempSuffix = ".tmp";

        /// <summary>The previous save, kept by the replace step — the defined fallback when the current file
        /// turns out to be unreadable (UNITY.md §7).</summary>
        private const string BackupSuffix = ".bak";

        private readonly ISerializer _serializer;
        private readonly SaveMigrator _migrator;

        public JsonSaveStore(ISerializer serializer, SaveMigrator migrator)
        {
            _serializer = serializer;
            _migrator = migrator;
        }

        /// <summary>
        /// Writes the save with UNITY.md §7's protocol: serialize into a temp file beside the destination,
        /// flush it all the way to disk, then swap it into place. The swap is
        /// <see cref="File.Replace(string, string, string)"/> — effectively atomic on one volume, and it
        /// leaves the previous save as a backup — falling back to <see cref="File.Move(string, string)"/>
        /// for the first ever save, because Replace throws when the destination does not exist yet.
        /// <para>
        /// Treat the swap as an OPTIMISATION of the protocol, not its correctness: the BCL does not formally
        /// guarantee atomicity, so <see cref="Load"/> still has to survive a torn file. The interruption
        /// points the device smoke test has to exercise are exactly write → flush → replace/move → cleanup,
        /// on every platform/backend combination shipped (Android Mono/IL2CPP, iOS IL2CPP) — absence of
        /// failure reports is not a platform guarantee.
        /// </para>
        /// </summary>
        public Result Save(string path, SeasonSaveData data)
        {
            string temp = path + TempSuffix;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                WriteThroughToDisk(temp, data);
                Commit(temp, path);
                return Result.Success();
            }
            catch (Exception e) when (IsExpectedFileFailure(e))
            {
                // The destination is untouched — the failure cost the new save, not the old one.
                TryDeleteLeftoverTemp(temp);
                return Result.Failure("Could not write save: " + e.Message);
            }
        }

        /// <summary>
        /// Reads the save, falling back to the backup the replace step leaves behind when the current file is
        /// missing or unreadable. A corrupt save is an expected <see cref="Result"/> failure with a defined
        /// fallback — backup first, then the caller starts a fresh run — and never an unhandled crash at
        /// boot (UNITY.md §7). The load path is the boot path: an exception escaping here is a game that
        /// cannot be opened.
        /// </summary>
        public Result<SeasonSaveData> Load(string path)
        {
            Result<SeasonSaveData> current = LoadFrom(path);
            if (current.IsSuccess)
            {
                return current;
            }

            // A backup exists only where a save was successfully replaced at least once, so reaching for it
            // means the newer file was torn or deleted — exactly the case it is kept for.
            string backup = path + BackupSuffix;
            if (!File.Exists(backup))
            {
                return current;
            }

            Result<SeasonSaveData> previous = LoadFrom(backup);
            if (previous.IsSuccess)
            {
                return previous;
            }

            return Result<SeasonSaveData>.Failure(current.Error + " The backup save is unusable too: " + previous.Error);
        }

        private Result<SeasonSaveData> LoadFrom(string path)
        {
            if (!File.Exists(path))
            {
                return Result<SeasonSaveData>.Failure("No save at " + path + ".");
            }

            Result<SeasonSaveData> parsed;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // The codec owns the encoding (and, for the binary container, the fact that there is
                    // none) — the store just supplies the bytes.
                    parsed = _serializer.Deserialize(stream);
                }
            }
            catch (Exception e) when (IsExpectedFileFailure(e))
            {
                return Result<SeasonSaveData>.Failure("Could not read save: " + e.Message);
            }

            if (parsed.IsFailure)
            {
                return parsed;
            }

            // Bring an older save up to the current schema before the caller maps it back to the domain.
            return _migrator.Migrate(parsed.Value);
        }

        /// <summary>Serializes straight into the file (PERFORMANCE §4: no multi-MB intermediate buffer for a
        /// world-sized save) and forces the bytes past the OS write cache before the swap. Without the
        /// <c>Flush(true)</c> the rename can land while the content is still only in the cache, which is the
        /// zero-length-save-file symptom on a killed app.</summary>
        private void WriteThroughToDisk(string path, SeasonSaveData data)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                _serializer.Serialize(data, stream);
                stream.Flush(flushToDisk: true);
            }
        }

        /// <summary>Swaps the finished temp file into place.</summary>
        private static void Commit(string temp, string path)
        {
            if (!File.Exists(path))
            {
                // First ever save: File.Replace throws when the destination does not exist (UNITY.md §7).
                File.Move(temp, path);
                return;
            }

            try
            {
                File.Replace(temp, path, path + BackupSuffix, ignoreMetadataErrors: true);
            }
            catch (UnauthorizedAccessException)
            {
                // File.Replace documents an UnauthorizedAccessException branch for "this operation is not
                // supported on the current platform" as well as for a genuine permission denial, and the two
                // are indistinguishable here. So retry with plain copies: same outcome, minus the atomicity
                // — the previous save is copied to the backup FIRST, so an interruption during the second
                // copy still leaves a complete file to fall back to. A real permission denial throws again
                // from here and is reported as a Result failure by the caller.
                File.Copy(path, path + BackupSuffix, overwrite: true);
                File.Copy(temp, path, overwrite: true);
                File.Delete(temp);
            }
        }

        private static void TryDeleteLeftoverTemp(string temp)
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception e) when (IsExpectedFileFailure(e))
            {
                // A stranded temp file is litter, not a failure — the next save overwrites it, and reporting
                // it would replace the real reason the save failed with a cleanup detail.
            }
        }

        /// <summary>
        /// The file-system failures this adapter expects and converts into a <see cref="Result"/>. Converting
        /// a third-party/BCL exception at the boundary is exactly the adapter's job (CONVENTIONS §4), and
        /// this is the one place in the codebase where the catch should be this wide: <c>File</c> throws from
        /// several unrelated hierarchies for the same "the disk said no" outcome, and
        /// <see cref="UnauthorizedAccessException"/> — the one a read-only or sandboxed path raises — does
        /// NOT derive from <see cref="IOException"/>, so catching IOException alone lets it escape onto the
        /// boot path unhandled.
        /// <para>
        /// It is a filter rather than a bare <c>catch</c> so it still cannot swallow what must not be
        /// swallowed: an <see cref="OperationCanceledException"/> must reach its awaiter, and a
        /// <see cref="NullReferenceException"/> or an <see cref="ArgumentException"/> from our own misuse is
        /// a bug that has to fail fast rather than turn into a friendly "could not save" — a malformed path
        /// is code we wrote, not a disk that said no, so it is deliberately NOT in this list.
        /// </para>
        /// </summary>
        private static bool IsExpectedFileFailure(Exception e)
        {
            return e is IOException                 // includes FileNotFound, DirectoryNotFound, PathTooLong, DriveNotFound
                || e is UnauthorizedAccessException // read-only path, sandbox denial, or "unsupported on this platform"
                || e is SecurityException           // the caller lacks the required permission
                || e is NotSupportedException;      // a path form the platform cannot express
        }
    }
}
