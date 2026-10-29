# Changelog

## 0.3.0

- `UiBuilder`, `UiBuilderOptions`, `UiSurface`, `UiDecorator` and `UiEvent`
  are gone, by decision 95. An interface is made of Avalonia controls, so the
  package no longer depends on `Orblit.Ui`.
- `TouchStick`, `TouchHold` and `TouchSwipe` are public controls. Each wraps
  the face it is given and reports through an event. `TouchStick.Tilted`
  gives a `Vector`, `TouchHold.Held` a `bool` and `TouchSwipe.Swiped` a
  `Vector`. The values are the ones the elements sent, positive up and to the
  right.
- `TouchArea` is public so that the three can be. Only they derive from it.

## 0.2.0

- The `stick`, `touch-button` and `swipe-pad` elements listen for touches.
  Each claims the first finger that lands on it and follows that finger
  alone, so a thumb on a stick and another on a jump button do not compete.
  A mouse drives them too.
- A stick sends `double[] { x, y }`, each from -1 to 1 and positive up and to
  the right, only when the value changes. It sends `{ 0, 0 }` when let go. The
  knob is drawn over the face and follows the thumb.
- A touch button sends `true` on touch and `false` on lift, and dims its whole
  face while held. A swipe pad sends `{ dx, dy }` for each move, up positive,
  and nothing for a touch alone.
- A release is reported when the finger lifts, when the system takes it and
  when the control leaves the tree while the finger is down. The Flutter build
  reports that last one a microtask later.
- `LabelAtlas` packs many short strings into one picture with shelf packing.
  `Paint` returns PNG bytes. The picture grows in height and never moves a
  string. A string wider than the picture is an `ArgumentException`. A full
  atlas at its limit is an `InvalidOperationException`.
- `UiPanel` is not here. Where a panel gets its host in Avalonia waits on
  decision 85.

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
