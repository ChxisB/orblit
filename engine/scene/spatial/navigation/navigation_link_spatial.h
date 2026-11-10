/**************************************************************************/
/*  navigation_link_spatial.h                                                  */

#pragma once

#include "scene/spatial/entity_spatial.h"

class ArrayMesh;

class NavigationLink3D : public Entity3D {
	ORBLIT_CLASS(NavigationLink3D, Entity3D);

	bool enabled = true;
	RID link;
	RID map_override;
	bool bidirectional = true;
	uint32_t navigation_layers = 1;
	Vector3 end_position;
	Vector3 start_position;
	real_t enter_cost = 0.0;
	real_t travel_cost = 1.0;

#ifdef DEBUG_ENABLED
	RID debug_instance;
	Ref<ArrayMesh> debug_mesh;

	void _update_debug_mesh();
#endif // DEBUG_ENABLED

protected:
	static void register_members();
	void _notification(int p_what);

public:
	NavigationLink3D();
	~NavigationLink3D();

	RID get_rid() const;

	void set_enabled(bool p_enabled);
	bool is_enabled() const { return enabled; }

	void set_navigation_map(RID p_navigation_map);
	RID get_navigation_map() const;

	void set_bidirectional(bool p_bidirectional);
	bool is_bidirectional() const { return bidirectional; }

	void set_navigation_layers(uint32_t p_navigation_layers);
	uint32_t get_navigation_layers() const { return navigation_layers; }

	void set_navigation_layer_value(int p_layer_number, bool p_value);
	bool get_navigation_layer_value(int p_layer_number) const;

	void set_start_position(Vector3 p_position);
	Vector3 get_start_position() const { return start_position; }

	void set_end_position(Vector3 p_position);
	Vector3 get_end_position() const { return end_position; }

	void set_global_start_position(Vector3 p_position);
	Vector3 get_global_start_position() const;

	void set_global_end_position(Vector3 p_position);
	Vector3 get_global_end_position() const;

	void set_enter_cost(real_t p_enter_cost);
	real_t get_enter_cost() const { return enter_cost; }

	void set_travel_cost(real_t p_travel_cost);
	real_t get_travel_cost() const { return travel_cost; }

	PackedStringArray get_configuration_warnings() const override;

private:
	void _link_enter_navigation_map();
	void _link_exit_navigation_map();
	void _link_update_transform();
};
