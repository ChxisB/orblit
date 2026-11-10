/**************************************************************************/
/*  library_runtime.js                                                    */

const OrblitRuntime = {
	$OrblitRuntime: {
		/*
		 * Functions
		 */
		get_func: function (ptr) {
			return wasmTable.get(ptr);
		},

		/*
		 * Prints
		 */
		error: function () {
			err.apply(null, Array.from(arguments)); // eslint-disable-line no-undef
		},

		print: function () {
			out.apply(null, Array.from(arguments)); // eslint-disable-line no-undef
		},

		/*
		 * Memory
		 */
		malloc: function (p_size) {
			return _malloc(p_size);
		},

		free: function (p_ptr) {
			_free(p_ptr);
		},

		getHeapValue: function (p_ptr, p_type) {
			return getValue(p_ptr, p_type);
		},

		setHeapValue: function (p_ptr, p_value, p_type) {
			setValue(p_ptr, p_value, p_type);
		},

		heapSub: function (p_heap, p_ptr, p_len) {
			const bytes = p_heap.BYTES_PER_ELEMENT;
			return p_heap.subarray(p_ptr / bytes, p_ptr / bytes + p_len);
		},

		heapSlice: function (p_heap, p_ptr, p_len) {
			const bytes = p_heap.BYTES_PER_ELEMENT;
			return p_heap.slice(p_ptr / bytes, p_ptr / bytes + p_len);
		},

		heapCopy: function (p_dst, p_src, p_ptr) {
			const bytes = p_src.BYTES_PER_ELEMENT;
			return p_dst.set(p_src, p_ptr / bytes);
		},

		/*
		 * Strings
		 */
		parseString: function (p_ptr) {
			return UTF8ToString(p_ptr);
		},

		parseStringArray: function (p_ptr, p_size) {
			const strings = [];
			const ptrs = OrblitRuntime.heapSub(HEAP32, p_ptr, p_size); // TODO wasm64
			ptrs.forEach(function (ptr) {
				strings.push(OrblitRuntime.parseString(ptr));
			});
			return strings;
		},

		strlen: function (p_str) {
			return lengthBytesUTF8(p_str);
		},

		allocString: function (p_str) {
			const length = OrblitRuntime.strlen(p_str) + 1;
			const c_str = OrblitRuntime.malloc(length);
			stringToUTF8(p_str, c_str, length);
			return c_str;
		},

		allocStringArray: function (p_strings) {
			const size = p_strings.length;
			const c_ptr = OrblitRuntime.malloc(size * 4);
			for (let i = 0; i < size; i++) {
				HEAP32[(c_ptr >> 2) + i] = OrblitRuntime.allocString(p_strings[i]);
			}
			return c_ptr;
		},

		freeStringArray: function (p_ptr, p_len) {
			for (let i = 0; i < p_len; i++) {
				OrblitRuntime.free(HEAP32[(p_ptr >> 2) + i]);
			}
			OrblitRuntime.free(p_ptr);
		},

		stringToHeap: function (p_str, p_ptr, p_len) {
			return stringToUTF8Array(p_str, HEAP8, p_ptr, p_len);
		},
	},
};
autoAddDeps(OrblitRuntime, '$OrblitRuntime');
mergeInto(LibraryManager.library, OrblitRuntime);
