using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Avocado;

public sealed class CommandDefinition
{
    public string Command { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Parameter { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ShowWindow { get; set; }
}

public sealed record CommandMatch(
    CommandDefinition Definition,
    string ExpandedParameter,
    bool IsExecutable,
    int Score);

public static class CommandPaletteLogic
{
    private const string RegexPrefix = "regex:";

    public static IReadOnlyList<CommandMatch> Search(
        IEnumerable<CommandDefinition> commands,
        string input,
        int limit = 8)
    {
        var query = input.Trim();
        return commands
            .Where(IsUsable)
            .Select(command => CreateSearchMatch(command, query))
            .Where(match => match is not null)
            .Select(match => match!)
            .OrderByDescending(match => match.IsExecutable)
            .ThenByDescending(match => match.Score)
            .ThenBy(match => match.Definition.Command, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public static CommandMatch? Resolve(CommandDefinition command, string input)
    {
        if (!IsUsable(command)) return null;
        var query = input.Trim();
        if (TryMatch(command.Command, query, out var groups))
        {
            return new CommandMatch(command, Expand(command.Parameter, groups), true, 1000);
        }

        return IsStatic(command.Command)
            ? new CommandMatch(command, command.Parameter, true, 100)
            : null;
    }

    public static bool IsSupportedAction(string action) =>
        action.Equals("open-browser", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("run-bash", StringComparison.OrdinalIgnoreCase);

    private static CommandMatch? CreateSearchMatch(CommandDefinition command, string query)
    {
        if (query.Length == 0)
            return new CommandMatch(command, command.Parameter, IsStatic(command.Command), 1);

        if (TryMatch(command.Command, query, out var groups))
            return new CommandMatch(command, Expand(command.Parameter, groups), true, 1000);

        var display = DisplayPattern(command.Command);
        if (display.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return new CommandMatch(command, command.Parameter, IsStatic(command.Command), 200);
        if (display.Contains(query, StringComparison.OrdinalIgnoreCase))
            return new CommandMatch(command, command.Parameter, IsStatic(command.Command), 100);
        return null;
    }

    private static bool TryMatch(string command, string input, out IReadOnlyList<string> groups)
    {
        groups = [];
        try
        {
            var pattern = BuildPattern(command);
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            if (!match.Success) return false;
            groups = match.Groups.Cast<Group>().Skip(1).Select(group => group.Value).ToList();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string BuildPattern(string command)
    {
        if (command.StartsWith(RegexPrefix, StringComparison.OrdinalIgnoreCase))
            return Anchor(command[RegexPrefix.Length..]);
        if (command.Length >= 2 && command[0] == '/' && command[^1] == '/')
            return Anchor(command[1..^1]);
        if (LooksLikeRegex(command)) return Anchor(command);

        var escaped = Regex.Escape(command);
        escaped = Regex.Replace(escaped, "%[1-9]", "(.+?)");
        return Anchor(escaped);
    }

    private static bool LooksLikeRegex(string command) =>
        command.StartsWith('^') || command.EndsWith('$') ||
        command.IndexOfAny(['(', ')', '[', ']', '{', '}', '*', '+', '?', '|', '\\']) >= 0;

    private static bool IsStatic(string command) =>
        !command.Contains('%') &&
        !command.StartsWith(RegexPrefix, StringComparison.OrdinalIgnoreCase) &&
        !(command.Length >= 2 && command[0] == '/' && command[^1] == '/') &&
        !LooksLikeRegex(command);

    private static string Anchor(string pattern) => $"\\A(?:{pattern})\\z";

    private static string Expand(string template, IReadOnlyList<string> groups)
    {
        var result = template;
        for (var index = 0; index < groups.Count; index++)
            result = result.Replace($"%{index + 1}", groups[index], StringComparison.Ordinal);
        return result;
    }

    private static string DisplayPattern(string command) =>
        command.StartsWith(RegexPrefix, StringComparison.OrdinalIgnoreCase)
            ? command[RegexPrefix.Length..]
            : command.Trim('/');

    private static bool IsUsable(CommandDefinition command) =>
        !string.IsNullOrWhiteSpace(command.Command) && IsSupportedAction(command.Action);
}
