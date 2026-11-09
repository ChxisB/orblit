/**************************************************************************/
/*  library_webrtc.js                                                     */

const OrblitRTCDataChannel = {
	// Our socket implementation that forwards events to C++.
	$OrblitRTCDataChannel__deps: ['$IDHandler', '$OrblitRuntime'],
	$OrblitRTCDataChannel: {
		connect: function (p_id, p_on_open, p_on_message, p_on_error, p_on_close) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return;
			}

			ref.binaryType = 'arraybuffer';
			ref.onopen = function (event) {
				p_on_open();
			};
			ref.onclose = function (event) {
				p_on_close();
			};
			ref.onerror = function (event) {
				p_on_error();
			};
			ref.onmessage = function (event) {
				let buffer;
				let is_string = 0;
				if (event.data instanceof ArrayBuffer) {
					buffer = new Uint8Array(event.data);
				} else if (event.data instanceof Blob) {
					OrblitRuntime.error('Blob type not supported');
					return;
				} else if (typeof event.data === 'string') {
					is_string = 1;
					const enc = new TextEncoder('utf-8');
					buffer = new Uint8Array(enc.encode(event.data));
				} else {
					OrblitRuntime.error('Unknown message type');
					return;
				}
				const len = buffer.length * buffer.BYTES_PER_ELEMENT;
				const out = OrblitRuntime.malloc(len);
				HEAPU8.set(buffer, out);
				p_on_message(out, len, is_string);
				OrblitRuntime.free(out);
			};
		},

		close: function (p_id) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return;
			}
			ref.onopen = null;
			ref.onmessage = null;
			ref.onerror = null;
			ref.onclose = null;
			ref.close();
		},

		get_prop: function (p_id, p_prop, p_def) {
			const ref = IDHandler.get(p_id);
			return (ref && ref[p_prop] !== undefined) ? ref[p_prop] : p_def;
		},
	},

	orblit_js_rtc_datachannel_ready_state_get__proxy: 'sync',
	orblit_js_rtc_datachannel_ready_state_get__sig: 'ii',
	orblit_js_rtc_datachannel_ready_state_get: function (p_id) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return 3; // CLOSED
		}

		switch (ref.readyState) {
		case 'connecting':
			return 0;
		case 'open':
			return 1;
		case 'closing':
			return 2;
		case 'closed':
		default:
			return 3;
		}
	},

	orblit_js_rtc_datachannel_send__proxy: 'sync',
	orblit_js_rtc_datachannel_send__sig: 'iiiii',
	orblit_js_rtc_datachannel_send: function (p_id, p_buffer, p_length, p_raw) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return 1;
		}

		const bytes_array = new Uint8Array(p_length);
		for (let i = 0; i < p_length; i++) {
			bytes_array[i] = OrblitRuntime.getHeapValue(p_buffer + i, 'i8');
		}

		if (p_raw) {
			ref.send(bytes_array.buffer);
		} else {
			const string = new TextDecoder('utf-8').decode(bytes_array);
			ref.send(string);
		}
		return 0;
	},

	orblit_js_rtc_datachannel_is_ordered__proxy: 'sync',
	orblit_js_rtc_datachannel_is_ordered__sig: 'ii',
	orblit_js_rtc_datachannel_is_ordered: function (p_id) {
		return OrblitRTCDataChannel.get_prop(p_id, 'ordered', true);
	},

	orblit_js_rtc_datachannel_id_get__proxy: 'sync',
	orblit_js_rtc_datachannel_id_get__sig: 'ii',
	orblit_js_rtc_datachannel_id_get: function (p_id) {
		return OrblitRTCDataChannel.get_prop(p_id, 'id', 65535);
	},

	orblit_js_rtc_datachannel_max_packet_lifetime_get__proxy: 'sync',
	orblit_js_rtc_datachannel_max_packet_lifetime_get__sig: 'ii',
	orblit_js_rtc_datachannel_max_packet_lifetime_get: function (p_id) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return 65535;
		}
		if (ref['maxPacketLifeTime'] !== undefined) {
			return ref['maxPacketLifeTime'];
		} else if (ref['maxRetransmitTime'] !== undefined) {
			// Guess someone didn't appreciate the standardization process.
			return ref['maxRetransmitTime'];
		}
		return 65535;
	},

	orblit_js_rtc_datachannel_max_retransmits_get__proxy: 'sync',
	orblit_js_rtc_datachannel_max_retransmits_get__sig: 'ii',
	orblit_js_rtc_datachannel_max_retransmits_get: function (p_id) {
		return OrblitRTCDataChannel.get_prop(p_id, 'maxRetransmits', 65535);
	},

	orblit_js_rtc_datachannel_is_negotiated__proxy: 'sync',
	orblit_js_rtc_datachannel_is_negotiated__sig: 'ii',
	orblit_js_rtc_datachannel_is_negotiated: function (p_id) {
		return OrblitRTCDataChannel.get_prop(p_id, 'negotiated', 65535);
	},

	orblit_js_rtc_datachannel_get_buffered_amount__proxy: 'sync',
	orblit_js_rtc_datachannel_get_buffered_amount__sig: 'ii',
	orblit_js_rtc_datachannel_get_buffered_amount: function (p_id) {
		return OrblitRTCDataChannel.get_prop(p_id, 'bufferedAmount', 0);
	},

	orblit_js_rtc_datachannel_label_get__proxy: 'sync',
	orblit_js_rtc_datachannel_label_get__sig: 'ii',
	orblit_js_rtc_datachannel_label_get: function (p_id) {
		const ref = IDHandler.get(p_id);
		if (!ref || !ref.label) {
			return 0;
		}
		return OrblitRuntime.allocString(ref.label);
	},

	orblit_js_rtc_datachannel_protocol_get__sig: 'ii',
	orblit_js_rtc_datachannel_protocol_get: function (p_id) {
		const ref = IDHandler.get(p_id);
		if (!ref || !ref.protocol) {
			return 0;
		}
		return OrblitRuntime.allocString(ref.protocol);
	},

	orblit_js_rtc_datachannel_destroy__proxy: 'sync',
	orblit_js_rtc_datachannel_destroy__sig: 'vi',
	orblit_js_rtc_datachannel_destroy: function (p_id) {
		OrblitRTCDataChannel.close(p_id);
		IDHandler.remove(p_id);
	},

	orblit_js_rtc_datachannel_connect__proxy: 'sync',
	orblit_js_rtc_datachannel_connect__sig: 'viiiiii',
	orblit_js_rtc_datachannel_connect: function (p_id, p_ref, p_on_open, p_on_message, p_on_error, p_on_close) {
		const onopen = OrblitRuntime.get_func(p_on_open).bind(null, p_ref);
		const onmessage = OrblitRuntime.get_func(p_on_message).bind(null, p_ref);
		const onerror = OrblitRuntime.get_func(p_on_error).bind(null, p_ref);
		const onclose = OrblitRuntime.get_func(p_on_close).bind(null, p_ref);
		OrblitRTCDataChannel.connect(p_id, onopen, onmessage, onerror, onclose);
	},

	orblit_js_rtc_datachannel_close__proxy: 'sync',
	orblit_js_rtc_datachannel_close__sig: 'vi',
	orblit_js_rtc_datachannel_close: function (p_id) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return;
		}
		OrblitRTCDataChannel.close(p_id);
	},
};

autoAddDeps(OrblitRTCDataChannel, '$OrblitRTCDataChannel');
mergeInto(LibraryManager.library, OrblitRTCDataChannel);

const OrblitRTCPeerConnection = {
	$OrblitRTCPeerConnection__deps: ['$IDHandler', '$OrblitRuntime', '$OrblitRTCDataChannel'],
	$OrblitRTCPeerConnection: {
		// Enums
		ConnectionState: {
			'new': 0,
			'connecting': 1,
			'connected': 2,
			'disconnected': 3,
			'failed': 4,
			'closed': 5,
		},

		ConnectionStateCompat: {
			// Using values from IceConnectionState for browsers that do not support ConnectionState (notably Firefox).
			'new': 0,
			'checking': 1,
			'connected': 2,
			'completed': 2,
			'disconnected': 3,
			'failed': 4,
			'closed': 5,
		},

		IceGatheringState: {
			'new': 0,
			'gathering': 1,
			'complete': 2,
		},

		SignalingState: {
			'stable': 0,
			'have-local-offer': 1,
			'have-remote-offer': 2,
			'have-local-pranswer': 3,
			'have-remote-pranswer': 4,
			'closed': 5,
		},

		// Callbacks
		create: function (config, onConnectionChange, onSignalingChange, onIceGatheringChange, onIceCandidate, onDataChannel) {
			let conn = null;
			try {
				conn = new RTCPeerConnection(config);
			} catch (e) {
				OrblitRuntime.error(e);
				return 0;
			}

			const id = IDHandler.add(conn);

			if ('connectionState' in conn && conn['connectionState'] !== undefined) {
				// Use "connectionState" if supported
				conn.onconnectionstatechange = function (event) {
					if (!IDHandler.get(id)) {
						return;
					}
					onConnectionChange(OrblitRTCPeerConnection.ConnectionState[conn.connectionState] || 0);
				};
			} else {
				// Fall back to using "iceConnectionState" when "connectionState" is not supported (notably Firefox).
				conn.oniceconnectionstatechange = function (event) {
					if (!IDHandler.get(id)) {
						return;
					}
					onConnectionChange(OrblitRTCPeerConnection.ConnectionStateCompat[conn.iceConnectionState] || 0);
				};
			}
			conn.onicegatheringstatechange = function (event) {
				if (!IDHandler.get(id)) {
					return;
				}
				onIceGatheringChange(OrblitRTCPeerConnection.IceGatheringState[conn.iceGatheringState] || 0);
			};
			conn.onsignalingstatechange = function (event) {
				if (!IDHandler.get(id)) {
					return;
				}
				onSignalingChange(OrblitRTCPeerConnection.SignalingState[conn.signalingState] || 0);
			};
			conn.onicecandidate = function (event) {
				if (!IDHandler.get(id)) {
					return;
				}
				const c = event.candidate;
				if (!c || !c.candidate) {
					return;
				}
				const candidate_str = OrblitRuntime.allocString(c.candidate);
				const mid_str = OrblitRuntime.allocString(c.sdpMid);
				onIceCandidate(mid_str, c.sdpMLineIndex, candidate_str);
				OrblitRuntime.free(candidate_str);
				OrblitRuntime.free(mid_str);
			};
			conn.ondatachannel = function (event) {
				if (!IDHandler.get(id)) {
					return;
				}
				const cid = IDHandler.add(event.channel);
				onDataChannel(cid);
			};
			return id;
		},

		destroy: function (p_id) {
			const conn = IDHandler.get(p_id);
			if (!conn) {
				return;
			}
			conn.onconnectionstatechange = null;
			conn.oniceconnectionstatechange = null;
			conn.onicegatheringstatechange = null;
			conn.onsignalingstatechange = null;
			conn.onicecandidate = null;
			conn.ondatachannel = null;
			IDHandler.remove(p_id);
		},

		onsession: function (p_id, callback, session) {
			if (!IDHandler.get(p_id)) {
				return;
			}
			const type_str = OrblitRuntime.allocString(session.type);
			const sdp_str = OrblitRuntime.allocString(session.sdp);
			callback(type_str, sdp_str);
			OrblitRuntime.free(type_str);
			OrblitRuntime.free(sdp_str);
		},

		onerror: function (p_id, callback, error) {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return;
			}
			OrblitRuntime.error(error);
			callback();
		},
	},

	orblit_js_rtc_pc_create__proxy: 'sync',
	orblit_js_rtc_pc_create__sig: 'iiiiiiii',
	orblit_js_rtc_pc_create: function (p_config, p_ref, p_on_connection_state_change, p_on_ice_gathering_state_change, p_on_signaling_state_change, p_on_ice_candidate, p_on_datachannel) {
		const wrap = function (p_func) {
			return OrblitRuntime.get_func(p_func).bind(null, p_ref);
		};
		return OrblitRTCPeerConnection.create(
			JSON.parse(OrblitRuntime.parseString(p_config)),
			wrap(p_on_connection_state_change),
			wrap(p_on_signaling_state_change),
			wrap(p_on_ice_gathering_state_change),
			wrap(p_on_ice_candidate),
			wrap(p_on_datachannel)
		);
	},

	orblit_js_rtc_pc_close__proxy: 'sync',
	orblit_js_rtc_pc_close__sig: 'vi',
	orblit_js_rtc_pc_close: function (p_id) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return;
		}
		ref.close();
	},

	orblit_js_rtc_pc_destroy__proxy: 'sync',
	orblit_js_rtc_pc_destroy__sig: 'vi',
	orblit_js_rtc_pc_destroy: function (p_id) {
		OrblitRTCPeerConnection.destroy(p_id);
	},

	orblit_js_rtc_pc_offer_create__proxy: 'sync',
	orblit_js_rtc_pc_offer_create__sig: 'viiii',
	orblit_js_rtc_pc_offer_create: function (p_id, p_obj, p_on_session, p_on_error) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return;
		}
		const onsession = OrblitRuntime.get_func(p_on_session).bind(null, p_obj);
		const onerror = OrblitRuntime.get_func(p_on_error).bind(null, p_obj);
		ref.createOffer().then(function (session) {
			OrblitRTCPeerConnection.onsession(p_id, onsession, session);
		}).catch(function (error) {
			OrblitRTCPeerConnection.onerror(p_id, onerror, error);
		});
	},

	orblit_js_rtc_pc_local_description_set__proxy: 'sync',
	orblit_js_rtc_pc_local_description_set__sig: 'viiiii',
	orblit_js_rtc_pc_local_description_set: function (p_id, p_type, p_sdp, p_obj, p_on_error) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return;
		}
		const type = OrblitRuntime.parseString(p_type);
		const sdp = OrblitRuntime.parseString(p_sdp);
		const onerror = OrblitRuntime.get_func(p_on_error).bind(null, p_obj);
		ref.setLocalDescription({
			'sdp': sdp,
			'type': type,
		}).catch(function (error) {
			OrblitRTCPeerConnection.onerror(p_id, onerror, error);
		});
	},

	orblit_js_rtc_pc_remote_description_set__proxy: 'sync',
	orblit_js_rtc_pc_remote_description_set__sig: 'viiiiii',
	orblit_js_rtc_pc_remote_description_set: function (p_id, p_type, p_sdp, p_obj, p_session_created, p_on_error) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return;
		}
		const type = OrblitRuntime.parseString(p_type);
		const sdp = OrblitRuntime.parseString(p_sdp);
		const onerror = OrblitRuntime.get_func(p_on_error).bind(null, p_obj);
		const onsession = OrblitRuntime.get_func(p_session_created).bind(null, p_obj);
		ref.setRemoteDescription({
			'sdp': sdp,
			'type': type,
		}).then(function () {
			if (type !== 'offer') {
				return Promise.resolve();
			}
			return ref.createAnswer().then(function (session) {
				OrblitRTCPeerConnection.onsession(p_id, onsession, session);
			});
		}).catch(function (error) {
			OrblitRTCPeerConnection.onerror(p_id, onerror, error);
		});
	},

	orblit_js_rtc_pc_ice_candidate_add__proxy: 'sync',
	orblit_js_rtc_pc_ice_candidate_add__sig: 'viiii',
	orblit_js_rtc_pc_ice_candidate_add: function (p_id, p_mid_name, p_mline_idx, p_sdp) {
		const ref = IDHandler.get(p_id);
		if (!ref) {
			return;
		}
		const sdpMidName = OrblitRuntime.parseString(p_mid_name);
		const sdpName = OrblitRuntime.parseString(p_sdp);
		ref.addIceCandidate(new RTCIceCandidate({
			'candidate': sdpName,
			'sdpMid': sdpMidName,
			'sdpMlineIndex': p_mline_idx,
		}));
	},

	orblit_js_rtc_pc_datachannel_create__deps: ['$OrblitRTCDataChannel'],
	orblit_js_rtc_pc_datachannel_create__proxy: 'sync',
	orblit_js_rtc_pc_datachannel_create__sig: 'iiii',
	orblit_js_rtc_pc_datachannel_create: function (p_id, p_label, p_config) {
		try {
			const ref = IDHandler.get(p_id);
			if (!ref) {
				return 0;
			}

			const label = OrblitRuntime.parseString(p_label);
			const config = JSON.parse(OrblitRuntime.parseString(p_config));

			const channel = ref.createDataChannel(label, config);
			return IDHandler.add(channel);
		} catch (e) {
			OrblitRuntime.error(e);
			return 0;
		}
	},
};

autoAddDeps(OrblitRTCPeerConnection, '$OrblitRTCPeerConnection');
mergeInto(LibraryManager.library, OrblitRTCPeerConnection);
