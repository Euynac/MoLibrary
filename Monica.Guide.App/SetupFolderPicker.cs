using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Monica.Guide.App;

/// <summary>
/// Native folder selection for wizard path fields, one face per host: the WinForms dialog
/// on Windows (compiled only into the windows TFM), osascript on macOS, and zenity or
/// kdialog on Linux desktops. A picker failure or a headless host without any dialog
/// returns null; manual path entry always remains available.
/// </summary>
public static class SetupFolderPicker
{
    private static readonly Lazy<bool> LinuxDialogAvailable = new(DetectLinuxDialog);

    /// <summary>Whether this host can show a native directory dialog at all.</summary>
    public static bool IsSupported
        => OperatingSystem.IsWindows()
           || OperatingSystem.IsMacOS()
           || LinuxDialogAvailable.Value;

    /// <summary>Shows the native folder picker; returns the chosen path or null when cancelled.</summary>
    public static async Task<string?> PickDirectoryAsync(string? title, string? initialPath = null)
    {
        try
        {
            return await PickAsync(title, initialPath).ConfigureAwait(false);
        }
        catch
        {
            // A picker failure must never break the wizard; manual path entry still works.
            return null;
        }
    }

    private static async Task<string?> PickAsync(string? title, string? initialPath)
    {
        if (OperatingSystem.IsWindows())
        {
#if WINDOWS
            return await WindowsFolderDialog.PickAsync(title, initialPath).ConfigureAwait(false);
#else
            // The portable build carries no WinForms; PowerShell hosts the same native
            // FolderBrowserDialog out of process so Windows still gets a real picker.
            return await PowerShellFolderDialog.PickAsync(title, initialPath).ConfigureAwait(false);
#endif
        }

        if (OperatingSystem.IsMacOS())
        {
            // osascript ships with macOS; 'choose folder' prints the POSIX path in quotes.
            var prompt = string.IsNullOrWhiteSpace(title)
                ? string.Empty
                : $" with prompt {AppleQuoted(title)}";
            var dialog = await RunDialogAsync(
                "osascript",
                ["-e", $"POSIX path of (choose folder{prompt})"],
                static output => output.Trim().Trim('"')).ConfigureAwait(false);
            return dialog.Ran ? dialog.Directory : null;
        }

        if (OperatingSystem.IsLinux())
        {
            // GNOME's zenity first, then KDE's kdialog; the first one present wins.
            var zenityArguments = new List<string> { "--file-selection", "--directory" };
            if (!string.IsNullOrWhiteSpace(title))
            {
                zenityArguments.Add($"--title={title}");
            }
            if (!string.IsNullOrWhiteSpace(initialPath))
            {
                zenityArguments.Add($"--filename={EnsureTrailingSeparator(initialPath)}");
            }

            var zenity = await RunDialogAsync("zenity", zenityArguments, static output => output.Trim()).ConfigureAwait(false);
            if (zenity.Ran)
            {
                return zenity.Directory;
            }

            var kdialogArguments = new List<string> { "--getexistingdirectory", initialPath ?? Environment.GetEnvironmentVariable("HOME") ?? "." };
            if (!string.IsNullOrWhiteSpace(title))
            {
                kdialogArguments.Insert(0, $"--title={title}");
            }

            var kdialog = await RunDialogAsync("kdialog", kdialogArguments, static output => output.Trim()).ConfigureAwait(false);
            return kdialog.Ran ? kdialog.Directory : null;
        }

        return null;
    }

    private static bool DetectLinuxDialog()
    {
        try
        {
            var info = new ProcessStartInfo("/bin/sh")
            {
                ArgumentList = { "-c", "command -v zenity >/dev/null 2>&1 || command -v kdialog >/dev/null 2>&1" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(info);
            if (process is null)
            {
                return false;
            }
            process.WaitForExit(5_000);
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Runs one dialog command; a false <c>Ran</c> means the binary is absent.</summary>
    private static async Task<(bool Ran, string? Directory)> RunDialogAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Func<string, string?> parseResult)
    {
        try
        {
            var start = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            if (process is null)
            {
                return (false, null);
            }

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0 ? (true, parseResult(output)) : (true, null);
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            // The dialog binary is not installed on this host.
            return (false, null);
        }
    }

#if !WINDOWS
    /// <summary>
    /// Native dialog for the portable build: PowerShell hosts the picker out of process.
    /// It prefers the modern Explorer dialog (WPF OpenFolderDialog, available on .NET 8+
    /// hosts such as pwsh 7.4) and falls back to the WinForms tree dialog on Windows
    /// PowerShell 5.1.
    /// </summary>
    private static class PowerShellFolderDialog
    {
        public static async Task<string?> PickAsync(string? title, string? initialPath)
        {
            var selectedPath = string.IsNullOrWhiteSpace(initialPath) || !Directory.Exists(initialPath)
                ? null
                : Path.GetFullPath(initialPath);
            var script = new StringBuilder()
                .Append("Add-Type -AssemblyName PresentationFramework -ErrorAction SilentlyContinue; ")
                .Append("$type = [System.Type]::GetType('Microsoft.Win32.OpenFolderDialog, PresentationFramework'); ")
                .Append("if ($null -ne $type) { ")
                .Append("$dialog = [System.Activator]::CreateInstance($type); ")
                .Append("$dialog.Title = ").Append(PowerShellLiteral(title ?? string.Empty)).Append("; ");
            if (selectedPath is not null)
            {
                script.Append("$dialog.InitialDirectory = ").Append(PowerShellLiteral(selectedPath)).Append("; ");
            }
            script
                .Append("$owner = New-Object System.Windows.Window; ")
                .Append("$owner.Topmost = $true; $owner.ShowInTaskbar = $false; $owner.ShowActivated = $false; ")
                .Append("$owner.WindowStyle = 'None'; $owner.AllowsTransparency = $true; $owner.Opacity = 0; ")
                .Append("$owner.Width = 0; $owner.Height = 0; $owner.Show(); ")
                .Append("if ($dialog.ShowDialog($owner)) { [Console]::Out.Write($dialog.FolderName) }; ")
                .Append("$owner.Close() ")
                .Append("} else { ")
                .Append("Add-Type -AssemblyName System.Windows.Forms; ")
                .Append("$dialog = New-Object System.Windows.Forms.FolderBrowserDialog; ")
                .Append("$dialog.ShowNewFolderButton = $false; ")
                .Append("$dialog.Description = ").Append(PowerShellLiteral(title ?? string.Empty)).Append("; ")
                .Append("$dialog.UseDescriptionForTitle = $true; ");
            if (selectedPath is not null)
            {
                script.Append("$dialog.SelectedPath = ").Append(PowerShellLiteral(selectedPath)).Append("; ");
            }
            script
                .Append("$owner = New-Object System.Windows.Forms.Form; ")
                .Append("$owner.TopMost = $true; $owner.ShowInTaskbar = $false; ")
                .Append("$owner.FormBorderStyle = 'None'; $owner.Opacity = 0; $owner.StartPosition = 'Manual'; ")
                .Append("$owner.Width = 0; $owner.Height = 0; $owner.Show(); ")
                .Append("if ($dialog.ShowDialog($owner) -eq [System.Windows.Forms.DialogResult]::OK) ")
                .Append("{ [Console]::Out.Write($dialog.SelectedPath) }; ")
                .Append("$owner.Close() ")
                .Append("}");

            var start = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // -STA gives the dialog the apartment state both dialogs require.
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-STA");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add(script.ToString());

            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output.Trim()
                : null;
        }

        private static string PowerShellLiteral(string value)
            => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
#endif

    private static string AppleQuoted(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith('/') || path.EndsWith('\\') ? path : $"{path}/";

#if WINDOWS
    /// <summary>The Explorer-style folder dialog (WPF OpenFolderDialog) on a dedicated STA thread.</summary>
    private static class WindowsFolderDialog
    {
        public static async Task<string?> PickAsync(string? title, string? initialPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => completion.SetResult(Show(title, initialPath)))
            {
                Name = "guide-folder-picker",
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return await completion.Task.ConfigureAwait(false);
        }

        private static string? Show(string? title, string? initialPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = title ?? string.Empty,
                    Multiselect = false
                };
                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                {
                    dialog.InitialDirectory = Path.GetFullPath(initialPath);
                }

                // The wizard runs behind the user's browser; a dialog without an owner
                // lands in the browser's z-order layer. A borderless fully transparent
                // topmost owner keeps the owned dialog above every normal window while
                // staying invisible itself.
                var owner = new System.Windows.Window
                {
                    Topmost = true,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStyle = System.Windows.WindowStyle.None,
                    ResizeMode = System.Windows.ResizeMode.NoResize,
                    AllowsTransparency = true,
                    Opacity = 0,
                    Width = 0,
                    Height = 0
                };
                try
                {
                    owner.Show();
                    return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
                }
                finally
                {
                    owner.Close();
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"folder-picker failed: {exception}");
                // A picker failure must never break the wizard; manual path entry still works.
                return null;
            }
        }
    }
#endif
}
