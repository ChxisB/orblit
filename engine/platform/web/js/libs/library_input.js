/**************************************************************************/
/*  library_input.js                                                      */

/*
 * IME API helper.
 */

const OrblitIME = {
	$OrblitIME__deps: ['$OrblitRuntime', '$OrblitEventListeners'],
	$OrblitIME__postset: 'OrblitOS.atexit(function(resolve, reject) { OrblitIME.clear(); resolve(); });',
	$OrblitIME: {
		ime: null,
		active: false,
		focusTimerIntervalId: -1,

		getModifiers: function (evt) {
			return (evt.shiftKey + 0) + ((evt.altKey + 0) << 1) + ((evt.ctrlKey + 0) << 2) + ((evt.metaKey + 0) << 3);
		},

		ime_active: function (active) {
			function clearFocusTimerInterval() {
				clearInterval(OrblitIME.focusTimerIntervalId);
				OrblitIME.focusTimerIntervalId = -1;
			}

			function focusTimer() {
				if (OrblitIME.ime == null) {
					clearFocusTimerInterval();
					return;
				}
				OrblitIME.ime.focus();
			}

			if (OrblitIME.focusTimerIntervalId > -1) {
				clearFocusTimerInterval();
			}

			if (OrblitIME.ime == null) {
				return;
			}

			OrblitIME.active = active;
			if (active) {
				OrblitIME.ime.style.display = 'block';
				OrblitIME.focusTimerIntervalId = setInterval(focusTimer, 100);
			} else {
				OrblitIME.ime.style.display = 'none';
				OrblitConfig.canvas.focus();
			}
		},

		ime_position: function (x, y) {
			if (OrblitIME.ime == null) {
				return;
			}
			const canvas = OrblitConfig.canvas;
			const rect = canvas.getBoundingClientRect();
			const rw = canvas.width / rect.width;
			const rh = canvas.height / rect.height;
			const clx = (x / rw) + rect.x;
			const cly = (y / rh) + rect.y;

			OrblitIME.ime.style.left = `${clx}px`;
			OrblitIME.ime.style.top = `${cly}px`;
		},

		init: function (ime_cb, key_cb, code, key) {
			function key_event_cb(pressed, evt) {
				const modifiers = OrblitIME.getModifiers(evt);
				OrblitRuntime.stringToHeap(evt.code, code, 32);
				OrblitRuntime.stringToHeap(evt.key, key, 32);
				key_cb(pressed, evt.repeat, modifiers);
				evt.preventDefault();
			}
			function ime_event_cb(event) {
				if (OrblitIME.ime == null) {
					return;
				}
				switch (event.type) {
				case 'compositionstart':
					ime_cb(0, null);
					OrblitIME.ime.innerHTML = '';
					break;
				case 'compositionupdate': {
					const ptr = OrblitRuntime.allocString(event.data);
					ime_cb(1, ptr);
					OrblitRuntime.free(ptr);
				} break;
				case 'compositionend': {
					const ptr = OrblitRuntime.allocString(event.data);
					ime_cb(2, ptr);
					OrblitRuntime.free(ptr);
					OrblitIME.ime.innerHTML = '';
				} break;
				default:
					// Do nothing.
				}
			}

			const ime = document.createElement('div');
			ime.className = 'ime';
			ime.style.background = 'none';
			ime.style.opacity = 0.0;
			ime.style.position = 'fixed';
			ime.style.textAlign = 'left';
			ime.style.fontSize = '1px';
			ime.style.left = '0px';
			ime.style.top = '0px';
			ime.style.width = '100%';
			ime.style.height = '40px';
			ime.style.pointerEvents = 'none';
			ime.style.display = 'none';
			ime.contentEditable = 'true';

			OrblitEventListeners.add(ime, 'compositionstart', ime_event_cb, false);
			OrblitEventListeners.add(ime, 'compositionupdate', ime_event_cb, false);
			OrblitEventListeners.add(ime, 'compositionend', ime_event_cb, false);
			OrblitEventListeners.add(ime, 'keydown', key_event_cb.bind(null, 1), false);
			OrblitEventListeners.add(ime, 'keyup', key_event_cb.bind(null, 0), false);

			ime.onblur = function () {
				this.style.display = 'none';
				OrblitConfig.canvas.focus();
				OrblitIME.active = false;
			};

			OrblitConfig.canvas.parentElement.appendChild(ime);
			OrblitIME.ime = ime;
		},

		clear: function () {
			if (OrblitIME.ime == null) {
				return;
			}
			if (OrblitIME.focusTimerIntervalId > -1) {
				clearInterval(OrblitIME.focusTimerIntervalId);
				OrblitIME.focusTimerIntervalId = -1;
			}
			OrblitIME.ime.remove();
			OrblitIME.ime = null;
		},
	},
};
mergeInto(LibraryManager.library, OrblitIME);

/*
 * Gamepad API helper.
 */
const OrblitInputGamepads = {
	$OrblitInputGamepads__deps: ['$OrblitRuntime', '$OrblitEventListeners'],
	$OrblitInputGamepads: {
		samples: [],

		get_pads: function () {
			try {
				// Will throw in iframe when permission is denied.
				// Will throw/warn in the future for insecure contexts.
				// See https://github.com/w3c/gamepad/pull/120
				const pads = navigator.getGamepads();
				if (pads) {
					return pads;
				}
				return [];
			} catch (e) {
				return [];
			}
		},

		get_samples: function () {
			return OrblitInputGamepads.samples;
		},

		get_sample: function (index) {
			const samples = OrblitInputGamepads.samples;
			return index < samples.length ? samples[index] : null;
		},

		sample: function () {
			const pads = OrblitInputGamepads.get_pads();
			const samples = [];
			for (let i = 0; i < pads.length; i++) {
				const pad = pads[i];
				if (!pad) {
					samples.push(null);
					continue;
				}
				const s = {
					standard: pad.mapping === 'standard',
					buttons: [],
					axes: [],
					connected: pad.connected,
				};
				for (let b = 0; b < pad.buttons.length; b++) {
					s.buttons.push(pad.buttons[b].value);
				}
				for (let a = 0; a < pad.axes.length; a++) {
					s.axes.push(pad.axes[a]);
				}
				samples.push(s);
			}
			OrblitInputGamepads.samples = samples;
		},

		init: function (onchange) {
			OrblitInputGamepads.samples = [];
			function add(pad) {
				const guid = OrblitInputGamepads.get_guid(pad);
				const c_id = OrblitRuntime.allocString(pad.id);
				const c_guid = OrblitRuntime.allocString(guid);
				onchange(pad.index, 1, c_id, c_guid);
				OrblitRuntime.free(c_id);
				OrblitRuntime.free(c_guid);
			}
			const pads = OrblitInputGamepads.get_pads();
			for (let i = 0; i < pads.length; i++) {
				// Might be reserved space.
				if (pads[i]) {
					add(pads[i]);
				}
			}
			OrblitEventListeners.add(window, 'gamepadconnected', function (evt) {
				if (evt.gamepad) {
					add(evt.gamepad);
				}
			}, false);
			OrblitEventListeners.add(window, 'gamepaddisconnected', function (evt) {
				if (evt.gamepad) {
					onchange(evt.gamepad.index, 0);
				}
			}, false);
		},

		get_guid: function (pad) {
			if (pad.mapping) {
				return pad.mapping;
			}
			const ua = navigator.userAgent;
			let os = 'Unknown';
			if (ua.indexOf('Android') >= 0) {
				os = 'Android';
			} else if (ua.indexOf('Linux') >= 0) {
				os = 'Linux';
			} else if (ua.indexOf('iPhone') >= 0) {
				os = 'iOS';
			} else if (ua.indexOf('Macintosh') >= 0) {
				// Updated iPads will fall into this category.
				os = 'MacOSX';
			} else if (ua.indexOf('Windows') >= 0) {
				os = 'Windows';
			}

			const id = pad.id;
			// Chrom* style: NAME (Vendor: xxxx Product: xxxx).
			const exp1 = /vendor: ([0-9a-f]{4}) product: ([0-9a-f]{4})/i;
			// Firefox/Safari style (Safari may remove leading zeroes).
			const exp2 = /^([0-9a-f]+)-([0-9a-f]+)-/i;
			let vendor = '';
			let product = '';
			if (exp1.test(id)) {
				const match = exp1.exec(id);
				vendor = match[1].padStart(4, '0');
				product = match[2].padStart(4, '0');
			} else if (exp2.test(id)) {
				const match = exp2.exec(id);
				vendor = match[1].padStart(4, '0');
				product = match[2].padStart(4, '0');
			}
			if (!vendor || !product) {
				return `${os}Unknown`;
			}
			return os + vendor + product;
		},
	},
};
mergeInto(LibraryManager.library, OrblitInputGamepads);

/*
 * Drag and drop helper.
 * This is pretty big, but basically detect dropped files on OrblitConfig.canvas,
 * process them one by one (recursively for directories), and copies them to
 * the temporary FS path '/tmp/drop-[random]/' so it can be emitted as a orblit
 * event (that requires a string array of paths).
 *
 * NOTE: The temporary files are removed after the callback. This means that
 * deferred callbacks won't be able to access the files.
 */
const OrblitInputDragDrop = {
	$OrblitInputDragDrop__deps: ['$FS', '$OrblitFS'],
	$OrblitInputDragDrop: {
		MAX_DROP_SIZE: 200 * 1024 * 1024,
		create_drop: function () {
			const drop = {};
			drop.pending = [];
			drop.dirs = [];
			drop.files = [];
			drop.size = 0;
			drop.add_entry = function (entry, path) {
				if (entry.isDirectory) {
					drop.add_dir(entry, path);
				} else if (entry.isFile) {
					drop.add_file(entry, path);
				} else {
					OrblitRuntime.error('Unrecognized entry...', entry);
				}
			};
			drop.add_dir = function (entry, path) {
				const next = `${path}/${entry.name}`;
				drop.pending.push(() => new Promise(function (resolve, reject) {
					const reader = entry.createReader();
					reader.readEntries(function (entries) {
						drop.dirs.push(next);
						for (let i = 0; i < entries.length; i++) {
							drop.add_entry(entries[i], next);
						}
						resolve();
					});
				}));
			};
			drop.add_file = function (entry, path) {
				drop.pending.push(() => new Promise(function (resolve, reject) {
					entry.file(function (file) {
						drop.size += file.size ?? 0;
						if (drop.size > OrblitInputDragDrop.MAX_DROP_SIZE) {
							reject(new Error('Drop size limit reached.'));
							return;
						}
						const reader = new FileReader();
						reader.onload = function () {
							const f = {
								'path': `${path}/${file.name}`,
								'name': file.name,
								'type': file.type,
								'size': file.size,
								'data': reader.result,
							};
							drop.files.push(f);
							resolve();
						};
						reader.onerror = function (err) {
							reject(err);
						};
						reader.readAsArrayBuffer(file);
					}, function (err) {
						reject(err);
					});
				}));
			};
			return drop;
		},

		process: function (drop) {
			if (drop.pending.length === 0) {
				return Promise.resolve();
			}
			// Execute pending operations.
			return drop.pending.splice(0).reduce(function (a, b) {
				return a.then(b);
			}, Promise.resolve()).then(function (result) {
				// Will check if new operations have been queued or resolve.
				return OrblitInputDragDrop.process(drop);
			});
		},

		_process_event: function (ev, callback) {
			ev.preventDefault();
			const ROOT = `/tmp/drop-${parseInt(Math.random() * (1 << 30), 10)}`;
			const drop = OrblitInputDragDrop.create_drop();
			if (ev.dataTransfer.items) {
				// Use DataTransferItemList interface to access the file(s)
				for (let i = 0; i < ev.dataTransfer.items.length; i++) {
					const item = ev.dataTransfer.items[i];
					let entry = null;
					if ('getAsEntry' in item) {
						entry = item.getAsEntry();
					} else if ('webkitGetAsEntry' in item) {
						entry = item.webkitGetAsEntry();
					}
					if (entry) {
						drop.add_entry(entry, ROOT);
					}
				}
			} else {
				OrblitRuntime.error('File upload not supported');
			}
			OrblitInputDragDrop.process(drop).then(function () {
				const out = [ROOT];
				drop.dirs.forEach((path) => {
					FS.mkdirTree(path);
					// Add dirs to the string array if in the drop root.
					const idx = path.indexOf('/', ROOT.length + 1);
					if (idx === -1) {
						out.push(path);
					}
				});
				drop.files.forEach((elem) => {
					const path = elem['path'];
					OrblitFS.copy_to_fs(path, elem['data']);
					// Add file to the string array if in the drop root.
					const idx = path.indexOf('/', ROOT.length + 1);
					if (idx === -1) {
						out.push(path);
					}
				});
				callback(out);
			}).catch(function (err) {
				OrblitRuntime.error(err);
			});
		},

		handler: function (callback) {
			return function (ev) {
				OrblitInputDragDrop._process_event(ev, callback);
			};
		},
	},
};
mergeInto(LibraryManager.library, OrblitInputDragDrop);

/*
 * Orblit exposed input functions.
 */
const OrblitInput = {
	$OrblitInput__deps: ['$OrblitRuntime', '$OrblitConfig', '$OrblitEventListeners', '$OrblitInputGamepads', '$OrblitInputDragDrop', '$OrblitIME'],
	$OrblitInput: {
		inputKeyCallback: null,
		setInputKeyData: null,

		getModifiers: function (evt) {
			return (evt.shiftKey + 0) + ((evt.altKey + 0) << 1) + ((evt.ctrlKey + 0) << 2) + ((evt.metaKey + 0) << 3);
		},

		computePosition: function (evt, rect) {
			const canvas = OrblitConfig.canvas;
			const rw = canvas.width / rect.width;
			const rh = canvas.height / rect.height;
			const x = (evt.clientX - rect.x) * rw;
			const y = (evt.clientY - rect.y) * rh;
			return [x, y];
		},

		onKeyEvent: function (pIsPressed, pEvent) {
			if (OrblitInput.inputKeyCallback == null) {
				throw new TypeError('OrblitInput.onKeyEvent(): OrblitInput.inputKeyCallback is null, cannot process key event.');
			}
			if (OrblitInput.setInputKeyData == null) {
				throw new TypeError('OrblitInput.onKeyEvent(): OrblitInput.setInputKeyData is null, cannot process key event.');
			}

			const modifiers = OrblitInput.getModifiers(pEvent);
			OrblitInput.setInputKeyData(pEvent.code, pEvent.key);
			OrblitInput.inputKeyCallback(pIsPressed ? 1 : 0, pEvent.repeat, modifiers);
			pEvent.preventDefault();
		},
	},

	/*
	 * Mouse API
	 */
	orblit_js_input_mouse_move_cb__proxy: 'sync',
	orblit_js_input_mouse_move_cb__sig: 'vi',
	orblit_js_input_mouse_move_cb: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		const canvas = OrblitConfig.canvas;
		function move_cb(evt) {
			const rect = canvas.getBoundingClientRect();
			const pos = OrblitInput.computePosition(evt, rect);
			// Scale movement
			const rw = canvas.width / rect.width;
			const rh = canvas.height / rect.height;
			const rel_pos_x = evt.movementX * rw;
			const rel_pos_y = evt.movementY * rh;
			const modifiers = OrblitInput.getModifiers(evt);
			func(pos[0], pos[1], rel_pos_x, rel_pos_y, modifiers, evt.pressure);
		}
		OrblitEventListeners.add(window, 'pointermove', move_cb, false);
	},

	orblit_js_input_mouse_wheel_cb__proxy: 'sync',
	orblit_js_input_mouse_wheel_cb__sig: 'vi',
	orblit_js_input_mouse_wheel_cb: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		function wheel_cb(evt) {
			if (func(evt.deltaMode, evt.deltaX ?? 0, evt.deltaY ?? 0)) {
				evt.preventDefault();
			}
		}
		OrblitEventListeners.add(OrblitConfig.canvas, 'wheel', wheel_cb, false);
	},

	orblit_js_input_mouse_button_cb__proxy: 'sync',
	orblit_js_input_mouse_button_cb__sig: 'vi',
	orblit_js_input_mouse_button_cb: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		const canvas = OrblitConfig.canvas;
		function button_cb(p_pressed, evt) {
			const rect = canvas.getBoundingClientRect();
			const pos = OrblitInput.computePosition(evt, rect);
			const modifiers = OrblitInput.getModifiers(evt);
			// Since the event is consumed, focus manually.
			// NOTE: The iframe container may not have focus yet, so focus even when already active.
			if (p_pressed) {
				OrblitConfig.canvas.focus();
			}
			if (func(p_pressed, evt.button, pos[0], pos[1], modifiers)) {
				evt.preventDefault();
			}
		}
		OrblitEventListeners.add(canvas, 'mousedown', button_cb.bind(null, 1), false);
		OrblitEventListeners.add(window, 'mouseup', button_cb.bind(null, 0), false);
	},

	/*
	 * Touch API
	 */
	orblit_js_input_touch_cb__proxy: 'sync',
	orblit_js_input_touch_cb__sig: 'viii',
	orblit_js_input_touch_cb: function (callback, ids, coords) {
		const func = OrblitRuntime.get_func(callback);
		const canvas = OrblitConfig.canvas;
		function touch_cb(type, evt) {
			// Since the event is consumed, focus manually.
			// NOTE: The iframe container may not have focus yet, so focus even when already active.
			if (type === 0) {
				OrblitConfig.canvas.focus();
			}
			const rect = canvas.getBoundingClientRect();
			const touches = evt.changedTouches;
			for (let i = 0; i < touches.length; i++) {
				const touch = touches[i];
				const pos = OrblitInput.computePosition(touch, rect);
				OrblitRuntime.setHeapValue(coords + (i * 2) * 8, pos[0], 'double');
				OrblitRuntime.setHeapValue(coords + (i * 2 + 1) * 8, pos[1], 'double');
				OrblitRuntime.setHeapValue(ids + i * 4, touch.identifier, 'i32');
			}
			func(type, touches.length);
			if (evt.cancelable) {
				evt.preventDefault();
			}
		}
		OrblitEventListeners.add(canvas, 'touchstart', touch_cb.bind(null, 0), false);
		OrblitEventListeners.add(canvas, 'touchend', touch_cb.bind(null, 1), false);
		OrblitEventListeners.add(canvas, 'touchcancel', touch_cb.bind(null, 1), false);
		OrblitEventListeners.add(canvas, 'touchmove', touch_cb.bind(null, 2), false);
	},

	/*
	 * Key API
	 */
	orblit_js_input_key_cb__proxy: 'sync',
	orblit_js_input_key_cb__sig: 'viii',
	orblit_js_input_key_cb: function (pCallback, pCodePtr, pKeyPtr) {
		OrblitInput.inputKeyCallback = OrblitRuntime.get_func(pCallback);
		OrblitInput.setInputKeyData = (pCode, pKey) => {
			OrblitRuntime.stringToHeap(pCode, pCodePtr, 32);
			OrblitRuntime.stringToHeap(pKey, pKeyPtr, 32);
		};
		OrblitEventListeners.add(OrblitConfig.canvas, 'keydown', OrblitInput.onKeyEvent.bind(null, true), false);
		OrblitEventListeners.add(OrblitConfig.canvas, 'keyup', OrblitInput.onKeyEvent.bind(null, false), false);
	},

	/*
	 * IME API
	 */
	orblit_js_set_ime_active__proxy: 'sync',
	orblit_js_set_ime_active__sig: 'vi',
	orblit_js_set_ime_active: function (p_active) {
		OrblitIME.ime_active(p_active);
	},

	orblit_js_set_ime_position__proxy: 'sync',
	orblit_js_set_ime_position__sig: 'vii',
	orblit_js_set_ime_position: function (p_x, p_y) {
		OrblitIME.ime_position(p_x, p_y);
	},

	orblit_js_set_ime_cb__proxy: 'sync',
	orblit_js_set_ime_cb__sig: 'viiii',
	orblit_js_set_ime_cb: function (p_ime_cb, p_key_cb, code, key) {
		const ime_cb = OrblitRuntime.get_func(p_ime_cb);
		const key_cb = OrblitRuntime.get_func(p_key_cb);
		OrblitIME.init(ime_cb, key_cb, code, key);
	},

	orblit_js_is_ime_focused__proxy: 'sync',
	orblit_js_is_ime_focused__sig: 'i',
	orblit_js_is_ime_focused: function () {
		return OrblitIME.active;
	},

	/*
	 * Gamepad API
	 */
	orblit_js_input_gamepad_cb__proxy: 'sync',
	orblit_js_input_gamepad_cb__sig: 'vi',
	orblit_js_input_gamepad_cb: function (change_cb) {
		const onchange = OrblitRuntime.get_func(change_cb);
		OrblitInputGamepads.init(onchange);
	},

	orblit_js_input_gamepad_sample_count__proxy: 'sync',
	orblit_js_input_gamepad_sample_count__sig: 'i',
	orblit_js_input_gamepad_sample_count: function () {
		return OrblitInputGamepads.get_samples().length;
	},

	orblit_js_input_gamepad_sample__proxy: 'sync',
	orblit_js_input_gamepad_sample__sig: 'i',
	orblit_js_input_gamepad_sample: function () {
		OrblitInputGamepads.sample();
		return 0;
	},

	orblit_js_input_gamepad_sample_get__proxy: 'sync',
	orblit_js_input_gamepad_sample_get__sig: 'iiiiiii',
	orblit_js_input_gamepad_sample_get: function (p_index, r_btns, r_btns_num, r_axes, r_axes_num, r_standard) {
		const sample = OrblitInputGamepads.get_sample(p_index);
		if (!sample || !sample.connected) {
			return 1;
		}
		const btns = sample.buttons;
		const btns_len = btns.length < 16 ? btns.length : 16;
		for (let i = 0; i < btns_len; i++) {
			OrblitRuntime.setHeapValue(r_btns + (i << 2), btns[i], 'float');
		}
		OrblitRuntime.setHeapValue(r_btns_num, btns_len, 'i32');
		const axes = sample.axes;
		const axes_len = axes.length < 10 ? axes.length : 10;
		for (let i = 0; i < axes_len; i++) {
			OrblitRuntime.setHeapValue(r_axes + (i << 2), axes[i], 'float');
		}
		OrblitRuntime.setHeapValue(r_axes_num, axes_len, 'i32');
		const is_standard = sample.standard ? 1 : 0;
		OrblitRuntime.setHeapValue(r_standard, is_standard, 'i32');
		return 0;
	},

	/*
	 * Drag/Drop API
	 */
	orblit_js_input_drop_files_cb__proxy: 'sync',
	orblit_js_input_drop_files_cb__sig: 'vi',
	orblit_js_input_drop_files_cb: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		const dropFiles = function (files) {
			const args = files || [];
			if (!args.length) {
				return;
			}
			const argc = args.length;
			const argv = OrblitRuntime.allocStringArray(args);
			func(argv, argc);
			OrblitRuntime.freeStringArray(argv, argc);
		};
		const canvas = OrblitConfig.canvas;
		OrblitEventListeners.add(canvas, 'dragover', function (ev) {
			// Prevent default behavior (which would try to open the file(s))
			ev.preventDefault();
		}, false);
		OrblitEventListeners.add(canvas, 'drop', OrblitInputDragDrop.handler(dropFiles));
	},

	/* Paste API */
	orblit_js_input_paste_cb__proxy: 'sync',
	orblit_js_input_paste_cb__sig: 'vi',
	orblit_js_input_paste_cb: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		OrblitEventListeners.add(window, 'paste', function (evt) {
			const text = evt.clipboardData.getData('text');
			const ptr = OrblitRuntime.allocString(text);
			func(ptr);
			OrblitRuntime.free(ptr);
		}, false);
	},

	orblit_js_input_vibrate_handheld__proxy: 'sync',
	orblit_js_input_vibrate_handheld__sig: 'vi',
	orblit_js_input_vibrate_handheld: function (p_duration_ms) {
		if (typeof navigator.vibrate !== 'function') {
			OrblitRuntime.print('This browser does not support vibration.');
		} else {
			navigator.vibrate(p_duration_ms);
		}
	},
};

autoAddDeps(OrblitInput, '$OrblitInput');
mergeInto(LibraryManager.library, OrblitInput);
