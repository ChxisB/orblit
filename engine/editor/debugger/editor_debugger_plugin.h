/**************************************************************************/
/*  editor_debugger_plugin.h                                              */

#pragma once

#include "core/object/ref_counted.h"
#include "core/object/script_language.h"
#include "scene/ui/control.h"

class ScriptEditorDebugger;

class EditorDebuggerSession : public RefCounted {
	ORBLIT_CLASS(EditorDebuggerSession, RefCounted);

private:
	HashSet<Control *> tabs;

	ScriptEditorDebugger *debugger = nullptr;
	ObjectID debugger_id;

	void _breaked(bool p_really_did, bool p_can_debug, const String &p_message, bool p_has_stackdump);
	void _started();
	void _stopped();

protected:
	static void register_members();

public:
	void detach_debugger();

	void add_session_tab(Control *p_tab);
	void remove_session_tab(Control *p_tab);
	void send_message(const String &p_message, const Array &p_args = Array());
	void toggle_profiler(const String &p_profiler, bool p_enable, const Array &p_data = Array());
	bool is_breaked();
	bool is_debuggable();
	bool is_active();

	void set_breakpoint(const String &p_path, int p_line, bool p_enabled);

	EditorDebuggerSession(ScriptEditorDebugger *p_debugger);
	~EditorDebuggerSession();
};

class EditorDebuggerPlugin : public RefCounted {
	ORBLIT_CLASS(EditorDebuggerPlugin, RefCounted);

private:
	List<Ref<EditorDebuggerSession>> sessions;

protected:
	static void register_members();

public:
	void create_session(ScriptEditorDebugger *p_debugger);
	void clear();

	virtual void setup_session(int p_idx);
	virtual bool capture(const String &p_message, const Array &p_data, int p_session);
	virtual bool has_capture(const String &p_capture) const;

	Ref<EditorDebuggerSession> get_session(int p_session_id);
	Array get_sessions();

	ORBLIT_VIRTUAL3R(bool, _capture, const String &, const Array &, int);
	ORBLIT_VIRTUAL1RC(bool, _has_capture, const String &);
	ORBLIT_VIRTUAL1(_setup_session, int);

	virtual void goto_script_line(const Ref<Script> &p_script, int p_line);
	virtual void breakpoints_cleared_in_tree();
	virtual void breakpoint_set_in_tree(const Ref<Script> &p_script, int p_line, bool p_enabled);

	ORBLIT_VIRTUAL2(_goto_script_line, const Ref<Script> &, int);
	ORBLIT_VIRTUAL0(_breakpoints_cleared_in_tree);
	ORBLIT_VIRTUAL3(_breakpoint_set_in_tree, const Ref<Script> &, int, bool);

	EditorDebuggerPlugin();
	~EditorDebuggerPlugin();
};
