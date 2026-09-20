#pragma once

// The GTK half of a scene: turning the `setScene` message into one.
//
// Parsed once, so a malformed message is a channel error with something to
// read rather than an out-of-bounds read inside the renderer. What a scene
// holds and what is done with it are the same on Windows and live in
// ../common/orblit_scene_common.h; the one part that is not shared is this,
// because the embedders hand the message over differently.
//
// The standard codec hands Kotlin typed arrays already (a Dart Float32List
// *is* a FloatArray), while GTK hands back an FlValue that has to be asked
// its type and then unwrapped. So every field goes through one of the small
// borrowers in the .cc, each of which answers "absent, or the wrong type" as
// an empty array rather than as a failure -- which is exactly what the ABI's
// own "a null pointer and a count of nought" means for an optional field.

#include <flutter_linux/flutter_linux.h>

#include <memory>

#include "orblit_scene_common.h"

namespace orblit_linux {

using orblit_scene::Bytes;
using orblit_scene::Floats;
using orblit_scene::Ints;
using orblit_scene::Longs;
using orblit_scene::Strings;

class Scene : public orblit_scene::SceneData {
 public:
  // Null for a message missing what every scene must carry: the object
  // arrays and a camera, or object arrays that disagree on length -- which
  // the ABI would otherwise read as a short array past its end. Everything
  // else is optional in the way Swift's and Kotlin's treat it: absent
  // decodes to "this part of the scene has nothing to say".
  static std::unique_ptr<Scene> From(FlValue* args);

 private:
  Scene() = default;
};

}  // namespace orblit_linux
