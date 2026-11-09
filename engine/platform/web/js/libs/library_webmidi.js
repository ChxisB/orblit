/**************************************************************************/
/*  library_webmidi.js                                                    */

const OrblitWebMidi = {

	$OrblitWebMidi__deps: ['$OrblitRuntime'],
	$OrblitWebMidi: {
		abortControllers: [],
		isListening: false,
	},

	orblit_js_webmidi_open_midi_inputs__deps: ['$OrblitWebMidi'],
	orblit_js_webmidi_open_midi_inputs__proxy: 'sync',
	orblit_js_webmidi_open_midi_inputs__sig: 'iiii',
	orblit_js_webmidi_open_midi_inputs: function (pSetInputNamesCb, pOnMidiMessageCb, pDataBuffer, dataBufferLen) {
		if (OrblitWebMidi.is_listening) {
			return 0; // OK
		}
		if (!navigator.requestMIDIAccess) {
			return 2; // ERR_UNAVAILABLE
		}
		const setInputNamesCb = OrblitRuntime.get_func(pSetInputNamesCb);
		const onMidiMessageCb = OrblitRuntime.get_func(pOnMidiMessageCb);

		OrblitWebMidi.isListening = true;
		navigator.requestMIDIAccess().then((midi) => {
			const inputs = [...midi.inputs.values()];
			const inputNames = inputs.map((input) => input.name);

			const c_ptr = OrblitRuntime.allocStringArray(inputNames);
			setInputNamesCb(inputNames.length, c_ptr);
			OrblitRuntime.freeStringArray(c_ptr, inputNames.length);

			inputs.forEach((input, i) => {
				const abortController = new AbortController();
				OrblitWebMidi.abortControllers.push(abortController);
				input.addEventListener('midimessage', (event) => {
					const status = event.data[0];
					const data = event.data.slice(1);
					const size = data.length;

					if (size > dataBufferLen) {
						throw new Error(`data too big ${size} > ${dataBufferLen}`);
					}
					HEAPU8.set(data, pDataBuffer);

					onMidiMessageCb(i, status, pDataBuffer, data.length);
				}, { signal: abortController.signal });
			});
		});

		return 0; // OK
	},

	orblit_js_webmidi_close_midi_inputs__deps: ['$OrblitWebMidi'],
	orblit_js_webmidi_close_midi_inputs__proxy: 'sync',
	orblit_js_webmidi_close_midi_inputs__sig: 'v',
	orblit_js_webmidi_close_midi_inputs: function () {
		for (const abortController of OrblitWebMidi.abortControllers) {
			abortController.abort();
		}
		OrblitWebMidi.abortControllers = [];
		OrblitWebMidi.isListening = false;
	},
};

mergeInto(LibraryManager.library, OrblitWebMidi);
