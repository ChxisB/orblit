/**************************************************************************/
/*  xr_face_modifier_spatial.h                                                 */

#pragma once

#include "core/containers/rb_map.h"
#include "scene/spatial/entity_spatial.h"
#include "scene/spatial/mesh_instance_spatial.h"

/**
	The XRFaceModifier3D node drives the blend shapes of a MeshInstance3D
	with facial expressions from an XRFaceTracking instance.

	The blend shapes provided by the mesh are interrogated, and used to
	deduce an optimal mapping from the Unified Expressions blend shapes
	provided by the	XRFaceTracking instance to drive the face.
 */

class XRFaceModifier3D : public Entity3D {
	ORBLIT_CLASS(XRFaceModifier3D, Entity3D);

private:
	StringName tracker_name = "/user/face_tracker";
	EntityPath target;

	// Map from XRFaceTracker blend shape index to mesh blend shape index.
	RBMap<int, int> blend_mapping;

	MeshInstance3D *get_mesh_instance() const;
	void _get_blend_data();
	void _update_face_blends() const;

protected:
	static void register_members();

public:
	void set_face_tracker(const StringName &p_tracker_name);
	StringName get_face_tracker() const;

	void set_target(const EntityPath &p_target);
	EntityPath get_target() const;

	void _notification(int p_what);
};
