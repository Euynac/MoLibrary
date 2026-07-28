using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Exceptions;
using Monica.Authority.Authorization.Models;
using Monica.Authority.Authorization.Services;
using Monica.Authority.Authorization.Services.Behaviors;
using Monica.Authority.Identity.Abstractions;
using Monica.Core.Execution;
using Xunit;

namespace Test.Monica.Authority.Authorization;

public sealed class ExecutionAuthorizationTests
{
    [Fact]
    public async Task CheckAsync_WhenOperationHasNoMetadata_ShouldBypassAuthorization()
    {
        var policyProvider = new TrackingPolicyProvider();
        var authorizationService = new TrackingAuthorizationService(AuthorizationResult.Failed());
        var service = CreateService(policyProvider, authorizationService);

        await service.CheckAsync(
            CreateAuthorizationContext<UnprotectedComponent>(
                nameof(UnprotectedComponent.Execute),
                new ClaimsPrincipal()),
            TestContext.Current.CancellationToken);

        policyProvider.DefaultPolicyRequestCount.Should().Be(0);
        authorizationService.AuthorizationCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckAsync_WhenOperationAllowsAnonymous_ShouldBypassAuthorization()
    {
        var policyProvider = new TrackingPolicyProvider();
        var authorizationService = new TrackingAuthorizationService(AuthorizationResult.Failed());
        var service = CreateService(policyProvider, authorizationService);

        await service.CheckAsync(
            CreateAuthorizationContext<AnonymousComponent>(
                nameof(AnonymousComponent.Execute),
                new ClaimsPrincipal()),
            TestContext.Current.CancellationToken);

        policyProvider.DefaultPolicyRequestCount.Should().Be(0);
        authorizationService.AuthorizationCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckAsync_WhenProtectedOperationIsAnonymous_ShouldRejectAsNotLoggedIn()
    {
        var service = CreateService(
            new TrackingPolicyProvider(),
            new TrackingAuthorizationService(AuthorizationResult.Success()));
        var context = CreateAuthorizationContext<ProtectedComponent>(
            nameof(ProtectedComponent.Execute),
            new ClaimsPrincipal());

        var action = () => service.CheckAsync(context, TestContext.Current.CancellationToken);

        var assertion = await action.Should().ThrowAsync<AuthorizationException>();
        assertion.Which.Type.Should().Be(AuthorizationException.ExceptionType.NotLogin);
    }

    [Fact]
    public async Task CheckAsync_WhenClassIsProtectedAndEntryMethodIsUnavailable_ShouldRejectAsNotLoggedIn()
    {
        var service = CreateService(
            new TrackingPolicyProvider(),
            new TrackingAuthorizationService(AuthorizationResult.Success()));
        var context = new ExecutionAuthorizationContext(
            CreateExecutionContextWithoutEntryMethod<ClassProtectedComponent>().Descriptor,
            new ClaimsPrincipal());

        var action = () => service.CheckAsync(context, TestContext.Current.CancellationToken);

        var assertion = await action.Should().ThrowAsync<AuthorizationException>();
        assertion.Which.Type.Should().Be(AuthorizationException.ExceptionType.NotLogin);
    }

    [Fact]
    public async Task CheckAsync_WhenClassAllowsAnonymousAndEntryMethodIsUnavailable_ShouldBypassAuthorization()
    {
        var policyProvider = new TrackingPolicyProvider();
        var authorizationService = new TrackingAuthorizationService(AuthorizationResult.Failed());
        var service = CreateService(policyProvider, authorizationService);
        var context = new ExecutionAuthorizationContext(
            CreateExecutionContextWithoutEntryMethod<ClassAnonymousComponent>().Descriptor,
            new ClaimsPrincipal());

        await service.CheckAsync(context, TestContext.Current.CancellationToken);

        policyProvider.DefaultPolicyRequestCount.Should().Be(0);
        authorizationService.AuthorizationCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckAsync_WhenAuthenticatedPrincipalIsAuthorized_ShouldEnforcePolicyAndAllow()
    {
        var policyProvider = new TrackingPolicyProvider();
        var authorizationService = new TrackingAuthorizationService(AuthorizationResult.Success());
        var service = CreateService(policyProvider, authorizationService);
        var principal = CreateAuthenticatedPrincipal();

        await service.CheckAsync(
            CreateAuthorizationContext<ProtectedComponent>(
                nameof(ProtectedComponent.Execute),
                principal),
            TestContext.Current.CancellationToken);

        policyProvider.DefaultPolicyRequestCount.Should().Be(1);
        authorizationService.AuthorizationCount.Should().Be(1);
        authorizationService.LastPrincipal.Should().BeSameAs(principal);
    }

    [Fact]
    public async Task CheckAsync_WhenAuthenticatedPrincipalIsDenied_ShouldRejectAsPermissionDenied()
    {
        var authorizationService = new TrackingAuthorizationService(AuthorizationResult.Failed());
        var service = CreateService(new TrackingPolicyProvider(), authorizationService);
        var context = CreateAuthorizationContext<ProtectedComponent>(
            nameof(ProtectedComponent.Execute),
            CreateAuthenticatedPrincipal());

        var action = () => service.CheckAsync(context, TestContext.Current.CancellationToken);

        var assertion = await action.Should().ThrowAsync<AuthorizationException>();
        assertion.Which.Type.Should().Be(AuthorizationException.ExceptionType.PermissionDenied);
        authorizationService.AuthorizationCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckAsync_WhenAlreadyCanceled_ShouldStopBeforePolicyEvaluation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var policyProvider = new TrackingPolicyProvider();
        var authorizationService = new TrackingAuthorizationService(AuthorizationResult.Success());
        var service = CreateService(policyProvider, authorizationService);

        var action = () => service.CheckAsync(
            CreateAuthorizationContext<ProtectedComponent>(nameof(ProtectedComponent.Execute), CreateAuthenticatedPrincipal()),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        policyProvider.DefaultPolicyRequestCount.Should().Be(0);
        authorizationService.AuthorizationCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckAsync_WhenCanceledDuringAuthentication_ShouldPreferCancellationOverAuthenticationFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var authenticationService = new CancelingAuthenticationService(cancellation);
        using var requestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticationService)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        var service = new ExecutionAuthorizationService(
            new TrackingPolicyProvider(),
            new TrackingAuthorizationService(AuthorizationResult.Success()),
            new HttpContextAccessor { HttpContext = httpContext });

        var action = () => service.CheckAsync(
            CreateAuthorizationContext<ProtectedComponent>(
                nameof(ProtectedComponent.Execute),
                new ClaimsPrincipal()),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        authenticationService.AuthenticationCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorized_ShouldAuthorizeBeforeInvokingTerminal()
    {
        var callOrder = new List<string>();
        var principal = CreateAuthenticatedPrincipal();
        var authorizationService = new RecordingExecutionAuthorizationService(callOrder);
        var behavior = new ExecutionAuthorizationBehavior<string, int>(
            authorizationService,
            new FixedPrincipalAccessor(principal));
        var context = CreateExecutionContext<ProtectedComponent>(nameof(ProtectedComponent.Execute));

        var result = await behavior.ExecuteAsync(
            context,
            () =>
            {
                callOrder.Add("terminal");
                return Task.FromResult(42);
            });

        result.Should().Be(42);
        callOrder.Should().Equal("authorize", "terminal");
        authorizationService.Context!.Principal.Should().BeSameAs(principal);
        authorizationService.Context.Descriptor.Should().BeSameAs(context.Descriptor);
        authorizationService.CancellationToken.Should().Be(context.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationFails_ShouldNotInvokeTerminal()
    {
        var expected = new AuthorizationException(AuthorizationException.ExceptionType.PermissionDenied);
        var authorizationService = new RecordingExecutionAuthorizationService([], expected);
        var behavior = new ExecutionAuthorizationBehavior<string, int>(
            authorizationService,
            new FixedPrincipalAccessor(CreateAuthenticatedPrincipal()));
        var terminalCalled = false;

        var action = () => behavior.ExecuteAsync(
            CreateExecutionContext<ProtectedComponent>(nameof(ProtectedComponent.Execute)),
            () =>
            {
                terminalCalled = true;
                return Task.FromResult(42);
            });

        (await action.Should().ThrowAsync<AuthorizationException>()).Which.Should().BeSameAs(expected);
        terminalCalled.Should().BeFalse();
    }

    private static ExecutionAuthorizationService CreateService(
        TrackingPolicyProvider policyProvider,
        TrackingAuthorizationService authorizationService)
    {
        return new ExecutionAuthorizationService(
            policyProvider,
            authorizationService,
            new HttpContextAccessor());
    }

    private static ExecutionAuthorizationContext CreateAuthorizationContext<TComponent>(
        string methodName,
        ClaimsPrincipal principal)
    {
        return new ExecutionAuthorizationContext(
            CreateExecutionContext<TComponent>(methodName).Descriptor,
            principal);
    }

    private static ExecutionContext<string> CreateExecutionContext<TComponent>(string methodName)
    {
        var method = typeof(TComponent).GetMethod(methodName)!;
        var descriptor = new ExecutionDescriptor(
            new ExecutionPoint("test.authorization"),
            methodName,
            typeof(TComponent),
            method,
            typeof(string),
            typeof(int),
            isBusinessOperation: true,
            isLongRunning: false);
        return new ExecutionContext<string>(
            descriptor,
            "input",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static ExecutionContext<string> CreateExecutionContextWithoutEntryMethod<TComponent>()
    {
        var descriptor = new ExecutionDescriptor(
            new ExecutionPoint("test.authorization"),
            "class-only",
            typeof(TComponent),
            entryMethod: null,
            typeof(string),
            typeof(int),
            isBusinessOperation: true,
            isLongRunning: false);
        return new ExecutionContext<string>(
            descriptor,
            "input",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "operator")],
            "test"));
    }

    private sealed class TrackingPolicyProvider : IAuthorityAuthorizationPolicyProvider
    {
        public int DefaultPolicyRequestCount { get; private set; }

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        {
            DefaultPolicyRequestCount++;
            return Task.FromResult(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        }

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        public Task<List<string>> GetPoliciesNamesAsync()
        {
            return Task.FromResult<List<string>>([]);
        }
    }

    private sealed class TrackingAuthorizationService(AuthorizationResult result)
        : IAuthorityAuthorizationService
    {
        public int AuthorizationCount { get; private set; }

        public ClaimsPrincipal? LastPrincipal { get; private set; }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            AuthorizationCount++;
            LastPrincipal = user;
            return Task.FromResult(result);
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingExecutionAuthorizationService(
        List<string> callOrder,
        Exception? exception = null)
        : IExecutionAuthorizationService
    {
        public ExecutionAuthorizationContext? Context { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task CheckAsync(
            ExecutionAuthorizationContext context,
            CancellationToken cancellationToken)
        {
            Context = context;
            CancellationToken = cancellationToken;
            callOrder.Add("authorize");
            return exception is null
                ? Task.CompletedTask
                : Task.FromException(exception);
        }
    }

    private sealed class FixedPrincipalAccessor(ClaimsPrincipal principal) : ICurrentPrincipalAccessor
    {
        public ClaimsPrincipal Principal => principal;
    }

    private sealed class CancelingAuthenticationService(CancellationTokenSource cancellation)
        : IAuthenticationService
    {
        public int AuthenticationCount { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            AuthenticationCount++;
            cancellation.Cancel();
            return Task.FromResult(AuthenticateResult.Fail("authentication failed"));
        }

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class UnprotectedComponent
    {
        public void Execute()
        {
        }
    }

    private sealed class ProtectedComponent
    {
        [Authorize]
        public void Execute()
        {
        }
    }

    private sealed class AnonymousComponent
    {
        [Authorize]
        [AllowAnonymous]
        public void Execute()
        {
        }
    }

    [Authorize]
    private sealed class ClassProtectedComponent;

    [Authorize]
    [AllowAnonymous]
    private sealed class ClassAnonymousComponent;
}
