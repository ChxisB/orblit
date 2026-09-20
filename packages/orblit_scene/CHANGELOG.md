# Changelog

## 0.2.0

- Material files. `MaterialDocument` reads an `.omat`: a shading model, a
  blend mode, the parameters in `MaterialFields`, the maps a surface wears,
  and a parent to take the rest from. A file with one unreadable parameter
  keeps the others and says what it dropped.
- `MaterialLibrary` resolves a parent chain and a group's overrides once, in
  Dart, and caches the answer. A loop or a missing parent is reported rather
  than thrown or hung on.
- `MaterialComponent` carries named looks, the shape `KHR_materials_variants`
  uses: an object states only what it wears differently under each name.

## 0.1.0

- First cut of `orblit_scene`. Pre-alpha: everything is subject to change.
