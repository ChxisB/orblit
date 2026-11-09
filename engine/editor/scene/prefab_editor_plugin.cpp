/**************************************************************************/
/*  prefab_editor_plugin.cpp                                              */

#include "prefab_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/editor_node.h"
#include "scene/resources/prefab.h"

void PrefabEditor::_on_open_scene_pressed() {
	// Using deferred call because changing scene updates the Inspector and thus destroys this plugin.
	callable_mp(EditorNode::get_singleton(), &EditorNode::open_scene).call_deferred(prefab->get_path(), false, false, false);
}

PrefabEditor::PrefabEditor(Ref<Prefab> &p_prefab) {
	prefab = p_prefab;

	EditorInspectorActionButton *open_scene_button = memnew(EditorInspectorActionButton(TTRC("Open Scene"), SNAME("Prefab")));
	open_scene_button->connect(SceneStringName(pressed), callable_mp(this, &PrefabEditor::_on_open_scene_pressed));
	open_scene_button->set_disabled(!prefab->get_path().get_file().is_valid_filename());
	add_child(open_scene_button);

	add_child(memnew(Control)); // Add padding before the regular properties.
}

///////////////////////

bool EditorInspectorPluginPrefab::can_handle(Object *p_object) {
	return Object::cast_to<Prefab>(p_object) != nullptr;
}

void EditorInspectorPluginPrefab::parse_begin(Object *p_object) {
	Ref<Prefab> prefab(p_object);
	PrefabEditor *editor = memnew(PrefabEditor(prefab));
	add_custom_control(editor);
}

///////////////////////

PrefabEditorPlugin::PrefabEditorPlugin() {
	Ref<EditorInspectorPluginPrefab> plugin;
	plugin.instantiate();
	add_inspector_plugin(plugin);
}
