#pragma once

// Bytes a host hands the renderer by name, found before the disk is asked.
//
// Every asset a scene names — a mesh, a texture, an environment, a decal's
// picture, a splat capture, and the files a .gltf names beside itself — used
// to be a path, read with fopen. That is fine on a desktop and wrong almost
// everywhere else: a browser has no file system, an Android application's
// assets are inside an archive rather than on disk, and anything fetched over
// a network or out of a cache is already bytes in memory, which writing to a
// temporary file only to read straight back is a slow way to hand over.
//
// So the renderer asks here first. A name that was provided is answered from
// memory, and anything else falls through to the file of that name, which
// keeps every path that worked before working exactly as it did.
//
// Plain C++ and nothing else, and one store for the whole process rather
// than one per renderer: an application with two views of the same world
// should not have to hand the same forty megabytes over twice, and a view
// opened later should find what was provided before it existed.

#include <cstddef>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace orblit {

/// Bytes shared rather than copied. A mesh's bytes can be held by the store,
/// by a loader part-way through reading them and by Filament waiting to
/// upload them, all at once, and each lets go when it is finished.
using SharedBytes = std::shared_ptr<const std::vector<uint8_t>>;

/// Keeps `bytes` under `name` for every renderer in the process.
///
/// A name stands for bytes that do not change. Providing a name again
/// replaces what the next load of it sees, but nothing already loaded is
/// loaded again — a renderer keeps what it read, as it does for a file on
/// disk — so the way to change an asset is a new name, and a content hash in
/// the name is the natural one. Any thread.
void provideResource(const std::string &name, std::vector<uint8_t> &&bytes);

/// Lets go of what was provided under `name`. False when nothing was.
/// Anything already loaded from it stays loaded. Any thread.
bool releaseResource(const std::string &name);

/// The bytes provided under `name`, or null. Any thread.
SharedBytes findResource(const std::string &name);

/// How many times anything has been provided, since the process started.
///
/// A renderer that looked for a name and found nothing remembers that, so
/// forty objects naming a missing file do not each read the disk on every
/// frame. This is how it knows the answer may have changed: a load that
/// failed at one count is worth trying again at a higher one.
uint64_t resourceGeneration();

/// The bytes a path names: provided under that name, or read from the file.
/// Null when neither has it.
SharedBytes readResource(const std::string &path);

/// A name with its `.` and `..` segments worked out, so that a .gltf provided
/// as `models/robot.gltf` finds `models/../textures/wood.png` under the name
/// `textures/wood.png` it was provided as. Only for names: a path on disk is
/// left to the operating system, which knows about links this does not.
std::string collapseDotSegments(const std::string &name);

}  // namespace orblit
