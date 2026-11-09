/**************************************************************************/
/*  openxr_render_model.cpp                                               */

#include "openxr_render_model.h"

#ifdef MODULE_GLTF_ENABLED

#include "../extensions/openxr_render_model_extension.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"

void OpenXRRenderModel::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_top_level_path"), &OpenXRRenderModel::get_top_level_path);

	ClassRegistry::bind_method(D_METHOD("get_render_model"), &OpenXRRenderModel::get_render_model);
	ClassRegistry::bind_method(D_METHOD("set_render_model", "render_model"), &OpenXRRenderModel::set_render_model);
	ADD_PROPERTY(PropertyInfo(Variant::RID, "render_model"), "set_render_model", "get_render_model");

	ADD_SIGNAL(MethodInfo("render_model_top_level_path_changed"));
}

void OpenXRRenderModel::_load_render_model_scene() {
	OpenXRRenderModelExtension *render_model_extension = OpenXRRenderModelExtension::get_singleton();
	ERR_FAIL_NULL(render_model_extension);
	ERR_FAIL_COND(render_model.is_null());

	scene = render_model_extension->render_model_new_scene_instance(render_model);
	if (scene) {
		// Get and cache our animatable nodes.
		animatable_nodes.clear();
		uint32_t count = render_model_extension->render_model_get_animatable_node_count(render_model);
		for (uint32_t i = 0; i < count; i++) {
			String node_name = render_model_extension->render_model_get_animatable_node_name(render_model, i);
			if (!node_name.is_empty()) {
				Entity3D *child = Object::cast_to<Entity3D>(scene->find_child(node_name));
				if (child) {
					animatable_nodes[node_name] = child;
				}
			}
		}

		// Now add to scene.
		add_child(scene);
	}
}

void OpenXRRenderModel::_on_render_model_top_level_path_changed(RID p_render_model) {
	if (render_model == p_render_model) {
		emit_signal("render_model_top_level_path_changed");
	}
}

void OpenXRRenderModel::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			OpenXRRenderModelExtension *render_model_extension = OpenXRRenderModelExtension::get_singleton();
			ERR_FAIL_NULL(render_model_extension);
			if (render_model.is_valid()) {
				_load_render_model_scene();
			}

			set_internal_frame_enabled(true);
			render_model_extension->connect("render_model_top_level_path_changed", callable_mp(this, &OpenXRRenderModel::_on_render_model_top_level_path_changed));
		} break;
		case NOTIFICATION_EXIT_STAGE: {
			set_internal_frame_enabled(false);

			if (scene) {
				animatable_nodes.clear();

				remove_child(scene);
				scene->queue_destroy();
				scene = nullptr;
			}

			OpenXRRenderModelExtension *render_model_extension = OpenXRRenderModelExtension::get_singleton();
			if (render_model_extension) {
				render_model_extension->disconnect("render_model_top_level_path_changed", callable_mp(this, &OpenXRRenderModel::_on_render_model_top_level_path_changed));
			}
		} break;
		case NOTIFICATION_INTERNAL_FRAME: {
			if (render_model.is_valid()) {
				OpenXRRenderModelExtension *render_model_extension = OpenXRRenderModelExtension::get_singleton();
				ERR_FAIL_NULL(render_model_extension);

				if (render_model_extension->render_model_get_confidence(render_model) != XRPose::TrackingConfidence::XR_TRACKING_CONFIDENCE_NONE) {
					set_transform(render_model_extension->render_model_get_root_transform(render_model));

					if (scene) {
						uint32_t count = render_model_extension->render_model_get_animatable_node_count(render_model);
						for (uint32_t i = 0; i < count; i++) {
							String node_name = render_model_extension->render_model_get_animatable_node_name(render_model, i);
							if (!node_name.is_empty() && animatable_nodes.has(node_name)) {
								Entity3D *child = animatable_nodes[node_name];
								child->set_visible(render_model_extension->render_model_is_animatable_node_visible(render_model, i));
								child->set_transform(render_model_extension->render_model_get_animatable_node_transform(render_model, i));
							}
						}
					}
				}
			}
		} break;
	}
}

String OpenXRRenderModel::get_top_level_path() const {
	String ret;

	OpenXRRenderModelExtension *render_model_extension = OpenXRRenderModelExtension::get_singleton();
	if (render_model.is_valid() && render_model_extension) {
		ret = render_model_extension->render_model_get_top_level_path_as_string(render_model);
	}

	return ret;
}

PackedStringArray OpenXRRenderModel::get_configuration_warnings() const {
	PackedStringArray warnings;

	Entity *parent = get_parent();
	if (!parent->is_class("XROrigin3D") && !parent->is_class("OpenXRRenderModelManager")) {
		warnings.push_back("This node must be a child of either a XROrigin3D or OpenXRRenderModelManager node!");
	}

	if (!GLOBAL_GET("xr/openxr/extensions/render_model")) {
		warnings.push_back("The render model extension is not enabled in project settings!");
	}

	return warnings;
}

RID OpenXRRenderModel::get_render_model() const {
	return render_model;
}

void OpenXRRenderModel::set_render_model(RID p_render_model) {
	render_model = p_render_model;
	if (is_inside_stage() && render_model.is_valid()) {
		_load_render_model_scene();
	}
}
#endif // MODULE_GLTF_ENABLED
