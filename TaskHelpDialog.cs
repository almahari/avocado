using System.Drawing;
using Forms = System.Windows.Forms;

namespace Avocado;

public sealed class TaskHelpDialog : Forms.Form
{
    private const string HelpText =
        "Create tasks with these input patterns:\r\n\r\n" +
        "• Plain task\r\n" +
        "  Buy groceries\r\n\r\n" +
        "• Multiple tasks in one entry, separated by semicolons\r\n" +
        "  task 1; 12:00 task 2\r\n\r\n" +
        "• Time-based reminder\r\n" +
        "  17:50 Submit report\r\n\r\n" +
        "• Natural dates\r\n" +
        "  today 3pm Call Ali\r\n" +
        "  tomorrow 9am Call Ali\r\n" +
        "  Friday Submit report\r\n" +
        "  next Friday 14:30 Submit report\r\n\r\n" +
        "• Exact date\r\n" +
        "  2026-09-03 08:15 Release build\r\n\r\n" +
        "• Recurring reminders: daily\r\n" +
        "  daily 09:00 Drink water\r\n\r\n" +
        "• Recurring reminders: weekly (weekday name + time)\r\n" +
        "  monday 18:00 Gym\r\n" +
        "  friday 16:30 !! Send weekly summary\r\n" +
        "  Works with monday, tuesday, wednesday, thursday, friday, saturday, sunday.\r\n" +
        "  Without a time (Monday Gym) it is a one-time reminder, not weekly.\r\n" +
        "  Daily and weekly need a 24-hour HH:mm time: monday 6pm Gym is one-time.\r\n\r\n" +
        "• Recurring reminders: monthly (day 1-31 or last + time)\r\n" +
        "  monthly 1 09:00 Pay rent            (1st of every month)\r\n" +
        "  monthly 15th 2:30pm Send invoice    (15th of every month)\r\n" +
        "  monthly last 17:00 Close the books  (last day of every month)\r\n" +
        "  monthly 09:00 Review budget         (no day = the 1st)\r\n" +
        "  A day the month doesn't have (e.g. 31) falls on its last day.\r\n\r\n" +
        "• Ticking off a recurring task hides it until its next time\r\n" +
        "  (tomorrow, next Monday, or next month) while 'Done recurring\r\n" +
        "  tasks come back next time' is on in the tray menu.\r\n\r\n" +
        "• Missed a weekly or monthly task? Its row shows MISSED in red until\r\n" +
        "  you tick it off or its next day arrives.\r\n" +
        "• Reminders that fell while Avocado was closed or the PC was asleep\r\n" +
        "  fire once when it starts or wakes (same day only).\r\n\r\n" +
        "• Priority marks\r\n" +
        "  ! Read article\r\n" +
        "  !! Prepare notes\r\n" +
        "  !!! Ship release\r\n\r\n" +
        "• Clickable links\r\n" +
        "  https://example.com/docs\r\n" +
        "  https://example.com : Open documentation\r\n\r\n" +
        "• Categories\r\n" +
        "  Add hashtags like #work or #personal to filter later.\r\n\r\n" +
        "Notes:\r\n" +
        "• Times use 24-hour HH:mm, but natural dates also accept 12-hour forms like 9am or 2:30pm.\r\n" +
        "• For scheduled tasks, put priority marks after the time, such as friday 16:30 !! Send summary.\r\n" +
        "• Select +, Ctrl+N, or the global quick-add shortcut to open the task editor.\r\n" +
        "• Press Ctrl+Alt+V to create a task from clipboard text.";

    private TaskHelpDialog()
    {
        Text = "Avocado - Task help";
        Width = 700;
        Height = 640;
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = Forms.FormStartPosition.CenterScreen;
        TopMost = true;
        Font = new Font("Segoe UI", 9);

        var header = new Forms.Label
        {
            Text = "Available options for creating tasks",
            Left = 16,
            Top = 16,
            Width = 640,
            Height = 24,
            Font = new Font(Font, FontStyle.Bold)
        };

        var textBox = new Forms.TextBox
        {
            Left = 16,
            Top = 48,
            Width = 648,
            Height = 520,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = Forms.ScrollBars.Vertical,
            Text = HelpText,
            Font = new Font("Consolas", 9),
            BorderStyle = Forms.BorderStyle.FixedSingle
        };

        var closeButton = new Forms.Button
        {
            Text = "Close",
            Width = 90,
            Height = 30,
            Left = Width - 122,
            Top = 578,
            DialogResult = Forms.DialogResult.OK
        };

        Controls.Add(header);
        Controls.Add(textBox);
        Controls.Add(closeButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    public static void ShowHelp()
    {
        using var dialog = new TaskHelpDialog();
        dialog.ShowDialog();
    }
}
