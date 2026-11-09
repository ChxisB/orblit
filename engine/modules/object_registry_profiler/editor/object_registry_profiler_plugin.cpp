/**************************************************************************/
/*  object_registry_profiler_plugin.cpp                                   */

#include "object_registry_profiler_plugin.h"

#include "object_registry_profiler_panel.h"

#include "core/object/callable_mp.h"

bool ObjectRegistryProfilerDebuggerPlugin::has_capture(const String &p_capture) const {
	return p_capture == "snapshot";
}

bool ObjectRegistryProfilerDebuggerPlugin::capture(const String &p_message, const Array &p_data, int p_index) {
	ERR_FAIL_NULL_V(debugger_panel, false);
	return debugger_panel->handle_debug_message(p_message, p_data, p_index);
}

void ObjectRegistryProfilerDebuggerPlugin::setup_session(int p_session_id) {
	Ref<EditorDebuggerSession> session = get_session(p_session_id);
	ERR_FAIL_COND(session.is_null());
	debugger_panel = memnew(ObjectRegistryProfilerPanel);
	session->connect("started", callable_mp(debugger_panel, &ObjectRegistryProfilerPanel::set_enabled).bind(true));
	session->connect("stopped", callable_mp(debugger_panel, &ObjectRegistryProfilerPanel::set_enabled).bind(false));
	session->add_session_tab(debugger_panel);
}

ObjectRegistryProfilerPlugin::ObjectRegistryProfilerPlugin() {
	debugger.instantiate();
}

void ObjectRegistryProfilerPlugin::_notification(int p_what) {
	switch (p_what) {
		case Entity::NOTIFICATION_ENTER_STAGE: {
			add_debugger_plugin(debugger);
		} break;
		case Entity::NOTIFICATION_EXIT_STAGE: {
			remove_debugger_plugin(debugger);
		}
	}
}
