/**************************************************************************/
/*  csg.h                                                                 */

#pragma once

#include "core/math/aabb.h"
#include "core/math/transform_spatial.h"
#include "core/math/vector2.h"
#include "core/math/vector3.h"
#include "core/object/ref_counted.h"
#include "core/containers/vector.h"
#include "scene/resources/material.h"

struct CSGBrush {
	struct Face {
		Vector3 vertices[3];
		Vector2 uvs[3];
		AABB aabb;
		bool smooth = false;
		bool invert = false;
		int material = 0;
	};

	Vector<Face> faces;
	Vector<Ref<Material>> materials;

	inline void _regen_face_aabbs() {
		for (int i = 0; i < faces.size(); i++) {
			faces.write[i].aabb = AABB();
			faces.write[i].aabb.position = faces[i].vertices[0];
			faces.write[i].aabb.expand_to(faces[i].vertices[1]);
			faces.write[i].aabb.expand_to(faces[i].vertices[2]);
		}
	}

	// Create a brush from faces.
	void build_from_faces(const Vector<Vector3> &p_vertices, const Vector<Vector2> &p_uvs, const Vector<bool> &p_smooth, const Vector<Ref<Material>> &p_materials, const Vector<bool> &p_invert_faces);
	void copy_from(const CSGBrush &p_brush, const Transform3D &p_xform);
};
