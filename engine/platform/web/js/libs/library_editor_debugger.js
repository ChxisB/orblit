/**************************************************************************/
/*  library_editor_debugger.js                                            */

const OrblitEditorDebugger = {
	$OrblitEditorDebugger__postset: [
		'Module["add_debugger_session"] = OrblitEditorDebugger.addSession;',
	].join(''),
	$OrblitEditorDebugger__deps: ['$OrblitDebugger', '$OrblitRuntime', '$IDHandler'],
	$OrblitEditorDebugger: {
		session_callback: null,
		addSession: function (port) {
			if (!OrblitEditorDebugger.session_callback) {
				OrblitRuntime.error('Editor debugger not bound.');
				return;
			}
			const id = IDHandler.add({
				port: port,
				active: true,
			});
			OrblitEditorDebugger.session_callback(id);
		},
	},

	orblit_js_editor_debugger_cb__proxy: 'sync',
	orblit_js_editor_debugger_cb__sig: 'vp',
	orblit_js_editor_debugger_cb: function (p_cb) {
		OrblitEditorDebugger.session_callback = p_cb ? OrblitRuntime.get_func(p_cb) : null;
	},

	orblit_js_editor_debugger_active__proxy: 'sync',
	orblit_js_editor_debugger_active__sig: 'i',
	orblit_js_editor_debugger_active: function () {
		return OrblitEditorDebugger.session_callback ? 1 : 0;
	},
};

autoAddDeps(OrblitEditorDebugger, '$OrblitEditorDebugger');
mergeInto(LibraryManager.library, OrblitEditorDebugger);
