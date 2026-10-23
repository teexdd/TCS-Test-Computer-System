# Test Computer System 1.0 (TCS)

**English** | [ภาษาไทย](README.th.md)

A small Windows health-check tool. At sign-in it shows a full-screen, installer-style screen that checks and tests the PC in three steps, saves a local report, then takes you to the desktop and opens the apps you chose. A separate app window gives you a dashboard, settings, warnings and logs.

> **Status:** early version. It compiles, but it has not been fully tested on real Windows hardware yet. Please test before distributing.

## Features

### Three-step check screen (full screen, shown at sign-in)
1. **Checking** - device info and **PC vs. notebook** detection, drivers, power on/off history, recently launched apps.
2. **Testing** - display (14 full-screen colour/pattern frames, 0.5 s each by default), speakers (left, right, both), internet (ping, DNS, HTTP, optional download speed).
3. **Summary** - saves a report, then goes to the desktop and launches your auto-start apps (countdown, or click).

- `Esc` or **Skip to desktop** works at any time. A flashing-light warning is shown before the display test.
- **Unattended mode:** runs every test with no questions (results for display/speaker are marked "not verified"). **Interactive mode:** asks you to confirm display and speaker results.
- Rotating tips slideshow (10 tips), like the Windows 11 setup screen.

### Checks
| Check | What it does |
|---|---|
| Device info / type | Maker, model, CPU, RAM, GPU, disks, OS, BIOS; PC vs. notebook (chassis type, PCSystemType, battery); C: free space warning (<10%) |
| Drivers | Lists key driver classes with version/date; finds devices with problem codes (e.g. 28 = driver missing); flags the generic "Microsoft Basic Display Adapter" |
| Display | Black, white, R/G/B, grays, gradient, colour bars, checkerboard, grid, text sharpness, border |
| Speakers | Generated stereo tones (440 / 660 / 550 Hz) via `SoundPlayer`; L/R indicators |
| Internet | Adapters, gateway/8.8.8.8/1.1.1.1 ping, DNS, HTTP check, optional download speed |
| Power history | Last boot, uptime, events 6005/6006/6008/41/1074 from the last 30 days |
| Recent apps | Open windows + Windows UserAssist history (current user) |

### Dashboard (main app window)
- **Health score** ring (100 - 18 per failed check - 8 per warning), device illustration (PC/notebook), memory and C: usage.
- One card per check with an on/off switch and a **Test** button (runs only that check).
- Recent warnings and score history chart.
- Pages: **Home**, **Activity**, **Settings**, **Warnings**, **Logs**.

### Boot-time checks (optional)
A Windows scheduled task (SYSTEM, after one UAC approval) runs the device/driver/boot-history checks shortly after Windows starts, **before anyone signs in**. At sign-in the check screen shows those results instantly and continues with the rest. Windows does not allow normal apps to show UI between the boot logo and the sign-in screen, so the UI itself appears at sign-in.

### App activity monitor (optional, off by default)
A tiny background helper records **only the app name and the time it was opened/closed** (apps with a visible window). No window titles, no content. History length is configurable; clear it any time.

### Three UI skins
Windows 11 (Fluent, light/dark), Retro (90s desktop), Hi-Tech Terminal. Pick one in setup or Settings.

### Languages
English (US, default), Thai, Simplified Chinese, Japanese, Spanish. Chosen in setup, changeable in Settings.

## Privacy and security
- **All data stays on the PC.** Nothing is sent to the developer. No ads, analytics or telemetry.
- Network connections happen only during the internet test: `www.msftconnecttest.com`, `8.8.8.8`, `1.1.1.1`, and `speed.cloudflare.com` (only if speed test is on).
- Optional items are **off by default**: serial number, Windows user name, speed test can be turned off, activity monitor, boot task.
- Terms of Use + Privacy Notice (international: GDPR/UK GDPR, CCPA/CPRA, PDPA, LGPD, PIPEDA principles) are shown in setup and must be accepted. The English text in `lang/legal.txt` is the binding version. **This is a template, not legal advice - have it reviewed and fill in the placeholders.**
- Installs per user (`%LOCALAPPDATA%`), no admin needed, except the optional early-start/boot tasks (one UAC prompt). The SYSTEM boot task runs only from a protected copy in `Program Files`.
- No keyboard hooks, no injection, no shell replacement, no self-deleting tricks. Uninstall removes tasks, registry entries and (optionally) all data.

## Build
Requires only Windows (uses the .NET Framework C# compiler that ships with it).
```
build.bat
```
Output: `Test Computer System.exe`. Language files and legal text are embedded.

## Usage
Run the exe once: it starts the **setup wizard** (language, style, checks, startup behaviour, terms, install). After installing, run it from the Start menu to open the app.

Command-line switches:
| Switch | Meaning |
|---|---|
| `/startup` | Check screen at sign-in (respects "once a day" and unattended settings) |
| `/test` | Check screen now (interactive); add `/auto` for unattended |
| `/only=<name>` | With `/test`: run one check (`drivers`, `display`, `speaker`, `network`, `boot`, `apps`) |
| `/monitor` | Start the activity helper |
| `/boot` | Boot-time headless checks (used by the scheduled task) |
| `/admin-apply ...` | Elevated helper that creates/removes the scheduled tasks |
| `/uninstall` | Uninstall |

## Where things are stored
| What | Location |
|---|---|
| Program | `%LOCALAPPDATA%\Programs\TestComputerSystem` |
| Settings, warnings, last results, history, activity | `%LOCALAPPDATA%\TestComputerSystem` |
| Reports | `C:\TestComputerSystem\Logs` (falls back to the settings folder) |
| Boot-time results and protected copy | `%ProgramFiles%\TestComputerSystem` (only if boot checks are enabled) |

## Project layout
```
src/            C# source (Program, Setup, Onboard, MainForm, Checks, Boot, Monitor, Cfg, Theme, I18n)
lang/           *.lang translations + legal.txt (embedded into the exe)
app.manifest    asInvoker, DPI aware
app.ico         icon
build.bat       build script
```

## Add a language
1. Copy `lang/en-US.lang` to e.g. `lang/fr-FR.lang` and translate the values (keep `{0}` placeholders and `\n`).
2. Add one line for it to the `Langs` table in `src/I18n.cs` (code, native name, font names).
3. Run `build.bat`.


## Known limitations
- Display test covers the primary monitor only.
- Speaker/display results can only be confirmed by the user; unattended runs mark them "not verified".
- Windows keeps no full app-launch history by default; the activity monitor only sees apps that run while it is on.
- The activity helper stays resident while enabled (small, but not zero).

