# PandaTools

PandaTools is a Windows system tray productivity utility.
PandaTools provides quick access to web tools, network utilities, and PowerShell scripts fetched live from GitLab - configured entirely through JSON flavour files with no recompilation needed.

---

## Features

- System tray icon with right-click context menu
- **Flavour system** - swap entire menu configurations via JSON files without rebuilding
- **Automatic flavour syncing** - new and updated flavour files are downloaded silently in the background; drop a file into the config repo and every agent gets it on the next poll cycle
- **Org defaults syncing** - LAPS config, token expiry policy, and RunAs profile seeds are polled from a central `defaults.json` and merged into each agent's local config automatically
- **Default menu on first run** - a built-in `Default.json` gives new users a working menu immediately with no configuration required
- Quick launch shortcuts for web tools, applications, and network utilities
- **RunAs profiles** - launch tools as different domain accounts with optional stored credentials
- **DPAPI-encrypted RunAs passwords** - saved RunAs profile passwords are encrypted per-user via Windows DPAPI, never stored as plain text
- Live PowerShell script execution fetched directly from GitLab - always runs the latest version
- Smart script caching - scripts are cached locally and only re-downloaded when the GitLab commit hash changes
- Automatic fallback to cached scripts when GitLab is unreachable (off-network or no VPN)
- Automatic update checker on startup - prompts when a newer release is available on GitLab
- **DPAPI-encrypted GitLab API token** - token is encrypted per-user via Windows DPAPI, never stored as plain text
- **First-time setup wizard** - prompts for API token on first launch and encrypts it immediately
- **Configurable browsers** - independently choose which browser opens `url` and `incognito` items; supports Chrome, Edge, Firefox, Brave, or a custom path
- **Multi-URL items** - open multiple URLs in a single click using the `values` array on `url` or `incognito` items
- **RunAs for browser items** - `url` and `incognito` items support `runas_profile` to open in a different domain account
- **Personal local menus** - each user can maintain their own `local_flavour_{username}.json` alongside the managed IT menu, editable via the built-in Menu Editor
- **Menu Editor** - a visual editor for building personal local menus; context-aware fields show only what is relevant for the selected item type
- **PandaShell** - embedded SSH terminal with one-click connection bookmarks
- **PandaLAPS** - fetch and copy local admin passwords from AD directly within the app
- **PandaPassGen** - secure password generator with NATO phonetic output, configurable length and complexity
- **Single instance enforcement** - launching PandaTools while already running brings the existing tray instance to focus

---

## Requirements

- Windows 11
- .NET 10 runtime (or use the self-contained build - no runtime required)
- A GitLab instance and Personal Access Token for GitLab features

> GitLab features (script runner, flavour polling, update checker) are silently disabled if no token is configured or the network is unreachable. The tray icon and local features always load regardless.

---

## Getting Started

### 1. Install

1. Download `PandaToolsSetup-vX.X.X.exe` from the [Releases](../../releases) page
2. Run the installer - choose your install location, configure shortcuts, then click Install
3. If a `provision.json` is found alongside the installer it will be applied automatically, seeding your GitLab connection and any org-specific settings
4. If PandaTools is already running the installer will detect it, offer to close it, and continue automatically
5. Once complete, the installer offers to launch PandaTools immediately

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

On first run a `Default.json` is written automatically, giving you a working menu with local scripts, web tools, and common applications. If a GitLab config repo is configured, all flavour files in its `flavours/` folder are downloaded automatically on the first poll cycle and appear in **Settings → Active Flavour**.

You can also drop your own `.json` files into the `flavours\` folder next to the executable - they will be copied to AppData on startup.

### 4. Provisioned Deployments

For managed deployments, place a `provision.json` alongside the installer. On a fresh install the installer will apply it automatically, writing `config.json` without any manual steps. Provision files are never applied on updates - existing config is always preserved.

Generate a `provision.json` using the **Provision Builder** - a standalone HTML tool that opens in any browser with no install required.

### 5. Usage

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
  "show_pandashell": false,
  "show_pandapassgen": false,
  "show_pandalaps": false,
  "menu":
  [
    {
      "section": "Web Tools",
      "icon": "🌐",
      "items":
      [
        {
          "label": "Google",
          "type": "url",
          "value": "https://www.google.com"
        },
        {
          "label": "Google (Workstation Admin)",
          "type": "url",
          "value": "https://www.google.com",
          "runas_profile": "Workstation Admin"
        },
        {
          "label": "Two Sites (Workstation Admin)",
          "type": "url",
          "runas_profile": "Workstation Admin",
          "values":
          [
            "https://google.com/",
            "https://youtube.com/"
          ]
        },
        {
          "label": "Google (incognito)",
          "type": "incognito",
          "value": "https://google.com/"
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
          "projectId": 0,
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
          "label": "Notepad (Admin)",
          "type": "app",
          "value": "C:\\Windows\\System32\\notepad.exe",
          "admin": true
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

### Flavour Flags

| Field               | Type   | Description                                              |
|---------------------|--------|----------------------------------------------------------|
| `hidden`            | `bool` | Hides the flavour from the active flavour dropdown       |
| `show_pandashell`   | `bool` | Shows PandaShell shortcut in the tray menu               |
| `show_pandapassgen` | `bool` | Shows PandaPassGen shortcut in the tray menu             |
| `show_pandalaps`    | `bool` | Shows PandaLAPS shortcut in the tray menu                |

### Item Types

| Type           | Description                                                        |
|----------------|--------------------------------------------------------------------|
| `url`          | Opens URL in the configured default browser                        |
| `incognito`    | Opens URL in the configured private/incognito browser              |
| `app`          | Launches a local application (use `admin: true` for UAC elevation) |
| `runas`        | Launches as a different domain user via a named RunAs profile      |
| `powershell`   | Runs an inline PowerShell command                                  |
| `script`       | Fetches and runs a `.ps1` from GitLab (cached by commit hash)      |
| `explorer`     | Opens a file or folder path in Windows Explorer                    |
| `pandashell`   | Opens PandaShell                                                   |
| `pandapassgen` | Opens PandaPassGen                                                 |
| `pandalaps`    | Opens PandaLAPS                                                    |
| `exe`          | Direct `.exe` launch (legacy - prefer `app`)                       |

### Item Fields

| Field           | Type       | Applies to                  | Description                                                               |
|-----------------|------------|-----------------------------|---------------------------------------------------------------------------|
| `label`         | `string`   | all                         | Menu item display text                                                    |
| `type`          | `string`   | all                         | Item type (see above)                                                     |
| `value`         | `string`   | all except `script`         | Single URL, path, or command                                              |
| `values`        | `string[]` | `url`, `incognito`          | Multiple URLs - opens all in one click, password prompted once if needed  |
| `runas_profile` | `string`   | `url`, `incognito`, `runas` | Named RunAs profile to launch as a different domain user                  |
| `arguments`     | `string`   | `app`, `exe`, `runas`       | Extra command-line arguments appended after `value`                       |
| `admin`         | `bool`     | `app`                       | Triggers UAC elevation when `true`                                        |
| `projectId`     | `int`      | `script`                    | GitLab project ID containing the script                                   |
| `filePath`      | `string`   | `script`                    | Path to the `.ps1` file within the GitLab project                         |
| `branch`        | `string`   | `script`                    | Branch to fetch the script from (default: `main`)                         |

> **Multi-URL + RunAs:** When `values` is used with `runas_profile` and no password is stored, the password prompt appears **once** and is reused for all URLs in the array.

---

## RunAs Profiles

RunAs profiles allow tools to be launched as a different domain account. Configure them under **Settings → RunAs Profiles**.

| Field    | Description                                                                                  |
|----------|----------------------------------------------------------------------------------------------|
| Name     | Profile identifier referenced by `runas_profile` in flavour JSON                             |
| Username | Domain account in `DOMAIN\user` or `user@domain` format                                      |
| Password | Optional - leave blank to be prompted at launch; if set, encrypted with DPAPI before saving  |

Password behaviour at launch:
- **No saved password** - a prompt appears each time; the password is used for that session only and never stored
- **Saved password** - used automatically; if it is wrong, a re-entry prompt appears and you are offered the option to update the saved value
- The credential prompt displays the **item label** in the title bar and includes a **👁 toggle** to show or hide the password while typing

---

## Settings

Access via **right-click → Settings**.

| Setting              | Description                                                                        |
|----------------------|------------------------------------------------------------------------------------|
| GitLab Server        | Base URL of your GitLab instance                                                   |
| Config Project ID    | GitLab project ID of the config repo used for flavour and defaults polling         |
| Update Token         | Enter a new token to re-encrypt and save                                           |
| Active Flavour       | Switch between available flavour JSON files                                        |
| Manual Mode          | Disables automatic GitLab flavour polling                                          |
| Poll Interval        | How often (seconds) to check GitLab for flavour and defaults updates               |
| Token Warn Days      | Days before token expiry to show the tray warning balloon (default: 14)            |
| Browser → Default    | Browser used for `url` type items (Default, Chrome, Edge, Firefox, Brave, Custom)  |
| Browser → Incognito  | Browser used for `incognito` type items                                            |
| RunAs Profiles       | Named credential profiles used by `runas`, `url`, and `incognito` items            |
| Restore Defaults     | Re-applies org defaults from the config repo - resets LAPS, token warn days, and RunAs profile seeds |

> **Browser and RunAs:** When a `url` or `incognito` item has a `runas_profile`, the configured browser for that type is launched as the specified user. If **Default** is selected and a RunAs profile is in use, PandaTools will automatically locate an installed browser (Edge → Chrome → Firefox → Brave) since an explicit path is required for credential passing.

---

## Updates

PandaTools checks for a newer release on GitLab silently at startup. If an update is available, a prompt will appear offering to download and run the installer automatically. Manual check available via **Settings → App Updates**.

---

## Project Structure

```
PandaTools/
├── src/
│   ├── AppConfig.cs
│   ├── AppIcon.cs
│   ├── ConfigLoader.cs
│   ├── CredentialPrompt.cs
│   ├── FlavourEditorWindow.cs
│   ├── GitLabScriptRunner.cs
│   ├── LaunchURL.cs
│   ├── MenuBuilder.cs
│   ├── MenuConfig.cs
│   ├── SettingsWindow.cs
│   ├── TokenExpiryChecker.cs
│   ├── TokenManager.cs
│   ├── TrayContext.cs
│   └── Updater.cs
├── PandaShell/
│   ├── PandaShellBookmarkStore.cs
│   └── PandaShellWindow.cs
├── PandaPass/
│   ├── PandaPassGenWindow.cs
│   └── PhoneticSpeller.cs
├── PandaLaps/
│   ├── LapsClient.cs
│   ├── LapsConfig.cs
│   ├── LapsSettingsWindow.cs
│   └── PandaLapsWindow.cs
├── PandaTools.Setup/
│   ├── InstallerForm.cs
│   ├── UninstallerForm.cs
│   ├── Program.cs
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