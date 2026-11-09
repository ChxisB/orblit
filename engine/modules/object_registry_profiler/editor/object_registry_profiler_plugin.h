/**************************************************************************/
/*  object_registry_profiler_plugin.h                                     */

#pragma once

#include "editor/debugger/editor_debugger_plugin.h"
#include "editor/plugins/editor_plugin.h"

class ObjectRegistryProfilerPanel;
class ObjectRegistryProfilerDebuggerPlugin;

// First, ObjectRegistryProfilerPlugin is loaded. Then it loads ObjectRegistryProfilerDebuggerPlugin.
class ObjectRegistryProfilerPlugin : public EditorPlugin {
	ORBLIT_CLASS(ObjectRegistryProfilerPlugin, EditorPlugin);

protected:
	Ref<ObjectRegistryProfilerDebuggerPlugin> debugger;
	void _notification(int p_what);

public:
	ObjectRegistryProfilerPlugin();
};

class ObjectRegistryProfilerDebuggerPlugin : public EditorDebuggerPlugin {
	ORBLIT_CLASS(ObjectRegistryProfilerDebuggerPlugin, EditorDebuggerPlugin);

protected:
	ObjectRegistryProfilerPanel *debugger_panel = nullptr;

	void _request_object_snapshot(int p_request_id);

public:
	ObjectRegistryProfilerDebuggerPlugin() {}

	virtual bool has_capture(const String &p_capture) const override;
	virtual bool capture(const String &p_message, const Array &p_data, int p_index) override;
	virtual void setup_session(int p_session_id) override;
};
