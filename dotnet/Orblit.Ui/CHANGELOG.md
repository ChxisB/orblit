# Changelog

## 0.3.0

- The interface vocabulary is gone, by decision 95: `UiNode`, `UiDocument`,
  `UiStyle`, `UiCss`, `UiUtilities`, `UiTheme`, `UiBreakpoints`, `UiCanvas`,
  `CanvasFit` and `UiColour`. An interface is made of Avalonia controls.
- `UiStrings` and its plural rules stay.

## 0.2.0

- `UiCanvas` and `CanvasFit`: the size an interface was laid out at, how it
  is scaled onto a screen, the fluid scale, and the column grid with its
  snapping.
- `UiDocument`: an interface saved as a file, read as null when it is not one.
- `UiStrings`: tables of text by key and language, plural forms for 33
  languages, a lookup that walks from the regional language to the default,
  and the gaps a translator fills.
- A canvas read from a map takes an `int` as well as a `long`.

## 0.1.0

- A port of the model half of `orblit_ui` 0.4.0: the node tree, the style, the
  CSS and class list readers, the theme and the breakpoints.
- `UiColour` is a 32 bit value, alpha first. The package does not depend on a
  UI toolkit, so a host turns it into its own colour type.
- `UiNode` is an immutable record. An edit gives a new tree and shares every
  sibling it did not touch. A path is a list of child indices.
- A tree nested deeper than the JSON reader allows reads as the text node
  `The interface could not be read: Nested too deep`.
- `UiTheme.spacing` is not ported. Nothing reads it.
