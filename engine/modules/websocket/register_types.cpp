/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "remote_debugger_peer_websocket.h"
#include "websocket_multiplayer_peer.h"
#include "websocket_peer.h"

#ifdef WEB_ENABLED
#include "emws_peer.h"
#else
#include "wsl_peer.h"
#endif

#ifdef TOOLS_ENABLED
#include "editor/editor_debugger_server_websocket.h"
#endif

#include "core/debugger/engine_debugger.h"
#include "core/error/error_macros.h"
#include "core/object/class_registry.h"

#ifdef TOOLS_ENABLED
#include "editor/debugger/editor_debugger_server.h"
#include "editor/editor_node.h"
#endif

#ifdef TOOLS_ENABLED
static void _editor_init_callback() {
	EditorDebuggerServer::register_protocol_handler("ws://", EditorDebuggerServerWebSocket::create);
}
#endif

void initialize_websocket_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_CORE) {
#ifdef WEB_ENABLED
		EMWSPeer::initialize();
#else
		WSLPeer::initialize();
#endif

		ORBLIT_REGISTER_CLASS(WebSocketMultiplayerPeer);
		ClassRegistry::register_custom_instance_class<WebSocketPeer>();

		EngineDebugger::register_uri_handler("ws://", RemoteDebuggerPeerWebSocket::create);
		EngineDebugger::register_uri_handler("wss://", RemoteDebuggerPeerWebSocket::create);
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorNode::add_init_callback(&_editor_init_callback);
	}
#endif
}

void uninitialize_websocket_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_CORE) {
		return;
	}
}
