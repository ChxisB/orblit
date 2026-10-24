# Changelog

## 0.1.0

- A port of the builder and the surface of `orblit_ui` 0.4.0 to Avalonia.
  `UiBuilder` turns a `UiNode` into controls. `UiSurface` measures itself,
  picks the width the prefixed classes are read against and builds again when
  its description, its canvas or its strings change.
- `FlexPanel` is the row and the column. It shares free space the way a flex
  box does, with `Grow`, `FillWidth` and `FillHeight` on the children. With no
  bounded space to share, a growing child keeps its own size.
- `WebImages` loads the pictures an `image` element names over http and https.
  Anything else, and anything that fails, gives no picture.
- No font is bundled. `UiBuilderOptions.FontFamily` and `FontFallback` name
  fonts the host already has. Which font a build ships waits on decision 84.

Where this differs from the Flutter build:

- A baseline cross alignment reads as the start.
- A border takes room inside the box. In Flutter it is drawn over it.
- Uppercase uses the invariant rules, so the sharp s stays one letter.
- An element key is the control's `Tag`. A rebuild keeps no state, so a field
  loses focus when the description changes.
- A spacer outside a row or a column is an empty box. In Flutter it throws.
- Growing, filling and placing apply only inside a row, a column or a stack.
  At the root, Flutter throws and this ignores them.
- A fixed size on the main axis of a growing child is kept. Flutter's tight
  constraints override it.
- A stack is a `Grid`, so every child counts toward its size.
- A field tells its handler about every change to its text, typed or set.
- Headless tests replace the widget tests. Their goldens are rebaselined.
