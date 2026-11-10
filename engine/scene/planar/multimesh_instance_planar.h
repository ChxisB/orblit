/**************************************************************************/
/*  multimesh_instance_planar.h                                               */

#pragma once

#include "scene/planar/entity_planar.h"
#include "scene/resources/multimesh.h"

class NavigationPolygon;
class NavigationMeshSourceGeometryData2D;

class MultiMeshInstance2D : public Entity2D {
	ORBLIT_CLASS(MultiMeshInstance2D, Entity2D);

	Ref<MultiMesh> multimesh;

	Ref<Texture2D> texture;

	void _refresh_interpolated();

protected:
	virtual void _physics_interpolated_changed() override;
	void _notification(int p_what);
	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual Rect2 _edit_get_rect() const override;
#endif // DEBUG_ENABLED

	void set_multimesh(const Ref<MultiMesh> &p_multimesh);
	Ref<MultiMesh> get_multimesh() const;

	void set_texture(const Ref<Texture2D> &p_texture);
	Ref<Texture2D> get_texture() const;

private:
	static Callable _navmesh_source_geometry_parsing_callback;
	static RID _navmesh_source_geometry_parser;

public:
#ifndef NAVIGATION_PLANAR_DISABLED
	static void navmesh_parse_init();
	static void navmesh_parse_source_geometry(const Ref<NavigationPolygon> &p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data, Entity *p_node);
#endif // NAVIGATION_PLANAR_DISABLED

	MultiMeshInstance2D();
	~MultiMeshInstance2D();
};
