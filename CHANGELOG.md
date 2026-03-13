# PandaTools Changelog

## [2.3.3] - 13/03/26
### Fixed
- **Bug Fixes**

## [2.3.2] - 13/03/26
### Added
- **Update cache** - if a cached installer is found for the available version,
  PandaTools will offer to reuse it instead of re-downloading, saving time on
  repeated update attempts
- **Clear Update Cache** - a new button in Settings → Advanced allows cached
  installer files to be deleted from the temp directory at any time

### Changed
- **Check Token Expiry moved inline** - the expiry check button now sits alongside
  the Update Token button in the Connection section rather than on its own row,
  keeping all token actions in one place

### Fixed
- **In-place update failing with "file is being used by another process"** - the
  download stream is now fully closed and disposed before the installer process is
  launched, preventing the file lock error on update


## [2.3.1] - 13/03/26
### Fixed
- **Linguistic** - Fixed some linguistic errors.

## [2.3.0] - 13/03/26
### Added
- **Password prompt shows app name in title bar** - the title bar of the credential
  prompt now displays the label of the item being launched (e.g. "LAPS GUI – Enter Password")
  rather than a generic title
- **Show/hide password toggle** - a 👁 button now appears alongside the password
  field in the credential prompt, allowing the password to be revealed while typing
- **Single instance enforcement** - launching PandaTools a second time while it is
  already running will bring the existing instance to focus rather than opening a
  duplicate tray process
- **Installer detects running instance** - the installer will detect if PandaTools is
  already running, offer to close it, and proceed with the update automatically
- **Launch after install** - once installation completes, the installer offers to
  launch PandaTools immediately
- **Configurable install location** - the installer now allows the user to choose
  the install directory via a folder browser dialog

### Changed
- **RunAs passwords encrypted at rest** - saved RunAs profile passwords are now
  encrypted using Windows DPAPI before being written to `config.json`, matching the
  same per-user, per-machine protection used for the GitLab token; plain-text passwords
  are never stored on disk
- **Incorrect password retry loop now prompts silently until cancelled** - if the user
  cancels the prompt at any point the dialog closes without any error message
- **OK / Cancel button alignment fixed** - buttons in the credential prompt are now
  right-aligned with a consistent gap between them, matching standard Windows dialog
  conventions

## [2.2.0] - 12/03/26
### Added
- **Token expiry notifications** - a tray balloon is shown on startup if the GitLab
  token is expired or within the warning threshold
- **Check Token Expiry button** - Settings → Connection now has a button to manually
  check and display token expiry, also triggering a tray balloon with the result
- **Configurable expiry warning threshold** - Settings → Advanced → Token Warn Days
  controls how many days before expiry the warning balloon appears (default: 14)

### Changed
- **Token storage simplified** - legacy AES key-file/token-file decryption path removed;
  DPAPI (`token_encrypted`) is now the sole token mechanism
- **Settings → Connection cleaned up** - Key File and Token File fields removed as they
  are no longer used; token management is now purely DPAPI-based

### Removed
- `keyFile` and `tokenFile` fields from `AppConfig` and all references in `TokenManager`
- Legacy PowerShell AES decryption from the active `GetToken`/`HasToken`/`SaveToken`
  paths (`LegacyDecrypt` retained as a standalone migration helper only)

## [2.1.0] - 11/03/26
### Added
- **Multi-URL items** - `url` and `incognito` items now support a `values` array to open
  multiple URLs in a single click
- **RunAs for browser items** - `url` and `incognito` items now support `runas_profile`
  to open in a different domain account
- **Single password prompt** - when using `runas_profile` with multiple `values`, the
  password is prompted once and reused for all URLs in the array
- **Configurable update checker** - App Project ID and App Repo Path are now editable in
  Settings → Advanced, allowing the update checker to follow the app if the repo moves
- **Independent browser selection** - `url` and `incognito` items each have their own
  configurable browser in Settings → Browser

### Changed
- Settings label alignment - all field labels now left-align their first letters
  consistently across Connection, Browser, Advanced, and RunAs Profiles groups

## [2.0.3] - 11/03/26
### Added
- `url` type items now support `runas_profile` - open URLs as a different domain user
  via the configured RunAs profile, with optional stored or prompted credentials
- `url` and `incognito` type items now support a `values` array for opening multiple
  URLs in a single click (e.g. open two tools together under the same RunAs profile)
- Separate **Default** browser setting for `url` type items (`url_browser_name` /
  `url_browser_path`) - independently configurable from the incognito browser
- Browser settings group in Settings redesigned into two clearly labelled rows:
  **Default** (for `url` items) and **Incognito** (for `incognito` items), separated
  by a visual divider

### Changed
- `AppConfig` gains two new fields: `url_browser_name` and `url_browser_path` -
  existing configs default to `"default"` (system browser) with no breaking change
- RunAs Profiles **Add / Save / Delete** buttons are now left-aligned with the
  field inputs above them rather than sitting at the left edge of the panel
- `FlavourItem` gains a `values` property (`List<string>`) - `value` (single string)
  remains fully supported alongside it for backwards compatibility

### Fixed
- `url` items with `runas_profile` set but no explicit browser path now fall back
  to Edge (always present on managed Windows) instead of silently failing

## [2.0.2] - 11/03/26
### Changed
Updated the default **RunAs** profile so the `Workstation Admin` username is now blank by default,
requiring explicit configuration per installation instead of shipping a placeholder `DOMAIN\Username` value.

## [2.0.1] - 10/03/26
### Fixed
- Updated Syntax in Source File and also updated Languestic Errors
- Updated Flavours

## [2.0.0] - 10/03/26
### Overview
Version 2 is a major overhaul introducing the flavour system, DPAPI token encryption,
a first-time setup wizard, and a significantly expanded Settings window.
The app is now fully configurable at runtime with no recompilation required for menu changes.

### Added
- **Flavour system** - menus are now driven by JSON files in `%AppData%\PandaTools\flavours\`
  - Switch active flavour at runtime via Settings without restarting
  - Add, remove, and import flavour files directly from the Settings window
  - Automatic GitLab polling syncs flavour files from the configured repo in the background
  - `hidden: true` flag on a flavour excludes it from the active flavour dropdown
- **First-time setup wizard** - on first launch, prompts for a GitLab Personal Access Token
  before the tray icon appears; token is encrypted immediately
- **DPAPI token encryption** - tokens are encrypted via Windows `ProtectedData` (per-user,
  per-machine); no plain text is ever written to disk or left in memory between sessions
- **"Update Token" button** in Settings - enter a new token at any time without editing config files
- **GitLab Project ID field** in Settings - configure the flavour polling project ID at runtime
- **RunAs profiles** - named credential profiles stored in `config.json`; launch menu items
  as different domain users with optional stored or prompted passwords
- **Default RunAs profiles** baked in at compile time - edit `AppConfig.cs` before building
  to pre-populate profiles for all deployments
- **Flavour seeding** - `.json` files in the `flavours\` folder next to the exe are
  automatically copied to AppData on each startup (new files only, never overwrites live edits)
- **`_Template.json`** written to AppData on first run if no flavour files are found -
  documents all supported item types; hidden from the active flavour dropdown
- **No-flavour warning** - a one-time message shown on startup if no visible flavour files exist,
  pointing to the flavours directory
- Private/incognito browser support (`incognito` item type) with configurable browser selection
- `app` item type with optional `admin: true` for UAC elevation and `arguments` support
- `runas` item type for launching tools under different domain credentials
- Manual mode toggle - disables automatic GitLab flavour polling per-device

### Changed
- **Token storage** migrated from AES key+token file pair (CredEncrypt-Utility) to Windows DPAPI -
  legacy `keyFile` / `tokenFile` config fields are retained for backwards-compatible migration
- `ConfigLoader` no longer generates default flavour JSONs at runtime - menus are entirely
  driven by files in the flavours directory
- `ConfigLoader.Save()` added as a public helper for atomic config writes
- Settings window redesigned with expanded Connection group, Project ID field, token update row,
  and improved RunAs profile editor
- Tray tooltip now shows active flavour name
- `.csproj` cleaned up - removed dead `config.json` and `flavours\**` copy rules; removed
  unnecessary `ProtectedData` NuGet reference (inbox on .NET 10)
- `PandaTools.Setup` excluded from main project compile via `<Compile Remove>` to fix CS0017

### Fixed
- CS0017 entry point conflict when `PandaTools.Setup/Program.cs` was swept into the main build
- CS7022 warning caused by top-level statements coexisting with an explicit `Main` method
- NU1100 / NU1510 package resolution errors for `ProtectedData` on .NET 10
- CA1416 platform compatibility warnings in `PandaTools.Setup/Program.cs`
- Flavour seeding blocked by `_Template.json` presence - seeder now operates per-file
  and ignores the template when deciding whether real flavours exist

### Removed
- Dependency on **CredEncrypt-Utility** - token encryption is now self-contained via DPAPI
- Hardcoded default flavour generation (`EnsureDefaultFiles` / `WriteDefaultFlavour`) -
  replaced by the seed-from-exe-directory mechanism


## [1.1.0] - 10/03/26
### Fixed
- Config and flavour files now saved to `%APPDATA%\PandaTools\` instead of the install
  directory - resolves "Access denied" error on save when installed to `Program Files`

## [1.0.0] - 05/03/26
- Initial release
- System tray icon with right-click context menu
- Web tools, network and script shortcuts
- GitLab update checker
- GitLab API token decryption via AES key files
- Live PowerShell script fetching from GitLab repositories
