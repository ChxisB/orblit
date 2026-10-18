# Changelog

## 0.2.0

- `UiPanel` draws a widget tree without a window and takes pointers in its own
  pixels. It returns PNG bytes and tells listeners when the picture is out of
  date. A slider or a menu in it has an overlay. A press gets a pointer
  number of its own, so it never closes the arena of a real pointer.
- `LabelAtlas` packs many short strings into one picture and says where
  each sits. The picture grows in height and never moves a string.

## 0.1.0

- First cut of `orblit_ui`. Pre-alpha: everything is subject to change.
