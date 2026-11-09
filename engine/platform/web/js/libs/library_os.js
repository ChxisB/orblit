/**************************************************************************/
/*  library_os.js                                                         */

const IDHandler = {
	$IDHandler: {
		_last_id: 0,
		_references: {},

		get: function (p_id) {
			return IDHandler._references[p_id];
		},

		add: function (p_data) {
			const id = ++IDHandler._last_id;
			IDHandler._references[id] = p_data;
			return id;
		},

		remove: function (p_id) {
			delete IDHandler._references[p_id];
		},
	},
};

autoAddDeps(IDHandler, '$IDHandler');
mergeInto(LibraryManager.library, IDHandler);

const OrblitConfig = {
	$OrblitConfig__postset: 'Module["initConfig"] = OrblitConfig.init_config;',
	$OrblitConfig__deps: ['$OrblitRuntime'],
	$OrblitConfig: {
		canvas: null,
		locale: 'en',
		canvas_resize_policy: 2, // Adaptive
		virtual_keyboard: false,
		orblit_pool_size: 4,
		debug_port: null,
		on_execute: null,
		on_terminate_pid: null,
		on_exit: null,
		pid: 0,

		init_config: function (p_opts) {
			OrblitConfig.canvas_resize_policy = p_opts['canvasResizePolicy'];
			OrblitConfig.canvas = p_opts['canvas'];
			OrblitConfig.locale = p_opts['locale'] || OrblitConfig.locale;
			OrblitConfig.virtual_keyboard = p_opts['virtualKeyboard'];
			OrblitConfig.orblit_pool_size = p_opts['orblitPoolSize'];
			OrblitConfig.debug_port = p_opts['debugPort'];
			OrblitConfig.pid = p_opts['pid'];
			OrblitConfig.on_execute = p_opts['onExecute'];
			OrblitConfig.on_terminate_pid = p_opts['onTerminatePID'];
			OrblitConfig.on_exit = p_opts['onExit'];
			if (p_opts['focusCanvas']) {
				OrblitConfig.canvas.focus();
			}
		},

		locate_file: function (file) {
			return Module['locateFile'](file);
		},

		clear: function () {
			OrblitConfig.canvas = null;
			OrblitConfig.locale = 'en';
			OrblitConfig.canvas_resize_policy = 2;
			OrblitConfig.virtual_keyboard = false;
			OrblitConfig.debug_port = null;
			OrblitConfig.on_execute = null;
			OrblitConfig.on_exit = null;
		},
	},

	orblit_js_config_pid_get__proxy: 'sync',
	orblit_js_config_pid_get__sig: 'i',
	orblit_js_config_pid_get: function () {
		return OrblitConfig.pid;
	},

	orblit_js_config_canvas_id_get__proxy: 'sync',
	orblit_js_config_canvas_id_get__sig: 'vii',
	orblit_js_config_canvas_id_get: function (p_ptr, p_ptr_max) {
		OrblitRuntime.stringToHeap(`#${OrblitConfig.canvas.id}`, p_ptr, p_ptr_max);
	},

	orblit_js_config_locale_get__proxy: 'sync',
	orblit_js_config_locale_get__sig: 'vii',
	orblit_js_config_locale_get: function (p_ptr, p_ptr_max) {
		OrblitRuntime.stringToHeap(OrblitConfig.locale, p_ptr, p_ptr_max);
	},
};

autoAddDeps(OrblitConfig, '$OrblitConfig');
mergeInto(LibraryManager.library, OrblitConfig);

const OrblitFS = {
	$OrblitFS__deps: ['$FS', '$IDBFS', '$OrblitRuntime'],
	$OrblitFS__postset: [
		'Module["initFS"] = OrblitFS.init;',
		'Module["copyToFS"] = OrblitFS.copy_to_fs;',
	].join(''),
	$OrblitFS: {
		// ERRNO_CODES works every odd version of emscripten, but this will break too eventually.
		ENOENT: 44,
		_idbfs: false,
		_syncing: false,
		_mount_points: [],

		is_persistent: function () {
			return OrblitFS._idbfs ? 1 : 0;
		},

		// Initialize orblit file system, setting up persistent paths.
		// Returns a promise that resolves when the FS is ready.
		// We keep track of mount_points, so that we can properly close the IDBFS
		// since emscripten is not doing it by itself. (emscripten GH#12516).
		init: function (persistentPaths) {
			OrblitFS._idbfs = false;
			if (!Array.isArray(persistentPaths)) {
				return Promise.reject(new Error('Persistent paths must be an array'));
			}
			if (!persistentPaths.length) {
				return Promise.resolve();
			}
			OrblitFS._mount_points = persistentPaths.slice();

			function createRecursive(dir) {
				try {
					FS.stat(dir);
				} catch (e) {
					if (e.errno !== OrblitFS.ENOENT) {
						// Let mkdirTree throw in case, we cannot trust the above check.
						OrblitRuntime.error(e);
					}
					FS.mkdirTree(dir);
				}
			}

			OrblitFS._mount_points.forEach(function (path) {
				createRecursive(path);
				FS.mount(IDBFS, {}, path);
			});
			return new Promise(function (resolve, reject) {
				FS.syncfs(true, function (err) {
					if (err) {
						OrblitFS._mount_points = [];
						OrblitFS._idbfs = false;
						OrblitRuntime.print(`IndexedDB not available: ${err.message}`);
					} else {
						OrblitFS._idbfs = true;
					}
					resolve(err);
				});
			});
		},

		// Deinit orblit file system, making sure to unmount file systems, and close IDBFS(s).
		deinit: function () {
			OrblitFS._mount_points.forEach(function (path) {
				try {
					FS.unmount(path);
				} catch (e) {
					OrblitRuntime.print('Already unmounted', e);
				}
				if (OrblitFS._idbfs && IDBFS.dbs[path]) {
					IDBFS.dbs[path].close();
					delete IDBFS.dbs[path];
				}
			});
			OrblitFS._mount_points = [];
			OrblitFS._idbfs = false;
			OrblitFS._syncing = false;
		},

		sync: function () {
			if (OrblitFS._syncing) {
				OrblitRuntime.error('Already syncing!');
				return Promise.resolve();
			}
			OrblitFS._syncing = true;
			return new Promise(function (resolve, reject) {
				FS.syncfs(false, function (error) {
					if (error) {
						OrblitRuntime.error(`Failed to save IDB file system: ${error.message}`);
					}
					OrblitFS._syncing = false;
					resolve(error);
				});
			});
		},

		// Copies a buffer to the internal file system. Creating directories recursively.
		copy_to_fs: function (path, buffer) {
			const idx = path.lastIndexOf('/');
			let dir = '/';
			if (idx > 0) {
				dir = path.slice(0, idx);
			}
			try {
				FS.stat(dir);
			} catch (e) {
				if (e.errno !== OrblitFS.ENOENT) {
					// Let mkdirTree throw in case, we cannot trust the above check.
					OrblitRuntime.error(e);
				}
				FS.mkdirTree(dir);
			}
			FS.writeFile(path, new Uint8Array(buffer));
		},
	},
};
mergeInto(LibraryManager.library, OrblitFS);

const OrblitOS = {
	$OrblitOS__deps: ['$OrblitRuntime', '$OrblitConfig', '$OrblitFS'],
	$OrblitOS__postset: [
		'Module["request_quit"] = function() { OrblitOS.request_quit() };',
		'Module["force_quit"] = function(code) { _emscripten_force_exit(code ?? 1) };',
		'Module["onExit"] = OrblitOS.cleanup;',
		'OrblitOS._fs_sync_promise = Promise.resolve();',
	].join(''),
	$OrblitOS: {
		request_quit: function () {},
		_async_cbs: [],
		_fs_sync_promise: null,

		atexit: function (p_promise_cb) {
			OrblitOS._async_cbs.push(p_promise_cb);
		},

		cleanup: function (exit_code) {
			const cb = OrblitConfig.on_exit;
			OrblitFS.deinit();
			OrblitConfig.clear();
			if (cb) {
				cb(exit_code);
			}
		},

		finish_async: function (callback) {
			OrblitOS._fs_sync_promise.then(function (err) {
				const promises = [];
				OrblitOS._async_cbs.forEach(function (cb) {
					promises.push(new Promise(cb));
				});
				return Promise.all(promises);
			}).then(function () {
				return OrblitFS.sync(); // Final FS sync.
			}).then(function (err) {
				// Always deferred.
				setTimeout(function () {
					callback();
				}, 0);
			});
		},
	},

	orblit_js_os_finish_async__proxy: 'sync',
	orblit_js_os_finish_async__sig: 'vi',
	orblit_js_os_finish_async: function (p_callback) {
		const func = OrblitRuntime.get_func(p_callback);
		OrblitOS.finish_async(func);
	},

	orblit_js_os_request_quit_cb__proxy: 'sync',
	orblit_js_os_request_quit_cb__sig: 'vi',
	orblit_js_os_request_quit_cb: function (p_callback) {
		OrblitOS.request_quit = OrblitRuntime.get_func(p_callback);
	},

	orblit_js_os_fs_is_persistent__proxy: 'sync',
	orblit_js_os_fs_is_persistent__sig: 'i',
	orblit_js_os_fs_is_persistent: function () {
		return OrblitFS.is_persistent();
	},

	orblit_js_os_fs_sync__proxy: 'sync',
	orblit_js_os_fs_sync__sig: 'vi',
	orblit_js_os_fs_sync: function (callback) {
		const func = OrblitRuntime.get_func(callback);
		OrblitOS._fs_sync_promise = OrblitFS.sync();
		OrblitOS._fs_sync_promise.then(function (err) {
			func();
		});
	},

	orblit_js_os_has_feature__proxy: 'sync',
	orblit_js_os_has_feature__sig: 'ii',
	orblit_js_os_has_feature: function (p_ftr) {
		const ftr = OrblitRuntime.parseString(p_ftr);
		const ua = navigator.userAgent;
		if (ftr === 'web_macos') {
			return (ua.indexOf('Mac') !== -1) ? 1 : 0;
		}
		if (ftr === 'web_windows') {
			return (ua.indexOf('Windows') !== -1) ? 1 : 0;
		}
		if (ftr === 'web_android') {
			return (ua.indexOf('Android') !== -1) ? 1 : 0;
		}
		if (ftr === 'web_ios') {
			return ((ua.indexOf('iPhone') !== -1) || (ua.indexOf('iPad') !== -1) || (ua.indexOf('iPod') !== -1)) ? 1 : 0;
		}
		if (ftr === 'web_linuxbsd') {
			return ((ua.indexOf('CrOS') !== -1) || (ua.indexOf('BSD') !== -1) || (ua.indexOf('Linux') !== -1) || (ua.indexOf('X11') !== -1)) ? 1 : 0;
		}
		return 0;
	},

	orblit_js_os_execute__proxy: 'sync',
	orblit_js_os_execute__sig: 'ipp',
	orblit_js_os_execute: function (p_path, p_args) {
		const path = OrblitRuntime.parseString(p_path);
		const json_args = OrblitRuntime.parseString(p_args);
		const args = JSON.parse(json_args);
		if (OrblitConfig.on_execute) {
			return OrblitConfig.on_execute(path, args) ?? 0;
		}
		return -1;
	},

	orblit_js_os_kill__proxy: 'sync',
	orblit_js_os_kill__sig: 'ii',
	orblit_js_os_kill: function (p_pid) {
		if (!OrblitConfig.on_terminate_pid) {
			return -1;
		}
		OrblitConfig.on_terminate_pid(p_pid);
		return 0;
	},

	orblit_js_os_shell_open__proxy: 'sync',
	orblit_js_os_shell_open__sig: 'vi',
	orblit_js_os_shell_open: function (p_uri) {
		window.open(OrblitRuntime.parseString(p_uri), '_blank');
	},

	orblit_js_os_hw_concurrency_get__proxy: 'sync',
	orblit_js_os_hw_concurrency_get__sig: 'i',
	orblit_js_os_hw_concurrency_get: function () {
		// TODO Orblit core needs fixing to avoid spawning too many threads (> 24).
		const concurrency = navigator.hardwareConcurrency || 1;
		return concurrency < 2 ? concurrency : 2;
	},

	orblit_js_os_thread_pool_size_get__proxy: 'sync',
	orblit_js_os_thread_pool_size_get__sig: 'i',
	orblit_js_os_thread_pool_size_get: function () {
		if (typeof PThread === 'undefined') {
			// Threads aren't supported, so default to `1`.
			return 1;
		}

		return OrblitConfig.orblit_pool_size;
	},

	orblit_js_os_download_buffer__proxy: 'sync',
	orblit_js_os_download_buffer__sig: 'viiii',
	orblit_js_os_download_buffer: function (p_ptr, p_size, p_name, p_mime) {
		const buf = OrblitRuntime.heapSlice(HEAP8, p_ptr, p_size);
		const name = OrblitRuntime.parseString(p_name);
		const mime = OrblitRuntime.parseString(p_mime);
		const blob = new Blob([buf], { type: mime });
		const url = window.URL.createObjectURL(blob);
		const a = document.createElement('a');
		a.href = url;
		a.download = name;
		a.style.display = 'none';
		document.body.appendChild(a);
		a.click();
		a.remove();
		window.URL.revokeObjectURL(url);
	},
};

autoAddDeps(OrblitOS, '$OrblitOS');
mergeInto(LibraryManager.library, OrblitOS);

/*
 * Orblit event listeners.
 * Keeps track of registered event listeners so it can remove them on shutdown.
 */
const OrblitEventListeners = {
	$OrblitEventListeners__deps: ['$OrblitOS'],
	$OrblitEventListeners__postset: 'OrblitOS.atexit(function(resolve, reject) { OrblitEventListeners.clear(); resolve(); });',
	$OrblitEventListeners: {
		handlers: [],

		has: function (target, event, method, capture) {
			return OrblitEventListeners.handlers.findIndex(function (e) {
				return e.target === target && e.event === event && e.method === method && e.capture === capture;
			}) !== -1;
		},

		add: function (target, event, method, capture) {
			if (OrblitEventListeners.has(target, event, method, capture)) {
				return;
			}
			function Handler(p_target, p_event, p_method, p_capture) {
				this.target = p_target;
				this.event = p_event;
				this.method = p_method;
				this.capture = p_capture;
			}
			OrblitEventListeners.handlers.push(new Handler(target, event, method, capture));
			target.addEventListener(event, method, capture);
		},

		clear: function () {
			OrblitEventListeners.handlers.forEach(function (h) {
				h.target.removeEventListener(h.event, h.method, h.capture);
			});
			OrblitEventListeners.handlers.length = 0;
		},
	},
};
mergeInto(LibraryManager.library, OrblitEventListeners);

const OrblitPWA = {

	$OrblitPWA__deps: ['$OrblitRuntime', '$OrblitEventListeners'],
	$OrblitPWA: {
		hasUpdate: false,

		updateState: function (cb, reg) {
			if (!reg) {
				return;
			}
			if (!reg.active) {
				return;
			}
			if (reg.waiting) {
				OrblitPWA.hasUpdate = true;
				cb();
			}
			OrblitEventListeners.add(reg, 'updatefound', function () {
				const installing = reg.installing;
				OrblitEventListeners.add(installing, 'statechange', function () {
					if (installing.state === 'installed') {
						OrblitPWA.hasUpdate = true;
						cb();
					}
				});
			});
		},
	},

	orblit_js_pwa_cb__proxy: 'sync',
	orblit_js_pwa_cb__sig: 'vi',
	orblit_js_pwa_cb: function (p_update_cb) {
		if ('serviceWorker' in navigator) {
			try {
				const cb = OrblitRuntime.get_func(p_update_cb);
				navigator.serviceWorker.getRegistration().then(OrblitPWA.updateState.bind(null, cb));
			} catch (e) {
				OrblitRuntime.error('Failed to assign PWA callback', e);
			}
		}
	},

	orblit_js_pwa_update__proxy: 'sync',
	orblit_js_pwa_update__sig: 'i',
	orblit_js_pwa_update: function () {
		if ('serviceWorker' in navigator && OrblitPWA.hasUpdate) {
			try {
				navigator.serviceWorker.getRegistration().then(function (reg) {
					if (!reg || !reg.waiting) {
						return;
					}
					reg.waiting.postMessage('update');
				});
			} catch (e) {
				OrblitRuntime.error(e);
				return 1;
			}
			return 0;
		}
		return 1;
	},
};

autoAddDeps(OrblitPWA, '$OrblitPWA');
mergeInto(LibraryManager.library, OrblitPWA);
