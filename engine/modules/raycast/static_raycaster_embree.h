/**************************************************************************/
/*  static_raycaster_embree.h                                             */

#pragma once

#ifdef TOOLS_ENABLED

#include "core/math/static_raycaster.h"

#include <embree4/rtcore.h>

class StaticRaycasterEmbree : public StaticRaycaster {
	ORBLIT_CLASS(StaticRaycasterEmbree, StaticRaycaster);

private:
	static RTCDevice embree_device;
	RTCScene embree_scene;

	HashSet<int> filter_meshes;

public:
	virtual bool intersect(Ray &p_ray) override;
	virtual void intersect(Vector<Ray> &r_rays) override;

	virtual void add_mesh(const PackedVector3Array &p_vertices, const PackedInt32Array &p_indices, unsigned int p_id) override;
	virtual void commit() override;

	virtual void set_mesh_filter(const HashSet<int> &p_mesh_ids) override;
	virtual void clear_mesh_filter() override;

	static Ref<StaticRaycaster> create_embree_raycaster();
	static void make_default_raycaster();
	static void free();

	StaticRaycasterEmbree();
	~StaticRaycasterEmbree();
};

#endif // TOOLS_ENABLED
