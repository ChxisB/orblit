/**************************************************************************/
/*  groove_joint_planar.cpp                                                   */

#include "groove_joint_planar.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"
#include "scene/planar/physics/physics_body_planar.h"
#include "scene/stage/stage.h"
#include "systems/physics_planar/physics_system_planar.h"

void GrooveJoint2D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_DRAW: {
			if (!is_inside_stage()) {
				break;
			}

			if (!Engine::get_singleton()->is_editor_hint() && !PhysicsSystem2D::get_singleton()->debug_is_enabled()) {
				break;
			}

			draw_line(Point2(-10, 0), Point2(+10, 0), Color(0.7, 0.6, 0.0, 0.5), 3);
			draw_line(Point2(-10, length), Point2(+10, length), Color(0.7, 0.6, 0.0, 0.5), 3);
			draw_line(Point2(0, 0), Point2(0, length), Color(0.7, 0.6, 0.0, 0.5), 3);
			draw_line(Point2(-10, initial_offset), Point2(+10, initial_offset), Color(0.8, 0.8, 0.9, 0.5), 5);
		} break;
	}
}

void GrooveJoint2D::_configure_joint(RID p_joint, PhysicsBody2D *body_a, PhysicsBody2D *body_b) {
	Transform2D gt = get_global_transform();
	Vector2 groove_A1 = gt.get_origin();
	Vector2 groove_A2 = gt.xform(Vector2(0, length));
	Vector2 anchor_B = gt.xform(Vector2(0, initial_offset));

	PhysicsSystem2D::get_singleton()->joint_make_groove(p_joint, groove_A1, groove_A2, anchor_B, body_a->get_rid(), body_b->get_rid());
}

void GrooveJoint2D::set_length(real_t p_length) {
	length = p_length;
	queue_redraw();
}

real_t GrooveJoint2D::get_length() const {
	return length;
}

void GrooveJoint2D::set_initial_offset(real_t p_initial_offset) {
	initial_offset = p_initial_offset;
	queue_redraw();
}

real_t GrooveJoint2D::get_initial_offset() const {
	return initial_offset;
}

void GrooveJoint2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_length", "length"), &GrooveJoint2D::set_length);
	ClassRegistry::bind_method(D_METHOD("get_length"), &GrooveJoint2D::get_length);
	ClassRegistry::bind_method(D_METHOD("set_initial_offset", "offset"), &GrooveJoint2D::set_initial_offset);
	ClassRegistry::bind_method(D_METHOD("get_initial_offset"), &GrooveJoint2D::get_initial_offset);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "length", PROPERTY_HINT_RANGE, "1,65535,1,exp,suffix:px"), "set_length", "get_length");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "initial_offset", PROPERTY_HINT_RANGE, "1,65535,1,exp,suffix:px"), "set_initial_offset", "get_initial_offset");
}

GrooveJoint2D::GrooveJoint2D() {
}
