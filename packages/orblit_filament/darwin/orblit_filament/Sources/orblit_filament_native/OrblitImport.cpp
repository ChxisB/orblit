#include "OrblitImport.h"
#include "OrblitImportInternal.h"

// The FBX and OBJ importer's entry points, and the order the work is done in.
//
// What a host asks for: whether a name is a file this importer takes, and the
// GLB made from its bytes. ufbx does the reading — including the companion
// files an OBJ names, which are fetched through the same callback rather than
// off disk — and `Converter::run` below is the order the three writing
// sources are called in. Everything they share is OrblitImportInternal.h.

namespace orblit {

bool importsAsGlb(const std::string &name) {
  auto endsWith = [&](const char *suffix) {
    const size_t length = std::strlen(suffix);
    if (name.size() <= length) return false;
    for (size_t i = 0; i < length; i++) {
      const char c = name[name.size() - length + i];
      const char lower = (c >= 'A' && c <= 'Z') ? char(c - 'A' + 'a') : c;
      if (lower != suffix[i]) return false;
    }
    return true;
  };
  return endsWith(".fbx") || endsWith(".obj");
}

namespace glb {

void Converter::run(ImportSummary &summary) {
  noteUnsupported();
  collectMeshes();
  planNodes();
  measure();
  writeSkins();
  writeNodes();
  writeAnimations();

  if (doc_.nodes.empty()) {
    throw Failure{"the file has no nodes or geometry to import"};
  }
  summary.nodes = uint32_t(doc_.nodes.size());
  summary.meshes = uint32_t(doc_.meshes.size());
  summary.materials = uint32_t(doc_.materials.size());
  summary.skins = uint32_t(doc_.skins.size());
  summary.primitives = primitives_;
  summary.joints = joints_;
  summary.morphTargets = morphTargets_;
  summary.embeddedImages = embeddedImages_;
  summary.referencedImages = referencedImages_;
  summary.clips = clips_;
  if (!sceneBox_.empty) {
    summary.hasBounds = true;
    for (int a = 0; a < 3; a++) {
      summary.minimum[a] = float(sceneBox_.minimum[a]);
      summary.maximum[a] = float(sceneBox_.maximum[a]);
    }
  }
}

// ---- Loading ---------------------------------------------------------------

struct Companions {
  const ImportReader *read = nullptr;
  Losses *losses = nullptr;
};

/// ufbx asks for the files a model names beside itself through this, and
/// only an OBJ's material library is ever answered. Geometry caches are
/// refused, and so is any path the file gives as absolute: a model from
/// somewhere else has no business reading this machine's files by name.
bool openCompanion(void *user, ufbx_stream *stream, const char *path,
                   size_t pathLength, const ufbx_open_file_info *info) {
  // Called from inside ufbx, which cannot unwind; nothing thrown gets past.
  try {
    const Companions *companions = static_cast<const Companions *>(user);
    if (!info || info->type != UFBX_OPEN_FILE_OBJ_MTL) return false;
    if (!companions->read || !*companions->read) return false;
    const std::string original(
        static_cast<const char *>(info->original_filename.data),
        info->original_filename.data ? info->original_filename.size : 0);
    if (looksAbsolute(original)) {
      companions->losses->add("material library '" + original +
                              "' is an absolute path; not read");
      return false;
    }
    const SharedBytes bytes = (*companions->read)(std::string(path, pathLength));
    if (!bytes) return false;
    if (bytes->size() > kMaxCompanionBytes) {
      companions->losses->add("material library '" + original +
                              "' is too large to be one; not read");
      return false;
    }
    static const uint8_t kNothing = 0;
    ufbx_open_memory_opts opts = {};
    ufbx_error error;
    return ufbx_open_memory_ctx(stream, info->context,
                                bytes->empty() ? &kNothing : bytes->data(),
                                bytes->size(), &opts, &error);
  } catch (...) {
    return false;
  }
}

Imported convert(const uint8_t *bytes, size_t size, const std::string &name,
                 const ImportReader &read) {
  const std::string shown = baseName(name);
  if (!importsAsGlb(name)) {
    throw Failure{"'" + shown + "' is not an .fbx or .obj file"};
  }
  if (!bytes || size == 0) throw Failure{"'" + shown + "' is empty"};
  if (size > kImportMaxInputBytes) {
    throw Failure{"'" + shown + "' is larger than the " +
                  std::to_string(kImportMaxInputBytes / (1024 * 1024)) +
                  " MB an import reads"};
  }
  const bool obj = lowerExtension(name) == "obj";

  Losses losses;
  Companions companions;
  companions.read = &read;
  companions.losses = &losses;

  ufbx_load_opts opts = {};
  opts.temp_allocator.memory_limit = kUfbxMemoryLimit;
  opts.result_allocator.memory_limit = kUfbxMemoryLimit;
  // The extension decides, not the content: a file named .fbx is only ever
  // read as FBX, whatever its first bytes claim.
  opts.file_format = obj ? UFBX_FILE_FORMAT_OBJ : UFBX_FILE_FORMAT_FBX;
  opts.no_format_from_content = true;
  opts.no_format_from_extension = true;

  // glTF's space: right-handed, +Y up, +Z the front, one unit a metre.
  opts.target_axes = ufbx_axes_right_handed_y_up;
  opts.target_unit_meters = 1;
  // An OBJ carries neither, and the usual exporters write Y-up metres, so
  // that is what it is taken to be: nothing is rotated or scaled.
  opts.obj_axes = ufbx_axes_right_handed_y_up;
  opts.obj_unit_meters = 1;
  // How the conversion is done matters to everything after it. Scaling the
  // root would leave 0.01 on a node every centimetre file has, and a skinned
  // mesh's joints under it. Adjusting transforms alone would leave the
  // vertices in centimetres under a node scaled to metres. Modifying the
  // geometry puts vertices, translations, blend shape offsets and every
  // skin's bind matrices in metres together, with node scales left as the
  // artist set them — so bounds, skinning and baked animation all agree.
  opts.space_conversion = UFBX_SPACE_CONVERSION_MODIFY_GEOMETRY;
  // A left-handed file is mirrored across X, the axis glTF exporters use,
  // and its faces rewound so they still face out.
  opts.handedness_conversion_axis = UFBX_MIRROR_AXIS_X;

  // FBX transforms have three things glTF nodes do not. A geometric
  // transform moves a node's mesh but not its children: a helper node
  // between the two carries it, which works for instanced meshes, where
  // changing the vertices would not.
  opts.geometry_transform_handling = UFBX_GEOMETRY_TRANSFORM_HANDLING_HELPER_NODES;
  // A node may ignore its parent's scale, or apply it per axis. glTF
  // inherits one way only: ufbx scales the children back where that is
  // exact (uniform, unanimated) and inserts a helper node where it is not.
  opts.inherit_mode_handling = UFBX_INHERIT_MODE_HANDLING_COMPENSATE;
  // Rotation and scaling pivots: the node is moved onto its rotation pivot
  // and its geometry and children moved back, so an animated rotation is a
  // rotation channel rather than a translation that follows it — which
  // baked keys interpolate exactly. Empties keep their authored origin, the
  // place a user would attach something to by name.
  opts.pivot_handling = UFBX_PIVOT_HANDLING_ADJUST_TO_ROTATION_PIVOT;
  opts.pivot_handling_retain_empties = true;

  opts.generate_missing_normals = true;
  opts.normalize_normals = true;
  opts.clean_skin_weights = true;
  // Blender writes its principled material into FBX's Phong slots in a way
  // ufbx can read back as metallic and roughness.
  opts.use_blender_pbr_material = true;
  opts.node_depth_limit = kNodeDepthLimit;

  // Paths inside the file resolve against this one's directory, using
  // whichever separator it was named with.
  opts.filename.data = name.c_str();
  opts.filename.length = name.size();
  opts.path_separator =
      name.find('\\') != std::string::npos && name.find('/') == std::string::npos
          ? '\\'
          : '/';
  opts.load_external_files = true;
  opts.ignore_missing_external_files = true;
  opts.obj_search_mtl_by_filename = true;
  opts.open_file_cb.fn = &openCompanion;
  opts.open_file_cb.user = &companions;

  ufbx_error error;
  std::unique_ptr<ufbx_scene, void (*)(ufbx_scene *)> scene(
      ufbx_load_memory(bytes, size, &opts, &error), ufbx_free_scene);
  if (!scene) {
    // ufbx's description of its commonest failure is "Failed to load", which
    // tells a person nothing they did not know; say what it means instead.
    const std::string why = error.type == UFBX_ERROR_UNKNOWN
                                ? std::string("the file is damaged, or not a valid ") +
                                      (obj ? "OBJ" : "FBX")
                            : error.type == UFBX_ERROR_MEMORY_LIMIT
                                ? "it needs more memory than an import may use"
                                : text(error.description);
    std::string note = "could not read '" + shown + "': " + why;
    if (error.info_length > 0 && error.info_length < sizeof error.info) {
      note += " (" + std::string(error.info, error.info_length) + ")";
    }
    throw Failure{note};
  }

  for (size_t w = 0; w < scene->metadata.warnings.count; w++) {
    const ufbx_warning &warning = scene->metadata.warnings.data[w];
    // Finding model.mtl for model.obj when the file named none is ufbx
    // doing what was asked, not something lost.
    if (warning.type == UFBX_WARNING_IMPLICIT_MTL) continue;
    std::string line = text(warning.description);
    if (warning.count > 1) line += " (" + std::to_string(warning.count) + " times)";
    losses.add(line);
  }

  Imported result;
  Converter converter(scene.get(), losses);
  converter.run(result.summary);
  result.glb = converter.glb();
  result.losses = losses.take();
  return result;
}

}  // namespace glb

Imported importToGlb(const uint8_t *bytes, size_t size, const std::string &name,
                     const ImportReader &read) {
  try {
    return glb::convert(bytes, size, name, read);
  } catch (const glb::Failure &failure) {
    Imported failed;
    failed.note = failure.note;
    return failed;
  } catch (const std::bad_alloc &) {
    Imported failed;
    failed.note = "ran out of memory converting '" + glb::baseName(name) + "'";
    return failed;
  } catch (...) {
    Imported failed;
    failed.note = "could not glb::convert '" + glb::baseName(name) + "'";
    return failed;
  }
}

}  // namespace orblit
