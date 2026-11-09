/**************************************************************************/
/*  library_audio.js                                                      */

function OrblitSampleDriver(audioContext, positionWorkletPath) {
	/** @type {?AudioContext} */
	let ctx = audioContext;
	/** @type {?Promise} */
	let audioPositionWorkletPromise = ctx.audioWorklet ? ctx.audioWorklet.addModule(positionWorkletPath) : null;
	/** @type {!Array<AudioWorkletNode>} */
	const audioPositionWorkletNodes = [];

	/** @enum {string} */
	const LoopMode = {
		DISABLED: 'disabled',
		FORWARD: 'forward',
		BACKWARD: 'backward',
		PINGPONG: 'pingpong',
	};
	const OrblitChannel = Object.freeze({
		CHANNEL_L: 0,
		CHANNEL_R: 1,
		CHANNEL_C: 2,
		CHANNEL_LFE: 3,
		CHANNEL_SL: 4, // Surround left
		CHANNEL_SR: 5, // Surround right
		CHANNEL_SBL: 6, // Surround back left (7.1)
		CHANNEL_SBR: 7, // Surround back right (7.1)
	});
	const WebChannel = Object.freeze({
		CHANNEL_L: 0,
		CHANNEL_R: 1,
		CHANNEL_SL: 2,
		CHANNEL_SR: 3,
		CHANNEL_C: 4,
		CHANNEL_LFE: 5,
	});
	const MAX_VOLUME_CHANNELS = 8;
	const NUMBER_OF_WEB_CHANNELS = Object.keys(WebChannel).length;
	const MAX_SAFE_FLOAT32_INTEGER = 16777215;
	/**
	 * JS pair of a Orblit AudioBus.
	 * @typedef {{
	 *   getId: function(): number,
	 *   getInputNode: function(): !AudioNode,
	 *   getOutputNode: function(): !AudioNode,
	 *   setVolumeDb: function (OrblitBus?): void,
	 *   setSend: function(OrblitBus?): void,
	 *   setMute: function(boolean): void,
	 *   setSoloMute: function(boolean): void,
	 *   clear: function(): void,
	 * }}
	 */
	var OrblitBus; // eslint-disable-line no-var, no-unassigned-vars
	/**
	 * A mixing sub-graph to control OrblitPlayback volume outputs to a OrblitBus.
	 * @typedef {{
	 *   setVolume: function(!Float32Array): void,
	 *   getInputNode: function(): !AudioNode,
	 *   getOutputNode: function(): !AudioNode,
	 *   clear: function(): void,
	 * }}
	 */
	var OrblitVolumeMixer; // eslint-disable-line no-var, no-unassigned-vars
	/**
	 * JS pair of a Orblit AudioStream/AudioSample.
	 * @typedef {{
	 *   loopBegin: number,
	 *   loopEnd: number,
	 *   loopMode: LoopMode,
	 *   length: number,
	 *   copyBuffer: function(): !AudioBuffer,
	 * }}
	 */
	var OrblitStream; // eslint-disable-line no-var, no-unassigned-vars
	/**
	 * JS pair of a Orblit AudioStreamPlayback/AudioSamplePlayback.
	 * @typedef {{
	 *   stop: function(): void,
	 *   start: function(number): void,
	 *   pause: function(boolean): void,
	 *   setVolumes: function(Array<number>, !Float32Array),
	 *   setPitchScale: function(number): void,
	 *   getPlaybackPosition: function(): number,
	 * }}
	 */
	var OrblitPlayback; // eslint-disable-line no-var, no-unassigned-vars

	/** @type {OrblitBus?} */
	let busSolo = null;
	/** @type {Array<OrblitBus>} */
	let buses = [];
	/** @type {Map<string, OrblitStream>} */
	const streams = new Map();
	/** @type {Map<string, OrblitPlayback>} */
	const playbacks = new Map();

	/** @type {?function(number): void}*/
	let finishedCallback = null;

	/**
	 * @param {?function(number): void} callback
	 */
	function setFinishedCallback(callback) {
		finishedCallback = callback;
	}

	/**
	 * @param {number} db
	 * @return {number}
	 */
	function db_to_linear(db) {
		return Math.exp(db * 0.11512925464970228420089957273422); // eslint-disable-line no-loss-of-precision
	}

	/**
	 * @return {!OrblitBus}
	 */
	function createBus() {
		/** @type {!GainNode} */ const muteNode = ctx.createGain();
		/** @type {!GainNode} */ const gainNode = ctx.createGain();
		/** @type {!GainNode} */ const soloNode = ctx.createGain();
		gainNode.connect(soloNode).connect(muteNode);

		const bus = {};
		/** @return {!AudioNode} */ bus.getInputNode = () => gainNode;
		/** @return {!AudioNode} */ bus.getOutputNode = () => muteNode;
		/** @return {number} */
		bus.getId = function () {
			return buses.indexOf(bus);
		};
		/** @param {number} val */
		bus.setVolumeDb = function (val) {
			const linear = db_to_linear(val);
			if (isFinite(linear)) {
				gainNode.gain.value = linear;
			}
		};
		/** @param {?OrblitBus} val */
		bus.setSend = function (val) {
			if (val == null) {
				if (bus.getId() != 0) {
					OrblitRuntime.error(`Cannot set bus send to null when the bus not at index 0 (current index: ${bus.getId()})`);
					return;
				}
				bus.getOutputNode().connect(ctx.destination);
			} else {
				bus.getOutputNode().disconnect();
				bus.getOutputNode().connect(val.getInputNode());
			}
		};
		/** @param {boolean} enable */
		bus.setMute = function (enable) {
			muteNode.gain.value = enable ? 0 : 1;
		};
		/** @param {boolean} enable */
		bus.setSoloMute = function (enable) {
			soloNode.gain.value = enable ? 0 : 1;
		};
		bus.clear = function () {
			bus.getInputNode().disconnect();
			bus.getOutputNode().disconnect();
		};
		buses.push(bus);
		return bus;
	}
	/**
	 * @param {number} index
	 * @return {?OrblitBus}
	 */
	function getBus(index) {
		return buses[index] ?? null;
	}

	/**
	 * @param {number} bus
	 * @return {!OrblitVolumeMixer}
	 */
	function createVolumeMixer(bus) {
		/** @type {!ChannelSplitterNode} */ const splitter = ctx.createChannelSplitter(NUMBER_OF_WEB_CHANNELS);
		/** @type {!Array<!GainNode>} */ const channels = [];
		/** @type {!ChannelMergerNode} */ const merger = ctx.createChannelMerger(NUMBER_OF_WEB_CHANNELS);
		Object.values(WebChannel).forEach((idx) => {
			const ch = ctx.createGain();
			splitter.connect(ch, idx).connect(merger, 0, idx);
			channels[idx] = ch;
		});
		merger.connect(getBus(bus).getInputNode());

		const node = {};
		/** @return {!AudioNode} */ node.getInputNode = () => splitter;
		/** @return {!AudioNode} */ node.getOutputNode = () => merger;
		/** @param {!Float32Array} volume */
		node.setVolume = function (volume) {
			if (volume.length !== MAX_VOLUME_CHANNELS) {
				OrblitRuntime.error(`Volume length isn't "${MAX_VOLUME_CHANNELS}", is ${volume.length} instead`);
				return;
			}
			channels[WebChannel.CHANNEL_L].gain.value = volume[OrblitChannel.CHANNEL_L] ?? 0;
			channels[WebChannel.CHANNEL_R].gain.value = volume[OrblitChannel.CHANNEL_R] ?? 0;
			channels[WebChannel.CHANNEL_SL].gain.value = volume[OrblitChannel.CHANNEL_SL] ?? 0;
			channels[WebChannel.CHANNEL_SR].gain.value = volume[OrblitChannel.CHANNEL_SR] ?? 0;
			channels[WebChannel.CHANNEL_C].gain.value = volume[OrblitChannel.CHANNEL_C] ?? 0;
			channels[WebChannel.CHANNEL_LFE].gain.value = volume[OrblitChannel.CHANNEL_LFE] ?? 0;
		};
		node.clear = function () {
			splitter.disconnect();
			channels.forEach((ch) => {
				ch.disconnect();
			});
			merger.disconnect();
		};
		return node;
	}
	/**
	 * @param {string} streamId
	 * @param {!AudioBuffer} sourceBuffer
	 * @param {{
	 *   loopMode: (LoopMode|undefined),
	 *   loopBegin: (number|undefined),
	 *   loopEnd: (number|undefined),
	 * }=} options
	 * @return {!OrblitStream}
	 */
	function createStream(streamId, sourceBuffer, options = {}) {
		const stream = {
			/** @type {LoopMode} */ loopMode: options.loopMode ?? LoopMode.DISABLED,
			/** @type {number} */ loopBegin: options.loopBegin ?? 0,
			/** @type {number} */ loopEnd: options.loopEnd ?? 0,
			/** @type {number} */ length: sourceBuffer.length,
		};
		/** @return {!AudioBuffer} */
		stream.copyBuffer = function () {
			const channels = new Array(sourceBuffer.numberOfChannels);
			for (let i = 0; i < sourceBuffer.numberOfChannels; i++) {
				channels[i] = new Float32Array(sourceBuffer.getChannelData(i));
			}
			const buffer = ctx.createBuffer(sourceBuffer.numberOfChannels, sourceBuffer.length, sourceBuffer.sampleRate);
			for (let i = 0; i < channels.length; i++) {
				buffer.copyToChannel(channels[i], i, 0);
			}
			return buffer;
		};
		streams.set(streamId, stream);
		return stream;
	}
	/**
	 * @param {string} id
	 * @return {!OrblitStream}
	 */
	function getStream(id) {
		return streams.get(id) ?? null;
	}
	/** @param {string} id */
	function removeStream(id) {
		streams.delete(id);
	}

	/**
	 * @param {string} playbackId
	 * @return {!OrblitPlayback}
	 */
	function createPlayback(playbackId, streamId) {
		/** @type {number} */ const sampleRate = ctx.sampleRate;
		const output = /** @struct */ {
			/** @type {!Map<number, OrblitVolumeMixer|null>} */
			mixers: new Map(),
			/** @type {!Float32Array} */
			volumes: new Float32Array(0),
		};
		const {
			/** @type {number} */ length,
			/** @type {LoopMode} */ loopMode,
			/** @type {number} */ loopBegin,
			/** @type {number} */ loopEnd,
		} = getStream(streamId);
		/** @type {boolean} */ const looping = loopMode !== LoopMode.DISABLED;
		/** @type {boolean} */ let started = false;
		/** @type {boolean} */ let paused = false;
		/** @type {number} */ let processed = 0;
		/** @type {number} */ let pitchScale = 1;
		/** @type {?AudioBufferSourceNode} */ let source = null;
		/** @type {?AudioWorkletNode} */ let worklet = null;
		/** @type {number} */ let workletId = 0;

		const node = {};
		function attachWorklet() {
			if (!source) {
				return; // Already stopped.
			}
			if (audioPositionWorkletNodes.length > 0) {
				worklet = audioPositionWorkletNodes.pop();
			} else {
				worklet = new AudioWorkletNode(/** @type {!AudioContext} */ (ctx), 'orblit-position-reporting-processor');
			}
			const end = loopEnd ? Math.min(loopEnd, length) : length;
			worklet.port.onmessage = (event) => {
				const [id, data] = event.data;
				if (id != workletId) {
					return;
				}
				processed += data;
				if (processed > end) {
					processed -= end;
					if (looping) {
						processed += loopBegin;
					}
				}
			};
			worklet.parameters.get('id').setValueAtTime(workletId, ctx.currentTime);
			worklet.parameters.get('scale').setValueAtTime(pitchScale, ctx.currentTime);
			source.connect(worklet);
		}
		/** @param {!Array<number>} newBuses */
		function compareOutputs(newBuses) {
			const keys = Array.from(output.mixers.keys());
			if (keys.length != newBuses.length) {
				return false;
			}
			return newBuses.find((b) => !keys.includes(b)) === undefined;
		}
		/**
		 * @param {!Array<number>} newBuses
		 * @param {!Float32Array} volumes
		 */
		function setVolumes(newBuses, volumes) {
			if (!compareOutputs(newBuses)) {
				for (const mixer of output.mixers.values()) {
					if (!mixer) {
						continue;
					}
					if (source) {
						source.disconnect(mixer.getInputNode());
					}
					mixer.clear();
				}
				output.mixers.clear();
			}
			output.volumes = volumes;
			for (let idx = 0; idx < newBuses.length; idx++) {
				const bus = newBuses[idx];
				let mixer = output.mixers.get(bus);
				if (!mixer) {
					mixer = createVolumeMixer(bus);
					if (source) {
						source.connect(mixer.getInputNode());
					}
					output.mixers.set(bus, mixer);
				}
				const size = MAX_VOLUME_CHANNELS;
				mixer.setVolume(volumes.slice(idx * size, (idx * size) + size));
			}
		}
		function clearSource() {
			if (source) {
				source.onended = null;
				source.stop();
				source.disconnect();
				source = null;
			}
			output.mixers.forEach(function (mixer, key, map) {
				if (mixer) {
					if (source) {
						source.disconnect(mixer.getInputNode());
					}
					mixer.clear();
				}
				map.set(key, null);
			});
			if (worklet) {
				worklet.disconnect();
				worklet.port.onmessage = null;
				audioPositionWorkletNodes.push(worklet);
				worklet = null;
			}
			started = false;
			paused = false;
			processed = 0;
		}
		function createSource() {
			clearSource();

			// Source
			source = ctx.createBufferSource();
			source.buffer = getStream(streamId).copyBuffer();
			source.onended = (_) => {
				if (paused) {
					return;
				}
				node.stop();
				if (!finishedCallback) {
					playbacks.delete(playbackId); // Cleanup after ourselves.
					return;
				}
				const cstr = OrblitRuntime.allocString(playbackId);
				finishedCallback(cstr);
				OrblitRuntime.free(cstr);
			};
			if (looping) {
				source.loop = true;
				source.loopStart = loopBegin / sampleRate;
				source.loopEnd = (loopEnd ? loopEnd : length) / sampleRate;
			}
			node.setPitchScale(pitchScale);
			node.setVolumes(Array.from(output.mixers.keys()), output.volumes);

			// Worklet
			workletId = Math.floor(Math.random() * MAX_SAFE_FLOAT32_INTEGER);
			if (audioPositionWorkletPromise) { // Unavailable outside secure contexts.
				audioPositionWorkletPromise.then(attachWorklet).catch((err) => {
					const newErr = new Error('Failed to create PositionWorklet.');
					newErr.cause = err;
					OrblitRuntime.error(newErr);
				});
			}
		}
		/** @param {number} startPosition */
		function start(startPosition) {
			if (started) {
				return;
			}
			createSource();
			processed = startPosition * sampleRate;
			source.start(0, startPosition);
			started = true;
			paused = false;
		}
		/** @param {number} newPitchScale */
		function setPitchScale(newPitchScale) {
			pitchScale = newPitchScale;
			if (!source || paused) {
				return;
			}
			source.playbackRate.value = pitchScale;
			if (worklet) {
				worklet.parameters.get('scale').setValueAtTime(pitchScale, ctx.currentTime);
			}
		};
		/** @param {boolean} enable */
		function pause(enable) {
			if (paused == enable) {
				return;
			}
			paused = enable;
			if (!started || !source) {
				return;
			}
			if (enable) {
				source.playbackRate.value = 0;
				if (worklet) {
					worklet.parameters.get('scale').setValueAtTime(0, ctx.currentTime);
				}
			} else {
				setPitchScale(pitchScale);
			}
		};
		/** @return {number} */
		function getPlaybackPosition() {
			return processed / sampleRate;
		}
		node.stop = clearSource;
		node.start = start;
		node.pause = pause;
		node.setVolumes = setVolumes;
		node.setPitchScale = setPitchScale;
		node.getPlaybackPosition = getPlaybackPosition;
		playbacks.set(playbackId, node);
		return node;
	}
	/**
	 * @param {string} id
	 * @return {?OrblitPlayback}
	 */
	function getPlayback(id) {
		return playbacks.get(id) ?? null;
	}
	/** @param {string} id */
	function removePlayback(id) {
		const playback = playbacks.get(id) ?? null;
		if (playback) {
			playback.stop();
			playbacks.delete(id);
		}
	}
	/** @param {number} count */
	function setBusCount(count) {
		if (count < 1) {
			OrblitRuntime.error('Invalid bus index, driver bug');
			return;
		}
		if (count === buses.length) {
			return;
		}
		if (count < buses.length) {
			// TODO: what to do with nodes connected to the deleted buses?
			const deletedBuses = buses.slice(count);
			for (let i = 0; i < deletedBuses.length; i++) {
				const deletedBus = deletedBuses[i];
				deletedBus.clear();
			}
			buses = buses.slice(0, count);
			return;
		}
		for (let i = buses.length; i < count; i++) {
			const bus = createBus();
			if (i === 0) {
				bus.setSend(null);
			} else {
				bus.setSend(getBus(0));
			}
		}
	}
	/**
	 * @param {number} fromIndex
	 * @param {number} toIndex
	 */
	function moveBus(fromIndex, toIndex) {
		if (fromIndex < 1 || toIndex < 1) {
			OrblitRuntime.error('Invalid bus index, driver bug');
			return;
		}
		const movedBus = getBus(fromIndex);
		if (!movedBus) {
			return;
		}
		const newBuses = buses.filter((_, i) => i !== fromIndex);
		// Inserts at index.
		newBuses.splice(toIndex - 1, 0, movedBus);
		buses = newBuses;
	}
	/** @param {number} index */
	function addBusAt(index) {
		if (index < 1) {
			OrblitRuntime.error('Invalid bus index, driver bug');
			return;
		}
		const bus = createBus();
		if (index !== bus.getId()) {
			moveBus(bus.getId(), index);
		}
		if (bus.getId() === 0) {
			bus.setSend(null);
		} else {
			bus.setSend(getBus(0));
		}
	}
	/** @param {number} index */
	function removeBus(index) {
		if (index < 1) {
			OrblitRuntime.error('Invalid bus index, driver bug');
			return;
		}
		const bus = getBus(index);
		if (!bus) {
			return;
		}
		bus.clear();
		buses = buses.filter((v) => v !== bus);
	}
	/**
	 * @param {number} busIndex
	 * @param {boolean} enable
	 */
	function setBusSolo(busIndex, enable) {
		const bus = getBus(busIndex);
		if (!bus) {
			return;
		}
		const current = busSolo;
		if (enable && current != bus) {
			busSolo = bus;
			bus.setSoloMute(false);
			buses.filter((o) => o !== bus).forEach((b) => {
				b.setSoloMute(true);
			});
		} else if (!enable && current == bus) {
			busSolo = null;
			buses.forEach((b) => {
				b.setSoloMute(false);
			});
		}
	}
	function clear() {
		audioPositionWorkletPromise = null;
		playbacks.forEach((p) => p.stop());
		playbacks.clear();
		streams.clear();
		buses.splice(0, buses.length).forEach((b) => b.clear());
		audioPositionWorkletNodes.splice(0, audioPositionWorkletNodes.length);
		ctx = null;
	}
	const driver = /** @struct */ {
		setFinishedCallback,
		clear,
		// Streams
		createStream,
		getStream,
		removeStream,
		// Playbacks
		createPlayback,
		getPlayback,
		removePlayback,
		// Buses
		getBus,
		setBusSolo,
		setBusCount,
		moveBus,
		addBusAt,
		removeBus,
		// So they survive pre-stripping.
		_: { OrblitBus, OrblitVolumeMixer, OrblitStream, OrblitPlayback },
	};
	return driver;
};

const _OrblitAudio = {
	$OrblitAudio__deps: ['$OrblitRuntime', '$OrblitOS'],
	$OrblitAudio: {
		/** @type {AudioContext} */
		ctx: null,
		input: null,
		driver: null,
		sample_driver: null,
		interval: 0,

		OrblitSampleDriver,
		init: function (mix_rate, latency, onstatechange, onlatencyupdate) {
			const opts = {};
			// If mix_rate is 0, let the browser choose.
			if (mix_rate) {
				OrblitAudio.sampleRate = mix_rate;
				opts['sampleRate'] = mix_rate;
			}
			// Do not specify, leave 'interactive' for good performance.
			// opts['latencyHint'] = latency / 1000;
			const ctx = new (window.AudioContext || window.webkitAudioContext)(opts);
			OrblitAudio.ctx = ctx;
			ctx.onstatechange = function () {
				let state = 0;
				switch (ctx.state) {
				case 'suspended':
					state = 0;
					break;
				case 'running':
					state = 1;
					break;
				case 'closed':
					state = 2;
					break;
				default:
					// Do nothing.
				}
				onstatechange(state);
			};
			ctx.onstatechange(); // Immediately notify state.
			// Update computed latency
			OrblitAudio.interval = setInterval(function () {
				let computed_latency = 0;
				if (ctx.baseLatency) {
					computed_latency += OrblitAudio.ctx.baseLatency;
				}
				if (ctx.outputLatency) {
					computed_latency += OrblitAudio.ctx.outputLatency;
				}
				onlatencyupdate(computed_latency);
			}, 1000);
			OrblitOS.atexit(OrblitAudio.close_async);

			// Initialize sample driver.
			const path = OrblitConfig.locate_file('orblit.audio.position.worklet.js');
			OrblitAudio.sample_driver = OrblitAudio.OrblitSampleDriver(ctx, path);

			return ctx.destination.channelCount;
		},

		create_input: function (callback) {
			if (OrblitAudio.input) {
				return 0; // Already started.
			}
			function gotMediaInput(stream) {
				try {
					OrblitAudio.input = OrblitAudio.ctx.createMediaStreamSource(stream);
					callback(OrblitAudio.input);
				} catch (e) {
					OrblitRuntime.error('Failed creating input.', e);
				}
			}
			if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
				navigator.mediaDevices.getUserMedia({
					'audio': true,
				}).then(gotMediaInput, function (e) {
					OrblitRuntime.error('Error getting user media.', e);
				});
			} else {
				if (!navigator.getUserMedia) {
					navigator.getUserMedia = navigator.webkitGetUserMedia || navigator.mozGetUserMedia;
				}
				if (!navigator.getUserMedia) {
					OrblitRuntime.error('getUserMedia not available.');
					return 1;
				}
				navigator.getUserMedia({
					'audio': true,
				}, gotMediaInput, function (e) {
					OrblitRuntime.print(e);
				});
			}
			return 0;
		},

		close_async: function (resolve, reject) {
			const ctx = OrblitAudio.ctx;
			OrblitAudio.ctx = null;
			// Audio was not initialized.
			if (!ctx) {
				resolve();
				return;
			}
			// Clear audio driver
			if (OrblitAudio.sample_driver) {
				OrblitAudio.sample_driver.clear();
				OrblitAudio.sample_driver = null;
			}
			// Remove latency callback
			if (OrblitAudio.interval) {
				clearInterval(OrblitAudio.interval);
				OrblitAudio.interval = 0;
			}
			// Disconnect input, if it was started.
			if (OrblitAudio.input) {
				OrblitAudio.input.disconnect();
				OrblitAudio.input = null;
			}
			// Disconnect output
			let closed = Promise.resolve();
			if (OrblitAudio.driver) {
				closed = OrblitAudio.driver.close();
			}
			closed.then(function () {
				return ctx.close();
			}).then(function () {
				ctx.onstatechange = null;
				resolve();
			}).catch(function (e) {
				ctx.onstatechange = null;
				OrblitRuntime.error('Error closing AudioContext', e);
				resolve();
			});
		},
	},

	orblit_audio_is_available__sig: 'i',
	orblit_audio_is_available__proxy: 'sync',
	orblit_audio_is_available: function () {
		if (!(window.AudioContext || window.webkitAudioContext)) {
			return 0;
		}
		return 1;
	},

	orblit_audio_has_worklet__proxy: 'sync',
	orblit_audio_has_worklet__sig: 'i',
	orblit_audio_has_worklet: function () {
		return OrblitAudio.ctx && OrblitAudio.ctx.audioWorklet ? 1 : 0;
	},

	orblit_audio_has_script_processor__proxy: 'sync',
	orblit_audio_has_script_processor__sig: 'i',
	orblit_audio_has_script_processor: function () {
		return OrblitAudio.ctx && OrblitAudio.ctx.createScriptProcessor ? 1 : 0;
	},

	orblit_audio_init__proxy: 'sync',
	orblit_audio_init__sig: 'iiiii',
	orblit_audio_init: function (
		p_mix_rate,
		p_latency,
		p_state_change,
		p_latency_update
	) {
		const statechange = OrblitRuntime.get_func(p_state_change);
		const latencyupdate = OrblitRuntime.get_func(p_latency_update);
		const mix_rate = OrblitRuntime.getHeapValue(p_mix_rate, 'i32');
		const channels = OrblitAudio.init(
			mix_rate,
			p_latency,
			statechange,
			latencyupdate
		);
		OrblitRuntime.setHeapValue(p_mix_rate, OrblitAudio.ctx.sampleRate, 'i32');
		return channels;
	},

	orblit_audio_resume__proxy: 'sync',
	orblit_audio_resume__sig: 'v',
	orblit_audio_resume: function () {
		if (OrblitAudio.ctx && OrblitAudio.ctx.state !== 'running') {
			OrblitAudio.ctx.resume();
		}
	},

	orblit_audio_input_start__proxy: 'sync',
	orblit_audio_input_start__sig: 'i',
	orblit_audio_input_start: function () {
		return OrblitAudio.create_input(function (input) {
			input.connect(OrblitAudio.driver.get_node());
		});
	},

	orblit_audio_input_stop__proxy: 'sync',
	orblit_audio_input_stop__sig: 'v',
	orblit_audio_input_stop: function () {
		if (OrblitAudio.input) {
			const tracks = OrblitAudio.input['mediaStream']['getTracks']();
			for (let i = 0; i < tracks.length; i++) {
				tracks[i]['stop']();
			}
			OrblitAudio.input.disconnect();
			OrblitAudio.input = null;
		}
	},
};

autoAddDeps(_OrblitAudio, '$OrblitAudio');
mergeInto(LibraryManager.library, _OrblitAudio);

/**
 * The audio sample driver used by AudioStreamPlayer with playback_type = Sample.
 */
const _OrblitAudioSampleDriver = {
	$OrblitAudioSampleDriver__deps: ['$OrblitAudio', '$OrblitRuntime'],
	$OrblitAudioSampleDriver: {},

	orblit_audio_sample_stream_is_registered__proxy: 'sync',
	orblit_audio_sample_stream_is_registered__sig: 'ii',
	orblit_audio_sample_stream_is_registered: function (p_id) {
		if (!OrblitAudio.sample_driver) {
			return 0;
		}
		return OrblitAudio.sample_driver.getStream(OrblitRuntime.parseString(p_id)) ? 1 : 0;
	},

	orblit_audio_sample_register_stream__proxy: 'sync',
	orblit_audio_sample_register_stream__sig: 'viiiiii',
	orblit_audio_sample_register_stream: function (p_id, p_frames, p_frames_count, p_loop_mode, p_loop_begin, p_loop_end) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const BYTES_PER_FLOAT32 = 4;
		const sid = OrblitRuntime.parseString(p_id);
		const loopMode = OrblitRuntime.parseString(p_loop_mode);
		const numberOfChannels = 2;
		const sampleRate = OrblitAudio.ctx.sampleRate;

		// AudioBuffer Cant's copy from a SharedArrayBuffer-backed view.
		const sliceOrSub = HEAPF32.buffer instanceof ArrayBuffer ? OrblitRuntime.heapSub : OrblitRuntime.heapSlice;
		const subLeft = sliceOrSub(HEAPF32, p_frames, p_frames_count);
		const subRight = sliceOrSub(HEAPF32, p_frames + p_frames_count * BYTES_PER_FLOAT32, p_frames_count);
		const audioBuffer = OrblitAudio.ctx.createBuffer(numberOfChannels, p_frames_count, sampleRate);
		audioBuffer.copyToChannel(subLeft, 0, 0);
		audioBuffer.copyToChannel(subRight, 1, 0);

		const opts = {
			/** @export */ loopBegin: p_loop_begin,
			/** @export */ loopEnd: p_loop_end,
			/** @export */ loopMode,
		};
		OrblitAudio.sample_driver.createStream(sid, audioBuffer, opts);
	},

	orblit_audio_sample_unregister_stream__proxy: 'sync',
	orblit_audio_sample_unregister_stream__sig: 'vi',
	orblit_audio_sample_unregister_stream: function (p_id) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.removeStream(OrblitRuntime.parseString(p_id));
	},

	orblit_audio_sample_start__proxy: 'sync',
	orblit_audio_sample_start__sig: 'viiiifi',
	orblit_audio_sample_start: function (p_playback, p_stream, p_bus, p_offset, p_pitch_scale, p_volume) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const sid = OrblitRuntime.parseString(p_stream);
		if (!OrblitAudio.sample_driver.getStream(sid)) {
			return;
		}
		const pid = OrblitRuntime.parseString(p_playback);
		OrblitAudio.sample_driver.removePlayback(pid);
		const playback = OrblitAudio.sample_driver.createPlayback(pid, sid);
		const volume = OrblitRuntime.heapSlice(HEAPF32, p_volume, 8); // Slice (copy) since it will be stored.
		playback.setVolumes([p_bus], volume);
		playback.setPitchScale(p_pitch_scale);
		playback.start(p_offset);
	},

	orblit_audio_sample_stop__proxy: 'sync',
	orblit_audio_sample_stop__sig: 'vi',
	orblit_audio_sample_stop: function (p_id) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.removePlayback(OrblitRuntime.parseString(p_id));
	},

	orblit_audio_sample_set_pause__proxy: 'sync',
	orblit_audio_sample_set_pause__sig: 'vii',
	orblit_audio_sample_set_pause: function (p_id, p_pause) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const playback = OrblitAudio.sample_driver.getPlayback(OrblitRuntime.parseString(p_id));
		if (playback) {
			playback.pause(Boolean(p_pause));
		}
	},

	orblit_audio_sample_is_active__proxy: 'sync',
	orblit_audio_sample_is_active__sig: 'ii',
	orblit_audio_sample_is_active: function (p_id) {
		if (!OrblitAudio.sample_driver) {
			return 0;
		}
		return OrblitAudio.sample_driver.getPlayback(OrblitRuntime.parseString(p_id)) ? 1 : 0;
	},

	orblit_audio_get_sample_playback_position__proxy: 'sync',
	orblit_audio_get_sample_playback_position__sig: 'di',
	orblit_audio_get_sample_playback_position: function (p_id) {
		if (!OrblitAudio.sample_driver) {
			return 0;
		}
		const playback = OrblitAudio.sample_driver.getPlayback(OrblitRuntime.parseString(p_id));
		return playback ? playback.getPlaybackPosition() : 0;
	},

	orblit_audio_sample_update_pitch_scale__proxy: 'sync',
	orblit_audio_sample_update_pitch_scale__sig: 'vii',
	orblit_audio_sample_update_pitch_scale: function (p_id, p_pitch_scale) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const playback = OrblitAudio.sample_driver.getPlayback(OrblitRuntime.parseString(p_id));
		if (playback) {
			playback.setPitchScale(p_pitch_scale);
		}
	},

	orblit_audio_sample_set_volumes_linear__proxy: 'sync',
	orblit_audio_sample_set_volumes_linear__sig: 'viiiii',
	orblit_audio_sample_set_volumes_linear: function (p_id, p_buses, p_buses_size, p_volume, p_volume_size) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const playback = OrblitAudio.sample_driver.getPlayback(OrblitRuntime.parseString(p_id));
		if (!playback) {
			return;
		}
		const busesIndexes = Array.from(OrblitRuntime.heapSub(HEAP32, p_buses, p_buses_size));
		const volumes = OrblitRuntime.heapSlice(HEAPF32, p_volume, p_volume_size); // Slice (copy) since it will be stored.
		playback.setVolumes(busesIndexes, volumes);
	},

	orblit_audio_sample_bus_set_count__proxy: 'sync',
	orblit_audio_sample_bus_set_count__sig: 'vi',
	orblit_audio_sample_bus_set_count: function (p_count) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.setBusCount(p_count);
	},

	orblit_audio_sample_bus_remove__proxy: 'sync',
	orblit_audio_sample_bus_remove__sig: 'vi',
	orblit_audio_sample_bus_remove: function (p_index) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.removeBus(p_index);
	},

	orblit_audio_sample_bus_add__proxy: 'sync',
	orblit_audio_sample_bus_add__sig: 'vi',
	orblit_audio_sample_bus_add: function (p_index) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.addBusAt(p_index);
	},

	orblit_audio_sample_bus_move__proxy: 'sync',
	orblit_audio_sample_bus_move__sig: 'vii',
	orblit_audio_sample_bus_move: function (p_from, p_to) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.moveBus(p_from, p_to);
	},

	orblit_audio_sample_bus_set_send__proxy: 'sync',
	orblit_audio_sample_bus_set_send__sig: 'vii',
	orblit_audio_sample_bus_set_send: function (p_index, p_send_index) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const bus = OrblitAudio.sample_driver.getBus(p_index);
		if (!bus) {
			return;
		}
		let target = OrblitAudio.sample_driver.getBus(p_send_index);
		if (target == null) { // Send to master.
			target = OrblitAudio.sample_driver.getBus(0);
		}
		bus.setSend(target);
	},

	orblit_audio_sample_bus_set_volume_db__proxy: 'sync',
	orblit_audio_sample_bus_set_volume_db__sig: 'vii',
	orblit_audio_sample_bus_set_volume_db: function (p_bus, p_volume_db) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const bus = OrblitAudio.sample_driver.getBus(p_bus);
		if (!bus) {
			return;
		}
		bus.setVolumeDb(p_volume_db);
	},

	orblit_audio_sample_bus_set_solo__proxy: 'sync',
	orblit_audio_sample_bus_set_solo__sig: 'vii',
	orblit_audio_sample_bus_set_solo: function (p_bus, p_enable) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.setBusSolo(p_bus, p_enable);
	},

	orblit_audio_sample_bus_set_mute__proxy: 'sync',
	orblit_audio_sample_bus_set_mute__sig: 'vii',
	orblit_audio_sample_bus_set_mute: function (p_bus, p_enable) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		const bus = OrblitAudio.sample_driver.getBus(p_bus);
		if (!bus) {
			return;
		}
		bus.setMute(Boolean(p_enable));
	},

	orblit_audio_sample_set_finished_callback__proxy: 'sync',
	orblit_audio_sample_set_finished_callback__sig: 'vi',
	orblit_audio_sample_set_finished_callback: function (p_callback) {
		if (!OrblitAudio.sample_driver) {
			return;
		}
		OrblitAudio.sample_driver.setFinishedCallback(OrblitRuntime.get_func(p_callback));
	},
};

autoAddDeps(_OrblitAudioSampleDriver, '$OrblitAudioSampleDriver');
mergeInto(LibraryManager.library, _OrblitAudioSampleDriver);

/**
 * The AudioWorklet API driver, used when threads are available.
 */
const OrblitAudioWorklet = {
	$OrblitAudioWorklet__deps: ['$OrblitAudio', '$OrblitConfig'],
	$OrblitAudioWorklet: {
		promise: null,
		worklet: null,
		ring_buffer: null,

		create: function (channels) {
			const path = OrblitConfig.locate_file('orblit.audio.worklet.js');
			OrblitAudioWorklet.promise = OrblitAudio.ctx.audioWorklet
				.addModule(path)
				.then(function () {
					OrblitAudioWorklet.worklet = new AudioWorkletNode(
						OrblitAudio.ctx,
						'orblit-processor',
						{
							outputChannelCount: [channels],
						}
					);
					return Promise.resolve();
				});
			OrblitAudio.driver = OrblitAudioWorklet;
		},

		start: function (in_buf, out_buf, state) {
			OrblitAudioWorklet.promise.then(function () {
				const node = OrblitAudioWorklet.worklet;
				node.connect(OrblitAudio.ctx.destination);
				node.port.postMessage({
					'cmd': 'start',
					'data': [state, in_buf, out_buf],
				});
				node.port.onmessage = function (event) {
					OrblitRuntime.error(event.data);
				};
			});
		},

		start_no_threads: function (
			p_out_buf,
			p_out_size,
			out_callback,
			p_in_buf,
			p_in_size,
			in_callback
		) {
			/** @constructor */
			function RingBuffer() {
				let wpos = 0;
				let rpos = 0;
				let pending_samples = 0;
				const wbuf = new Float32Array(p_out_size);

				function send(port) {
					if (pending_samples === 0) {
						return;
					}
					const buffer = OrblitRuntime.heapSub(HEAPF32, p_out_buf, p_out_size);
					const size = buffer.length;
					const tot_sent = pending_samples;
					out_callback(wpos, pending_samples);
					if (wpos + pending_samples >= size) {
						const high = size - wpos;
						wbuf.set(buffer.subarray(wpos, size));
						pending_samples -= high;
						wpos = 0;
					}
					if (pending_samples > 0) {
						wbuf.set(
							buffer.subarray(wpos, wpos + pending_samples),
							tot_sent - pending_samples
						);
					}
					port.postMessage({ 'cmd': 'chunk', 'data': wbuf.subarray(0, tot_sent) });
					wpos += pending_samples;
					pending_samples = 0;
				}
				this.receive = function (recv_buf) {
					const buffer = OrblitRuntime.heapSub(HEAPF32, p_in_buf, p_in_size);
					const from = rpos;
					let to_write = recv_buf.length;
					let high = 0;
					if (rpos + to_write >= p_in_size) {
						high = p_in_size - rpos;
						buffer.set(recv_buf.subarray(0, high), rpos);
						to_write -= high;
						rpos = 0;
					}
					if (to_write) {
						buffer.set(recv_buf.subarray(high, to_write), rpos);
					}
					in_callback(from, recv_buf.length);
					rpos += to_write;
				};
				this.consumed = function (size, port) {
					pending_samples += size;
					send(port);
				};
			}
			OrblitAudioWorklet.ring_buffer = new RingBuffer();
			OrblitAudioWorklet.promise.then(function () {
				const node = OrblitAudioWorklet.worklet;
				const buffer = OrblitRuntime.heapSlice(HEAPF32, p_out_buf, p_out_size);
				node.connect(OrblitAudio.ctx.destination);
				node.port.postMessage({
					'cmd': 'start_nothreads',
					'data': [buffer, p_in_size],
				});
				node.port.onmessage = function (event) {
					if (!OrblitAudioWorklet.worklet) {
						return;
					}
					if (event.data['cmd'] === 'read') {
						const read = event.data['data'];
						OrblitAudioWorklet.ring_buffer.consumed(
							read,
							OrblitAudioWorklet.worklet.port
						);
					} else if (event.data['cmd'] === 'input') {
						const buf = event.data['data'];
						if (buf.length > p_in_size) {
							OrblitRuntime.error('Input chunk is too big');
							return;
						}
						OrblitAudioWorklet.ring_buffer.receive(buf);
					} else {
						OrblitRuntime.error(event.data);
					}
				};
			});
		},

		get_node: function () {
			return OrblitAudioWorklet.worklet;
		},

		close: function () {
			return new Promise(function (resolve, reject) {
				if (OrblitAudioWorklet.promise === null) {
					return;
				}
				const p = OrblitAudioWorklet.promise;
				p.then(function () {
					OrblitAudioWorklet.worklet.port.postMessage({
						'cmd': 'stop',
						'data': null,
					});
					OrblitAudioWorklet.worklet.disconnect();
					OrblitAudioWorklet.worklet.port.onmessage = null;
					OrblitAudioWorklet.worklet = null;
					OrblitAudioWorklet.promise = null;
					resolve();
				}).catch(function (err) {
					// Aborted?
					OrblitRuntime.error(err);
				});
			});
		},
	},

	orblit_audio_worklet_create__proxy: 'sync',
	orblit_audio_worklet_create__sig: 'ii',
	orblit_audio_worklet_create: function (channels) {
		try {
			OrblitAudioWorklet.create(channels);
		} catch (e) {
			OrblitRuntime.error('Error starting AudioDriverWorklet', e);
			return 1;
		}
		return 0;
	},

	orblit_audio_worklet_start__proxy: 'sync',
	orblit_audio_worklet_start__sig: 'viiiii',
	orblit_audio_worklet_start: function (
		p_in_buf,
		p_in_size,
		p_out_buf,
		p_out_size,
		p_state
	) {
		const out_buffer = OrblitRuntime.heapSub(HEAPF32, p_out_buf, p_out_size);
		const in_buffer = OrblitRuntime.heapSub(HEAPF32, p_in_buf, p_in_size);
		const state = OrblitRuntime.heapSub(HEAP32, p_state, 4);
		OrblitAudioWorklet.start(in_buffer, out_buffer, state);
	},

	orblit_audio_worklet_start_no_threads__proxy: 'sync',
	orblit_audio_worklet_start_no_threads__sig: 'viiiiii',
	orblit_audio_worklet_start_no_threads: function (
		p_out_buf,
		p_out_size,
		p_out_callback,
		p_in_buf,
		p_in_size,
		p_in_callback
	) {
		const out_callback = OrblitRuntime.get_func(p_out_callback);
		const in_callback = OrblitRuntime.get_func(p_in_callback);
		OrblitAudioWorklet.start_no_threads(
			p_out_buf,
			p_out_size,
			out_callback,
			p_in_buf,
			p_in_size,
			in_callback
		);
	},

	orblit_audio_worklet_state_wait__sig: 'iiii',
	orblit_audio_worklet_state_wait: function (
		p_state,
		p_idx,
		p_expected,
		p_timeout
	) {
		Atomics.wait(HEAP32, (p_state >> 2) + p_idx, p_expected, p_timeout);
		return Atomics.load(HEAP32, (p_state >> 2) + p_idx);
	},

	orblit_audio_worklet_state_add__sig: 'iiii',
	orblit_audio_worklet_state_add: function (p_state, p_idx, p_value) {
		return Atomics.add(HEAP32, (p_state >> 2) + p_idx, p_value);
	},

	orblit_audio_worklet_state_get__sig: 'iii',
	orblit_audio_worklet_state_get: function (p_state, p_idx) {
		return Atomics.load(HEAP32, (p_state >> 2) + p_idx);
	},
};

autoAddDeps(OrblitAudioWorklet, '$OrblitAudioWorklet');
mergeInto(LibraryManager.library, OrblitAudioWorklet);

/*
 * The ScriptProcessorNode API, used as a fallback if AudioWorklet is not available.
 */
const OrblitAudioScript = {
	$OrblitAudioScript__deps: ['$OrblitAudio'],
	$OrblitAudioScript: {
		script: null,

		create: function (buffer_length, channel_count) {
			OrblitAudioScript.script = OrblitAudio.ctx.createScriptProcessor(
				buffer_length,
				2,
				channel_count
			);
			OrblitAudio.driver = OrblitAudioScript;
			return OrblitAudioScript.script.bufferSize;
		},

		start: function (p_in_buf, p_in_size, p_out_buf, p_out_size, onprocess) {
			OrblitAudioScript.script.onaudioprocess = function (event) {
				// Read input
				const inb = OrblitRuntime.heapSub(HEAPF32, p_in_buf, p_in_size);
				const input = event.inputBuffer;
				if (OrblitAudio.input) {
					const inlen = input.getChannelData(0).length;
					for (let ch = 0; ch < 2; ch++) {
						const data = input.getChannelData(ch);
						for (let s = 0; s < inlen; s++) {
							inb[s * 2 + ch] = data[s];
						}
					}
				}

				// Let Orblit process the input/output.
				onprocess();

				// Write the output.
				const outb = OrblitRuntime.heapSub(HEAPF32, p_out_buf, p_out_size);
				const output = event.outputBuffer;
				const channels = output.numberOfChannels;
				for (let ch = 0; ch < channels; ch++) {
					const data = output.getChannelData(ch);
					// Loop through samples and assign computed values.
					for (let sample = 0; sample < data.length; sample++) {
						data[sample] = outb[sample * channels + ch];
					}
				}
			};
			OrblitAudioScript.script.connect(OrblitAudio.ctx.destination);
		},

		get_node: function () {
			return OrblitAudioScript.script;
		},

		close: function () {
			return new Promise(function (resolve, reject) {
				OrblitAudioScript.script.disconnect();
				OrblitAudioScript.script.onaudioprocess = function () {};
				OrblitAudioScript.script = null;
				resolve();
			});
		},
	},

	orblit_audio_script_create__proxy: 'sync',
	orblit_audio_script_create__sig: 'iii',
	orblit_audio_script_create: function (buffer_length, channel_count) {
		const buf_len = OrblitRuntime.getHeapValue(buffer_length, 'i32');
		try {
			const out_len = OrblitAudioScript.create(buf_len, channel_count);
			OrblitRuntime.setHeapValue(buffer_length, out_len, 'i32');
		} catch (e) {
			OrblitRuntime.error('Error starting AudioDriverScriptProcessor', e);
			return 1;
		}
		return 0;
	},

	orblit_audio_script_start__proxy: 'sync',
	orblit_audio_script_start__sig: 'viiiii',
	orblit_audio_script_start: function (
		p_in_buf,
		p_in_size,
		p_out_buf,
		p_out_size,
		p_cb
	) {
		const onprocess = OrblitRuntime.get_func(p_cb);
		OrblitAudioScript.start(
			p_in_buf,
			p_in_size,
			p_out_buf,
			p_out_size,
			onprocess
		);
	},
};

autoAddDeps(OrblitAudioScript, '$OrblitAudioScript');
mergeInto(LibraryManager.library, OrblitAudioScript);
