/**************************************************************************/
/*  editor_debugger_server_messageport.cpp                                */

#include "editor_debugger_server_messageport.h"

#include "editor/editor_log.h"
#include "editor/editor_node.h"

extern "C" {
bool orblit_js_editor_debugger_active();
void orblit_js_editor_debugger_cb(void (*p_callback)(int p_id));
}

EditorDebuggerServerMessagePort *EditorDebuggerServerMessagePort::singleton = nullptr;

void EditorDebuggerServerMessagePort::_add_session(int p_session) {
	ERR_FAIL_NULL(singleton);
	singleton->pending.push_back(p_session);
}

void EditorDebuggerServerMessagePort::initialize() {
	EditorDebuggerServer::register_protocol_handler("messageport://", EditorDebuggerServerMessagePort::create);
}

void EditorDebuggerServerMessagePort::poll() {
}

String EditorDebuggerServerMessagePort::get_uri() const {
	return "messageport://";
}

Error EditorDebuggerServerMessagePort::start(const String &p_uri) {
	orblit_js_editor_debugger_cb(&_add_session);
	return OK;
}

void EditorDebuggerServerMessagePort::stop() {
	orblit_js_editor_debugger_cb(nullptr);
	pending.clear();
}

bool EditorDebuggerServerMessagePort::is_active() const {
	return orblit_js_editor_debugger_active();
}

bool EditorDebuggerServerMessagePort::is_connection_available() const {
	return pending.size();
}

Ref<RemoteDebuggerPeer> EditorDebuggerServerMessagePort::take_connection() {
	ERR_FAIL_COND_V(!is_connection_available(), Ref<RemoteDebuggerPeer>());
	Ref<RemoteDebuggerPeerMessagePort> peer = memnew(RemoteDebuggerPeerMessagePort(pending.front()->get()));
	pending.pop_front();
	return peer;
}

EditorDebuggerServerMessagePort::EditorDebuggerServerMessagePort() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = this;
}

EditorDebuggerServerMessagePort::~EditorDebuggerServerMessagePort() {
	stop();
	singleton = nullptr;
}

Ref<EditorDebuggerServer> EditorDebuggerServerMessagePort::create(const String &p_protocol) {
	ERR_FAIL_COND_V(p_protocol != "messageport://", nullptr);
	return memnew(EditorDebuggerServerMessagePort);
}
