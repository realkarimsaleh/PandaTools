# PandaTools Changelog

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
