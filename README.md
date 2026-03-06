# PandaTools

A Windows system tray utility for Digital Services Colleagues at Leeds Beckett University.
PandaTools provides quick access to web tools, network utilities, and PowerShell scripts fetched live from GitLab.

## Features

- System tray icon with right-click context menu
- Quick launch shortcuts for web tools and network utilities
- Live PowerShell script execution fetched directly from GitLab
- Automatic update checker against the latest GitLab release
- AES-encrypted GitLab API token — no plaintext credentials on disk

## Requirements

- Windows 11
- .NET 8.0 Runtime or later
- Access to gitlab.leedsbeckett.ac.uk (on-network or VPN)
- AES key and token files at:
  - C:\Windows\Build\PandaTools\K_PandaTools.txt
  - C:\Windows\Build\PandaTools\C_PandaTools.txt

## Installation

1. Download the latest release from the Releases page
2. Extract and run PandaTools.exe
3. PandaTools will appear in the system tray

Token files must be generated using CredEncrypt-Utility before first run.
Without them, GitLab features will be silently disabled.

## Project Structure

PandaTools/
├── src/
│   ├── Program.cs
│   ├── TrayApp.cs
│   ├── GitLabScriptRunner.cs
│   ├── TokenManager.cs
│   └── ...
├── CHANGELOG.md
└── README.md