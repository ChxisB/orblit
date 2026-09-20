#pragma once

// The Windows half of a scene: turning the `setScene` message into one.
//
// Parsed once, so a malformed message is a channel error with something to
// read rather than an out-of-bounds read inside the renderer. What a scene
// holds and what is done with it are the same on Linux and live in
// ../common/orblit_scene_common.h; the one part that is not shared is this,
// because the embedders hand the message over differently.
//
// Flutter's Windows embedder decodes the standard codec into an
// `EncodableValue` -- a std::variant -- so a Float32List arrives already as
// a `std::vector<float>` and is reached with std::get_if rather than through
// FlValue's accessors. That makes the borrowing simpler than the GTK one's,
// not harder: each reader in the .cpp answers "absent, or the wrong type"
// with an empty array, which is exactly what the ABI's "a null pointer and a
// count of nought" means for an optional field.

#include <flutter/encodable_value.h>

#include <memory>

#include "orblit_scene_common.h"

namespace orblit_windows {

using orblit_scene::Bytes;
using orblit_scene::Floats;
using orblit_scene::Ints;
using orblit_scene::List;
using orblit_scene::Longs;
using orblit_scene::Strings;

class Scene : public orblit_scene::SceneData {
 public:
  // Null for a message missing what every scene must carry: the object
  // arrays and a camera, or object arrays that disagree on length -- which
  // the ABI would otherwise read as a short array past its end. Everything
  // else is optional in the way Swift's, Kotlin's and the GTK one's treat
  // it: absent decodes to "this part of the scene has nothing to say".
  static std::unique_ptr<Scene> From(const flutter::EncodableValue* args);

 private:
  Scene() = default;
};

}  // namespace orblit_windows
