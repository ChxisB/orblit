/**************************************************************************/
/*  editor_script_plugin.cpp                                              */

#include "editor_script_plugin.h"

#include "core/io/resource_loader.h"
#include "core/object/callable_mp.h"
#include "editor/editor_interface.h"
#include "editor/script/editor_script.h"
#include "editor/settings/editor_command_palette.h"

Ref<EditorScript> create_instance(const StringName &p_name) {
	Ref<EditorScript> es;
	es.instantiate();
	Ref<Script> scr = ResourceLoader::load(ScriptSystem::get_global_class_path(p_name), "Script", ResourceFormatLoader::CACHE_MODE_REUSE);
	if (scr.is_valid()) {
		es->set_script(scr);
	}
	return es;
}

void EditorScriptPlugin::run_command(const StringName &p_name) {
	create_instance(p_name)->run();
}

void EditorScriptPlugin::command_palette_about_to_popup() {
	for (const StringName &command : commands) {
		EditorInterface::get_singleton()->get_command_palette()->remove_command("editor_scripts/" + command);
	}
	commands.clear();
	ScriptSystem::get_indirect_inheriters_list(SNAME("EditorScript"), &commands);
	for (const StringName &command : commands) {
		EditorInterface::get_singleton()->get_command_palette()->add_command(String(command).capitalize(), "editor_scripts/" + command, callable_mp(this, &EditorScriptPlugin::run_command).bind(command));
	}
}

EditorScriptPlugin::EditorScriptPlugin() {
	EditorInterface::get_singleton()->get_command_palette()->connect("about_to_popup", callable_mp(this, &EditorScriptPlugin::command_palette_about_to_popup));
}
