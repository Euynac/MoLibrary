using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.EventBus.Modules;
using Monica.Markdown.Git.Abstractions;
using Monica.Markdown.Git.Models;
using Monica.Markdown.Git.Services;
using Monica.Markdown.Git.WebHooks;

namespace Monica.Markdown.Modules;

public static class ModuleGitBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Git integration module for repository management and webhook handling.
        /// </summary>
        public static ModuleGitGuide AddGit(Action<ModuleGitOption>? action = null)
        {
            return new ModuleGitGuide().Register(action);
        }
    }
}

/// <summary>
/// Git integration module providing repository clone/pull operations and webhook handling.
/// </summary>
public class ModuleGit(ModuleGitOption option)
    : MoModuleWithDependencies<ModuleGit, ModuleGitOption, ModuleGitGuide>(option)
{
    /// <inheritdoc />
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Git;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGitCredentialStore, GitCredentialStore>();
        services.AddSingleton<IGitRepositoryService, GitRepositoryService>();

        // Register webhook providers as keyed singletons
        foreach (var (key, providerType) in Option.WebHookProviders)
        {
            services.AddKeyedSingleton(typeof(IGitWebHookProvider), key, (sp, _) =>
            {
                var provider = (IGitWebHookProvider)ActivatorUtilities.CreateInstance(sp, providerType);
                if (Option.WebHookSecrets.TryGetValue(key, out var secret))
                {
                    provider.Secret = secret;
                }
                return provider;
            });
        }
    }

    /// <inheritdoc />
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapPost("/git/webhook/{provider}",
                async ([FromRoute] string provider,
                       [FromServices] IGitRepositoryService service,
                       HttpRequest request,
                       CancellationToken ct) =>
                {
                    await service.HandleWebHookAsync(provider, request, ct);
                    return Results.Ok();
                })
                .WithName("HandleGitWebHook")
                .WithTags(tagName)
                .WithSummary("Handle incoming Git webhook")
                .WithDescription("Receives and processes webhook notifications from Git hosting providers (GitHub, GitLab, etc.).");
        });
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusGuide>().Register();
    }
}

/// <summary>
/// Fluent configuration guide for the Git module.
/// </summary>
public class ModuleGitGuide : MoModuleGuide<ModuleGit, ModuleGitOption, ModuleGitGuide>
{
    /// <summary>
    /// Registers a Git credential for repository authentication.
    /// </summary>
    /// <param name="credential">The credential entry.</param>
    public ModuleGitGuide AddCredential(GitCredential credential)
    {
        ConfigureModuleOption(option =>
        {
            option.Credentials.Add(credential);
        }, secondKey: credential.Id);

        return this;
    }

    /// <summary>
    /// Registers a personal access token credential.
    /// </summary>
    /// <param name="id">Unique credential identifier.</param>
    /// <param name="token">Personal access token value.</param>
    /// <param name="displayName">Optional display name.</param>
    public ModuleGitGuide AddTokenCredential(string id, string token, string? displayName = null)
    {
        return AddCredential(new GitCredential
        {
            Id = id,
            Type = EGitCredentialType.Token,
            Token = token,
            DisplayName = displayName
        });
    }

    /// <summary>
    /// Registers a username/password credential.
    /// </summary>
    /// <param name="id">Unique credential identifier.</param>
    /// <param name="username">Username.</param>
    /// <param name="password">Password.</param>
    /// <param name="displayName">Optional display name.</param>
    public ModuleGitGuide AddBasicCredential(string id, string username, string password, string? displayName = null)
    {
        return AddCredential(new GitCredential
        {
            Id = id,
            Type = EGitCredentialType.UsernamePassword,
            Username = username,
            Password = password,
            DisplayName = displayName
        });
    }

    /// <summary>
    /// Registers a Git repository for tracking and synchronization.
    /// </summary>
    /// <param name="id">Unique repository identifier.</param>
    /// <param name="url">Remote URL of the repository.</param>
    /// <param name="localPath">Local filesystem path for the cloned repository.</param>
    /// <param name="credentialId">Optional credential ID for authentication.</param>
    /// <param name="branch">Optional branch to track.</param>
    public ModuleGitGuide AddRepository(string id, string url, string localPath, string? credentialId = null, string? branch = null)
    {
        ConfigureModuleOption(option =>
        {
            option.Repositories.Add(new GitRepositoryRegistration
            {
                Id = id,
                Url = url,
                LocalPath = localPath,
                CredentialId = credentialId,
                Branch = branch
            });
        }, secondKey: id);

        return this;
    }

    /// <summary>
    /// Registers a custom webhook provider type under the given key.
    /// </summary>
    /// <typeparam name="TProvider">Custom webhook provider type implementing <see cref="IGitWebHookProvider"/>.</typeparam>
    /// <param name="providerKey">Provider key for the webhook endpoint route.</param>
    /// <param name="secret">Optional webhook secret for the provider.</param>
    public ModuleGitGuide UseWebHookProvider<TProvider>(string providerKey, string? secret = null)
        where TProvider : class, IGitWebHookProvider
    {
        ConfigureModuleOption(option =>
        {
            option.WebHookProviders[providerKey] = typeof(TProvider);
            if (secret is not null)
            {
                option.WebHookSecrets[providerKey] = secret;
            }
        }, secondKey: providerKey);

        return this;
    }

    /// <summary>
    /// Sets the webhook secret for a specific provider (e.g. built-in GitHub/GitLab).
    /// </summary>
    /// <param name="providerKey">Provider key.</param>
    /// <param name="secret">Webhook secret value.</param>
    public ModuleGitGuide WithWebHookSecret(string providerKey, string secret)
    {
        ConfigureModuleOption(option =>
        {
            option.WebHookSecrets[providerKey] = secret;
        }, secondKey: $"secret:{providerKey}");

        return this;
    }
}

/// <summary>
/// Configuration options for the Git module.
/// </summary>
public class ModuleGitOption : MoModuleOptionWithMinimalApi<ModuleGit>
{
    /// <summary>
    /// Registered Git credentials.
    /// </summary>
    public List<GitCredential> Credentials { get; set; } = [];

    /// <summary>
    /// Registered Git repository descriptors.
    /// </summary>
    public List<GitRepositoryRegistration> Repositories { get; set; } = [];

    /// <summary>
    /// Webhook provider types keyed by provider name.
    /// </summary>
    public Dictionary<string, Type> WebHookProviders { get; set; } = new()
    {
        ["github"] = typeof(GitHubWebHookProvider),
        ["gitlab"] = typeof(GitLabWebHookProvider)
    };

    /// <summary>
    /// Webhook secrets keyed by provider name.
    /// </summary>
    public Dictionary<string, string?> WebHookSecrets { get; set; } = new();
}
