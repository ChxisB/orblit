/**************************************************************************/
/*  library_game.js                                                       */

// The page sets Module.orblitGameFrame and Module.orblitScriptEvent before it
// calls orblit_game_start.
const OrblitGame = {
	orblit_js_game_frame__proxy: 'sync',
	orblit_js_game_frame__sig: 'vdi',
	orblit_js_game_frame: function (p_delta, p_frame) {
		Module['orblitGameFrame'](p_delta, p_frame);
	},

	orblit_js_script_event__proxy: 'sync',
	orblit_js_script_event__sig: 'viipfff',
	orblit_js_script_event__deps: ['$OrblitRuntime'],
	orblit_js_script_event: function (p_kind, p_handle, p_class_name, p_x, p_y, p_z) {
		Module['orblitScriptEvent'](p_kind, p_handle, OrblitRuntime.parseString(p_class_name), p_x, p_y, p_z);
	},
};

mergeInto(LibraryManager.library, OrblitGame);
