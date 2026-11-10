/**************************************************************************/
/*  library_debugger.js                                                   */

const OrblitDebugger = {
	$OrblitDebugger__deps: ['$OrblitRuntime', '$IDHandler', '$OrblitConfig'],
	$OrblitDebugger: {
		debug_id: -1,
		bind: function (id, ref, cb) {
			const o = IDHandler.get(id);
			if (!o) {
				return -1;
			}
			o.port.onmessage = function (event) {
				const obj = IDHandler.get(id);
				if (!obj) {
					return;
				}
				if (typeof (event.data) === 'string') {
					if (event.data === 'stop') {
						obj.active = false;
					} else {
						OrblitRuntime.error('Invalid message', event.data);
					}
					return;
				} else if (!(event.data instanceof Uint8Array)) {
					OrblitRuntime.error('Invalid message', event.data);
					return;
				}
				const buffer = event.data;
				if (buffer.length == 0) {
					return;
				}
				const len = buffer.length * buffer.BYTES_PER_ELEMENT;
				const out = OrblitRuntime.malloc(len);
				HEAPU8.set(buffer, out);
				cb(ref, out, len);
				OrblitRuntime.free(out);
			};
			return id;
		},
		post: function (id, msg) {
			const obj = IDHandler.get(id);
			if (!obj) {
				return;
			}
			obj.port.postMessage(msg);
		},
		unbind: function (id) {
			const obj = IDHandler.get(id);
			if (!obj) {
				return;
			}
			obj.port.postMessage('stop');
			obj.port.close();
			obj.port.onmessage = null;
		},
	},

	orblit_js_debugger_post__proxy: 'sync',
	orblit_js_debugger_post__sig: 'vipi',
	orblit_js_debugger_post: function (p_id, p_ptr, p_size) {
		if (!p_size) {
			return;
		}
		const msg = OrblitRuntime.heapSlice(HEAPU8, p_ptr, p_size);
		OrblitDebugger.post(p_id, msg);
	},

	orblit_js_debugger_bind__proxy: 'sync',
	orblit_js_debugger_bind__sig: 'iipp',
	orblit_js_debugger_bind: function (p_id, p_cb, p_ref) {
		let id = p_id;
		if (id == -1) {
			if (!OrblitConfig.debug_port) {
				return -1;
			}
			if (OrblitDebugger.debug_id !== -1) {
				OrblitRuntime.error('Already bound');
				return -1;
			}
			id = IDHandler.add({
				port: OrblitConfig.debug_port,
				active: true,
			});
			OrblitDebugger.debug_id = id;
		}
		const cb = OrblitRuntime.get_func(p_cb);
		return OrblitDebugger.bind(id, p_ref, cb);
	},

	orblit_js_debugger_unbind__proxy: 'sync',
	orblit_js_debugger_unbind__sig: 'vi',
	orblit_js_debugger_unbind: function (p_id) {
		OrblitDebugger.unbind(p_id);
		IDHandler.remove(p_id);
		OrblitDebugger.debug_id = -1;
	},

	orblit_js_debugger_active__proxy: 'sync',
	orblit_js_debugger_active__sig: 'ii',
	orblit_js_debugger_active: function (p_id) {
		const obj = IDHandler.get(p_id);
		if (!obj) {
			return 0;
		}
		return obj.active ? 1 : 0;
	},

};

autoAddDeps(OrblitDebugger, '$OrblitDebugger');
mergeInto(LibraryManager.library, OrblitDebugger);
