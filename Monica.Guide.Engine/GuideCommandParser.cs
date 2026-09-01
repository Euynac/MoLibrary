namespace Monica.Guide;

/// <summary>Strict dependency-free parser for the public <c>guide</c> command surface.</summary>
public static class GuideCommandParser
{
    private static readonly string[] SOURCE_ACTIONS = ["list", "resolve", "bind", "unbind"];
    private static readonly string[] ISSUE_ACTIONS = ["status", "set"];
    private static readonly string[] ISSUE_MODES = ["prepare", "ask", "never"];
    private static readonly string[] MUTATING_COMMANDS = ["configure", "unconfigure", "init", "forget", "source"];

    public static GuideCommand Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            return new GuideCommand("overview", [], [], [], null, null, null, null, null, false, null, false);
        }

        var command = arguments[0].Trim().ToLowerInvariant();
        if (command is not ("overview" or "status" or "doctor" or "configure" or "unconfigure"
                or "init" or "forget" or "source" or "issue" or "workspaces"))
        {
            throw new GuideUsageException($"Unknown guide command '{arguments[0]}'.");
        }
        if (command == "workspaces" && arguments.Count > 1 && arguments[1] != "--json")
        {
            throw new GuideUsageException("workspaces accepts only --json.");
        }

        var targets = new List<GuideTarget>();
        var environments = new List<GuideEnvironment>();
        var skills = new List<string>();
        var capabilities = new List<string>();
        string? profile = null;
        string? executable = null;
        string? releaseManifest = null;
        string? workspace = null;
        string? repository = null;
        string? sourcePath = null;
        string? sourceRef = null;
        string? sourceResolver = null;
        string? sourceAction = null;
        string? issueAction = null;
        string? issueMode = null;
        int? port = null;
        var apply = false;
        string? digest = null;
        string? locale = null;
        var json = false;
        for (var index = 1; index < arguments.Count; index++)
        {
            var option = arguments[index];
            if (command == "source" && sourceAction is null && !option.StartsWith('-'))
            {
                sourceAction = option.Trim().ToLowerInvariant();
                continue;
            }

            if (command == "issue" && issueAction is null && !option.StartsWith('-'))
            {
                issueAction = option.Trim().ToLowerInvariant();
                continue;
            }

            switch (option)
            {
                case "--target":
                    targets.Add(ParseTarget(Next(arguments, ref index, option)));
                    break;
                case "--environment":
                    environments.Add(ParseEnvironment(Next(arguments, ref index, option)));
                    break;
                case "--skill":
                    skills.Add(Next(arguments, ref index, option).Trim());
                    break;
                case "--profile":
                    profile = Next(arguments, ref index, option).Trim();
                    break;
                case "--capability":
                    capabilities.Add(Next(arguments, ref index, option).Trim().ToLowerInvariant());
                    break;
                case "--executable":
                    executable = Path.GetFullPath(Next(arguments, ref index, option));
                    break;
                case "--release-manifest":
                    releaseManifest = Path.GetFullPath(Next(arguments, ref index, option));
                    break;
                case "--workspace":
                    workspace = Path.GetFullPath(Next(arguments, ref index, option));
                    break;
                case "--repository":
                    repository = Next(arguments, ref index, option).Trim();
                    break;
                case "--source-path":
                    sourcePath = Path.GetFullPath(Next(arguments, ref index, option));
                    break;
                case "--source-ref":
                    sourceRef = Next(arguments, ref index, option).Trim();
                    break;
                case "--source-resolver":
                    sourceResolver = Path.GetFullPath(Next(arguments, ref index, option));
                    break;
                case "--mode":
                    issueMode = Next(arguments, ref index, option).Trim().ToLowerInvariant();
                    break;
                case "--port":
                    if (!int.TryParse(Next(arguments, ref index, option), out var parsedPort)
                        || parsedPort is < 1 or > 65535)
                    {
                        throw new GuideUsageException("--port must be an integer from 1 through 65535.");
                    }

                    port = parsedPort;
                    break;
                case "--apply":
                    apply = true;
                    break;
                case "--plan-digest":
                    digest = Next(arguments, ref index, option).Trim().ToLowerInvariant();
                    break;
                case "--locale":
                    locale = Next(arguments, ref index, option);
                    if (locale is not ("en-US" or "zh-CN"))
                    {
                        throw new GuideUsageException("--locale must be en-US or zh-CN.");
                    }

                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    throw new GuideUsageException($"Unknown guide option '{option}'.");
            }
        }

        ValidateOptions(
            command, sourceAction, issueAction, issueMode, targets, environments, skills, capabilities, profile, workspace,
            repository, sourcePath, sourceRef, sourceResolver, port, apply, digest, locale);
        if (targets.Distinct().Count() != targets.Count)
        {
            throw new GuideUsageException("Each --target selector may appear only once.");
        }
        if (environments.Select(static item => item.Selector).Distinct(StringComparer.OrdinalIgnoreCase).Count() != environments.Count)
        {
            throw new GuideUsageException("Each --environment selector may appear only once.");
        }
        return new GuideCommand(
            command, targets, environments, skills, profile, executable, releaseManifest, port, digest, apply, locale, json,
            sourceAction, workspace, capabilities, repository, sourcePath, sourceRef, sourceResolver,
            issueAction, issueMode);
    }

    public static GuideEnvironment ParseEnvironment(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        var parts = selector.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 1
            && (string.Equals(parts[0], "windows", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parts[0], "linux", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parts[0], "macos", StringComparison.OrdinalIgnoreCase)))
        {
            var kind = parts[0].ToLowerInvariant();
            return new GuideEnvironment(kind, kind);
        }

        if (parts.Length is 2 or 3
            && string.Equals(parts[0], "wsl", StringComparison.OrdinalIgnoreCase)
            && parts.Skip(1).All(static part => !string.IsNullOrWhiteSpace(part)))
        {
            var normalized = parts.Length == 2
                ? $"wsl:{parts[1]}"
                : $"wsl:{parts[1]}:{parts[2]}";
            return new GuideEnvironment(
                normalized,
                "wsl",
                parts[1],
                parts.Length == 3 ? parts[2] : null);
        }

        throw new GuideUsageException(
            "--environment must be 'windows', 'linux', 'macos', or 'wsl:<distro>[:<user>]'.");
    }

    private static GuideTarget ParseTarget(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "shared" => GuideTarget.Shared,
            "claude" => GuideTarget.Claude,
            _ => throw new GuideUsageException("--target must be shared or claude.")
        };

    private static string Next(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (++index >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index]))
        {
            throw new GuideUsageException($"{option} requires a value.");
        }

        return arguments[index];
    }

    private static void ValidateOptions(
        string command,
        string? sourceAction,
        string? issueAction,
        string? issueMode,
        IReadOnlyList<GuideTarget> targets,
        IReadOnlyList<GuideEnvironment> environments,
        IReadOnlyList<string> skills,
        IReadOnlyList<string> capabilities,
        string? profile,
        string? workspace,
        string? repository,
        string? sourcePath,
        string? sourceRef,
        string? sourceResolver,
        int? port,
        bool apply,
        string? digest,
        string? locale)
    {
        if (command == "source")
        {
            if (sourceAction is null || !SOURCE_ACTIONS.Contains(sourceAction))
            {
                throw new GuideUsageException(
                    $"source requires one action: {string.Join(", ", SOURCE_ACTIONS)}.");
            }

            if (sourceAction != "list" && string.IsNullOrWhiteSpace(repository))
            {
                throw new GuideUsageException("source resolve, bind, and unbind require --repository.");
            }

            if (sourceAction == "bind")
            {
                if (sourcePath is null && string.IsNullOrWhiteSpace(sourceRef))
                {
                    throw new GuideUsageException(
                        "source bind requires --source-path <checkout> or --source-ref <exact-ref>.");
                }
            }
            else if (sourcePath is not null || sourceRef is not null)
            {
                throw new GuideUsageException("--source-path and --source-ref are valid only for source bind.");
            }
        }
        else if (sourceAction is not null)
        {
            throw new GuideUsageException("A source action is valid only for the source command.");
        }

        if (command == "issue")
        {
            if (issueAction is null || !ISSUE_ACTIONS.Contains(issueAction))
            {
                throw new GuideUsageException(
                    $"issue requires one action: {string.Join(", ", ISSUE_ACTIONS)}.");
            }

            if (issueAction == "set" && issueMode is null)
            {
                throw new GuideUsageException("issue set requires --mode <prepare|ask|never>.");
            }

            if (issueAction == "status" && issueMode is not null)
            {
                throw new GuideUsageException("--mode is valid only for issue set.");
            }

            if (issueMode is not null && !ISSUE_MODES.Contains(issueMode))
            {
                throw new GuideUsageException($"--mode must be one of: {string.Join(", ", ISSUE_MODES)}.");
            }
        }
        else if (issueAction is not null)
        {
            throw new GuideUsageException("An issue action is valid only for the issue command.");
        }

        if (issueMode is not null && command != "issue")
        {
            throw new GuideUsageException("--mode is valid only for issue.");
        }

        if (repository is not null && command != "source")
        {
            throw new GuideUsageException("--repository is valid only for source.");
        }

        if (sourcePath is not null && command != "source")
        {
            throw new GuideUsageException("--source-path is valid only for source.");
        }

        if (sourceRef is not null && command != "source")
        {
            throw new GuideUsageException("--source-ref is valid only for source.");
        }

        if (sourceResolver is not null && command != "source")
        {
            throw new GuideUsageException("--source-resolver is valid only for source.");
        }

        if (targets.Count > 0 && command is not ("configure" or "unconfigure"))
        {
            throw new GuideUsageException("--target is valid only for configure and unconfigure.");
        }

        if (workspace is not null && command is ("configure" or "unconfigure")
            && (targets.Count > 0 || environments.Count > 0))
        {
            throw new GuideUsageException(
                "--workspace installs into the workspace's project directories; --target and --environment select global targets instead.");
        }

        if (command == "configure" && workspace is not null && skills.Count > 0)
        {
            throw new GuideUsageException(
                "--skill selects individual global installs; a workspace installs its configured profile closure.");
        }

        // A configure without explicit environments refreshes every recorded installation from
        // the selected bundle, so per-environment targets cannot apply there.
        if (targets.Count > 0 && command == "configure" && environments.Count == 0)
        {
            throw new GuideUsageException("--target requires --environment for configure.");
        }

        if (environments.Count > 0 && command is not ("status" or "doctor" or "configure" or "unconfigure"))
        {
            throw new GuideUsageException(
                "--environment is valid only for status, doctor, configure, and unconfigure.");
        }

        if (skills.Count > 0 && command != "configure")
        {
            throw new GuideUsageException("--skill is valid only for configure.");
        }

        if (profile is not null && command is not ("configure" or "init"))
        {
            throw new GuideUsageException("--profile is valid only for configure and init.");
        }

        if (skills.Count > 0 && profile is not null)
        {
            throw new GuideUsageException("--skill and --profile cannot be combined.");
        }

        if (capabilities.Count > 0 && command != "init")
        {
            throw new GuideUsageException("--capability is valid only for init.");
        }

        if (workspace is not null && command is not ("status" or "doctor" or "init" or "forget" or "configure" or "unconfigure"))
        {
            throw new GuideUsageException(
                "--workspace is valid only for status, doctor, init, forget, configure, and unconfigure.");
        }

        if (command is ("init" or "forget") && workspace is null)
        {
            throw new GuideUsageException($"{command} requires --workspace <path>.");
        }

        if (port is not null && command != "configure")
        {
            throw new GuideUsageException("--port is valid only for configure.");
        }

        if (port is not null && workspace is not null)
        {
            throw new GuideUsageException("--port configures the loopback service, not a workspace skill installation.");
        }

        var mutating = MUTATING_COMMANDS.Contains(command)
                       && (command != "source" || sourceAction is "bind" or "unbind");
        if (apply && !mutating)
        {
            throw new GuideUsageException("--apply is valid only for mutating commands.");
        }

        if (apply != (digest is not null))
        {
            throw new GuideUsageException("--apply and --plan-digest must be supplied together.");
        }

        if (digest is not null
            && (digest.Length != 64 || digest.Any(static character => !Uri.IsHexDigit(character))))
        {
            throw new GuideUsageException("--plan-digest must be a 64-character SHA-256 hexadecimal digest.");
        }

        if (locale is not null && command != "overview")
        {
            throw new GuideUsageException("--locale is valid only for overview.");
        }
    }
}

/// <summary>Parsed guide command ready for orchestration.</summary>
public sealed record GuideCommand(
    string Name,
    IReadOnlyList<GuideTarget> Targets,
    IReadOnlyList<GuideEnvironment> Environments,
    IReadOnlyList<string> Skills,
    string? Profile,
    string? ExecutablePath,
    string? ReleaseManifestPath,
    int? Port,
    string? PlanDigest,
    bool Apply,
    string? Locale,
    bool Json,
    string? SourceAction = null,
    string? WorkspacePath = null,
    IReadOnlyList<string>? Capabilities = null,
    string? Repository = null,
    string? SourcePath = null,
    string? SourceRef = null,
    string? ResolverPath = null,
    string? IssueAction = null,
    string? IssueMode = null);

/// <summary>Invalid command-line invocation; maps to exit code 2.</summary>
public sealed class GuideUsageException(string message) : Exception(message);
