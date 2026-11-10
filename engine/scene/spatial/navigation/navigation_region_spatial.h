/**************************************************************************/
/*  navigation_region_spatial.h                                                */

#pragma once

#include "scene/resources/navigation_mesh.h"
#include "scene/spatial/entity_spatial.h"

class NavigationRegion3D : public Entity3D {
	ORBLIT_CLASS(NavigationRegion3D, Entity3D);

	bool enabled = true;
	bool use_edge_connections = true;

	RID region;
	RID map_override;
	uint32_t navigation_layers = 1;
	real_t enter_cost = 0.0;
	real_t travel_cost = 1.0;
	Ref<NavigationMesh> navigation_mesh;

	void _navigation_mesh_changed();

	AABB bounds;

#ifdef DEBUG_ENABLED
	RID debug_instance;
	RID debug_edge_connections_instance;
	Ref<ArrayMesh> debug_mesh;
	Ref<ArrayMesh> debug_edge_connections_mesh;

private:
	void _update_debug_mesh();
	void _update_debug_edge_connections_mesh();
	void _navigation_map_changed(RID p_map);
	void _navigation_debug_changed();
#endif // DEBUG_ENABLED

protected:
	void _notification(int p_what);
	static void register_members();

public:
	RID get_rid() const;

	void set_enabled(bool p_enabled);
	bool is_enabled() const;

	void set_navigation_map(RID p_navigation_map);
	RID get_navigation_map() const;

	void set_use_edge_connections(bool p_enabled);
	bool get_use_edge_connections() const;

	void set_navigation_layers(uint32_t p_navigation_layers);
	uint32_t get_navigation_layers() const;

	void set_navigation_layer_value(int p_layer_number, bool p_value);
	bool get_navigation_layer_value(int p_layer_number) const;

	void set_enter_cost(real_t p_enter_cost);
	real_t get_enter_cost() const;

	void set_travel_cost(real_t p_travel_cost);
	real_t get_travel_cost() const;

	void set_navigation_mesh(const Ref<NavigationMesh> &p_navigation_mesh);
	Ref<NavigationMesh> get_navigation_mesh() const;

	/// Bakes the navigation mesh; once done, automatically
	/// sets the new navigation mesh and emits a signal
	void bake_navigation_mesh(bool p_on_thread);
	void _bake_finished();
	bool is_baking() const;

	PackedStringArray get_configuration_warnings() const override;

	AABB get_bounds() const { return bounds; }

	NavigationRegion3D();
	~NavigationRegion3D();

private:
	void _update_bounds();
	void _region_enter_navigation_map();
	void _region_exit_navigation_map();
	void _region_update_transform();
};
