#pragma once

// The producer's end of a feed on Android, which only the JNI bridge calls.
// The reading end is orblit::openFeed in OrblitPlatform.h, shared with every
// other platform.

#include <jni.h>

#include <cstdint>

namespace orblit {

/// A Java Surface that draws into feed `id`, or null for a number that is
/// not an open feed or a device below API 26. The caller owns the local
/// reference. Any thread.
jobject feedSurface(JNIEnv *env, int64_t id);

}  // namespace orblit
