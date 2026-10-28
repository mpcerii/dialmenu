<div align="center">

# DialMenu

**Your keyboard's volume dial becomes a menu.**

A radial menu that turns the rotary knob on a mechanical keyboard into a
full program and macro launcher.

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-2f2f33?style=flat-square)
![macOS](https://img.shields.io/badge/macOS-planned-c9c9cd?style=flat-square)
![Linux](https://img.shields.io/badge/Linux-planned-c9c9cd?style=flat-square)
![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-2f2f33?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-2f2f33?style=flat-square)

<img src="docs/ring-dark.png" width="300" alt="The ring in dark mode">

**English** · [Deutsch](docs/README.de.md) · [Español](docs/README.es.md) · [Русский](docs/README.ru.md)

</div>

---

## What this is

Most mechanical keyboards have a rotary knob that is hard-wired to volume.
DialMenu intercepts those signals before the system sees them and opens a menu
instead: **turn to select, press to run.**

The keyboard itself is never modified. No flashing, no firmware, no drivers.
DialMenu only listens — unplug the keyboard or quit the program and everything
is as it was.

```
   Turn ──▶ HID report ──▶ DialMenu ──▶ action
                              │
                              └──▶ the system no longer sees the volume key
```

## Features

<img src="docs/settings.png" alt="The settings window with command list, editor and ring preview">

- **Radial menu** — glass surfaces, monochrome, with a single lit selection
  outline. As many commands as you like; the ring grows with them.
- **Four ring themes** in two brightnesses each, plus light and dark on demand
  or following the system setting.
- **German and English**, switchable without a restart.
- **Programs, folders, websites** — including a picker over the programs that
  are actually installed, instead of typing paths.
- **Keyboard shortcuts** — `Ctrl+Alt+M`, `Win+D`, `Alt+F4` and everything
  between.
- **Macros** — chain shortcuts, text and pauses into one sequence.
- **Discord** — mute and deafen from the ring.
- **Multiple profiles** and **multiple devices**.
- **Automatic device detection** — three turns right, three left, one press.
  DialMenu finds the knob itself without anyone entering an ID.
- **Free placement** — twelve positions plus the monitor under the mouse
  cursor.

## Installation

Windows 10 or 11, .NET Framework 4.8 (preinstalled on Windows 11). There is no
installer — DialMenu is a single `.exe`.

1. [Download the latest release](../../releases/latest)
2. Put `DialMenu.exe` in a folder of your choice
3. Run it — the settings window opens

From there it takes two clicks: under **Devices**, hit *Start detection* and
turn the dial three steps right, three left, then press once. After that,
assemble what you want on the ring under **Commands**.

## Getting started

**Which keyboards work?**
Any whose knob sends standard volume keys — that covers most of them. Thanks to
automatic detection, nothing needs to be entered for it.

**The knob does nothing.**
Run the assistant under **Devices** again. The progress readout
("right 1/3 · left 0/3") shows whether any signal is arriving at all. If it
stays at zero, the keyboard sends something that does not arrive as a media key.

**Where are the settings when the knob owns the menu?**
In the notification area of the taskbar. One click there opens the window.

**Can I get the volume back?**
Yes: quit DialMenu from the notification area, or clear the checkbox under
**Behaviour**. The keyboard's own volume keys keep working either way.

## Configuration

Everything lives in `%APPDATA%\DialMenu\config.json` — readable and
versionable. A profile can be copied and shared.

## Building from source

<img src="docs/ring-light.png" width="260" align="right" alt="The ring in light mode">

Requirements: Windows with .NET Framework 4.8 and MSBuild (ships with Visual
Studio or the Build Tools).

```powershell
.\build.ps1
```

The finished file ends up at `bin\Release\DialMenu.exe`.

Without Visual Studio, the MSBuild copy that Windows already carries is enough:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' DialMenu.csproj /p:Configuration=Release
```

## How it works

Two mechanisms interlock:

**Raw Input** reads the raw HID packets. From those it derives which detent was
turned and when the knob was pressed — independent of which keys the system
makes of them.

**A keyboard hook** swallows the volume keys that result. It only lets them
through when they did *not* come from the own device immediately before. Volume
keys from other programs and keyboards are untouched.

Both run on a dedicated thread. The reason is measured: if the hook does not
answer within 300 ms, Windows discards the key and the volume changes anyway.
Run it on the UI thread and exactly that happens as soon as anything is drawn.

Keys the program generates itself are tagged and never swallowed — otherwise it
would block itself.

## Platforms

| | Status |
|---|---|
| Windows 10 / 11 | done |
| macOS | planned |
| Linux | planned |

Both mechanisms DialMenu relies on are platform-bound and have no direct
equivalent on macOS or Linux. The way there is therefore not a port but a
second implementation: on Linux via `evdev` and `uinput`, on macOS via
`IOHIDManager` and `CGEventTap`. The wheel itself, the themes and the
interaction logic are unaffected and carry over.

## Limits

- **Standard media keys only.** Firmware speaking its own protocol cannot be
  intercepted.
- **One detent wakes the menu, it does not select yet.** Otherwise the first
  step would be doing double duty.
- **System volume is unavailable on the knob** while DialMenu runs. The
  keyboard's own volume keys still work.
- **Labels in the ring** only appear up to six entries. Beyond that no word
  fits the arc — the icons stay.

## Contributing

Bug reports and suggestions are welcome. If a keyboard is not detected, a
snippet of the readout under **Devices** helps.

## License

MIT — see [LICENSE](../LICENSE).
