/**************************************************************************/
/*  stage_fti.h                                                           */

#pragma once

#include "core/os/mutex.h"
#include "core/containers/local_vector.h"

class Entity3D;
class Entity;
struct Transform3D;
class StageFTITests;

#ifdef DEV_ENABLED
// Uncomment this to verify traversal method results.
// #define ORBLIT_SCENE_TREE_FTI_VERIFY
#endif

#ifdef SPATIAL_DISABLED
// Stubs
class StageFTI {
public:
	void frame_update(Entity *p_root, bool p_frame_start) {}
	void tick_update() {}
	void set_enabled(Entity *p_root, bool p_enabled) {}
	bool is_enabled() const { return false; }

	void node_3d_notify_changed(Entity3D &r_node, bool p_transform_changed) {}
	void node_3d_notify_delete(Entity3D *p_node) {}
	void node_3d_request_reset(Entity3D *p_node) {}
};
#else

// Important.
// This class uses raw pointers, so it is essential that on deletion, this class is notified
// so that any references can be cleared up to prevent dangling pointer access.

// This class can be used from a custom Stage.

// Note we could potentially make StageFTI static / global to avoid the lookup through scene tree,
// but this covers the custom case of multiple scene trees.

class StageFTI {
	friend class StageFTITests;

	enum TraversalMode : unsigned {
		TM_DEFAULT,
		TM_LEGACY,
		TM_DEBUG,
	};

	struct Data {
		static const uint32_t scene_tree_depth_limit = 48;

		// Prev / Curr lists of Entity3Ds having local xforms pumped.
		LocalVector<Entity3D *> tick_xform_list[2];

		// The frame lists are changed nodes that need to start traversal,
		// either longterm (on the tick list) or single frame forced.
		LocalVector<Entity3D *> frame_xform_list;
		LocalVector<Entity3D *> frame_xform_list_forced;

		// Prev / Curr lists of Entity3Ds having actively interpolated properties.
		LocalVector<Entity3D *> tick_property_list[2];

		LocalVector<Entity3D *> frame_property_list;
		LocalVector<Entity3D *> request_reset_list;
		LocalVector<Entity3D *> dirty_node_depth_lists[scene_tree_depth_limit];

		// When we are using two alternating lists,
		// which one is current.
		uint32_t mirror = 0;

		// Global on / off switch for StageFTI.
		bool enabled = false;

		// Whether we are in physics ticks, or in a frame.
		bool in_frame = false;

		// Updating at the start of the frame, or the end on second pass.
		bool frame_start = true;

		Mutex mutex;

		TraversalMode traversal_mode = TM_DEFAULT;
		bool use_optimized_traversal_method = true;

		// DEBUGGING
		bool periodic_debug_log = false;
		uint32_t debug_node_count = 0;
		uint32_t debug_nodes_processed = 0;

	} data;

#ifdef ORBLIT_SCENE_TREE_FTI_VERIFY
	StageFTITests *_tests = nullptr;
#endif

	void _update_dirty_nodes(Entity *p_node, uint32_t p_current_half_frame, float p_interpolation_fraction, bool p_active, const Transform3D *p_parent_global_xform = nullptr, int p_depth = 0);
	void _update_request_resets();

	void _reset_flags(Entity *p_node);
	void _reset_node3d_flags(Entity3D &r_node);
	void _node_3d_notify_set_xform(Entity3D &r_node);
	void _node_3d_notify_set_property(Entity3D &r_node);

	void _node_add_to_frame_list(Entity3D &r_node, bool p_forced);
	void _node_remove_from_frame_list(Entity3D &r_node, bool p_forced);

	void _create_depth_lists();
	void _clear_depth_lists();

public:
	// Hottest function, allow inlining the data.enabled check.
	void node_3d_notify_changed(Entity3D &r_node, bool p_transform_changed) {
		if (!data.enabled) {
			return;
		}
		MutexLock lock(data.mutex);

		if (p_transform_changed) {
			_node_3d_notify_set_xform(r_node);
		} else {
			_node_3d_notify_set_property(r_node);
		}
	}

	void node_3d_request_reset(Entity3D *p_node);
	void node_3d_notify_delete(Entity3D *p_node);

	// Calculate interpolated xforms, send to visual server.
	void frame_update(Entity *p_root, bool p_frame_start);

	// Update local xform pumps.
	void tick_update();

	void set_enabled(Entity *p_root, bool p_enabled);
	bool is_enabled() const { return data.enabled; }

	void set_debug_next_frame() { data.periodic_debug_log = true; }

	StageFTI();
	~StageFTI();
};

#endif // ndef SPATIAL_DISABLED
