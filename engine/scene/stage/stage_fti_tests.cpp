/**************************************************************************/
/*  stage_fti_tests.cpp                                                   */

#ifndef SPATIAL_DISABLED

#ifdef ORBLIT_SCENE_TREE_FTI_VERIFY
#include "stage_fti_tests.h"

#include "scene/spatial/entity_spatial.h"
#include "scene/spatial/visual_instance_spatial.h"
#include "scene/stage/stage_fti.h"

void StageFTITests::debug_verify_failed(const Entity3D *p_node_3d, const Transform3D &p_test) {
	print_line("VERIFY FAILED\n");
	print_line("test xform : " + String(Variant(p_test)));

	bool first = true;

	while (p_node_3d) {
		int32_t depth = MAX(p_node_3d->_get_scene_tree_depth(), 0);
		String tabs;
		for (int32_t n = 0; n < depth; n++) {
			tabs += "\t";
		}

		bool interp_equal = p_node_3d->_get_cached_global_transform_interpolated() == p_test;
		bool glob_equal = p_node_3d->get_global_transform() == p_test;

		String sz = tabs + p_node_3d->get_name() + " [ " + p_node_3d->get_class_name() + " ]\n";

		if (first) {
			sz += tabs + "... " + String(Variant(p_test)) + "\n";
		}

		sz += tabs + (p_node_3d->data.fti_global_xform_interp_set ? "[I] " : "[i] ") + String(Variant(p_node_3d->_get_cached_global_transform_interpolated())) + (interp_equal ? " ***" : "") + "\n";
		sz += tabs + "[g] " + String(Variant(p_node_3d->get_global_transform())) + (glob_equal ? " ***" : "");

		print_line(sz);

		p_node_3d = p_node_3d->get_parent_entity_3d();
		first = false;
	}
}

void StageFTITests::update_dirty_nodes(Entity *p_node, uint32_t p_current_half_frame, float p_interpolation_fraction, bool p_active, const Transform3D *p_parent_global_xform, int p_depth) {
	StageFTI::Data &data = _fti.data;

	// There are two runs going on here.
	// FIRST the naive entire scene tree (reference), where we are
	// setting state (i.e. writing out xforms, and other state)
	// SECOND the optimized run, where we are NOT
	// writing state, but only verifying that the xforms calculated
	// match those from the reference approach.
	bool should_verify = (data.traversal_mode == StageFTI::TM_DEBUG) && data.use_optimized_traversal_method;
	bool set_state = !should_verify;

	Entity3D *s = Object::cast_to<Entity3D>(p_node);

	if (s && !s->is_visible()) {
		return;
	}

	if (!s) {
		for (int n = 0; n < p_node->get_child_count(); n++) {
			update_dirty_nodes(p_node->get_child(n), p_current_half_frame, p_interpolation_fraction, p_active, nullptr, p_depth + 1);
		}
		return;
	}

	if (s->_test_dirty_bits(Entity3D::DIRTY_GLOBAL_TRANSFORM)) {
		_ALLOW_DISCARD_ s->get_global_transform();
	}

	if (!p_active) {
		if (data.frame_start) {
			if (s->data.fti_on_frame_xform_list || s->data.fti_frame_xform_force_update) {
				p_active = true;
			}
		} else {
			if (s->_test_dirty_bits(Entity3D::DIRTY_GLOBAL_INTERPOLATED_TRANSFORM)) {
				p_active = true;
			}
		}
	}

	if (data.frame_start) {
		s->data.fti_global_xform_interp_set = p_active;
	}

	if (p_active) {
		Transform3D local_interp;
		if (s->is_physics_interpolated()) {
			if (s->data.fti_on_tick_xform_list) {
				TransformInterpolator::interpolate_transform_3d(s->data.local_transform_prev, s->get_transform(), local_interp, p_interpolation_fraction);
			} else {
				local_interp = s->get_transform();
			}
		} else {
			local_interp = s->get_transform();
		}

		if (!s->is_set_as_top_level()) {
			if (p_parent_global_xform) {
				if (should_verify) {
					Transform3D test = (*p_parent_global_xform) * local_interp;
					if (s->data.disable_scale) {
						test.basis.orthonormalize();
					}
					if (s->data.global_transform_interpolated != test) {
						debug_verify_failed(s, test);
						DEV_ASSERT(s->data.global_transform_interpolated == test);
					}
				} else {
					s->data.global_transform_interpolated = s->data.fti_is_identity_xform ? (*p_parent_global_xform) : (*p_parent_global_xform) * local_interp;
				}
			} else {
				const Entity3D *parent = s->get_parent_entity_3d();

				if (parent) {
					const Transform3D &parent_glob = parent->data.fti_global_xform_interp_set ? parent->data.global_transform_interpolated : parent->get_global_transform();

					if (should_verify) {
						Transform3D test = parent_glob * local_interp;
						if (s->data.disable_scale) {
							test.basis.orthonormalize();
						}
						if (s->data.global_transform_interpolated != test) {
							debug_verify_failed(s, test);
							DEV_ASSERT(s->data.global_transform_interpolated == test);
						}

					} else {
						s->data.global_transform_interpolated = s->data.fti_is_identity_xform ? parent_glob : parent_glob * local_interp;
					}
				} else {
					if (set_state) {
						s->data.global_transform_interpolated = local_interp;
					}
				}
			}
		} else {
			if (set_state) {
				s->data.global_transform_interpolated = local_interp;
			}
		}

		if (set_state) {
			if (s->data.disable_scale) {
				s->data.global_transform_interpolated.basis.orthonormalize();
			}

			s->fti_update_systems_xform();

			s->data.fti_frame_xform_force_update = false;
		}

		s->data.fti_processed = true;
	} // if active.

	if (set_state) {
		s->_clear_dirty_bits(Entity3D::DIRTY_GLOBAL_INTERPOLATED_TRANSFORM);
	}

	for (int n = 0; n < p_node->get_child_count(); n++) {
		update_dirty_nodes(p_node->get_child(n), p_current_half_frame, p_interpolation_fraction, p_active, s->data.fti_global_xform_interp_set ? &s->data.global_transform_interpolated : &s->data.global_transform, p_depth + 1);
	}
}

void StageFTITests::frame_update(Entity *p_root, uint32_t p_half_frame, float p_interpolation_fraction) {
	StageFTI::Data &data = _fti.data;

	// For testing, use both methods.
	// FIRST the entire tree, writing out state.
	{
		data.use_optimized_traversal_method = false;
		update_dirty_nodes(p_root, p_half_frame, p_interpolation_fraction, false);
	}

	// SECOND the optimized depth lists only,
	// no writing of state, and verifying results.
	{
		data.use_optimized_traversal_method = true;

		_fti._create_depth_lists();

		for (uint32_t d = 0; d < data.scene_tree_depth_limit; d++) {
			const LocalVector<Entity3D *> &list = data.dirty_node_depth_lists[d];

			for (uint32_t n = 0; n < list.size(); n++) {
				Entity3D *s = list[n];

				if (s->data.fti_processed) {
					continue;
				}

				if (Object::cast_to<VisualInstance3D>(s)) {
					if (!s->_is_vi_visible()) {
						continue;
					}
				} else if (!s->is_visible_in_stage()) {
					continue;
				}

				update_dirty_nodes(s, p_half_frame, p_interpolation_fraction, true);
			}
		}

		_fti._clear_depth_lists();
	}
}

StageFTITests::StageFTITests(StageFTI &p_fti) :
		_fti(p_fti) {
	print_line("StageFTI : ORBLIT_SCENE_TREE_FTI_VERIFY defined");
}

#endif // def ORBLIT_SCENE_TREE_FTI_VERIFY

#endif // ndef SPATIAL_DISABLED
