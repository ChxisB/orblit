/**************************************************************************/
/*  patch_em_gl.js                                                        */

addOnPostRun(function () {
	GL.getSource = (shader, count, string, length) => {
		let source = '';
		for (let i = 0; i < count; ++i) {
			const ptr = HEAPU32[(string + i * 4) >> 2];
			const len = length ? HEAPU32[(length + i * 4) >> 2] : undefined;
			if (len) {
				const endPtr = ptr + len;
				const slice = HEAPU8.buffer instanceof ArrayBuffer
					? HEAPU8.subarray(ptr, endPtr)
					: HEAPU8.slice(ptr, endPtr);
				source += UTF8Decoder.decode(slice);
			} else {
				source += UTF8ToString(ptr, len);
			}
		}
		return source;
	};
});
