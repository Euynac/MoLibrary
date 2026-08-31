using System.Text;

namespace Monica.Guide;

/// <summary>
/// Shared machinery for every digest-locked guide mutation: deterministic plan preparation,
/// guide-owned local file writes, and the engine-wide mutation lock. Configure, workspace
/// initialization, and source bindings all serialize on the same lock and the same
/// preview-first apply flow.
/// </summary>
internal static class GuideMutations
{
    /// <summary>Orders mutations and checks deterministically and digests the resulting plan.</summary>
    internal static GuidePreparedPlan Prepare(
        string operation,
        IEnumerable<GuidePlannedMutation> mutations,
        IEnumerable<GuideCheck> checks,
        ProductGuideState? state = null)
    {
        var orderedMutations = mutations
            .OrderBy(static item => ActionPhase(item.PublicAction))
            .ThenBy(static item => item.PublicAction.Id, StringComparer.Ordinal)
            .ToArray();
        var orderedChecks = checks.OrderBy(static item => item.Id, StringComparer.Ordinal).ToArray();
        var actions = orderedMutations.Select(static item => item.PublicAction).ToArray();
        var plan = new GuidePlan(GuideContractVersions.CURRENT, operation,
            GuidePlanning.DigestPlan(operation, actions, orderedChecks), false,
            actions.Length == 0, actions, orderedChecks);
        return new GuidePreparedPlan(plan, orderedMutations, state);
    }

    /// <summary>
    /// Orders mutations by stable action-id phase so skill trees settle before configuration
    /// files and the ownership ledger closes the apply. Unprefixed ids (workspace and source
    /// mutations) run after skill trees and before recorded state.
    /// </summary>
    private static int ActionPhase(GuidePlanAction action)
    {
        if (action.Id.StartsWith("skills.", StringComparison.Ordinal))
        {
            return 20;
        }
        if (action.Id.StartsWith("server.", StringComparison.Ordinal))
        {
            return 30;
        }
        if (action.Id.StartsWith("installation.", StringComparison.Ordinal))
        {
            return 40;
        }
        if (action.Id.StartsWith("configuration.state", StringComparison.Ordinal))
        {
            return 50;
        }
        return 10;
    }

    /// <summary>Queues one guide-owned file write; identical content produces no action.</summary>
    internal static void AddLocalWrite(
        string id,
        string path,
        byte[] content,
        string description,
        ICollection<GuidePlannedMutation> mutations)
    {
        var before = GuidePlanning.DigestFile(path);
        var after = GuidePlanning.Sha256(content);
        if (before == after)
        {
            return;
        }

        var action = new GuidePlanAction(id, GuidePlanActionKind.WriteFile, path, before, after, description);
        mutations.Add(new GuideLocalFileMutation(action, path, content));
    }

    /// <summary>Queues one guide-owned file deletion; absent files produce no action.</summary>
    internal static void AddLocalDelete(
        string id,
        string path,
        string description,
        ICollection<GuidePlannedMutation> mutations)
    {
        var before = GuidePlanning.DigestFile(path);
        if (before is null)
        {
            return;
        }

        var action = new GuidePlanAction(id, GuidePlanActionKind.DeleteFile, path, before, null, description);
        mutations.Add(new GuideLocalFileMutation(action, path, null));
    }

    /// <summary>Writes one file atomically, or deletes it when the planned content is null.</summary>
    internal static async Task ApplyLocalFileAsync(GuideLocalFileMutation mutation, CancellationToken cancellationToken)
    {
        if (mutation.Content is null)
        {
            if (File.Exists(mutation.Path))
            {
                File.Delete(mutation.Path);
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(mutation.Path)!);
        var temporary = $"{mutation.Path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temporary, mutation.Content, cancellationToken);
        File.Move(temporary, mutation.Path, overwrite: true);
    }

    /// <summary>
    /// Deletes whole skill directories below one native project target root and drops the
    /// root itself when nothing remains; never touches entries the plan did not name.
    /// </summary>
    internal static void ApplyProjectTreeRemove(GuideProjectTreeRemove mutation)
    {
        foreach (var skillName in mutation.SkillNames)
        {
            var directory = Path.Combine(mutation.TargetRoot, skillName);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        if (Directory.Exists(mutation.TargetRoot) && Directory.EnumerateFileSystemEntries(mutation.TargetRoot).Any())
        {
            return;
        }

        try
        {
            Directory.Delete(mutation.TargetRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A non-empty or locked root stays behind; the workspace still owns it.
        }
    }

    /// <summary>Removes one guide-owned directory when nothing remains inside it.</summary>
    internal static void ApplyDirectoryCleanup(GuideDirectoryCleanup mutation)
    {
        try
        {
            if (Directory.Exists(mutation.Path) && !Directory.EnumerateFileSystemEntries(mutation.Path).Any())
            {
                Directory.Delete(mutation.Path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A non-empty or locked directory stays behind; it is workspace territory.
        }
    }

    /// <summary>
    /// Acquires the engine-wide mutation lock, reclaiming a stale lock whose owning process
    /// is gone. Holders serialize ledger, workspace, and source-binding mutations.
    /// </summary>
    internal static async ValueTask<IAsyncDisposable> AcquireLockAsync(
        GuidePaths enginePaths,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(enginePaths.StateDirectory);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var stream = new FileStream(
                    enginePaths.GuideLockFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, true);
                await stream.WriteAsync(Encoding.UTF8.GetBytes($"{Environment.ProcessId}\n"), cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return new GuideLock(stream, enginePaths.GuideLockFile);
            }
            catch (IOException exception) when (attempt == 0)
            {
                try
                {
                    await using (new FileStream(
                                     enginePaths.GuideLockFile,
                                     FileMode.Open,
                                     FileAccess.ReadWrite,
                                     FileShare.None,
                                     1,
                                     FileOptions.Asynchronous))
                    {
                    }
                    File.Delete(enginePaths.GuideLockFile);
                }
                catch (Exception reclaimException) when (reclaimException is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException("Another guide mutation holds the engine lock.", exception);
                }
            }
        }

        throw new InvalidOperationException("The guide mutation lock could not be acquired.");
    }

    private sealed class GuideLock(FileStream stream, string path) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
            try
            {
                File.Delete(path);
            }
            catch (FileNotFoundException)
            {
            }
        }
    }
}
