/**************************************************************************/
/*  fbx_state.h                                                           */

#pragma once

#include "modules/gltf/gltf_defines.h"
#include "modules/gltf/gltf_state.h"
#include "modules/gltf/structures/gltf_skin.h"

#include <ufbx.h>

class FBXState : public GLTFState {
	ORBLIT_CLASS(FBXState, GLTFState);
	friend class FBXDocument;
	friend class SkinTool;
	friend class GLTFSkin;

	// Smart pointer that holds the loaded scene.
	ufbx_unique_ptr<ufbx_scene> scene;
	bool allow_geometry_helper_nodes = false;

	HashMap<uint64_t, Image::AlphaMode> alpha_mode_cache;
	HashMap<Pair<uint64_t, uint64_t>, GLTFTextureIndex> albedo_transparency_textures;

	Vector<GLTFSkinIndex> skin_indices;
	Vector<GLTFSkinIndex> original_skin_indices;
	HashMap<ObjectID, GLTFSkeletonIndex> skeleton3d_to_fbx_skeleton;
	HashMap<ObjectID, HashMap<ObjectID, GLTFSkinIndex>> skin_and_skeleton3d_to_fbx_skin;
	HashSet<String> unique_mesh_names; // Not in GLTFState because GLTFState prefixes mesh names with the scene name (or _)

protected:
	static void register_members();

public:
	bool get_allow_geometry_helper_nodes();
	void set_allow_geometry_helper_nodes(bool p_allow_geometry_helper_nodes);
};
