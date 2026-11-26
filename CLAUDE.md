# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ComCon is a raw serial terminal for Windows Terminal. Keystrokes go to a COM port as VT sequences, and bytes received from the port are written straight to the console so that Windows Terminal renders them, colours and cursor movement included. Usage and key bindings are documented in `README.md`.

All documentation (README, CLAUDE.md, CONTRIBUTING, code comments) is written in English. The one exception is `README.ja.md`, a Japanese translation of `README.md`. When `README.md` changes, update `README.ja.md` to match.

## Layout

The project lives directly in the repo root: `ComCon.csproj`, the `*.cs` sources and the `Strings*.resx` files. There is no `.sln` file and no project subfolder. `.gitignore` excludes `bin/` and `obj/`.

Also in the root: `.editorconfig` (code style, including `using` placement), `.gitattributes` (line endings), `CONTRIBUTING.md`, `SECURITY.md` and `LICENSE` (Apache-2.0). `.github/` holds the CI/release workflow, the release-note categories (`release.yml`, grouped by PR label), the Dependabot config, `CODEOWNERS`, and the issue and PR templates. CodeQL code scanning (C# and Actions) uses GitHub's default setup, so there is no workflow file for it.

## Build & Run

- SDK-style project (`ComCon.csproj`) that targets **.NET Framework 4.8.1** (`net481`), not modern .NET. Windows only. Reference assemblies come from the auto-referenced `Microsoft.NETFramework.ReferenceAssemblies` NuGet package, so no locally installed targeting pack is needed.
- Build: `dotnet build` from the repo root (`dotnet build -c Release` for a release build).
- Run: `bin\Debug\net481\ComCon.exe COM3 -b 115200`, or `ComCon.exe --list`. Interactive behaviour can only be checked in a real console (Windows Terminal) attached to a real or virtual COM port, such as a com0com pair.
- There are no tests. Code style is set in `.editorconfig`.
- Version: `<Version>` in the csproj is the default for local builds. The release workflow overrides it from the tag (`v1.2.3` → `-p:Version=1.2.3`).
- CI/release: `.github/workflows/build.yml` builds on every push and PR to `main` (`windows-latest`). On a `v*` tag it also creates a GitHub Release with `ComCon.exe`, its SHA-256 and a build provenance attestation. `main` is protected by rulesets that apply to admins too (no bypass). Every change goes through a pull request (squash merge only, no approval needed). The `build` check from GitHub Actions must pass. Force pushes and deletion are blocked. Work on a branch and open a PR. Never push to `main` directly.
- CI hardening (keep it when editing workflows):
  - Pin every action to a full commit SHA with a `# vX.Y.Z` comment. The repository enforces SHA pinning and allows only GitHub-owned actions.
  - Keep `permissions` minimal per job and `persist-credentials: false` on checkout.
  - Pass untrusted values such as the tag name through `env`, never `${{ }}` inside `run`.
  - The `release` job runs in the `release` environment, which allows only `v*` tags. A tag ruleset lets only admins create, move or delete `v*` tags.
  - PRs from external contributors need approval before workflows run. `CODEOWNERS` covers `.github/`.

## Architecture

- `Program` parses arguments into `Options`, then either lists ports (`PortList`) or runs a `Session`. It maps port-open exceptions (`UnauthorizedAccessException` = port in use, `IOException`, `ArgumentException`) to exit code 2.
- `TerminalConsole` owns the Win32 console modes. On input it turns off line, echo and processed input and turns on `ENABLE_VIRTUAL_TERMINAL_INPUT`, so Ctrl+C arrives as 0x03 and arrow keys arrive as escape sequences. On output it turns on `ENABLE_VIRTUAL_TERMINAL_PROCESSING` and `DISABLE_NEWLINE_AUTO_RETURN`. It reads with `ReadConsoleW` and writes with `WriteConsoleW` (UTF-16, independent of the code page). `Restore()` must run on every exit path.
- When stdin or stdout is redirected, `TerminalConsole` falls back to raw byte streams. In `Session`, `PipeLoop` then sends stdin to the port until EOF, and received bytes are written to stdout undecoded. Status messages go to stderr in that mode.
- `Session` runs two threads:
  - The **main thread** reads keys and handles the `Ctrl+]` (0x1D) command prefix. It translates Enter (`\r`) to the configured newline and encodes text with a stateful `Encoder`.
  - A **background receiver thread** does blocking `BaseStream.Read` calls and decodes the bytes with a stateful `Decoder`, so multi-byte characters split across reads still come out right.
  - Console writes are serialized by a lock inside `TerminalConsole`.
- **Localization:**
  - Every user-facing message comes from `Strings.resx`, which holds English, the neutral language (`<NeutralLanguage>en`). `Strings.ja.resx` holds Japanese.
  - The strongly typed `ComCon.Strings` class is generated **at build time** into `obj/` through the `StronglyTyped*` metadata in the csproj. There is no checked-in `Strings.Designer.cs`. Design-time builds normally skip resource generation, so the csproj target `GenerateStronglyTypedResourcesBeforeCompile` runs `PrepareResources` before compiling. That keeps the class visible to IntelliSense even before the first build. Keep that target.
  - Use `Localization.Format` for placeholders.
  - When adding a message, add the key to every `.resx` file.
  - A new language needs only a new `Strings.<culture>.resx`.
- **Single-file output:** the build produces only `ComCon.exe`, and this must stay that way.
  - The csproj embeds every `Strings.*.resx` in the main assembly (`WithCulture=false`, `LogicalName=ComCon.Strings.<culture>.resources`), so no satellite assemblies are built.
  - At startup, `EmbeddedResourceManager.InstallForStrings()` swaps the generated class's private `resourceMan` field, through reflection, for a `ResourceManager` that reads those embedded resources and walks `ja-JP` → `ja` → neutral.
  - `DebugType=embedded` puts the PDB inside the exe, and `GenerateSupportedRuntime=false` stops the build from writing `ComCon.exe.config`.
  - New dependencies must be GAC/Framework assemblies, or be embedded too.
  - The UI culture is the OS default. `--lang` overrides it, and `Localization.ApplyFromArgs` applies it before full parsing so that even parse errors appear in the chosen language. It also sets `DefaultThreadCurrentUICulture`, so the receiver thread uses the same language.
- On a fatal port error (such as a USB device being unplugged), `Session.Fatal` restores the console and calls `Environment.Exit`. This is needed because the main thread can't be woken while it is blocked in `ReadConsoleW`.

## Gotchas

- `GC.SuppressFinalize(port.BaseStream)` right after `Open()` works around a .NET Framework bug: after a USB-serial unplug, the `SerialStream` finalizer can crash the process. Keep it.
- Setting `SerialPort.RtsEnable` while `Handshake` uses RTS throws an exception. That's why `Session` only sets it for `None` and `XOnXOff`.
- `System.Management` (WMI, used for friendly port names) is an explicit `<Reference>` in the csproj.
- `using` directives go inside the `namespace` block, not at the top of the file. Keep that style.
- The language version is the default for net481 (C# 7.3). Newer syntax such as file-scoped namespaces, records, switch expressions, `using var` or nullable reference types won't compile unless `<LangVersion>` is raised in the csproj.
