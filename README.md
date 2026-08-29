# Avocado

Avocado is a borderless pixel-art todo widget for Windows. It lives in the notification tray, can be dragged anywhere on the desktop, and can run as either a normal window or always on top.

## Run locally

Requires the .NET 8 SDK on Windows.

```powershell
dotnet run --project .\Avocado.csproj
```

## Controls

- Drag any non-control part of the fruit to move it.
- Select `+`, press `Ctrl+N` while focused, or press the global `Ctrl+Alt+N` shortcut to add a task. Press Enter to save or Escape to cancel.
- Separate multiple new tasks with `;`, for example `task 1; 12:00 task 2`. Each task is parsed independently.
- Press the global `Ctrl+Alt+V` shortcut to immediately create a task from clipboard text. Normal task syntax for links, priorities, and reminders is supported.
- Select the avocado's `×` to hide it to the notification tray.
- Double-click the tray icon to show or hide the avocado.
- Open the tray icon menu to choose **Normal window**, **Always on top**, themes, reminder sounds, Do Not Disturb hours, startup behavior, or **Exit**.
- Toggle **Adaptive personality** in the tray menu to show or hide the fruit’s reactive face. The choice is saved.
- From the tray menu, choose **Size → Normal** or **Size → Small**; Small is exactly half-size.
- Enable **Resize when inactive** and choose a **Sleep time** to show a compact sleeping fruit with the active task count after inactivity; choose **Never** to disable sleeping. Hover or click to wake it.
- Scroll the task area to reveal tasks beyond the first five; `+N more` shows how many start below the viewport.
- Drag a task row up or down to change its saved order; nearby tasks animate aside while you drag.
- Use the `↕` button beside `×` to sort tasks by priority or reminder time.
- Use the `◇` button on a task to pin it above unpinned tasks. Pinned and unpinned tasks can each be reordered by dragging.
- Each task keeps its timer and pin controls visible. Select `•••` to reveal Duplicate, Edit, and Delete.
- Select `⧉` inside a task’s `•••` menu to duplicate it without copying completion or timer history.
- Add hashtags such as `#work` or `#personal` to categorize tasks, then choose a hashtag in the filter panel.
- Hover a task to see when it was created; creation timestamps are saved with task data.
- Choose **Archive cleanup** in the tray menu to retain completed tasks forever or remove them after 7, 30, or 90 days.
- Select an `http://`, `https://`, or `www.` link inside a task to open it in the default browser.
- Enter `https://example.com : Display text` to hide the URL and show only a clickable label.
- Select truncated task text to animate it open; select it again or click elsewhere to collapse it.

## Adaptive personality

When enabled, the fruit reacts to the current task state:

- Happy for a few seconds after a task is completed.
- Focused while a task timer is running.
- Worried while naturally dated tasks are overdue.
- Tired whenever more than five tasks remain.
- Calm, using the selected fruit theme’s own face, at other times.

## Do Not Disturb

Choose **Do not disturb** from the tray menu to disable reminder sounds, window activation, and shaking. It can be disabled, kept on continuously, or scheduled for `22:00–07:00`, `23:00–07:00`, or `00:00–08:00`. Reminders are still recorded and shown inside the app.

## Task entry formats

Task times use the 24-hour `HH:mm` format. Recurrence names are case-insensitive.

| Input | Result |
| --- | --- |
| `Buy groceries` | Plain task without a reminder |
| `task 1; 12:00 task 2` | Two tasks; the second has a 12:00 reminder |
| `17:50 Submit report` | One-time reminder at 17:50 |
| `today 3pm Call Ali` | One-time reminder today at 15:00 |
| `tomorrow 9am Call Ali` | One-time reminder tomorrow at 09:00 |
| `Friday Submit report` | One-time reminder next Friday at the default 09:00 time |
| `next Friday 14:30 Submit report` | One-time reminder on the following Friday at 14:30 |
| `2026-09-03 08:15 Release build` | One-time reminder on an exact date and time |
| `daily 09:00 Drink water` | Reminder every day at 09:00 |
| `monday 18:00 Gym` | Reminder every Monday at 18:00 |
| `! Read article` | Low-priority task |
| `!! Prepare notes` | Medium-priority task |
| `!!! Ship release` | High-priority task |
| `daily 09:00 !!! Important check` | Recurring high-priority task |
| `https://example.com/docs` | Task containing a clickable link |
| `https://example.com : Open documentation` | Clickable task showing only `Open documentation` |
| `17:50 https://example.com : Join meeting` | One-time linked reminder |

Weekly reminders support all weekday names: `monday`, `tuesday`, `wednesday`, `thursday`, `friday`, `saturday`, and `sunday`.

Natural dates support `today`, `tomorrow`, weekday names, `next weekday`, and exact `yyyy-MM-dd` dates. A natural date without a time defaults to 09:00. Times can use either 24-hour `HH:mm` or 12-hour forms such as `9am` and `2:30pm`.

Priority marks belong immediately before the task text. For a scheduled task, put them after the time, as in `friday 16:30 !! Send summary`.

Use the pencil icon to edit a task. The editor reconstructs its saved time, recurrence, and priority so any part can be changed or removed.

Tasks, window position, and window mode are saved under `%LOCALAPPDATA%\Avocado`.

## Verify

```powershell
dotnet build .\Avocado.csproj
dotnet run --project .\tests\Avocado.LogicTests\Avocado.LogicTests.csproj
```

## Create a standalone Windows executable

```powershell
dotnet publish .\Avocado.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\dist
```

The publish command creates `dist\Avocado.exe`, which includes the runtime and can be copied to another 64-bit Windows PC.
