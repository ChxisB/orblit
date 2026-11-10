/**************************************************************************/
/*  library_websocket.js                                                  */

const OrblitWebSocket = {
	// Our socket implementation that forwards events to C++.
	$OrblitWebSocket__deps: ['$IDHandler', '$OrblitRuntime'],
	$OrblitWebSocket: {
		// Connection opened, report selected protocol
		_onopen: function (p_id, callback, event) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return; // Orblit object is gone.
			}
			const c_str = OrblitRuntime.allocString(ref.protocol);
			callback(c_str);
			OrblitRuntime.free(c_str);
		},

		// Message received, report content and type (UTF8 vs binary)
		_onmessage: function (p_id, callback, event) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return; // Orblit object is gone.
			}
			let buffer;
			let is_string = 0;
			if (event.data instanceof ArrayBuffer) {
				buffer = new Uint8Array(event.data);
			} else if (event.data instanceof Blob) {
				OrblitRuntime.error('Blob type not supported');
				return;
			} else if (typeof event.data === 'string') {
				is_string = 1;
				buffer = new TextEncoder('utf-8').encode(event.data);
			} else {
				OrblitRuntime.error('Unknown message type');
				return;
			}
			const len = buffer.length * buffer.BYTES_PER_ELEMENT;
			const out = OrblitRuntime.malloc(len);
			HEAPU8.set(buffer, out);
			callback(out, len, is_string);
			OrblitRuntime.free(out);
		},

		// An error happened, 'onclose' will be called after this.
		_onerror: function (p_id, callback, event) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return; // Orblit object is gone.
			}
			callback();
		},

		// Connection is closed, this is always fired. Report close code, reason, and clean status.
		_onclose: function (p_id, callback, event) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return; // Orblit object is gone.
			}
			const c_str = OrblitRuntime.allocString(event.reason);
			callback(event.code, c_str, event.wasClean ? 1 : 0);
			OrblitRuntime.free(c_str);
		},

		// Send a message
		send: function (p_id, p_data) {
			const ref = IDHandler.get(p_id);
			if (!ref || ref.readyState !== ref.OPEN) {
				return 1; // Orblit object is gone or socket is not in a ready state.
			}
			ref.send(p_data);
			return 0;
		},

		// Get current bufferedAmount
		bufferedAmount: function (p_id) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return 0; // Orblit object is gone.
			}
			return ref.bufferedAmount;
		},

		create: function (socket, p_on_open, p_on_message, p_on_error, p_on_close) {
			const id = IDHandler.add(socket);
			socket.onopen = OrblitWebSocket._onopen.bind(null, id, p_on_open);
			socket.onmessage = OrblitWebSocket._onmessage.bind(null, id, p_on_message);
			socket.onerror = OrblitWebSocket._onerror.bind(null, id, p_on_error);
			socket.onclose = OrblitWebSocket._onclose.bind(null, id, p_on_close);
			return id;
		},

		// Closes the JavaScript WebSocket (if not already closing) associated to a given C++ object.
		close: function (p_id, p_code, p_reason) {
			const ref = IDHandler.get(p_id);
			if (ref && ref.readyState < ref.CLOSING) {
				const code = p_code;
				const reason = p_reason;
				ref.close(code, reason);
			}
		},

		// Deletes the reference to a C++ object (closing any connected socket if necessary).
		destroy: function (p_id) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return;
			}
			OrblitWebSocket.close(p_id, 3001, 'destroyed');
			IDHandler.remove(p_id);
			ref.onopen = null;
			ref.onmessage = null;
			ref.onerror = null;
			ref.onclose = null;
		},
	},

	orblit_js_websocket_create__proxy: 'sync',
	orblit_js_websocket_create__sig: 'iiiiiiii',
	orblit_js_websocket_create: function (p_ref, p_url, p_proto, p_on_open, p_on_message, p_on_error, p_on_close) {
		const on_open = OrblitRuntime.get_func(p_on_open).bind(null, p_ref);
		const on_message = OrblitRuntime.get_func(p_on_message).bind(null, p_ref);
		const on_error = OrblitRuntime.get_func(p_on_error).bind(null, p_ref);
		const on_close = OrblitRuntime.get_func(p_on_close).bind(null, p_ref);
		const url = OrblitRuntime.parseString(p_url);
		const protos = OrblitRuntime.parseString(p_proto);
		let socket = null;
		try {
			if (protos) {
				socket = new WebSocket(url, protos.split(','));
			} else {
				socket = new WebSocket(url);
			}
		} catch (e) {
			return 0;
		}
		socket.binaryType = 'arraybuffer';
		return OrblitWebSocket.create(socket, on_open, on_message, on_error, on_close);
	},

	orblit_js_websocket_send__proxy: 'sync',
	orblit_js_websocket_send__sig: 'iiiii',
	orblit_js_websocket_send: function (p_id, p_buf, p_buf_len, p_raw) {
		let out = OrblitRuntime.heapSlice(HEAPU8, p_buf, p_buf_len);
		if (!p_raw) {
			out = new TextDecoder('utf-8').decode(out);
		}
		return OrblitWebSocket.send(p_id, out);
	},

	orblit_js_websocket_buffered_amount__proxy: 'sync',
	orblit_js_websocket_buffered_amount__sig: 'ii',
	orblit_js_websocket_buffered_amount: function (p_id) {
		return OrblitWebSocket.bufferedAmount(p_id);
	},

	orblit_js_websocket_close__proxy: 'sync',
	orblit_js_websocket_close__sig: 'viii',
	orblit_js_websocket_close: function (p_id, p_code, p_reason) {
		const code = p_code;
		const reason = OrblitRuntime.parseString(p_reason);
		OrblitWebSocket.close(p_id, code, reason);
	},

	orblit_js_websocket_destroy__proxy: 'sync',
	orblit_js_websocket_destroy__sig: 'vi',
	orblit_js_websocket_destroy: function (p_id) {
		OrblitWebSocket.destroy(p_id);
	},
};

autoAddDeps(OrblitWebSocket, '$OrblitWebSocket');
mergeInto(LibraryManager.library, OrblitWebSocket);
