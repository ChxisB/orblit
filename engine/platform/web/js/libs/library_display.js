/**************************************************************************/
/*  library_display.js                                                    */

const OrblitDisplayVK = {

	$OrblitDisplayVK__deps: ['$OrblitRuntime', '$OrblitConfig', '$OrblitEventListeners', '$OrblitInput'],
	$OrblitDisplayVK__postset: 'OrblitOS.atexit(function(resolve, reject) { OrblitDisplayVK.clear(); resolve(); });',
	$OrblitDisplayVK: {
		textinput: null,
		textarea: null,

		available: function () {
			return OrblitConfig.virtual_keyboard && 'ontouchstart' in window;
		},

		init: function (input_cb) {
			function create(what) {
				const elem = document.createElement(what);
				elem.style.display = 'none';
				elem.style.position = 'absolute';
				elem.style.zIndex = '-1';
				elem.style.background = 'transparent';
				elem.style.padding = '0px';
				elem.style.margin = '0px';
				elem.style.overflow = 'hidden';
				elem.style.width = '0px';
				elem.style.height = '0px';
				elem.style.border = '0px';
				elem.style.outline = 'none';
				elem.readonly = true;
				elem.disabled = true;
				OrblitEventListeners.add(elem, 'input', function (evt) {
					const c_str = OrblitRuntime.allocString(elem.value);
					input_cb(c_str, elem.selectionEnd);
					OrblitRuntime.free(c_str);
				}, false);
				if (what === 'input') {
					// Handling the "Enter" key.
					const onKey = (pEvent, pEventName) => {
						if (pEvent.key !== 'Enter') {
							return;
						}
						OrblitInput.onKeyEvent(pEventName === 'keydown', pEvent);
					};
					OrblitEventListeners.add(elem, 'keydown', (pEvent) => onKey(pEvent, 'keydown'), false);
					OrblitEventListeners.add(elem, 'keyup', (pEvent) => onKey(pEvent, 'keyup'), false);
				}
				OrblitEventListeners.add(elem, 'blur', function (evt) {
					elem.style.display = 'none';
					elem.readonly = true;
					elem.disabled = true;
				}, false);
				OrblitConfig.canvas.insertAdjacentElement('beforebegin', elem);
				return elem;
			}
			OrblitDisplayVK.textinput = create('input');
			OrblitDisplayVK.textarea = create('textarea');
			OrblitDisplayVK.updateSize();
		},
		show: function (text, type, start, end) {
			if (!OrblitDisplayVK.textinput || !OrblitDisplayVK.textarea) {
				return;
			}
			if (OrblitDisplayVK.textinput.style.display !== '' || OrblitDisplayVK.textarea.style.display !== '') {
				OrblitDisplayVK.hide();
			}
			OrblitDisplayVK.updateSize();

			let elem = OrblitDisplayVK.textinput;
			switch (type) {
			case 0: // DisplaySystemEnums::KEYBOARD_TYPE_DEFAULT
				elem.type = 'text';
				elem.inputmode = '';
				break;
			case 1: // DisplaySystemEnums::KEYBOARD_TYPE_MULTILINE
				elem = OrblitDisplayVK.textarea;
				break;
			case 2: // DisplaySystemEnums::KEYBOARD_TYPE_NUMBER
				elem.type = 'text';
				elem.inputmode = 'numeric';
				break;
			case 3: // DisplaySystemEnums::KEYBOARD_TYPE_NUMBER_DECIMAL
				elem.type = 'text';
				elem.inputmode = 'decimal';
				break;
			case 4: // DisplaySystemEnums::KEYBOARD_TYPE_PHONE
				elem.type = 'tel';
				elem.inputmode = '';
				break;
			case 5: // DisplaySystemEnums::KEYBOARD_TYPE_EMAIL_ADDRESS
				elem.type = 'email';
				elem.inputmode = '';
				break;
			case 6: // DisplaySystemEnums::KEYBOARD_TYPE_PASSWORD
				elem.type = 'password';
				elem.inputmode = '';
				break;
			case 7: // DisplaySystemEnums::KEYBOARD_TYPE_URL
				elem.type = 'url';
				elem.inputmode = '';
				break;
			default:
				elem.type = 'text';
				elem.inputmode = '';
				break;
			}

			elem.readonly = false;
			elem.disabled = false;
			elem.value = text;
			elem.style.display = 'block';
			elem.focus();
			elem.setSelectionRange(start, end);
		},
		hide: function () {
			if (!OrblitDisplayVK.textinput || !OrblitDisplayVK.textarea) {
				return;
			}
			[OrblitDisplayVK.textinput, OrblitDisplayVK.textarea].forEach(function (elem) {
				elem.blur();
				elem.style.display = 'none';
				elem.value = '';
			});
		},
		updateSize: function () {
			if (!OrblitDisplayVK.textinput || !OrblitDisplayVK.textarea) {
				return;
			}
			const rect = OrblitConfig.canvas.getBoundingClientRect();
			function update(elem) {
				elem.style.left = `${rect.left}px`;
				elem.style.top = `${rect.top}px`;
				elem.style.width = `${rect.width}px`;
				elem.style.height = `${rect.height}px`;
			}
			update(OrblitDisplayVK.textinput);
			update(OrblitDisplayVK.textarea);
		},
		clear: function () {
			if (OrblitDisplayVK.textinput) {
				OrblitDisplayVK.textinput.remove();
				OrblitDisplayVK.textinput = null;
			}
			if (OrblitDisplayVK.textarea) {
				OrblitDisplayVK.textarea.remove();
				OrblitDisplayVK.textarea = null;
			}
		},
	},
};
mergeInto(LibraryManager.library, OrblitDisplayVK);

/*
 * Display server cursor helper.
 * Keeps track of cursor status and custom shapes.
 */
const OrblitDisplayCursor = {
	$OrblitDisplayCursor__deps: ['$OrblitOS', '$OrblitConfig'],
	$OrblitDisplayCursor__postset: 'OrblitOS.atexit(function(resolve, reject) { OrblitDisplayCursor.clear(); resolve(); });',
	$OrblitDisplayCursor: {
		shape: 'default',
		visible: true,
		cursors: {},
		set_style: function (style) {
			OrblitConfig.canvas.style.cursor = style;
		},
		set_shape: function (shape) {
			OrblitDisplayCursor.shape = shape;
			let css = shape;
			if (shape in OrblitDisplayCursor.cursors) {
				const c = OrblitDisplayCursor.cursors[shape];
				css = `url("${c.url}") ${c.x} ${c.y}, default`;
			}
			if (OrblitDisplayCursor.visible) {
				OrblitDisplayCursor.set_style(css);
			}
		},
		clear: function () {
			OrblitDisplayCursor.set_style('');
			OrblitDisplayCursor.shape = 'default';
			OrblitDisplayCursor.visible = true;
			Object.keys(OrblitDisplayCursor.cursors).forEach(function (key) {
				URL.revokeObjectURL(OrblitDisplayCursor.cursors[key]);
				delete OrblitDisplayCursor.cursors[key];
			});
		},
		lockPointer: function () {
			const canvas = OrblitConfig.canvas;
			if (canvas.requestPointerLock) {
				canvas.requestPointerLock();
			}
		},
		releasePointer: function () {
			if (document.exitPointerLock) {
				document.exitPointerLock();
			}
		},
		isPointerLocked: function () {
			return document.pointerLockElement === OrblitConfig.canvas;
		},
	},
};
mergeInto(LibraryManager.library, OrblitDisplayCursor);

const OrblitDisplayScreen = {
	$OrblitDisplayScreen__deps: ['$OrblitConfig', '$OrblitOS', '$GL', 'emscripten_webgl_get_current_context'],
	$OrblitDisplayScreen: {
		desired_size: [0, 0],
		hidpi: true,
		getPixelRatio: function () {
			return OrblitDisplayScreen.hidpi ? window.devicePixelRatio || 1 : 1;
		},
		isFullscreen: function () {
			const elem = document.fullscreenElement || document.mozFullscreenElement
				|| document.webkitFullscreenElement || document.msFullscreenElement;
			if (elem) {
				return elem === OrblitConfig.canvas;
			}
			// But maybe knowing the element is not supported.
			return document.fullscreen || document.mozFullScreen
				|| document.webkitIsFullscreen;
		},
		hasFullscreen: function () {
			return document.fullscreenEnabled || document.mozFullScreenEnabled
				|| document.webkitFullscreenEnabled;
		},
		requestFullscreen: function () {
			if (!OrblitDisplayScreen.hasFullscreen()) {
				return 1;
			}
			const canvas = OrblitConfig.canvas;
			try {
				const promise = (canvas.requestFullscreen || canvas.msRequestFullscreen
					|| canvas.mozRequestFullScreen || canvas.mozRequestFullscreen
					|| canvas.webkitRequestFullscreen
				).call(canvas);
				// Some browsers (Safari) return undefined.
				// For the standard ones, we need to catch it.
				if (promise) {
					promise.catch(function () {
						// nothing to do.
					});
				}
			} catch (e) {
				return 1;
			}
			return 0;
		},
		exitFullscreen: function () {
			if (!OrblitDisplayScreen.isFullscreen()) {
				return 0;
			}
			try {
				const promise = document.exitFullscreen();
				if (promise) {
					promise.catch(function () {
						// nothing to do.
					});
				}
			} catch (e) {
				return 1;
			}
			return 0;
		},
		_updateGL: function () {
			const gl_context_handle = _emscripten_webgl_get_current_context();
			const gl = GL.getContext(gl_context_handle);
			if (gl) {
				GL.resizeOffscreenFramebuffer(gl);
			}
		},
		updateSize: function () {
			const isFullscreen = OrblitDisplayScreen.isFullscreen();
			const wantsFullWindow = OrblitConfig.canvas_resize_policy === 2;
			const noResize = OrblitConfig.canvas_resize_policy === 0;
			const dWidth = OrblitDisplayScreen.desired_size[0];
			const dHeight = OrblitDisplayScreen.desired_size[1];
			const canvas = OrblitConfig.canvas;
			let width = dWidth;
			let height = dHeight;
			if (noResize) {
				// Don't resize canvas, just update GL if needed.
				if (canvas.width !== width || canvas.height !== height) {
					OrblitDisplayScreen.desired_size = [canvas.width, canvas.height];
					OrblitDisplayScreen._updateGL();
					return 1;
				}
				return 0;
			}
			const scale = OrblitDisplayScreen.getPixelRatio();
			if (isFullscreen || wantsFullWindow) {
				// We need to match screen size.
				width = Math.floor(window.innerWidth * scale);
				height = Math.floor(window.innerHeight * scale);
			}
			const csw = `${Math.floor(width / scale)}px`;
			const csh = `${Math.floor(height / scale)}px`;
			if (canvas.style.width !== csw || canvas.style.height !== csh || canvas.width !== width || canvas.height !== height) {
				// Size doesn't match.
				// Resize canvas, set correct CSS pixel size, update GL.
				canvas.width = width;
				canvas.height = height;
				canvas.style.width = csw;
				canvas.style.height = csh;
				OrblitDisplayScreen._updateGL();
				return 1;
			}
			return 0;
		},
	},
};
mergeInto(LibraryManager.library, OrblitDisplayScreen);

/**
 * Display server interface.
 *
 * Exposes all the functions needed by DisplaySystem implementation.
 */
const OrblitDisplay = {
	$OrblitDisplay__deps: ['$OrblitConfig', '$OrblitRuntime', '$OrblitDisplayCursor', '$OrblitEventListeners', '$OrblitDisplayScreen', '$OrblitDisplayVK'],
	$OrblitDisplay: {
		window_icon: '',
		getDPI: function () {
			// devicePixelRatio is given in dppx
			// https://drafts.csswg.org/css-values/#resolution
			// > due to the 1:96 fixed ratio of CSS *in* to CSS *px*, 1dppx is equivalent to 96dpi.
			const dpi = Math.round(window.devicePixelRatio * 96);
			return dpi >= 96 ? dpi : 96;
		},
	},

	orblit_js_display_is_swap_ok_cancel__proxy: 'sync',
	orblit_js_display_is_swap_ok_cancel__sig: 'i',
	orblit_js_display_is_swap_ok_cancel: function () {
		const win = (['Windows', 'Win64', 'Win32', 'WinCE']);
		const plat = navigator.platform || '';
		if (win.indexOf(plat) !== -1) {
			return 1;
		}
		return 0;
	},

	orblit_js_tts_is_speaking__proxy: 'sync',
	orblit_js_tts_is_speaking__sig: 'i',
	orblit_js_tts_is_speaking: function () {
		return window.speechSynthesis.speaking;
	},

	orblit_js_tts_is_paused__proxy: 'sync',
	orblit_js_tts_is_paused__sig: 'i',
	orblit_js_tts_is_paused: function () {
		return window.speechSynthesis.paused;
	},

	orblit_js_tts_get_voices__proxy: 'sync',
	orblit_js_tts_get_voices__sig: 'vi',
	orblit_js_tts_get_voices: function (p_callback) {
		const func = OrblitRuntime.get_func(p_callback);
		try {
			const arr = [];
			const voices = window.speechSynthesis.getVoices();
			for (let i = 0; i < voices.length; i++) {
				arr.push(`${voices[i].lang};${voices[i].name}`);
			}
			const c_ptr = OrblitRuntime.allocStringArray(arr);
			func(arr.length, c_ptr);
			OrblitRuntime.freeStringArray(c_ptr, arr.length);
		} catch (e) {
			// Fail graciously.
		}
	},

	orblit_js_tts_speak__proxy: 'sync',
	orblit_js_tts_speak__sig: 'viiiffii',
	orblit_js_tts_speak: function (p_text, p_voice, p_volume, p_pitch, p_rate, p_utterance_id, p_callback) {
		const func = OrblitRuntime.get_func(p_callback);

		function listener_end(evt) {
			evt.currentTarget.cb(1 /* DisplaySystemEnums::TTS_UTTERANCE_ENDED */, evt.currentTarget.id, 0);
		}

		function listener_start(evt) {
			evt.currentTarget.cb(0 /* DisplaySystemEnums::TTS_UTTERANCE_STARTED */, evt.currentTarget.id, 0);
		}

		function listener_error(evt) {
			evt.currentTarget.cb(2 /* DisplaySystemEnums::TTS_UTTERANCE_CANCELED */, evt.currentTarget.id, 0);
		}

		function listener_bound(evt) {
			evt.currentTarget.cb(3 /* DisplaySystemEnums::TTS_UTTERANCE_BOUNDARY */, evt.currentTarget.id, evt.charIndex);
		}

		const utterance = new SpeechSynthesisUtterance(OrblitRuntime.parseString(p_text));
		utterance.rate = p_rate;
		utterance.pitch = p_pitch;
		utterance.volume = p_volume / 100.0;
		utterance.addEventListener('end', listener_end);
		utterance.addEventListener('start', listener_start);
		utterance.addEventListener('error', listener_error);
		utterance.addEventListener('boundary', listener_bound);
		utterance.id = p_utterance_id;
		utterance.cb = func;
		const voice = OrblitRuntime.parseString(p_voice);
		const voices = window.speechSynthesis.getVoices();
		for (let i = 0; i < voices.length; i++) {
			if (voices[i].name === voice) {
				utterance.voice = voices[i];
				break;
			}
		}
		window.speechSynthesis.resume();
		window.speechSynthesis.speak(utterance);
	},

	orblit_js_tts_pause__proxy: 'sync',
	orblit_js_tts_pause__sig: 'v',
	orblit_js_tts_pause: function () {
		window.speechSynthesis.pause();
	},

	orblit_js_tts_resume__proxy: 'sync',
	orblit_js_tts_resume__sig: 'v',
	orblit_js_tts_resume: function () {
		window.speechSynthesis.resume();
	},

	orblit_js_tts_stop__proxy: 'sync',
	orblit_js_tts_stop__sig: 'v',
	orblit_js_tts_stop: function () {
		window.speechSynthesis.cancel();
		window.speechSynthesis.resume();
	},

	orblit_js_display_alert__proxy: 'sync',
	orblit_js_display_alert__sig: 'vi',
	orblit_js_display_alert: function (p_text) {
		window.alert(OrblitRuntime.parseString(p_text)); // eslint-disable-line no-alert
	},

	orblit_js_display_screen_dpi_get__proxy: 'sync',
	orblit_js_display_screen_dpi_get__sig: 'i',
	orblit_js_display_screen_dpi_get: function () {
		return OrblitDisplay.getDPI();
	},

	orblit_js_display_pixel_ratio_get__proxy: 'sync',
	orblit_js_display_pixel_ratio_get__sig: 'f',
	orblit_js_display_pixel_ratio_get: function () {
		return OrblitDisplayScreen.getPixelRatio();
	},

	orblit_js_display_fullscreen_request__proxy: 'sync',
	orblit_js_display_fullscreen_request__sig: 'i',
	orblit_js_display_fullscreen_request: function () {
		return OrblitDisplayScreen.requestFullscreen();
	},

	orblit_js_display_fullscreen_exit__proxy: 'sync',
	orblit_js_display_fullscreen_exit__sig: 'i',
	orblit_js_display_fullscreen_exit: function () {
		return OrblitDisplayScreen.exitFullscreen();
	},

	orblit_js_display_desired_size_set__proxy: 'sync',
	orblit_js_display_desired_size_set__sig: 'vii',
	orblit_js_display_desired_size_set: function (width, height) {
		OrblitDisplayScreen.desired_size = [width, height];
		OrblitDisplayScreen.updateSize();
	},

	orblit_js_display_size_update__proxy: 'sync',
	orblit_js_display_size_update__sig: 'i',
	orblit_js_display_size_update: function () {
		const updated = OrblitDisplayScreen.updateSize();
		if (updated) {
			OrblitDisplayVK.updateSize();
		}
		return updated;
	},

	orblit_js_display_screen_size_get__proxy: 'sync',
	orblit_js_display_screen_size_get__sig: 'vii',
	orblit_js_display_screen_size_get: function (width, height) {
		const scale = OrblitDisplayScreen.getPixelRatio();
		OrblitRuntime.setHeapValue(width, window.screen.width * scale, 'i32');
		OrblitRuntime.setHeapValue(height, window.screen.height * scale, 'i32');
	},

	orblit_js_display_window_size_get__proxy: 'sync',
	orblit_js_display_window_size_get__sig: 'vii',
	orblit_js_display_window_size_get: function (p_width, p_height) {
		OrblitRuntime.setHeapValue(p_width, OrblitConfig.canvas.width, 'i32');
		OrblitRuntime.setHeapValue(p_height, OrblitConfig.canvas.height, 'i32');
	},

	orblit_js_display_has_webgl__proxy: 'sync',
	orblit_js_display_has_webgl__sig: 'ii',
	orblit_js_display_has_webgl: function (p_version) {
		if (p_version !== 1 && p_version !== 2) {
			return false;
		}
		try {
			return !!document.createElement('canvas').getContext(p_version === 2 ? 'webgl2' : 'webgl');
		} catch (e) { /* Not available */ }
		return false;
	},

	/*
	 * Canvas
	 */
	orblit_js_display_canvas_focus__proxy: 'sync',
	orblit_js_display_canvas_focus__sig: 'v',
	orblit_js_display_canvas_focus: function () {
		OrblitConfig.canvas.focus();
	},

	orblit_js_display_canvas_is_focused__proxy: 'sync',
	orblit_js_display_canvas_is_focused__sig: 'i',
	orblit_js_display_canvas_is_focused: function () {
		return document.activeElement === OrblitConfig.canvas;
	},

	/*
	 * Touchscreen
	 */
	orblit_js_display_touchscreen_is_available__proxy: 'sync',
	orblit_js_display_touchscreen_is_available__sig: 'i',
	orblit_js_display_touchscreen_is_available: function () {
		return 'ontouchstart' in window;
	},

	/*
	 * Clipboard
	 */
	orblit_js_display_clipboard_set__proxy: 'sync',
	orblit_js_display_clipboard_set__sig: 'ii',
	orblit_js_display_clipboard_set: function (p_text) {
		const text = OrblitRuntime.parseString(p_text);
		if (!navigator.clipboard || !navigator.clipboard.writeText) {
			return 1;
		}
		navigator.clipboard.writeText(text).catch(function (e) {
			// Setting OS clipboard is only possible from an input callback.
			OrblitRuntime.error('Setting OS clipboard is only possible from an input callback for the Web platform. Exception:', e);
		});
		return 0;
	},

	orblit_js_display_clipboard_get__proxy: 'sync',
	orblit_js_display_clipboard_get__sig: 'ii',
	orblit_js_display_clipboard_get: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		try {
			navigator.clipboard.readText().then(function (result) {
				const ptr = OrblitRuntime.allocString(result);
				func(ptr);
				OrblitRuntime.free(ptr);
			}).catch(function (e) {
				// Fail graciously.
			});
		} catch (e) {
			// Fail graciously.
		}
	},

	/*
	 * Window
	 */
	orblit_js_display_window_title_set__proxy: 'sync',
	orblit_js_display_window_title_set__sig: 'vi',
	orblit_js_display_window_title_set: function (p_data) {
		document.title = OrblitRuntime.parseString(p_data);
	},

	orblit_js_display_window_icon_set__proxy: 'sync',
	orblit_js_display_window_icon_set__sig: 'vii',
	orblit_js_display_window_icon_set: function (p_ptr, p_len) {
		let link = document.getElementById('-gd-engine-icon');
		const old_icon = OrblitDisplay.window_icon;
		if (p_ptr) {
			if (link === null) {
				link = document.createElement('link');
				link.rel = 'icon';
				link.id = '-gd-engine-icon';
				document.head.appendChild(link);
			}
			const png = new Blob([OrblitRuntime.heapSlice(HEAPU8, p_ptr, p_len)], { type: 'image/png' });
			OrblitDisplay.window_icon = URL.createObjectURL(png);
			link.href = OrblitDisplay.window_icon;
		} else {
			if (link) {
				link.remove();
			}
			OrblitDisplay.window_icon = null;
		}
		if (old_icon) {
			URL.revokeObjectURL(old_icon);
		}
	},

	/*
	 * Cursor
	 */
	orblit_js_display_cursor_set_visible__proxy: 'sync',
	orblit_js_display_cursor_set_visible__sig: 'vi',
	orblit_js_display_cursor_set_visible: function (p_visible) {
		const visible = p_visible !== 0;
		if (visible === OrblitDisplayCursor.visible) {
			return;
		}
		OrblitDisplayCursor.visible = visible;
		if (visible) {
			OrblitDisplayCursor.set_shape(OrblitDisplayCursor.shape);
		} else {
			OrblitDisplayCursor.set_style('none');
		}
	},

	orblit_js_display_cursor_is_hidden__proxy: 'sync',
	orblit_js_display_cursor_is_hidden__sig: 'i',
	orblit_js_display_cursor_is_hidden: function () {
		return !OrblitDisplayCursor.visible;
	},

	orblit_js_display_cursor_set_shape__proxy: 'sync',
	orblit_js_display_cursor_set_shape__sig: 'vi',
	orblit_js_display_cursor_set_shape: function (p_string) {
		OrblitDisplayCursor.set_shape(OrblitRuntime.parseString(p_string));
	},

	orblit_js_display_cursor_set_custom_shape__proxy: 'sync',
	orblit_js_display_cursor_set_custom_shape__sig: 'viiiii',
	orblit_js_display_cursor_set_custom_shape: function (p_shape, p_ptr, p_len, p_hotspot_x, p_hotspot_y) {
		const shape = OrblitRuntime.parseString(p_shape);
		const old_shape = OrblitDisplayCursor.cursors[shape];
		if (p_len > 0) {
			const png = new Blob([OrblitRuntime.heapSlice(HEAPU8, p_ptr, p_len)], { type: 'image/png' });
			const url = URL.createObjectURL(png);
			OrblitDisplayCursor.cursors[shape] = {
				url: url,
				x: p_hotspot_x,
				y: p_hotspot_y,
			};
		} else {
			delete OrblitDisplayCursor.cursors[shape];
		}
		if (shape === OrblitDisplayCursor.shape) {
			OrblitDisplayCursor.set_shape(OrblitDisplayCursor.shape);
		}
		if (old_shape) {
			URL.revokeObjectURL(old_shape.url);
		}
	},

	orblit_js_display_cursor_lock_set__proxy: 'sync',
	orblit_js_display_cursor_lock_set__sig: 'vi',
	orblit_js_display_cursor_lock_set: function (p_lock) {
		if (p_lock) {
			OrblitDisplayCursor.lockPointer();
		} else {
			OrblitDisplayCursor.releasePointer();
		}
	},

	orblit_js_display_cursor_is_locked__proxy: 'sync',
	orblit_js_display_cursor_is_locked__sig: 'i',
	orblit_js_display_cursor_is_locked: function () {
		return OrblitDisplayCursor.isPointerLocked() ? 1 : 0;
	},

	/*
	 * Listeners
	 */
	orblit_js_display_fullscreen_cb__proxy: 'sync',
	orblit_js_display_fullscreen_cb__sig: 'vi',
	orblit_js_display_fullscreen_cb: function (callback) {
		const canvas = OrblitConfig.canvas;
		const func = OrblitRuntime.get_func(callback);
		function change_cb(evt) {
			if (evt.target === canvas) {
				func(OrblitDisplayScreen.isFullscreen());
			}
		}
		OrblitEventListeners.add(document, 'fullscreenchange', change_cb, false);
		OrblitEventListeners.add(document, 'mozfullscreenchange', change_cb, false);
		OrblitEventListeners.add(document, 'webkitfullscreenchange', change_cb, false);
	},

	orblit_js_display_window_blur_cb__proxy: 'sync',
	orblit_js_display_window_blur_cb__sig: 'vi',
	orblit_js_display_window_blur_cb: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		OrblitEventListeners.add(window, 'blur', function () {
			func();
		}, false);
	},

	orblit_js_display_notification_cb__proxy: 'sync',
	orblit_js_display_notification_cb__sig: 'viiiii',
	orblit_js_display_notification_cb: function (callback, p_enter, p_exit, p_in, p_out) {
		const canvas = OrblitConfig.canvas;
		const func = OrblitRuntime.get_func(callback);
		const notif = [p_enter, p_exit, p_in, p_out];
		['mouseover', 'mouseleave', 'focus', 'blur'].forEach(function (evt_name, idx) {
			OrblitEventListeners.add(canvas, evt_name, function () {
				func(notif[idx]);
			}, true);
		});
	},

	orblit_js_display_setup_canvas__proxy: 'sync',
	orblit_js_display_setup_canvas__sig: 'viiii',
	orblit_js_display_setup_canvas: function (p_width, p_height, p_fullscreen, p_hidpi) {
		const canvas = OrblitConfig.canvas;
		OrblitEventListeners.add(canvas, 'contextmenu', function (ev) {
			ev.preventDefault();
		}, false);
		OrblitEventListeners.add(canvas, 'webglcontextlost', function (ev) {
			alert('WebGL context lost, please reload the page'); // eslint-disable-line no-alert
			ev.preventDefault();
		}, false);
		OrblitDisplayScreen.hidpi = !!p_hidpi;
		switch (OrblitConfig.canvas_resize_policy) {
		case 0: // None
			OrblitDisplayScreen.desired_size = [canvas.width, canvas.height];
			break;
		case 1: // Project
			OrblitDisplayScreen.desired_size = [p_width, p_height];
			break;
		default: // Full window
			// Ensure we display in the right place, the size will be handled by updateSize
			canvas.style.position = 'absolute';
			canvas.style.top = 0;
			canvas.style.left = 0;
			break;
		}
		OrblitDisplayScreen.updateSize();
		if (p_fullscreen) {
			OrblitDisplayScreen.requestFullscreen();
		}
	},

	/*
	 * Virtual Keyboard
	 */
	orblit_js_display_vk_show__proxy: 'sync',
	orblit_js_display_vk_show__sig: 'viiii',
	orblit_js_display_vk_show: function (p_text, p_type, p_start, p_end) {
		const text = OrblitRuntime.parseString(p_text);
		const start = p_start > 0 ? p_start : 0;
		const end = p_end > 0 ? p_end : start;
		OrblitDisplayVK.show(text, p_type, start, end);
	},

	orblit_js_display_vk_hide__proxy: 'sync',
	orblit_js_display_vk_hide__sig: 'v',
	orblit_js_display_vk_hide: function () {
		OrblitDisplayVK.hide();
	},

	orblit_js_display_vk_available__proxy: 'sync',
	orblit_js_display_vk_available__sig: 'i',
	orblit_js_display_vk_available: function () {
		return OrblitDisplayVK.available();
	},

	orblit_js_display_tts_available__proxy: 'sync',
	orblit_js_display_tts_available__sig: 'i',
	orblit_js_display_tts_available: function () {
		return 'speechSynthesis' in window;
	},

	orblit_js_display_vk_cb__proxy: 'sync',
	orblit_js_display_vk_cb__sig: 'vi',
	orblit_js_display_vk_cb: function (p_input_cb) {
		const input_cb = OrblitRuntime.get_func(p_input_cb);
		if (OrblitDisplayVK.available()) {
			OrblitDisplayVK.init(input_cb);
		}
	},
};

autoAddDeps(OrblitDisplay, '$OrblitDisplay');
mergeInto(LibraryManager.library, OrblitDisplay);
