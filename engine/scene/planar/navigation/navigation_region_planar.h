/**************************************************************************/
/*  navigation_region_planar.h                                                */

#pragma once

#include "scene/planar/entity_planar.h"
#include "scene/resources/planar/navigation_polygon.h"

class NavigationRegion2D : public Entity2D {
	ORBLIT_CLASS(NavigationRegion2D, Entity2D);

	bool enabled = true;
	bool use_edge_connections = true;

	RID region;
	RID map_override;
	uint32_t navigation_layers = 1;
	real_t enter_cost = 0.0;
	real_t travel_cost = 1.0;
	Ref<NavigationPolygon> navigation_polygon;

	void _navigation_polygon_changed();

	Rect2 bounds;

#ifdef DEBUG_ENABLED
private:
	RID debug_mesh_rid;
	RID debug_instance_rid;

	bool debug_mesh_dirty = true;

	void _set_debug_visible(bool p_visible);
	void _update_debug_mesh();
	void _update_debug_edge_connections_mesh();
	void _update_debug_baking_rect();
	void _navigation_map_changed(RID p_map);
	void _navigation_debug_changed();
#endif // DEBUG_ENABLED

protected:
	void _notification(int p_what);
	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual Rect2 _edit_get_rect() const override;
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;
#endif // DEBUG_ENABLED
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

	void set_navigation_polygon(const Ref<NavigationPolygon> &p_navigation_polygon);
	Ref<NavigationPolygon> get_navigation_polygon() const;

	PackedStringArray get_configuration_warnings() const override;

	void bake_navigation_polygon(bool p_on_thread);
	void _bake_finished();
	bool is_baking() const;

	Rect2 get_bounds() const { return bounds; }

	NavigationRegion2D();
	~NavigationRegion2D();

private:
	void _update_bounds();
	void _region_enter_navigation_map();
	void _region_exit_navigation_map();
	void _region_update_transform();
};
