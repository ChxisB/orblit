/**************************************************************************/
/*  remote_transform_planar.h                                                 */

#pragma once

#include "scene/planar/entity_planar.h"

class RemoteTransform2D : public Entity2D {
	ORBLIT_CLASS(RemoteTransform2D, Entity2D);

	EntityPath remote_node;

	ObjectID cache;

	bool use_global_coordinates = true;
	bool update_remote_position = true;
	bool update_remote_rotation = true;
	bool update_remote_scale = true;

	// Each write of a matrix adds rounding error to the scale read back from it.
	// The scale is held until the node's own scale moves by more than that error.
	Size2 kept_remote_scale;

	void _track_remote_scale(const Transform2D &p_remote);
	Transform2D _compose_remote_transform(const Transform2D &p_remote, const Transform2D &p_ours) const;
	void _update_remote();
	void _update_cache();
	//void _node_exited_scene();
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

	RemoteTransform2D();
};
