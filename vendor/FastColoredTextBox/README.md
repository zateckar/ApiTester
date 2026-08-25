# FastColoredTextBox (vendored)

Source copied from <https://github.com/xiaoyuvax/FastColoredTextBox> (a fork of Pavel
Torgashov's FastColoredTextBox), **LGPLv3** — see `LICENSE.txt`.

## Why vendored rather than a NuGet package

The published control is not Native AOT safe. Its constructor reads a private field off the
framework's `TypeDescriptionProvider` by reflection:

```csharp
prov.GetType().GetField("Provider", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(prov)
```

Once metadata is trimmed — which Native AOT implies — `GetField` returns `null` and the
original code dereferenced it, so the app died with a `NullReferenceException` in
`FastColoredTextBox..ctor()` before the main form appeared.

## Local changes

Every local edit is tagged with an `AOT:` comment so it can be found when pulling in a newer
upstream version. Currently there is exactly one:

- **`FastColoredTextBox.cs`, constructor** — the reflection above is null-guarded. The provider
  it registers only customises how properties appear in the Visual Studio designer, so skipping
  registration costs nothing at run time.

Nothing else was modified. `GlobalSuppressions.cs`, `SyntaxHighlighter.cs.old`, the strong-name
key and `upgrade-assistant.clef` were not copied.

## Licensing note

LGPLv3 permits use and modification, but it also expects that a user can relink the application
against a modified version of the library. Consuming it as a separate DLL satisfies that
naturally; a Native AOT build statically links everything into one binary, which does not.
Worth confirming with whoever owns licensing before distributing an AOT build.

## API differences from the previous packages

This fork is not drop-in compatible with `FastColoredTextBox.Net6` + `PW.AutocompleteMenu`:

| Previously | In this fork |
| --- | --- |
| `TextStyle`, `Place`, `Style`, `AutocompleteItem` in `FastColoredTextBoxNS` | `FastColoredTextBoxNS.Types` |
| `Language` in `FastColoredTextBoxNS` | `FastColoredTextBoxNS.Text` |
| `AutocompleteMenuNS.AutocompleteMenu` | `FastColoredTextBoxNS.AutocompleteMenu`, and it now requires the target control in its constructor rather than acting as a designer extender provider |
| `GetStyleIndexMask(...)` + `this[place].style` bitmask | `GetStylesOfChar(place)` returning `List<Style>` |
