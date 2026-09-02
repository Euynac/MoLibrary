using System.Text.Json;
using Monica.Guide;

namespace Monica.Guide.App;

/// <summary>Parsed guide application command line: wizard options or one guide CLI command.</summary>
public sealed record GuideAppOptions(int Port, bool OpenBrowser, string? BundlePath, AgentProductDefinition? Product)
{
    private static readonly string[] CliCommands =
        ["overview", "status", "doctor", "configure", "unconfigure", "init", "forget", "source", "issue", "workspaces"];

    /// <summary>The guide CLI arguments to dispatch, or null to launch the wizard.</summary>
    public string[]? CliArguments { get; private set; }

    /// <summary>
    /// Parses the argument surface: an explicit <c>--product</c> selector applies to both
    /// faces, a known guide subcommand switches to CLI dispatch, and anything else launches
    /// the token-gated wizard.
    /// </summary>
    public static GuideAppOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var port = 0;
        var openBrowser = true;
        string? bundlePath = null;
        AgentProductDefinition? product = null;
        var cliArguments = new List<string>();
        var isCli = args.Length > 0 && CliCommands.Contains(args[0], StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--no-open-browser":
                    openBrowser = false;
                    if (isCli) cliArguments.Add(args[index]);
                    break;
                case "--port" when index + 1 < args.Length && int.TryParse(args[index + 1], out var parsedPort):
                    port = parsedPort;
                    index++;
                    if (isCli) cliArguments.Add(args[index - 1]);
                    if (isCli) cliArguments.Add(args[index]);
                    break;
                case "--bundle" when index + 1 < args.Length:
                    bundlePath = args[index + 1];
                    index++;
                    if (isCli) cliArguments.Add(args[index - 1]);
                    if (isCli) cliArguments.Add(args[index]);
                    break;
                case "--product" when index + 1 < args.Length:
                    product = ResolveProduct(args[index + 1]);
                    index++;
                    break;
                default:
                    if (isCli)
                    {
                        cliArguments.Add(args[index]);
                        break;
                    }
                    throw new ArgumentException(
                        $"Unknown guide argument '{args[index]}'. Usage: Monica.Guide [overview|status|doctor|configure|unconfigure|init|forget|source <action>|issue <action>|workspaces] [--product monica|workflow] ... | [--no-open-browser] [--port <1-65535>] [--bundle <path>]");
            }
        }

        var cli = isCli ? cliArguments.ToArray() : null;
        if (cli is not null && product is null)
        {
            product = DetectDefaultProduct();
        }

        return new GuideAppOptions(port, openBrowser, bundlePath, product) { CliArguments = cli };
    }

    private static AgentProductDefinition ResolveProduct(string selector)
        => KnownAgentProducts.All.FirstOrDefault(product =>
               string.Equals(product.ProductId, selector, StringComparison.OrdinalIgnoreCase)
               || string.Equals(product.ProductName, selector, StringComparison.OrdinalIgnoreCase)
               || string.Equals(product.DisplayName, selector, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException(
               $"Unknown product '{selector}'. Registered products: {string.Join(", ", KnownAgentProducts.All.Select(static product => product.ProductName))}.");

    /// <summary>
    /// The product this executable belongs to by structure: the bundle that ships it declares
    /// its product id; outside a bundle the guide serves the Monica skill catalog by default.
    /// Both faces use it, so a product bundle's setup wizard opens pinned to that product.
    /// </summary>
    internal static AgentProductDefinition DetectDefaultProduct()
    {
        var bundleRoot = GuideSetupPresenter.DetectBundleRootByStructure(AppContext.BaseDirectory);
        if (bundleRoot is null)
        {
            return KnownAgentProducts.Monica;
        }
        try
        {
            // The manifest lives at the bundle root for both layouts; ResolveManifestPath
            // maps an entry-tree directory onto it when the root is passed directly.
            var manifest = GuideReleaseMetadata.Observe(bundleRoot, null).Manifest;
            if (manifest is not null
                && KnownAgentProducts.FindByProductId(manifest.ProductId) is { } product)
            {
                return product;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentException)
        {
            // Structural defaults must never block startup; the wizard surfaces bundle errors.
        }
        return KnownAgentProducts.Monica;
    }
}
