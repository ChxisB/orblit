/**************************************************************************/
/*  mesh_instance_planar.h                                                    */

#pragma once

#include "scene/planar/entity_planar.h"
#include "scene/resources/mesh.h"

class NavigationPolygon;
class NavigationMeshSourceGeometryData2D;

class MeshInstance2D : public Entity2D {
	ORBLIT_CLASS(MeshInstance2D, Entity2D);

	Ref<Mesh> mesh;

	Ref<Texture2D> texture;

protected:
	void _notification(int p_what);
	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual Rect2 _edit_get_rect() const override;
	virtual bool _edit_use_rect() const override;
#endif // DEBUG_ENABLED

	void set_mesh(const Ref<Mesh> &p_mesh);
	Ref<Mesh> get_mesh() const;

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

	virtual PackedStringArray get_configuration_warnings() const override;
};
