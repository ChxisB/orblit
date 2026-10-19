# Changelog

## 0.3.0

- `UiStrings` holds an interface's words by key in as many languages as were
  added. A text or button node names its key in `props['textKey']` and its
  arguments in `props['textArgs']`. `{name}` is replaced by the argument of
  that name, and a whole `count` argument picks `key.one`, `key.few` and the
  other plural forms of the language the text is found in.
- A key a language lacks is looked up in its parent language and then in the
  default, and each of them is tried by its own parents. A key nobody has
  shows the node's own `text`, or the key when it has none.
- `UiStrings.addJson` reads one language file, an object of key to text.
  `UiStrings.gaps` lists what each language has no text for. `find` answers
  null for a key nobody has, and `clear` drops every table.
- `UiNode.textKey`, `textArgs` and `textKeys` read the keys of a description.
- `UiSurface` and `UiBuilder` take `strings`, and redraw when the language
  changes. They also take `fontFallback`, a list of font families tried in
  turn for a character the main font lacks.
- Text that is not keyed draws as before.

## 0.2.0

- Touch elements: `stick`, `touch-button` and `swipe-pad`. Each follows its
  own finger, so a thumb on a stick and another on a button work together.
  A stick sends `[x, y]` to the handler in `onMove`. A touch button sends a
  `bool` to `onHold`. A swipe pad sends `[dx, dy]` to `onSwipe`. Up is
  positive. A finger that lifts, is cancelled, or sits on an element that is
  rebuilt away reports its release. Padding and a background are part of what
  takes the touch.

## 0.1.0

- First cut of `orblit_ui`. Pre-alpha: everything is subject to change.
