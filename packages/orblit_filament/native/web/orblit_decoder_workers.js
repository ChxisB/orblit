// Decodes textures and environment pictures on Web Workers, for a browser
// build without threads.
//
// Natively the texture queue decodes on threads of its own and an
// environment picture is prepared on one (OrblitTextures.cpp,
// OrblitEnvironment.cpp). This build has none, so all of it ran on the page:
// in Chrome on an M4 Pro, twelve 2048² PNGs and JPEGs cost the page 850 ms of
// decoding, 130 ms at a time, and one 2K .hdr a single 200 ms task. So the
// work goes to workers, each running the decoder module — a small
// WebAssembly module of its own (orblit_decoder_module.cpp) holding the same
// readers the renderer links, and nothing of Filament.
//
// Handed to emcc as --pre-js by build.sh, after the file build.sh generates
// that sets Module.orblitDecoderSource: the decoder module's JavaScript as
// text, and its .wasm gzipped and in base64, so a page still serves
// orblit_renderer.js and .wasm and no third file. A worker's program is that
// text followed by the function below turned back into source, started from
// a Blob as the splat sorter's is (orblit_splat_worker.js).
//
// The renderer reaches this through Module.orblitDecoders, from
// OrblitDecodersWeb.cpp. It hands a job over only when a worker is idle, so a
// job not picked up within half a second means a worker that is not running;
// the renderer then decodes that job itself and stops handing jobs over until
// some worker answers again. Nothing here ever makes the page wait.
Module['orblitDecoders'] = (() => {
  'use strict';

  // The worker's whole program, after the decoder module's own JavaScript.
  function decodingWorker() {
    let wasmSource = null;
    let loading = null;

    async function load() {
      // Gzipped by build.sh so the embedded text is a third the size; the
      // browser's own gunzip takes it back.
      const text = atob(wasmSource);
      const gzipped = new Uint8Array(text.length);
      for (let i = 0; i < text.length; i++) gzipped[i] = text.charCodeAt(i);
      const stream = new Blob([gzipped]).stream()
        .pipeThrough(new DecompressionStream('gzip'));
      const wasm = new Uint8Array(await new Response(stream).arrayBuffer());
      // eslint-disable-next-line no-undef
      return OrblitDecoderModule({
        wasmBinary: wasm,
        print: (text) => console.log(text),
        printErr: (text) => console.warn(text),
      });
    }

    function run(decoder, job) {
      const input = decoder._malloc(Math.max(1, job.bytes.byteLength));
      decoder.HEAPU8.set(new Uint8Array(job.bytes), input);
      const parameters = decoder._malloc(Math.max(1, job.parameters.length) * 8);
      decoder.HEAPF64.set(job.parameters, parameters >>> 3);
      const nameRoom = decoder.lengthBytesUTF8(job.name) + 1;
      const name = decoder._malloc(nameRoom);
      decoder.stringToUTF8(job.name, name, nameRoom);

      const answer = decoder._orblit_decoder_run(
        job.kind, input, job.bytes.byteLength, parameters,
        job.parameters.length, name);
      decoder._free(input);
      decoder._free(parameters);
      decoder._free(name);

      const note = decoder.UTF8ToString(decoder._orblit_decoder_note(answer));
      const parts = [];
      for (let i = 0, n = decoder._orblit_decoder_part_count(answer); i < n; i++) {
        const at = decoder._orblit_decoder_part(answer, i) >>> 0;
        const size = decoder._orblit_decoder_part_size(answer, i) >>> 0;
        parts.push(decoder.HEAPU8.slice(at, at + size));
      }
      const count = decoder._orblit_decoder_number_count(answer);
      const at = decoder._orblit_decoder_numbers(answer) >>> 3;
      const numbers = decoder.HEAPF64.slice(at, at + count);
      const milliseconds = decoder._orblit_decoder_milliseconds(answer);
      decoder._orblit_decoder_free(answer);
      return { id: job.id, note, parts, numbers, milliseconds };
    }

    self.onmessage = async (event) => {
      const job = event.data;
      if (job.wasm !== undefined) {
        wasmSource = job.wasm;
        return;
      }
      // Said as soon as the job arrives: the page's patience is for a worker
      // that is not running at all, not for one that is busy loading.
      self.postMessage({ id: job.id, started: true });
      let decoder;
      try {
        decoder = await (loading = loading || load());
      } catch (error) {
        loading = null;
        self.postMessage({ id: job.id, broken: String(error) });
        return;
      }
      try {
        const answer = run(decoder, job);
        self.postMessage(answer, [answer.numbers.buffer, ...answer.parts.map((p) => p.buffer)]);
      } catch (error) {
        // A module that aborted is not used again; the next job loads a
        // fresh one.
        loading = null;
        self.postMessage({ id: job.id, failed: String(error) });
      }
    };
  }

  const source = Module['orblitDecoderSource'];

  // Answers to poll(), as OrblitDecodersWeb.cpp reads them.
  const WAITING = 0;
  const STARTED = 1;
  const DONE = 2;
  const FAILED = 3;

  // A worker left idle this long is let go, with the memory its module grew
  // to: one environment picture can leave a few hundred megabytes behind.
  const IDLE_MILLISECONDS = 15000;

  // Half the machine, at most four, as the native queue takes half of it.
  const most = Math.max(1, Math.min(4,
    Math.floor((globalThis.navigator?.hardwareConcurrency || 2) / 2)));

  const pool = [];
  const jobs = new Map();
  let next = 1;
  let url = null;
  // No worker will ever load the decoder here: none can be made, or the
  // module failed to start.
  let broken = !source || typeof Worker === 'undefined' ||
    typeof Blob === 'undefined' || typeof URL === 'undefined' ||
    typeof DecompressionStream === 'undefined';
  // A job was given up on: not picked up in time, or not finished. Cleared
  // by the next answer from any worker, so a worker that was only late gets
  // jobs again, and one that picks jobs up and never finishes them does not
  // take every job with it one bound at a time.
  let stalled = false;
  let warnedStalled = false;
  const counts = { submitted: 0, answered: 0, failed: 0, givenUp: 0 };

  function heard() {
    stalled = false;
  }

  function release(worker) {
    worker.job = 0;
    clearTimeout(worker.idleTimer);
    worker.idleTimer = setTimeout(() => {
      if (worker.job !== 0) return;
      worker.thread.terminate();
      const at = pool.indexOf(worker);
      if (at >= 0) pool.splice(at, 1);
    }, IDLE_MILLISECONDS);
  }

  function retire(worker) {
    clearTimeout(worker.idleTimer);
    worker.thread.terminate();
    const at = pool.indexOf(worker);
    if (at >= 0) pool.splice(at, 1);
  }

  function spawn() {
    if (url === null) {
      url = URL.createObjectURL(new Blob(
        [source.program, '\n;(' + decodingWorker.toString() + ')();'],
        { type: 'text/javascript' },
      ));
    }
    let thread;
    try {
      thread = new Worker(url);
    } catch (error) {
      // A content security policy that refuses a blob: script, most likely.
      console.warn('[orblit] decoders: no decoding worker: ' + error);
      broken = true;
      return null;
    }
    const worker = { thread, job: 0, idleTimer: 0, everAnswered: false };
    thread.onmessage = (event) => {
      const message = event.data;
      worker.everAnswered = true;
      const job = jobs.get(message.id);
      if (message.started) {
        if (job && job.state === WAITING) {
          job.state = STARTED;
          job.startedAt = performance.now();
        }
        return;
      }
      heard();
      if (worker.job === message.id) release(worker);
      if (message.broken !== undefined) {
        if (!broken) {
          console.warn('[orblit] decoders: the decoder module would not start: ' +
            message.broken + '; decoding on the page instead');
        }
        broken = true;
      }
      if (!job) return;
      if (message.broken !== undefined || message.failed !== undefined) {
        if (message.failed !== undefined) {
          console.warn('[orblit] decoders: a job failed on its worker: ' + message.failed);
        }
        job.state = FAILED;
        counts.failed++;
        return;
      }
      job.state = DONE;
      job.answer = message;
      counts.answered++;
    };
    thread.onerror = (event) => {
      // Uncaught, so the worker's program itself failed: a script refused or
      // a module that aborted outside a job. Its job is decoded on the page.
      console.warn('[orblit] decoders: a decoding worker failed: ' +
        (event.message || 'no reason given'));
      if (!worker.everAnswered) broken = true;
      const job = jobs.get(worker.job);
      if (job) job.state = FAILED;
      retire(worker);
    };
    thread.postMessage({ wasm: source.wasm });
    pool.push(worker);
    return worker;
  }

  return {
    // How many jobs a worker could start now, or -1 when none will be used.
    capacity() {
      if (broken || stalled) return -1;
      let idle = most - pool.length;
      for (const worker of pool) if (worker.job === 0) idle++;
      return idle;
    },

    // Hands a job over; its bytes are a copy, transferred. Nought when no
    // worker could take it.
    submit(kind, bytes, parameters, name) {
      if (broken || stalled) return 0;
      let worker = pool.find((w) => w.job === 0);
      if (!worker && pool.length < most) worker = spawn();
      if (!worker) return 0;
      clearTimeout(worker.idleTimer);
      const id = next++;
      worker.job = id;
      jobs.set(id, { worker, state: WAITING, at: performance.now(), startedAt: 0, answer: null });
      worker.thread.postMessage(
        { id, kind, bytes: bytes.buffer, parameters, name },
        [bytes.buffer, parameters.buffer],
      );
      counts.submitted++;
      return id;
    },

    // Where a job stands, after giving up on it if it has waited too long.
    poll(id, startPatience, runPatience) {
      const job = jobs.get(id);
      if (!job) return FAILED;
      const now = performance.now();
      if (job.state === WAITING && now - job.at > startPatience) {
        if (!warnedStalled) {
          console.warn('[orblit] decoders: a decoding worker has not started a job in ' +
            startPatience + ' ms; decoding on the page until one answers');
          warnedStalled = true;
        }
        stalled = true;
        job.state = FAILED;
        counts.givenUp++;
      } else if (job.state === STARTED && now - job.startedAt > runPatience) {
        console.warn('[orblit] decoders: a decoding worker has not finished a job in ' +
          runPatience + ' ms; stopping it and decoding on the page until one answers');
        stalled = true;
        retire(job.worker);
        job.state = FAILED;
        counts.givenUp++;
      }
      return job.state;
    },

    partCount(id) {
      const job = jobs.get(id);
      return job && job.answer ? job.answer.parts.length : 0;
    },

    partSize(id, index) {
      return jobs.get(id).answer.parts[index].byteLength;
    },

    numberCount(id) {
      const job = jobs.get(id);
      return job && job.answer ? job.answer.numbers.length : 0;
    },

    note(id) {
      const job = jobs.get(id);
      return job && job.answer ? job.answer.note : '';
    },

    milliseconds(id) {
      const job = jobs.get(id);
      return job && job.answer ? job.answer.milliseconds : 0;
    },

    // Copies the answer into the renderer's memory: each part to the address
    // at `pointers[index]`, the numbers from `numbersAt`, a word index into
    // HEAPF64.
    copy(id, heapU8, heapU32, pointersAt, heapF64, numbersAt) {
      const answer = jobs.get(id).answer;
      for (let i = 0; i < answer.parts.length; i++) {
        heapU8.set(answer.parts[i], heapU32[pointersAt + i]);
      }
      heapF64.set(answer.numbers, numbersAt);
    },

    // Forgets a job. One still running finishes, and its answer is dropped.
    forget(id) {
      jobs.delete(id);
    },

    counts() {
      return Object.assign({ workers: pool.length, broken, stalled }, counts);
    },
  };
})();
