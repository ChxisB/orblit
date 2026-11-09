/**************************************************************************/
/*  static_raycaster.h                                                    */

#pragma once

#include "core/object/ref_counted.h"

class StaticRaycaster : public RefCounted {
	ORBLIT_CLASS(StaticRaycaster, RefCounted)
protected:
	static Ref<StaticRaycaster> (*create_function)();

public:
	// Compatible with embree4 rays.
	struct alignas(16) Ray {
		const static unsigned int INVALID_GEOMETRY_ID = ((unsigned int)-1); // from rtcore_common.h

		/*! Default construction does nothing. */
		_FORCE_INLINE_ Ray() :
				geomID(INVALID_GEOMETRY_ID) {}

		/*! Constructs a ray from origin, direction, and ray segment. Near
		 *  has to be smaller than far. */
		_FORCE_INLINE_ Ray(const Vector3 &p_org,
				const Vector3 &p_dir,
				float p_tnear = 0.0f,
				float p_tfar = Math::INF) :
				org(p_org),
				tnear(p_tnear),
				dir(p_dir),
				time(0.0f),
				tfar(p_tfar),
				mask(-1),
				u(0.0),
				v(0.0),
				primID(INVALID_GEOMETRY_ID),
				geomID(INVALID_GEOMETRY_ID),
				instID(INVALID_GEOMETRY_ID) {}

		/*! Tests if we hit something. */
		_FORCE_INLINE_ explicit operator bool() const { return geomID != INVALID_GEOMETRY_ID; }

	public:
		Vector3 org; //!< Ray origin + tnear
		float tnear; //!< Start of ray segment
		Vector3 dir; //!< Ray direction + tfar
		float time; //!< Time of this ray for motion blur.
		float tfar; //!< End of ray segment
		unsigned int mask; //!< used to mask out objects during traversal
		unsigned int id; //!< ray ID
		unsigned int flags; //!< ray flags

		Vector3 normal; //!< Not normalized geometry normal
		float u; //!< Barycentric u coordinate of hit
		float v; //!< Barycentric v coordinate of hit
		unsigned int primID; //!< primitive ID
		unsigned int geomID; //!< geometry ID
		unsigned int instID; //!< instance ID
	};

	virtual bool intersect(Ray &p_ray) = 0;
	virtual void intersect(Vector<Ray> &r_rays) = 0;

	virtual void add_mesh(const PackedVector3Array &p_vertices, const PackedInt32Array &p_indices, unsigned int p_id) = 0;
	virtual void commit() = 0;

	virtual void set_mesh_filter(const HashSet<int> &p_mesh_ids) = 0;
	virtual void clear_mesh_filter() = 0;

	static Ref<StaticRaycaster> create();
};
