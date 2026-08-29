namespace Avocado;

public enum ArchiveRetentionOption
{
    Never = 0,
    SevenDays = 7,
    ThirtyDays = 30,
    NinetyDays = 90
}

public sealed record ArchiveRetentionChoice(
    ArchiveRetentionOption Option,
    string DisplayName,
    TimeSpan? Retention);

public static class ArchiveRetentionSettings
{
    public static IReadOnlyList<ArchiveRetentionChoice> Choices { get; } =
    [
        new(ArchiveRetentionOption.Never, "Never", null),
        new(ArchiveRetentionOption.SevenDays, "After 7 days", TimeSpan.FromDays(7)),
        new(ArchiveRetentionOption.ThirtyDays, "After 30 days", TimeSpan.FromDays(30)),
        new(ArchiveRetentionOption.NinetyDays, "After 90 days", TimeSpan.FromDays(90))
    ];

    public const ArchiveRetentionOption Default = ArchiveRetentionOption.Never;

    public static ArchiveRetentionChoice Get(ArchiveRetentionOption option) =>
        Choices.FirstOrDefault(choice => choice.Option == option) ?? Get(Default);

    public static int RemoveExpired(IList<TodoItem> archivedTasks, ArchiveRetentionOption option, DateTime now)
    {
        var retention = Get(option).Retention;
        if (retention is null) return 0;
        var cutoff = now - retention.Value;
        var removed = 0;
        for (var index = archivedTasks.Count - 1; index >= 0; index--)
        {
            if (archivedTasks[index].CompletedAt is not DateTime completedAt || completedAt >= cutoff) continue;
            archivedTasks.RemoveAt(index);
            removed++;
        }
        return removed;
    }
}
