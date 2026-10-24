# Changelog

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
