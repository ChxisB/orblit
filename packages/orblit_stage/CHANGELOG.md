# Changelog

## 0.4.0

- **Clips drive skins.** `OrblitSkinBinding.jointsFrom` takes an
  `orblit_motion` clip's bones at one moment straight to the joints the
  renderer sets, with no rig in between. `poseFrom` writes them into a rig's
  `Pose` instead, so a constraint or a limb reaching for a target starts from
  the clip rather than from rest. `localsFrom` is the joints' locals on
  their own. Whatever a clip leaves out, a part of a joint or a whole one,
  stays as the file has it.
- Through a rig, a joint stretched further along one of its own axes than
  another is carried as near as a bone can carry it: every joint still ends
  up where the clip puts it. `jointsFrom` carries it exactly.
- A joint scaled to nothing no longer throws: what hangs from it goes with
  it.
- `boneNamesOfSkin` makes names unique with `orblit_rig`'s `BoneNaming`,
  which the clip importer shares, so an imported clip names its bones the way
  the binding does. A joint the renderer could not name is `<unknown>`.

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
