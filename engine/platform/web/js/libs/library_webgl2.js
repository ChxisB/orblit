/**************************************************************************/
/*  library_webgl2.js                                                     */

const OrblitWebGL2 = {
	$OrblitWebGL2__deps: ['$GL', '$OrblitRuntime'],
	$OrblitWebGL2: {},

	// This is implemented as "glGetBufferSubData" in new emscripten versions.
	// Since we have to support older (pre 2.0.17) emscripten versions, we add this wrapper function instead.
	orblit_webgl2_glGetBufferSubData__proxy: 'sync',
	orblit_webgl2_glGetBufferSubData__sig: 'vippp',
	orblit_webgl2_glGetBufferSubData__deps: ['$GL', 'emscripten_webgl_get_current_context'],
	orblit_webgl2_glGetBufferSubData: function (target, offset, size, data) {
		const gl_context_handle = _emscripten_webgl_get_current_context();
		const gl = GL.getContext(gl_context_handle);
		if (gl) {
			gl.GLctx['getBufferSubData'](target, offset, HEAPU8, data, size);
		}
	},

	orblit_webgl2_glFramebufferTextureMultiviewOVR__deps: ['emscripten_webgl_get_current_context'],
	orblit_webgl2_glFramebufferTextureMultiviewOVR__proxy: 'sync',
	orblit_webgl2_glFramebufferTextureMultiviewOVR__sig: 'viiiiii',
	orblit_webgl2_glFramebufferTextureMultiviewOVR: function (target, attachment, texture, level, base_view_index, num_views) {
		const context = GL.currentContext;
		if (typeof context.multiviewExt === 'undefined') {
			const /** OVR_multiview2 */ ext = context.GLctx.getExtension('OVR_multiview2');
			if (!ext) {
				OrblitRuntime.error('Trying to call glFramebufferTextureMultiviewOVR() without the OVR_multiview2 extension');
				return;
			}
			context.multiviewExt = ext;
		}
		const /** OVR_multiview2 */ ext = context.multiviewExt;
		ext.framebufferTextureMultiviewOVR(target, attachment, GL.textures[texture], level, base_view_index, num_views);
	},

	orblit_webgl2_glFramebufferTextureMultisampleMultiviewOVR__deps: ['emscripten_webgl_get_current_context'],
	orblit_webgl2_glFramebufferTextureMultisampleMultiviewOVR__proxy: 'sync',
	orblit_webgl2_glFramebufferTextureMultisampleMultiviewOVR__sig: 'viiiiiii',
	orblit_webgl2_glFramebufferTextureMultisampleMultiviewOVR: function (target, attachment, texture, level, samples, base_view_index, num_views) {
		const context = GL.currentContext;
		if (typeof context.oculusMultiviewExt === 'undefined') {
			const /** OCULUS_multiview */ ext = context.GLctx.getExtension('OCULUS_multiview');
			if (!ext) {
				OrblitRuntime.error('Trying to call glFramebufferTextureMultisampleMultiviewOVR() without the OCULUS_multiview extension');
				return;
			}
			context.oculusMultiviewExt = ext;
		}
		const /** OCULUS_multiview */ ext = context.oculusMultiviewExt;
		ext.framebufferTextureMultisampleMultiviewOVR(target, attachment, GL.textures[texture], level, samples, base_view_index, num_views);
	},
};

autoAddDeps(OrblitWebGL2, '$OrblitWebGL2');
mergeInto(LibraryManager.library, OrblitWebGL2);
