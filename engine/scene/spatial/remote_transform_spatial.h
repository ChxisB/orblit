/**************************************************************************/
/*  remote_transform_spatial.h                                                 */

#pragma once

#include "scene/spatial/entity_spatial.h"

class RemoteTransform3D : public Entity3D {
	ORBLIT_CLASS(RemoteTransform3D, Entity3D);

	EntityPath remote_node;

	ObjectID cache;

	bool use_global_coordinates = true;
	bool update_remote_position = true;
	bool update_remote_rotation = true;
	bool update_remote_scale = true;

	void _update_remote();
	void _update_cache();

protected:
	static void register_members();
	void _notification(int p_what);

public:
	void set_remote_node(const EntityPath &p_remote_node);
	EntityPath get_remote_node() const;

	void set_use_global_coordinates(const bool p_enable);
	bool get_use_global_coordinates() const;

	void set_update_position(const bool p_update);
	bool get_update_position() const;

	void set_update_rotation(const bool p_update);
	bool get_update_rotation() const;

	void set_update_scale(const bool p_update);
	bool get_update_scale() const;

	void force_update_cache();

	PackedStringArray get_configuration_warnings() const override;

	RemoteTransform3D();
};
