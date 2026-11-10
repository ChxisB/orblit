/**************************************************************************/
/*  joint_planar.h                                                            */

#pragma once

#include "scene/planar/entity_planar.h"

class PhysicsBody2D;

class Joint2D : public Entity2D {
	ORBLIT_CLASS(Joint2D, Entity2D);

	RID joint;
	RID ba, bb;

	EntityPath a;
	EntityPath b;
	real_t bias = 0.0;

	bool exclude_from_collision = true;
	bool configured = false;
	String warning;

protected:
	void _disconnect_signals();
	void _body_exit_tree();
	void _update_joint(bool p_only_free = false);

	void _notification(int p_what);
	virtual void _configure_joint(RID p_joint, PhysicsBody2D *body_a, PhysicsBody2D *body_b) = 0;

	static void register_members();

	_FORCE_INLINE_ bool is_configured() const { return configured; }

public:
	virtual PackedStringArray get_configuration_warnings() const override;

	void set_node_a(const EntityPath &p_node_a);
	EntityPath get_node_a() const;

	void set_node_b(const EntityPath &p_node_b);
	EntityPath get_node_b() const;

	void set_bias(real_t p_bias);
	real_t get_bias() const;

	void set_exclude_nodes_from_collision(bool p_enable);
	bool get_exclude_nodes_from_collision() const;

	RID get_rid() const { return joint; }
	Joint2D();
	~Joint2D();
};
