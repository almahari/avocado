namespace Avocado;

public static class TodoListLogic
{
    public static IEnumerable<T> TopItems<T>(IEnumerable<T> items, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        return items.Take(limit);
    }

    public static int HiddenCount(int total, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        return Math.Max(0, total - limit);
    }

    public static bool Reorder<T>(IList<T> items, T moving, T target, bool insertAfter)
    {
        var sourceIndex = items.IndexOf(moving);
        var targetIndex = items.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex) return false;

        var insertionIndex = targetIndex + (insertAfter ? 1 : 0);
        if (sourceIndex < insertionIndex) insertionIndex--;
        if (insertionIndex == sourceIndex) return false;

        items.RemoveAt(sourceIndex);
        items.Insert(Math.Clamp(insertionIndex, 0, items.Count), moving);
        return true;
    }

    public static TodoItem Duplicate(TodoItem source) => new()
    {
        Text = source.Text,
        ReminderTime = source.ReminderTime,
        Recurrence = source.Recurrence,
        Priority = source.Priority,
        DueAt = source.DueAt,
        IsPinned = source.IsPinned
    };
}
