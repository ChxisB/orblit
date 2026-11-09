/**************************************************************************/
/*  camera_spatial_editor_plugin.cpp                                           */

#include "camera_spatial_editor_plugin.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "editor/editor_node.h"
#include "editor/scene/spatial/entity_spatial_editor_plugin.h"
#include "editor/themes/editor_scale.h"
#include "scene/stage/viewport.h"
#include "scene/ui/aspect_ratio_container.h"
#include "scene/ui/foldable_container.h"
#include "scene/ui/subviewport_container.h"
#include "systems/rendering/rendering_system.h"

void Camera3DEditor::_node_removed(Entity *p_node) {
	if (p_node == node) {
		node = nullptr;
		Entity3DEditor::get_singleton()->set_custom_camera(nullptr);
		hide();
	}
}

void Camera3DEditor::_pressed() {
	Entity *sn = (node && preview->is_pressed()) ? node : nullptr;
	Entity3DEditor::get_singleton()->set_custom_camera(sn);
}

void Camera3DEditor::edit(Entity *p_camera) {
	node = p_camera;

	if (!node) {
		preview->set_pressed(false);
		Entity3DEditor::get_singleton()->set_custom_camera(nullptr);
	} else {
		if (preview->is_pressed()) {
			Entity3DEditor::get_singleton()->set_custom_camera(p_camera);
		} else {
			Entity3DEditor::get_singleton()->set_custom_camera(nullptr);
		}
	}
}

Camera3DEditor::Camera3DEditor() {
	preview = memnew(Button);
	add_child(preview);

	preview->set_text(TTR("Preview"));
	preview->set_toggle_mode(true);
	preview->set_anchor(SIDE_LEFT, Control::ANCHOR_END);
	preview->set_anchor(SIDE_RIGHT, Control::ANCHOR_END);
	preview->set_offset(SIDE_LEFT, -60);
	preview->set_offset(SIDE_RIGHT, 0);
	preview->set_offset(SIDE_TOP, 0);
	preview->set_offset(SIDE_BOTTOM, 10);
	preview->connect(SceneStringName(pressed), callable_mp(this, &Camera3DEditor::_pressed));
}

bool Camera3DPreview::camera_preview_folded = false;

void Camera3DPreview::_update_sub_viewport_size() {
	const Size2i camera_size = Entity3DEditor::get_camera_viewport_size(camera);
	centering_container->set_ratio(camera_size.aspect());
}

void Camera3DPreview::_toggle_folding(bool p_folded) {
	camera_preview_folded = p_folded;
}

Camera3DPreview::Camera3DPreview(Camera3D *p_camera) {
	camera = p_camera;

	FoldableContainer *folder = memnew(FoldableContainer);
	folder->set_title(TTRC("Camera Preview"));
	folder->set_title_text_overrun_behavior(TextSystem::OVERRUN_TRIM_ELLIPSIS);
	folder->set_folded(camera_preview_folded);
	folder->connect("folding_changed", callable_mp(this, &Camera3DPreview::_toggle_folding));
	add_child(folder);

	centering_container = memnew(AspectRatioContainer);
	centering_container->set_custom_minimum_size(Size2(0.0, 256.0) * EDSCALE);
	folder->add_child(centering_container);

	SubViewportContainer *sub_viewport_container = memnew(SubViewportContainer);
	sub_viewport_container->set_stretch(true);
	sub_viewport_container->set_texture_filter(TEXTURE_FILTER_NEAREST_WITH_MIPMAPS);
	centering_container->add_child(sub_viewport_container);

	sub_viewport = memnew(SubViewport);
	sub_viewport_container->add_child(sub_viewport);

	RenderingSystem::get_singleton()->viewport_attach_camera(sub_viewport->get_viewport_rid(), camera->get_camera());

	EditorNode::get_singleton()->register_hdr_viewport(sub_viewport);

	ProjectSettings::get_singleton()->connect("settings_changed", callable_mp(this, &Camera3DPreview::_project_settings_changed));
	_update_sub_viewport_size();
}

void Camera3DPreview::_project_settings_changed() {
	if (ProjectSettings::get_singleton()->check_changed_settings_in_group("display/window/size")) {
		_update_sub_viewport_size();
	}
}

bool EditorInspectorPluginCamera3DPreview::can_handle(Object *p_object) {
	return Object::cast_to<Camera3D>(p_object) != nullptr;
}

void EditorInspectorPluginCamera3DPreview::parse_begin(Object *p_object) {
	Camera3D *camera = Object::cast_to<Camera3D>(p_object);
	Camera3DPreview *preview = memnew(Camera3DPreview(camera));
	add_custom_control(preview);
}

void Camera3DEditorPlugin::edit(Object *p_object) {
	Entity3DEditor::get_singleton()->set_can_preview(Object::cast_to<Camera3D>(p_object));
}

bool Camera3DEditorPlugin::handles(Object *p_object) const {
	return p_object->is_class("Camera3D");
}

void Camera3DEditorPlugin::make_visible(bool p_visible) {
	if (!p_visible) {
		Entity3DEditor::get_singleton()->set_can_preview(nullptr);
	}
}

Camera3DEditorPlugin::Camera3DEditorPlugin() {
	Ref<EditorInspectorPluginCamera3DPreview> plugin;
	plugin.instantiate();
	add_inspector_plugin(plugin);
}
