# Changelog

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
