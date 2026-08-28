# Avocado

Avocado is a borderless pixel-art todo widget for Windows. It lives in the notification tray, can be dragged anywhere on the desktop, and can run as either a normal window or always on top.

## Run locally

Requires the .NET 8 SDK on Windows.

```powershell
dotnet run --project .\Avocado.csproj
```

## Controls

- Drag any non-control part of the avocado to move it.
- Select `+` or press `Ctrl+N` to add a task; press Enter to save or Escape to cancel.
- Select the avocado's `×` to hide it to the notification tray.
- Double-click the tray icon to show or hide the avocado.
- Right-click the tray icon to choose **Normal window**, **Always on top**, or **Exit**.
- From the tray menu, choose **Size → Normal** or **Size → Small**; Small is exactly half-size.
- Enable **Resize when inactive** in the tray menu to show a half-size sleeping avocado after two idle minutes; click it to wake and restore the preferred size.
- Scroll the task area to reveal tasks beyond the first five; `+N more` shows how many start below the viewport.
- Drag a task row up or down to change its saved order; nearby tasks animate aside while you drag.
- Select an `http://`, `https://`, or `www.` link inside a task to open it in the default browser.
- Enter `https://example.com : Display text` to hide the URL and show only a clickable label.
- Select truncated task text to animate it open; select it again or click elsewhere to collapse it.

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
