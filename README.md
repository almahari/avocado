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
- Press the global `Ctrl+Alt+S` shortcut to put the fruit into sleeping mode immediately.
- Press the global `Ctrl+Alt+P` shortcut to open the command palette in the center of the current screen. Type to search, use Up/Down to select, Enter to run, or Escape to close.
- Open **Bookmarks** in the command palette to browse folders and nested subfolders. Select a website to open it, or press Backspace with an empty search box to move up one level.
- Open **Tasks** in the command palette to search active tasks and manage their reminders, or type `task <details>` to create a task directly.
- Double-click the tray icon to show or hide the avocado.
- Open the tray icon menu and choose **Task help** to see the supported task-entry formats and examples.
- Open the tray icon menu to choose **Normal window**, **Always on top**, themes, seasonal skins, reminder sounds, Do Not Disturb hours, startup behavior, global shortcuts, or **Exit**.
- Toggle **Always on top** in the tray menu to enable or disable it; the choice is saved. **Normal window** also disables it.
- Toggle **Enable MCP server** in the tray menu to allow local AI agents to list, create, and update tasks. It is off by default, and the choice is saved.
- Seasonal skins are independent of fruit themes: dress any fruit as a **Halloween pumpkin**, add a **Winter cap**, a **Spring blossom** crown, or **Summer shades**. Choose **None** to remove the accessory; the selection is saved.
- Choose **Global shortcuts** to change or disable the system-wide Quick Add, Clipboard Task, Sleep Now, Wake Up, and Command Palette shortcuts; press a modifier plus a letter, number, function key, or Space.
- Toggle **Adaptive personality** in the tray menu to show or hide the fruit’s reactive face. The choice is saved.
- From the tray menu, choose **Size → Normal** or **Size → Small**; Small is exactly half-size.
- Enable **Resize when inactive** and choose a **Sleep time** to show a compact sleeping fruit with the active task count after inactivity; choose **Never** to disable sleeping. Use **Sleep fruit size → Normal** for the current sleeping size or **Small** for half that size. Hover or click to wake it.
- Choose **Sleep reminder repeat** in the tray menu to repeat an unresolved reminder every 5, 10, 20, or 30 minutes while the fruit is sleeping. Snoozing or muting the reminder stops the repeated shake.
- Scroll the task area to reveal tasks beyond the first five; `+N more` shows how many start below the viewport.
- Drag a task row up or down to change its saved order; nearby tasks animate aside while you drag.
- Use the `↕` button beside the calendar to sort tasks by priority or reminder time.
- Use the `▦` button to open a compact day or week calendar, navigate dates, and jump to a scheduled task.
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

Each fruit has its own reminder movement, with timing and strength matched to that fruit’s
personality. Fruits remain still while idle, while timers run, and when tasks are completed.

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

## Command palette

Choose **Setup commands** from the tray menu to create and open `%LOCALAPPDATA%\Avocado\commands.json` in the default JSON editor. The palette reloads this file every time it opens. Each entry contains `command`, `action`, and `parameter`. Optional `aliases` provide alternate executable command patterns, `keywords` add search terms, and `showWindow` controls Bash visibility:

```json
[
  {
    "command": "google",
    "aliases": ["search"],
    "keywords": ["web", "internet"],
    "action": "open-browser",
    "parameter": "https://www.google.com"
  },
  {
    "command": "j%1",
    "action": "open-browser",
    "parameter": "https://jira.com/%1"
  },
  {
    "command": "regex:^gh\\s+(.+)$",
    "action": "open-browser",
    "parameter": "https://github.com/search?q=%1"
  },
  {
    "command": "echo %1",
    "action": "run-bash",
    "parameter": "echo \"%1\""
  }
]
```

- Static text such as `google` runs when selected.
- `aliases` contains alternate command patterns. Aliases support `%1` placeholders and regular expressions in the same way as `command`.
- `keywords` contains additional search terms. They make a command discoverable but are not executable command patterns.
- Search is fuzzy across commands, aliases, keywords, active tasks, bookmark names, URLs, and folder names. Exact matches rank first, while partial text, initials, missing characters, and small spelling mistakes are tolerated.
- `%1`, `%2`, and later placeholders capture text from the typed command and substitute it into `parameter`. For example, `j124-123` with `j%1` opens `https://jira.com/124-123`.
- Prefix a regular expression with `regex:` (or wrap it in `/.../`). Regex capture groups map to `%1`, `%2`, and so on.
- `open-browser` opens an HTTP or HTTPS address in the default browser. A missing scheme is treated as `https://`.
- `run-bash` uses Git Bash from a standard Git for Windows installation and runs the expanded parameter through `bash -lc`.
- To run a script with Git Bash, put the absolute `.sh` path first, followed by its arguments. Avocado finds Git for Windows, changes to the script directory, and invokes the script with `bash`, so executable permission is not required. Quote placeholders when an argument may contain spaces:

```json
{
  "command": "deploy %1",
  "action": "run-bash",
  "parameter": "\"C:\\scripts\\deploy.sh\" \"%1\" --verbose",
  "showWindow": true
}
```

Set `showWindow` to `true` to open a visible Git Bash terminal. The terminal displays the script output and exit code, then waits for Enter before closing. Omit the option or set it to `false` to run silently in the background.

Set `AVOCADO_GIT_BASH` to a specific `bash.exe` path if Git is installed outside its standard Windows locations.

### Palette task management

Open **Tasks** to browse active tasks. Selecting a task shows actions to complete it, snooze it for 5, 10, 20, or 30 minutes, mute its reminder, reschedule it, or delete it. Backspace with an empty search box returns to the previous level.

Type `task <details>` from anywhere in the palette to create a task using the normal task-entry syntax. For example, `task tomorrow 18:00 Send report` creates a scheduled task. When rescheduling, enter only the new schedule, such as `tomorrow 18:00`, `daily 09:00`, or `2026-10-01 14:30`.

## Command palette bookmarks

Choose **Setup bookmarks** from the tray menu to create and open `%LOCALAPPDATA%\Avocado\bookmarks.json`. The palette reloads bookmarks every time it opens. Bookmark folders can contain both `folders` and `websites`, with no fixed nesting limit:

```json
[
  {
    "name": "Folder 1",
    "folders": [
      {
        "name": "Subfolder 1",
        "folders": [],
        "websites": [
          {
            "name": "Website 1",
            "url": "https://example.com"
          }
        ]
      }
    ],
    "websites": []
  }
]
```

With an empty search box, the palette shows the current folder's subfolders and websites. Searching lists websites whose name or URL matches. When a folder or any parent folder matches the search, every website beneath that folder is included in the results.

## MCP server connection

Run Avocado and check **Enable MCP server** in the tray menu. The server runs inside the app, including while the window is hidden. Unchecking the option or exiting Avocado stops the server. An enabled setting starts the server again when Avocado next launches.

| Setting | Value |
| --- | --- |
| URL | `http://127.0.0.1:17842/mcp` |
| Transport | MCP Streamable HTTP (stateless) |
| Authentication | None; local connections only |
| Port | `17842` (fixed) |

In an MCP client that accepts `mcpServers` URL configuration, add:

```json
{
  "mcpServers": {
    "avocado": {
      "url": "http://127.0.0.1:17842/mcp"
    }
  }
}
```

If your client asks for a transport, select **Streamable HTTP** (some clients call it `http`). Clients with other configuration formats should use the same URL. This is an HTTP endpoint, not a stdio command or legacy SSE endpoint. A client must run on the same computer; a cloud-hosted agent cannot connect directly. The server supports both newer MCP discovery and the older `initialize` handshake through the [official MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).

The server binds only to IPv4 loopback and rejects untrusted Host and browser Origin headers. Any local process can use these tools while the server is enabled. No account, token, or additional server process is needed.

### Available tools

| Tool | Arguments | Behavior |
| --- | --- | --- |
| `list_tasks` | `includeCompleted` (optional, default `false`) | Returns active tasks; set `true` to also return the completed archive. |
| `create_task` | `input` (required), `isPinned` (optional, default `false`) | Creates one task using Avocado's task-entry syntax and returns it with its ID. |
| `update_task` | `id` (required), `text`, `priority`, `schedule`, `isPinned`, `isCompleted` (all optional) | Updates the matching active or archived task and returns it. |

Task results contain `id`, `text`, `isCompleted`, `isPinned`, `priority`, `createdAt`, `completedAt`, `dueAt`, `reminderTime`, `recurrence`, `snoozedUntil`, and `elapsedTicks`. Use the returned `id` for future updates. Dates and reminder expressions use the computer's local time. Changes immediately update the app and are saved to `%LOCALAPPDATA%\Avocado\state.json`.

Examples of tool arguments:

```json
{"input": "tomorrow 9am !! Review report #work", "isPinned": true}
```

```json
{"id": "<task ID from list_tasks>", "text": "Review final report", "priority": "High", "schedule": "daily 09:00"}
```

For updates, omitted or `null` fields keep their existing values. `text` is literal replacement text and must be nonempty; it does not parse reminders or priority prefixes. `priority` accepts `None`, `Low`, `Medium`, or `High`, case insensitively. Set `schedule` to a reminder expression such as `tomorrow 18:00`, `daily 09:00`, or `2026-12-01 14:30`; set it to `""` to remove the reminder. Setting `isCompleted` to `true` archives the task, and `false` restores it. `create_task` creates a single task, so semicolons remain in its text. Unknown IDs and invalid fields return MCP tool errors; validation failures apply no changes.

If connection fails, verify that Avocado is running and **Enable MCP server** is checked. If port `17842` is already occupied, enabling shows an error and the option remains unchecked; stop the other listener and enable it again. Reconnect your client after disabling and re-enabling the server.

## Verify

```powershell
dotnet build .\Avocado.csproj
dotnet run --project .\tests\Avocado.LogicTests\Avocado.LogicTests.csproj
dotnet run --project .\tests\Avocado.McpTests\Avocado.McpTests.csproj
```

## Create a standalone Windows executable

```powershell
dotnet publish .\Avocado.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\dist
```

The publish command creates `dist\Avocado.exe`, which includes the runtime and can be copied to another 64-bit Windows PC.
