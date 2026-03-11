# PandaTools

A Windows system tray utility for Digital Services colleagues at Leeds Beckett University.
PandaTools provides quick access to web tools, network utilities, and PowerShell scripts fetched live from GitLab - configured entirely through JSON flavour files with no recompilation needed.

---

## Features

- System tray icon with right-click context menu
- **Flavour system** - swap entire menu configurations via JSON files without rebuilding
- Quick launch shortcuts for web tools, applications, and network utilities
- **RunAs profiles** - launch tools as different domain accounts with optional stored credentials
- Live PowerShell script execution fetched directly from GitLab - always runs the latest version
- Smart script caching - scripts are cached locally and only re-downloaded when the GitLab commit hash changes
- Automatic fallback to cached scripts when GitLab is unreachable (off-network or no VPN)
- Automatic GitLab flavour polling - menus update silently in the background when flavour JSONs change in the repo
- Automatic update checker on startup - prompts when a newer release is available on GitLab
- **DPAPI-encrypted GitLab API token** - token is encrypted per-user via Windows DPAPI, never stored as plain text
- **First-time setup wizard** - prompts for API token on first launch and encrypts it immediately
- **Configurable browsers** - independently choose which browser opens `url` and `incognito` items; supports Chrome, Edge, Firefox, Brave, or a custom path
- **Multi-URL items** - open multiple URLs in a single click using the `values` array on `url` or `incognito` items
- **RunAs for browser items** - `url` and `incognito` items support `runas_profile` to open in a different domain account

---

## Requirements

- Windows 11
- .NET 10 runtime (or use the self-contained build - no runtime required)
- Access to `gitlab.leedsbeckett.ac.uk` (on-network or VPN) for GitLab features

> GitLab features (script runner, flavour polling, update checker) are silently disabled if no token is configured or the network is unreachable. The tray icon and local features always load regardless.

---

## Getting Started

### 1. Install

1. Download `PandaToolsSetup-vX.X.X.exe` from the [Releases](../../releases) page
2. Run the installer - it extracts and launches PandaTools automatically
3. PandaTools will appear in the system tray

### 2. First-Time Setup

On first launch, PandaTools will prompt you to enter your GitLab Personal Access Token.

- Generate one at: **GitLab → User Settings → Access Tokens**
- Minimum required scope: `read_api`
- The token is encrypted immediately using **Windows DPAPI** (per-user, per-machine) - it is never written to disk as plain text

You can skip this step and configure the token later via **right-click → Settings → Connection → Update Token**.

### 3. Flavour Files

PandaTools menus are driven by JSON flavour files stored at:

```
%APPDATA%\PandaTools\flavours\
```

On first run, any `.json` files in the `flavours\` folder next to the executable are automatically copied there. A hidden `_Template.json` is written if no flavour files are found, documenting all supported item types.

Drop your flavour `.json` files into the project's `flavours\` folder - they will be copied to AppData on startup.

### 4. Usage

Right-click the tray icon to access your configured menu. Cached scripts are stored at:

```
%LOCALAPPDATA%\PandaTools\ScriptCache\
```

---

## Flavour JSON Format

A flavour file defines sections and items that appear in the tray menu.

```json
{
  "version": "1.0",
  "hidden": false,
  "menu":
  [
    {
      "section": "Web Tools",
      "icon": "🌐",
      "items":
      [
        {
          "label": "Staff Site",
          "type": "url",
          "value": "https://www.leedsbeckett.ac.uk/staffsite/"
        },
        {
          "label": "Staff Site (Workstation Admin)",
          "type": "url",
          "value": "https://www.leedsbeckett.ac.uk/staffsite/",
          "runas_profile": "Workstation Admin"
        },
        {
          "label": "Two Sites (Workstation Admin)",
          "type": "url",
          "runas_profile": "Workstation Admin",
          "values":
          [
            "https://site-one.example.com/",
            "https://site-two.example.com/"
          ]
        },
        {
          "label": "Google (incognito)",
          "type": "incognito",
          "value": "https://google.com/"
        },
        {
          "label": "Two Sites (incognito, Workstation Admin)",
          "type": "incognito",
          "runas_profile": "Workstation Admin",
          "values":
          [
            "https://site-one.example.com/",
            "https://site-two.example.com/"
          ]
        }
      ]
    },
    {
      "section": "Scripts",
      "icon": "📜",
      "items":
      [
        {
          "label": "Example Script",
          "type": "script",
          "projectId": 000,
          "filePath": "Scripts/MyScript.ps1",
          "branch": "main"
        }
      ]
    },
    {
      "section": "Applications",
      "icon": "🖥️",
      "items":
      [
        {
          "label": "Notepad",
          "type": "app",
          "value": "C:\\Windows\\System32\\notepad.exe"
        },
        {
          "label": "LAPS GUI (Workstation Admin)",
          "type": "runas",
          "value": "C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\IT Services Technicians Software\\LAPS GUI.lnk",
          "runas_profile": "Workstation Admin"
        },
        {
          "label": "Flush DNS",
          "type": "powershell",
          "value": "ipconfig /flushdns"
        }
      ]
    }
  ]
}
```

### Item Types

| Type         | Description                                                        |
|--------------|--------------------------------------------------------------------|
| `url`        | Opens URL in the configured default browser                        |
| `incognito`  | Opens URL in the configured private/incognito browser              |
| `app`        | Launches a local application (use `admin: true` for UAC elevation) |
| `runas`      | Launches as a different domain user via a named RunAs profile      |
| `powershell` | Runs an inline PowerShell command                                  |
| `script`     | Fetches and runs a `.ps1` from GitLab (cached by commit hash)      |
| `exe`        | Direct `.exe` launch (legacy - prefer `app`)                       |
| `explorer`   | Opens a file or folder path in Windows Explorer                    |

### Item Fields

| Field           | Type       | Applies to                      | Description                                                                 |
|-----------------|------------|---------------------------------|-----------------------------------------------------------------------------|
| `label`         | `string`   | all                             | Menu item display text                                                      |
| `type`          | `string`   | all                             | Item type (see above)                                                       |
| `value`         | `string`   | all except `script`             | Single URL, path, or command                                                |
| `values`        | `string[]` | `url`, `incognito`              | Multiple URLs - opens all in one click, password prompted once if needed    |
| `runas_profile` | `string`   | `url`, `incognito`, `runas`     | Named RunAs profile to launch as a different domain user                    |
| `arguments`     | `string`   | `app`, `exe`, `runas`           | Extra command-line arguments appended after `value`                         |
| `admin`         | `bool`     | `app`                           | Triggers UAC elevation when `true`                                          |
| `projectId`     | `int`      | `script`                        | GitLab project ID containing the script                                     |
| `filePath`      | `string`   | `script`                        | Path to the `.ps1` file within the GitLab project                           |
| `branch`        | `string`   | `script`                        | Branch to fetch the script from (default: `main`)                           |

> **Multi-URL + RunAs:** When `values` is used with `runas_profile` and no password is stored, the password prompt appears **once** and is reused for all URLs in the array.

---

## Settings

Access via **right-click → Settings**.

| Setting             | Description                                                                       |
|---------------------|-----------------------------------------------------------------------------------|
| GitLab Server       | Base URL of your GitLab instance                                                  |
| Project ID          | GitLab project ID used for flavour polling                                        |
| Update Token        | Enter a new token to re-encrypt and save                                          |
| Active Flavour      | Switch between available flavour JSON files                                       |
| Manual Mode         | Disables automatic GitLab flavour polling                                         |
| Poll Interval       | How often (seconds) to check GitLab for flavour updates                           |
| Browser → Default   | Browser used for `url` type items (Default, Chrome, Edge, Firefox, Brave, Custom) |
| Browser → Incognito | Browser used for `incognito` type items                                           |
| RunAs Profiles      | Named credential profiles used by `runas`, `url`, and `incognito` items           |

> **Browser and RunAs:** When a `url` or `incognito` item has a `runas_profile`, the configured browser for that type is launched as the specified user. If **Default** is selected and a RunAs profile is in use, PandaTools will automatically locate an installed browser (Edge → Chrome → Firefox → Brave) since an explicit path is required for credential passing.

---

## Updates

PandaTools checks for a newer release on GitLab silently at startup. If an update is available, a prompt will appear with the option to open the releases page. Manual check available via **Settings → App Updates**.

---

## Project Structure

```
PandaTools/
├── src/
│   ├── Program.cs
│   ├── TrayContext.cs
│   ├── AppConfig.cs
│   ├── ConfigLoader.cs
│   ├── MenuBuilder.cs
│   ├── MenuConfig.cs
│   ├── GitLabScriptRunner.cs
│   ├── TokenManager.cs
│   ├── SettingsWindow.cs
│   ├── AppIcon.cs
│   └── Updater.cs
├── PandaTools.Setup/
│   ├── Program.cs
│   ├── InstallerForm.cs
│   ├── UninstallerForm.cs
│   ├── Payload/
│   │   └── payload.zip        # generated at build time
│   └── PandaTools.Setup.csproj
├── flavours/
│   └── *.json                 # seeded to %AppData%\PandaTools\flavours on first run
├── assets/
│   └── PandaTools.ico
├── .gitlab-ci.yml
├── CHANGELOG.md
└── README.md
```

---

## Building
Releases are built and published automatically via GitLab CI when a version tag (e.g. `v2.0.0`) is pushed.

To build manually:

```powershell
# Build main app
dotnet publish PandaTools.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  --output ./payload_staging

# Package payload
Compress-Archive -Path "./payload_staging/*" `
  -DestinationPath "./PandaTools.Setup/Payload/payload.zip" -Force

# Build installer
dotnet publish PandaTools.Setup/PandaTools.Setup.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  --output ./installer_out
```

Output: `./installer_out/PandaToolsSetup.exe`

---

## Changelog
See [CHANGELOG.md](CHANGELOG.md) for version history.
