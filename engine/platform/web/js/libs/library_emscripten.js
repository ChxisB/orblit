/**************************************************************************/
/*  library_emscripten.js                                                 */

const OrblitEmscripten = {
	$OrblitEmscripten__deps: ['$OrblitRuntime'],
	$OrblitEmscripten: {},

	orblit_js_emscripten_get_version__proxy: 'sync',
	orblit_js_emscripten_get_version__sig: 'p',
	orblit_js_emscripten_get_version: function () {
		// WARNING: The caller needs to free the string pointer.
		const emscriptenVersionPtr = OrblitRuntime.allocString('{{{ EMSCRIPTEN_VERSION }}}');
		return emscriptenVersionPtr;
	},
};
autoAddDeps(OrblitEmscripten, '$OrblitEmscripten');
addToLibrary(OrblitEmscripten);
