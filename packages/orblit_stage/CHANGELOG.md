# Changelog

## 0.3.0

- `materialFrom` turns a resolved `.omat` into an `OrblitMaterial`, with a
  contract test that fails if a parameter is added to the table and not
  applied here.
- `OrblitDocumentView` takes a `MaterialLibrary` and an optional look.
  Materials are now keyed by what decided them rather than by entity, so a
  hundred crates wearing one material are one material and one batch.
- Naming an image rather than an `.omat` still means "a plain surface wearing
  this picture", as it did before materials had files.

## 0.2.0

- **`OrblitSkinBinding`** drives a model file's skin from an `orblit_rig`
  armature: bones matched to joints by name, an evaluated `Pose` turned into
  the joints the renderer sets by hand. Bone and joint frames need not agree.
  `armatureOfSkin` makes an armature from a skin.

## 0.1.0

- First cut of `orblit_stage`. Pre-alpha: everything is subject to change.
