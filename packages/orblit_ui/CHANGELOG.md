# Changelog

## 0.2.0

- Touch elements: `stick`, `touch-button` and `swipe-pad`. Each follows its
  own finger, so a thumb on a stick and another on a button work together.
  They report through `onEvent` as `onMove` (`[x, y]`), `onHold` (`bool`) and
  `onSwipe` (`[dx, dy]`), with up positive. A finger that is lifted,
  cancelled, or on an element that is rebuilt away reports its release.

## 0.1.0

- First cut of `orblit_ui`. Pre-alpha: everything is subject to change.
