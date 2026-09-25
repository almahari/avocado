using System.Diagnostics;
using System.IO;

namespace Avocado;

public static class CommandActionExecutor
{
    public static Process? Execute(CommandMatch match)
    {
        if (match.Definition.Action.Equals("open-browser", StringComparison.OrdinalIgnoreCase))
        {
            var target = match.ExpandedParameter.Trim();
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                target = "https://" + target;
            }
            return Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        if (match.Definition.Action.Equals("run-bash", StringComparison.OrdinalIgnoreCase))
        {
            return Process.Start(CreateBashStartInfo(
                match.ExpandedParameter,
                showWindow: match.Definition.ShowWindow));
        }

        throw new InvalidOperationException($"Unsupported command action: {match.Definition.Action}");
    }

    public static ProcessStartInfo CreateBashStartInfo(
        string command,
        string? bashPath = null,
        bool showWindow = false)
    {
        var resolvedBashPath = bashPath ?? ResolveGitBashPath();
        var bashCommand = BuildBashCommand(command);
        if (showWindow)
        {
            var visibleCommand =
                $"{bashCommand}; avocado_status=$?; " +
                "printf '\n\n[Avocado] Exit code: %s\n' \"$avocado_status\"; " +
                "printf 'Press Enter to close...'; read -r; exit \"$avocado_status\"";
            var visibleStartInfo = new ProcessStartInfo(ResolveMinttyPath(resolvedBashPath))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };
            visibleStartInfo.ArgumentList.Add("-t");
            visibleStartInfo.ArgumentList.Add("Avocado command");
            visibleStartInfo.ArgumentList.Add("/usr/bin/bash");
            visibleStartInfo.ArgumentList.Add("-lc");
            visibleStartInfo.ArgumentList.Add(visibleCommand);
            return visibleStartInfo;
        }

        var startInfo = new ProcessStartInfo(resolvedBashPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add(bashCommand);
        return startInfo;
    }

    public static string BuildBashCommand(string command)
    {
        var trimmed = command.Trim();
        if (!TryReadScriptPath(trimmed, out var configuredPath, out var arguments)) return trimmed;

        var windowsPath = Environment.ExpandEnvironmentVariables(configuredPath);
        if (windowsPath.StartsWith("~/", StringComparison.Ordinal))
            windowsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), windowsPath[2..]);
        windowsPath = Path.GetFullPath(windowsPath);
        if (!File.Exists(windowsPath))
            throw new FileNotFoundException("The configured shell script was not found.", windowsPath);

        var directory = ToGitBashPath(Path.GetDirectoryName(windowsPath)!);
        var script = "./" + Path.GetFileName(windowsPath);
        return $"cd -- {QuoteForBash(directory)} && bash {QuoteForBash(script)}{arguments}";
    }

    public static string ResolveGitBashPath()
    {
        var configured = Environment.GetEnvironmentVariable("AVOCADO_GIT_BASH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Git", "bin", "bash.exe")
        };
        var gitBash = candidates.FirstOrDefault(File.Exists);
        if (gitBash is not null) return gitBash;

        throw new FileNotFoundException(
            "Git Bash was not found. Install Git for Windows or set AVOCADO_GIT_BASH to bash.exe.");
    }

    public static string ResolveMinttyPath(string bashPath)
    {
        var gitDirectory = Directory.GetParent(Path.GetDirectoryName(bashPath)!)?.FullName;
        var minttyPath = gitDirectory is null
            ? string.Empty
            : Path.Combine(gitDirectory, "usr", "bin", "mintty.exe");
        if (File.Exists(minttyPath)) return minttyPath;
        throw new FileNotFoundException(
            "The Git Bash terminal (mintty.exe) was not found. Reinstall Git for Windows.", minttyPath);
    }

    private static bool TryReadScriptPath(string command, out string path, out string arguments)
    {
        path = string.Empty;
        arguments = string.Empty;
        if (command.Length == 0) return false;

        if (command[0] is '\'' or '"')
        {
            var quote = command[0];
            var end = command.IndexOf(quote, 1);
            if (end < 0) return false;
            path = command[1..end];
            arguments = command[(end + 1)..];
        }
        else
        {
            var end = command.IndexOf(".sh", StringComparison.OrdinalIgnoreCase);
            if (end < 0) return false;
            end += 3;
            path = command[..end];
            arguments = command[end..];
        }

        return path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) &&
               (Path.IsPathRooted(path) || path.StartsWith("~/", StringComparison.Ordinal));
    }

    private static string ToGitBashPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':')
            return $"/{char.ToLowerInvariant(normalized[0])}{normalized[2..]}";
        return normalized;
    }

    private static string QuoteForBash(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
}
