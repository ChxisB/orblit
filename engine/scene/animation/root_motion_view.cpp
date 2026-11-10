/**************************************************************************/
/*  root_motion_view.cpp                                                  */

#ifndef SPATIAL_DISABLED

#include "root_motion_view.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"
#include "scene/animation/animation_mixer.h"
#include "scene/resources/material.h"

void RootMotionView::set_animation_mixer(const EntityPath &p_path) {
	path = p_path;
	first = true;
}

EntityPath RootMotionView::get_animation_mixer() const {
	return path;
}

void RootMotionView::set_color(const Color &p_color) {
	color = p_color;
	first = true;
}

Color RootMotionView::get_color() const {
	return color;
}

void RootMotionView::set_cell_size(float p_size) {
	cell_size = p_size;
	first = true;
}

float RootMotionView::get_cell_size() const {
	return cell_size;
}

void RootMotionView::set_radius(float p_radius) {
	radius = p_radius;
	first = true;
}

float RootMotionView::get_radius() const {
	return radius;
}

void RootMotionView::set_zero_y(bool p_zero_y) {
	zero_y = p_zero_y;
}

bool RootMotionView::get_zero_y() const {
	return zero_y;
}

void RootMotionView::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			immediate_material = StandardMaterial3D::get_material_for_2d(false, BaseMaterial3D::TRANSPARENCY_ALPHA, false);

			first = true;
		} break;

		case NOTIFICATION_INTERNAL_FRAME:
		case NOTIFICATION_INTERNAL_STEP: {
			Transform3D transform;
			Basis diff;

			if (has_entity(path)) {
				Entity *node = get_entity(path);
				AnimationMixer *mixer = Object::cast_to<AnimationMixer>(node);
				if (mixer && mixer->is_active() && mixer->get_root_motion_track() != EntityPath()) {
					if (is_internal_frame_enabled() && mixer->get_callback_mode_process() == AnimationMixer::ANIMATION_CALLBACK_MODE_PROCESS_PHYSICS) {
						set_internal_frame_enabled(false);
						set_internal_step_enabled(true);
					}

					if (is_internal_step_enabled() && mixer->get_callback_mode_process() == AnimationMixer::ANIMATION_CALLBACK_MODE_PROCESS_IDLE) {
						set_internal_frame_enabled(true);
						set_internal_step_enabled(false);
					}

					transform.origin = mixer->get_root_motion_position();
					transform.basis = mixer->get_root_motion_rotation(); // Scale is meaningless.
					diff = mixer->is_root_motion_local() ? Quaternion() : mixer->get_root_motion_rotation_accumulator();
				}
			}
			if (!first && transform == Transform3D()) {
				return;
			}

			first = false;

			accumulated.basis *= transform.basis;
			transform.origin = (diff.inverse() * accumulated.basis).xform(transform.origin);
			accumulated.origin += transform.origin;

			accumulated.origin.x = Math::fposmod(accumulated.origin.x, cell_size);
			if (zero_y) {
				accumulated.origin.y = 0;
			}
			accumulated.origin.z = Math::fposmod(accumulated.origin.z, cell_size);

			immediate->clear_surfaces();

			int cells_in_radius = int((radius / cell_size) + 1.0);

			immediate->surface_begin(Mesh::PRIMITIVE_LINES, immediate_material);

			for (int i = -cells_in_radius; i < cells_in_radius; i++) {
				for (int j = -cells_in_radius; j < cells_in_radius; j++) {
					Vector3 from(i * cell_size, 0, j * cell_size);
					Vector3 from_i((i + 1) * cell_size, 0, j * cell_size);
					Vector3 from_j(i * cell_size, 0, (j + 1) * cell_size);
					from = accumulated.xform_inv(from);
					from_i = accumulated.xform_inv(from_i);
					from_j = accumulated.xform_inv(from_j);

					Color c = color, c_i = color, c_j = color;
					c.a *= MAX(0, 1.0 - from.length() / radius);
					c_i.a *= MAX(0, 1.0 - from_i.length() / radius);
					c_j.a *= MAX(0, 1.0 - from_j.length() / radius);

					immediate->surface_set_color(c);
					immediate->surface_add_vertex(from);

					immediate->surface_set_color(c_i);
					immediate->surface_add_vertex(from_i);

					immediate->surface_set_color(c);
					immediate->surface_add_vertex(from);

					immediate->surface_set_color(c_j);
					immediate->surface_add_vertex(from_j);
				}
			}

			immediate->surface_end();
		} break;
	}
}

AABB RootMotionView::get_aabb() const {
	return AABB(Vector3(-radius, 0, -radius), Vector3(radius * 2, 0.001, radius * 2));
}

void RootMotionView::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_animation_path", "path"), &RootMotionView::set_animation_mixer);
	ClassRegistry::bind_method(D_METHOD("get_animation_path"), &RootMotionView::get_animation_mixer);

	ClassRegistry::bind_method(D_METHOD("set_color", "color"), &RootMotionView::set_color);
	ClassRegistry::bind_method(D_METHOD("get_color"), &RootMotionView::get_color);

	ClassRegistry::bind_method(D_METHOD("set_cell_size", "size"), &RootMotionView::set_cell_size);
	ClassRegistry::bind_method(D_METHOD("get_cell_size"), &RootMotionView::get_cell_size);

	ClassRegistry::bind_method(D_METHOD("set_radius", "size"), &RootMotionView::set_radius);
	ClassRegistry::bind_method(D_METHOD("get_radius"), &RootMotionView::get_radius);

	ClassRegistry::bind_method(D_METHOD("set_zero_y", "enable"), &RootMotionView::set_zero_y);
	ClassRegistry::bind_method(D_METHOD("get_zero_y"), &RootMotionView::get_zero_y);

	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "animation_path", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "AnimationMixer"), "set_animation_path", "get_animation_path");
	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "color"), "set_color", "get_color");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "cell_size", PROPERTY_HINT_RANGE, "0.1,16,0.01,or_greater,suffix:m"), "set_cell_size", "get_cell_size");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "radius", PROPERTY_HINT_RANGE, "0.1,16,0.01,or_greater,suffix:m"), "set_radius", "get_radius");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "zero_y"), "set_zero_y", "get_zero_y");
}

RootMotionView::RootMotionView() {
	if (Engine::get_singleton()->is_editor_hint()) {
		set_internal_frame_enabled(true);
	}
	immediate.instantiate();
	set_base(immediate->get_rid());
}

RootMotionView::~RootMotionView() {
	set_base(RID());
}

#endif // SPATIAL_DISABLED
