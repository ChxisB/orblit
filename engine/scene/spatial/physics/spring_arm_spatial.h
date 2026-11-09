/**************************************************************************/
/*  spring_arm_spatial.h                                                       */

#pragma once

#include "scene/spatial/entity_spatial.h"
#include "scene/resources/spatial/shape_spatial.h"

class SpringArm3D : public Entity3D {
	ORBLIT_CLASS(SpringArm3D, Entity3D);

	Ref<Shape3D> shape;
	HashSet<RID> excluded_objects;
	real_t spring_length = 1.0;
	real_t current_spring_length = 0.0;
	uint32_t mask = 1;
	real_t margin = 0.01;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_length(real_t p_length);
	real_t get_length() const;
	void set_shape(Ref<Shape3D> p_shape);
	Ref<Shape3D> get_shape() const;
	void set_mask(uint32_t p_mask);
	uint32_t get_mask();
	void add_excluded_object(RID p_rid);
	bool remove_excluded_object(RID p_rid);
	void clear_excluded_objects();
	real_t get_hit_length();
	void set_margin(real_t p_margin);
	real_t get_margin();

private:
	void process_spring();
};
