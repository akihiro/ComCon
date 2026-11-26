## Summary

<!-- What this changes and why. Link the issue if there is one (Fixes #123). -->

## Testing

<!-- How you checked it: port or adapter used (real device or com0com), and what you tried. -->

## Checklist

- [ ] `dotnet build -c Release` succeeds and the output is still the single file `ComCon.exe`
- [ ] `dotnet test tests/ComCon.Tests` passes (add tests for new options or parsing changes)
- [ ] New or changed messages are added to every `Strings*.resx` file
- [ ] `README.md` (and `README.ja.md`) are updated if usage changed
