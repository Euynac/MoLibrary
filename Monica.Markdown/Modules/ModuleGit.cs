using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Modules;
using Monica.EventBus.Modules;
using Monica.Markdown.Git.Interfaces;
using Monica.Markdown.Git.Models;
using Monica.Markdown.Git.Providers;
using Monica.Markdown.Git.Services;

namespace Monica.Markdown.Git.Modules;

/// <summary>
/// Builder extensions for the Git synchronization module.
/// </summary>
public static class ModuleGitBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Git synchronization module.
        /// </summary>
        public static ModuleGitGuide AddGit(Action<ModuleGitOption>? action = null)
        {
            return new ModuleGitGuide().Register(action);
        }
    }
}

/// <summary>
/// Git synchronization module.
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
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusGuide>().Register();
        DependsOnModule<ModuleHostedServiceGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<GitCredentialManager>();
        services.TryAddSingleton<IGitRepositoryService, GitRepositoryService>();
        services.TryAddSingleton<GitWebhookEndpointService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitCredentialResolver, GitOptionsCredentialResolver>());

        if (Option.IsSyncTriggerEnabled(GitSyncTrigger.Startup))
        {
            services.AddHostedService<GitStartupSyncHostedService>();
        }
    }

    /// <inheritdoc />
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapPost("/git/webhooks/{provider}", async (
                    [FromRoute] string provider,
                    HttpRequest request,
                    [FromServices] GitWebhookEndpointService service,
                    CancellationToken cancellationToken) =>
                {
                    return await service.HandleAsync(provider, request, cancellationToken);
                })
                .WithName("HandleGitWebhook")
                .WithTags(tagName)
                .WithSummary("Handle inbound Git webhooks")
                .WithDescription("Accepts normalized Git update events from configured webhook providers.");
        });
    }
}

/// <summary>
/// Fluent guide for the Git synchronization module.
/// </summary>
public class ModuleGitGuide : MoModuleGuide<ModuleGit, ModuleGitOption, ModuleGitGuide>
{
    /// <summary>
    /// Sets which synchronization triggers are enabled.
    /// </summary>
    public ModuleGitGuide UseSyncTriggers(params GitSyncTrigger[] triggers)
    {
        ConfigureModuleOption(option => option.SetEnabledSyncTriggers(triggers));
        return this;
    }

    /// <summary>
    /// Adds a Git repository.
    /// </summary>
    public ModuleGitGuide AddRepository(
        string id,
        string remoteUrl,
        string localPath,
        string? credentialId = null,
        string? provider = null,
        string? branch = null)
    {
        ConfigureModuleOption(option =>
        {
            option.RepositoryRegistrations.Add(new GitRepositoryRegistration
            {
                Id = id,
                RemoteUrl = remoteUrl,
                LocalPath = localPath,
                CredentialId = credentialId,
                Provider = provider,
                Branch = branch
            });
        }, secondKey: $"repository:{id}");

        return this;
    }

    /// <summary>
    /// Binds a repository to Markdown document groups.
    /// </summary>
    public ModuleGitGuide BindRepositoryToDocumentGroups(string repositoryId, params string[] documentGroupKeys)
    {
        ConfigureModuleOption(option =>
        {
            foreach (var groupKey in documentGroupKeys.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                option.RepositoryBindings.Add(new GitRepositoryBinding
                {
                    RepositoryId = repositoryId,
                    DocumentGroupKey = groupKey
                });
            }
        }, secondKey: $"{repositoryId}-bindings");

        return this;
    }

    /// <summary>
    /// Adds a token credential.
    /// </summary>
    public ModuleGitGuide AddTokenCredential(string id, string token, string? userName = null)
    {
        ConfigureModuleOption(option =>
        {
            option.CredentialRegistrations.Add(new GitCredentialRegistration
            {
                Id = id,
                AuthenticationType = GitAuthenticationType.Token,
                Token = token,
                UserName = userName
            });
        }, secondKey: $"credential-token:{id}");

        return this;
    }

    /// <summary>
    /// Adds a user name and password credential.
    /// </summary>
    public ModuleGitGuide AddUserPasswordCredential(string id, string userName, string password)
    {
        ConfigureModuleOption(option =>
        {
            option.CredentialRegistrations.Add(new GitCredentialRegistration
            {
                Id = id,
                AuthenticationType = GitAuthenticationType.UserPassword,
                UserName = userName,
                Password = password
            });
        }, secondKey: $"credential-userpass:{id}");

        return this;
    }

    /// <summary>
    /// Adds a custom credential resolver.
    /// </summary>
    public ModuleGitGuide AddCredentialResolver<TResolver>() where TResolver : class, IGitCredentialResolver
    {
        ConfigureServices(context =>
        {
            context.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitCredentialResolver, TResolver>());
        }, secondKey: typeof(TResolver).FullName);

        return this;
    }

    /// <summary>
    /// Enables the built-in GitHub webhook provider.
    /// </summary>
    public ModuleGitGuide UseGitHubWebhookProvider(Action<GitHubWebhookOption>? configure = null)
    {
        ConfigureModuleOption(option =>
        {
            configure?.Invoke(option.GitHubWebhook);
        }, secondKey: nameof(GitHubWebhookProvider));

        ConfigureServices(context =>
        {
            context.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitWebhookProvider, GitHubWebhookProvider>());
        }, secondKey: nameof(GitHubWebhookProvider));

        return this;
    }

    /// <summary>
    /// Enables the built-in GitLab webhook provider.
    /// </summary>
    public ModuleGitGuide UseGitLabWebhookProvider(Action<GitLabWebhookOption>? configure = null)
    {
        ConfigureModuleOption(option =>
        {
            configure?.Invoke(option.GitLabWebhook);
        }, secondKey: nameof(GitLabWebhookProvider));

        ConfigureServices(context =>
        {
            context.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitWebhookProvider, GitLabWebhookProvider>());
        }, secondKey: nameof(GitLabWebhookProvider));

        return this;
    }

    /// <summary>
    /// Registers a custom webhook provider.
    /// </summary>
    public ModuleGitGuide AddWebhookProvider<TProvider>(string? registrationKey = null)
        where TProvider : class, IGitWebhookProvider
    {
        ConfigureServices(context =>
        {
            context.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IGitWebhookProvider, TProvider>());
        }, secondKey: registrationKey ?? typeof(TProvider).FullName);

        return this;
    }
}

/// <summary>
/// Options for the Git synchronization module.
/// </summary>
public class ModuleGitOption : MoModuleOptionWithMinimalApi<ModuleGit>
{
    /// <summary>
    /// Gets the synchronization triggers that are allowed to execute.
    /// </summary>
    public HashSet<GitSyncTrigger> EnabledSyncTriggers { get; set; } =
    [
        GitSyncTrigger.Startup,
        GitSyncTrigger.Webhook,
        GitSyncTrigger.Manual
    ];

    /// <summary>
    /// Gets the configured repository registrations.
    /// </summary>
    public List<GitRepositoryRegistration> RepositoryRegistrations { get; set; } = [];

    /// <summary>
    /// Gets the configured repository bindings.
    /// </summary>
    public List<GitRepositoryBinding> RepositoryBindings { get; set; } = [];

    /// <summary>
    /// Gets the configured credential registrations.
    /// </summary>
    public List<GitCredentialRegistration> CredentialRegistrations { get; set; } = [];

    /// <summary>
    /// Gets or sets the GitHub webhook provider configuration.
    /// </summary>
    public GitHubWebhookOption GitHubWebhook { get; set; } = new();

    /// <summary>
    /// Gets or sets the GitLab webhook provider configuration.
    /// </summary>
    public GitLabWebhookOption GitLabWebhook { get; set; } = new();

    /// <summary>
    /// Replaces the enabled synchronization triggers.
    /// </summary>
    public void SetEnabledSyncTriggers(params GitSyncTrigger[] triggers)
    {
        EnabledSyncTriggers = triggers?
            .Distinct()
            .ToHashSet()
            ?? [];
    }

    /// <summary>
    /// Determines whether the specified synchronization trigger is enabled.
    /// </summary>
    public bool IsSyncTriggerEnabled(GitSyncTrigger trigger)
    {
        return EnabledSyncTriggers.Contains(trigger);
    }
}
