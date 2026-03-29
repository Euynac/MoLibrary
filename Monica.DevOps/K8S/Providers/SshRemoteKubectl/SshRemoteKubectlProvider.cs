using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DevOps.K8S.Abstractions;
using Monica.DevOps.K8S.Exceptions;
using Monica.DevOps.K8S.Models;
using Renci.SshNet;

namespace Monica.DevOps.K8S.Providers.SshRemoteKubectl;

public class SshRemoteKubectlProvider(
    IOptions<Modules.ModuleK8SOption> options,
    ILogger<SshRemoteKubectlProvider> logger) : IK8SProvider
{
    private readonly Modules.ModuleK8SOption _option = options.Value;

    public Task<string> ExecuteKubectlAsync(K8SRuntimeConfig runtimeConfig, string kubectlArguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(kubectlArguments);

        runtimeConfig.ValidateForConnection();

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var commandText = $"{_option.KubectlCommand} {kubectlArguments}".Trim();
            logger.LogInformation("Executing remote kubectl command on {ExecutionNode}: {Command}", runtimeConfig.ExecutionNode, commandText);

            using var client = new SshClient(runtimeConfig.ExecutionNode, _option.SshPort, runtimeConfig.UserName, runtimeConfig.Password);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(_option.CommandTimeoutSeconds);
            client.Connect();

            using var command = client.CreateCommand(commandText);
            command.CommandTimeout = TimeSpan.FromSeconds(_option.CommandTimeoutSeconds);

            var standardOutput = command.Execute();
            var standardError = command.Error ?? string.Empty;

            if (command.ExitStatus != 0)
            {
                throw K8SOperationException.RemoteKubectlCommandFailed(
                    command.ExitStatus ?? -1,
                    GetPrimaryMessage(standardError, standardOutput));
            }

            return string.IsNullOrWhiteSpace(standardOutput) ? standardError : standardOutput;
        }, cancellationToken);
    }

    private static string GetPrimaryMessage(string standardError, string standardOutput)
    {
        if (!string.IsNullOrWhiteSpace(standardError))
        {
            return standardError.Trim();
        }

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            return standardOutput.Trim();
        }

        return string.Empty;
    }
}
