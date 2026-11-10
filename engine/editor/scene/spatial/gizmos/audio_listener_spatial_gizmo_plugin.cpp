/**************************************************************************/
/*  audio_listener_spatial_gizmo_plugin.cpp                                    */

#include "audio_listener_spatial_gizmo_plugin.h"

#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "scene/spatial/audio_listener_spatial.h"

AudioListener3DGizmoPlugin::AudioListener3DGizmoPlugin() {
	create_icon_material("audio_listener_3d_icon", EditorNode::get_singleton()->get_editor_theme()->get_icon(SNAME("GizmoAudioListener3D"), EditorStringName(EditorIcons)));
}

bool AudioListener3DGizmoPlugin::has_gizmo(Entity3D *p_spatial) {
	return Object::cast_to<AudioListener3D>(p_spatial) != nullptr;
}

String AudioListener3DGizmoPlugin::get_gizmo_name() const {
	return "AudioListener3D";
}

int AudioListener3DGizmoPlugin::get_priority() const {
	return -1;
}

void AudioListener3DGizmoPlugin::redraw(EditorEntity3DGizmo *p_gizmo) {
	const Ref<Material> icon = get_material("audio_listener_3d_icon", p_gizmo);
	p_gizmo->add_unscaled_billboard(icon, 0.05);
}
