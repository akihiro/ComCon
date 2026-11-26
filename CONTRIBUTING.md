# Contributing to ComCon

Bug reports, feature requests, translations and pull requests are welcome.

## Reporting bugs

Use the [bug report form](https://github.com/akihiro/ComCon/issues/new/choose). Include the exact command line, the output of `ComCon --version` and the serial adapter you use. Many serial problems depend on the driver (FTDI, CP210x, CH340 and so on).

Please report security issues privately. See [SECURITY.md](SECURITY.md).

## Building

You need the .NET SDK (any recent version) on Windows. The project targets .NET Framework 4.8.1, and the reference assemblies come from NuGet, so no targeting pack is needed.

```shell
dotnet build              # Debug: bin\Debug\net481\ComCon.exe
dotnet build -c Release   # Release: bin\Release\net481\ComCon.exe
```

The build must produce a single file, `ComCon.exe`. Translations and the PDB are embedded in it. New dependencies must be .NET Framework (GAC) assemblies, or be embedded too.

There are no automated tests. Check interactive behaviour in Windows Terminal with a real device or a virtual COM port pair such as [com0com](https://com0com.sourceforge.net/).

## Code style

- The language version is C# 7.3 (the default for .NET Framework). Newer syntax such as file-scoped namespaces, records or `using var` does not compile.
- `using` directives go inside the `namespace` block.
- `.editorconfig` holds the remaining rules, and Visual Studio and VS Code apply it automatically.
- Code comments and documentation are written in English. `README.ja.md` is the exception: it is a Japanese translation of `README.md`, so update it when you change `README.md`. If you can't write Japanese, say so in the pull request.

## Messages and translations

All user-facing messages live in `Strings.resx` (English). The `ComCon.Strings` class is generated from it at build time.

- **Adding or changing a message:** update the key in every `Strings*.resx` file. Use `Localization.Format` for placeholders.
- **Adding a language:** copy `Strings.resx` to `Strings.<culture>.resx` (for example `Strings.de.resx`) and translate the values. Nothing else is needed: the csproj embeds every `Strings.*.resx` automatically. Mention the new language in `README.md`.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each pull request focused on one change.
3. Make sure `dotnet build -c Release` succeeds. CI runs the same build on every pull request.
4. Describe how you tested the change (device or virtual port, settings).

By contributing, you agree that your contributions are licensed under the [Apache License 2.0](LICENSE).

## Releases (maintainers)

Push a tag such as `v1.2.3`. Only admins can create `v*` tags. The workflow builds `ComCon.exe` with that version and attaches it, with its SHA-256 and a signed build provenance attestation, to a new GitHub Release. Tags that contain a hyphen (`v1.2.3-beta.1`) are marked as pre-releases. Tags that are not valid versions fail the build. The release notes are generated from merged pull requests and grouped by label, as configured in `.github/release.yml`. Label each pull request before merging: `breaking-change`, `enhancement`, `bug`, `translation`, `documentation`, or `ignore-for-release`.

Workflow changes must keep the CI hardening:

- Pin actions to full commit SHAs.
- Keep `permissions` minimal.
- Never interpolate untrusted input (`${{ }}`) inside `run` scripts.

The repository rejects actions that are not pinned or not GitHub-owned.
