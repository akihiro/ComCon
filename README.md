# ComCon

[![build](https://github.com/akihiro/ComCon/actions/workflows/build.yml/badge.svg)](https://github.com/akihiro/ComCon/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/akihiro/ComCon?include_prereleases)](https://github.com/akihiro/ComCon/releases/latest)
[![license](https://img.shields.io/github/license/akihiro/ComCon)](LICENSE)

[日本語](README.ja.md)

ComCon is a Serial/COM connector for Windows Terminal.

It sends your keystrokes to a serial port unchanged, as VT sequences, and writes the data it receives straight to Windows Terminal. Arrow keys, colours and cursor movement from the remote side (a Linux console, for example) work as they would in any terminal.

## Requirements

- Windows 10 version 1809 or later (Windows Terminal recommended)
- .NET Framework 4.8.1

## Installation

1. Download `ComCon.exe` from the [latest release](https://github.com/akihiro/ComCon/releases/latest).
2. Put it in any folder, for example `C:\Tools`. Add that folder to `PATH` if you want to run `ComCon` from anywhere.

ComCon is a single file with no installer. Translations and debug symbols are embedded in it.

The executable is not code-signed. On first run, Windows SmartScreen may show "Windows protected your PC". Click **More info** and then **Run anyway**. You can also unblock the file beforehand in its Properties dialog, or with PowerShell: `Unblock-File C:\Tools\ComCon.exe`.

## Usage

```console
ComCon --list                    List the available ports
ComCon COM3                      Connect to COM3 at 115200 8N1
ComCon COM3 -b 9600 -p even      Connect at 9600 8E1
ComCon 3 -f rtscts -n crlf       Use RTS/CTS flow control and send CRLF on Enter
```

Run `ComCon --help` for the full list of options.

Messages are shown in the Windows display language. English and Japanese are available; other languages fall back to English. Use `--lang en` or `--lang ja` to override.

While connected, press `Ctrl+]` and then one of these keys to run a local command:

| Key | Action |
| --- | ------ |
| `q` | Quit |
| `b` | Send a break signal |
| `e` | Turn local echo on or off |
| `?` | Show help |
| `Ctrl+]` | Send `Ctrl+]` (0x1D) itself |

`Ctrl+C` does not quit ComCon. It is sent to the remote side as 0x03.

When stdin or stdout is redirected, ComCon sends the contents of stdin to the port and exits at end of input. Received data is written out as raw bytes.

## Windows Terminal profile

To connect straight from a new tab, add a profile like this to `profiles.list` in `settings.json`:

```json
{
    "name": "COM3 (115200)",
    "commandline": "C:\Tools\ComCon.exe COM3 -b 115200",
    "icon": "\uE703"
}
```

## Building

```shell
dotnet build -c Release
```

The output is a single file, `bin\Release\net481\ComCon.exe`. Translations and debug symbols are embedded in it, so copying this one file is enough to install ComCon.

## Contributing

Bug reports, translations and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md). Please report security issues privately, as described in [SECURITY.md](SECURITY.md).

## License

ComCon is licensed under the [Apache License 2.0](LICENSE).
