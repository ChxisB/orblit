// Sorts Gaussian splats in a Web Worker, for a browser build without threads.
//
// Native builds sort a cloud on a thread of its own (OrblitSplats.cpp), and a
// browser build without -pthread cannot start one: std::thread's constructor
// throws, and the renderer stopped altogether the first time a scene held a
// cloud. Sorting on the page's own thread instead would work and would cost
// every frame the camera turns tens of milliseconds at a few hundred thousand
// splats, which is the hitch sorting off the render thread exists to avoid.
//
// So the sort runs here. The positions cross once, when the cloud is made;
// each sort sends the camera across, 35 floats, and the finished order comes
// back as a transferred buffer the renderer copies into its own memory on
// whichever frame it is ready. Nothing waits for anything.
//
// Handed to emcc as --pre-js by build.sh, so it runs inside the module's
// factory with `Module` in scope, and OrblitSplatSorterWeb.cpp reaches it
// through Module.orblitSplatWorkers. The worker's program is the function
// below turned back into source text and started from a Blob, so a page
// serves orblit_renderer.js and .wasm and no third file.
//
// The sort is OrblitSplats.cpp's sortSplats written again: the same culling
// test, the same keys, the same stable radix passes. Keep the two in step. The
// arithmetic here is in doubles where C++'s is in floats, so a splat right on
// the culling guard can land either side of it in the two — which is why the
// guard is wider than the shader's, and why only native output is held to
// being identical to the picture before culling.
Module['orblitSplatWorkers'] = (() => {
  'use strict';

  // The worker's whole program. Nothing outside it is in scope once it runs.
  function sortingWorker() {
    // How far past the screen's edge, in clip space, a splat's centre can be
    // and still be kept. kSplatCullGuard in OrblitSplats.h.
    const GUARD = 1.5;

    let positions = null;
    let count = 0;
    let scratch = null;
    let depths = null;
    const counts = new Uint32Array(4 * 256);
    const offsets = new Uint32Array(256);
    const float = new Float32Array(1);
    const bits = new Uint32Array(float.buffer);

    self.onmessage = (event) => {
      const message = event.data;
      if (message.positions) {
        positions = message.positions;
        count = message.count;
        // Keys and ids, twice over, ping-ponged between: the layout
        // sortSplats keeps in its own scratch.
        scratch = new ArrayBuffer(count * 16);
        depths = new Float32Array(count);
        return;
      }
      const started = performance.now();
      const order = sort(message.numbers, message.flags);
      self.postMessage(
        { order, milliseconds: performance.now() - started },
        [order.buffer],
      );
    };

    function sort(n, flags) {
      const cull = (flags & 1) !== 0;
      const coarse = (flags & 2) !== 0;
      const dx = n[0], dy = n[1], dz = n[2];
      // The view matrix starts at 3 and the clip matrix at 19, both
      // column-major, so row r of column c is at start + c * 4 + r.
      const v2 = n[5], v6 = n[9], v10 = n[13], v14 = n[17];
      const c0 = n[19], c1 = n[20], c3 = n[22];
      const c4 = n[23], c5 = n[24], c7 = n[26];
      const c8 = n[27], c9 = n[28], c11 = n[30];
      const c12 = n[31], c13 = n[32], c15 = n[34];

      let keys = new Uint32Array(scratch, 0, count);
      let ids = new Uint32Array(scratch, count * 4, count);
      let keysOut = new Uint32Array(scratch, count * 8, count);
      let idsOut = new Uint32Array(scratch, count * 12, count);

      counts.fill(0);
      let kept = 0;
      let nearest = Infinity;
      let farthest = -Infinity;
      for (let s = 0; s < count; s++) {
        const x = positions[s * 3];
        const y = positions[s * 3 + 1];
        const z = positions[s * 3 + 2];
        if (cull) {
          const ahead = -(v2 * x + v6 * y + v10 * z + v14);
          if (!(ahead > 0)) continue;
          const guard = GUARD * (c3 * x + c7 * y + c11 * z + c15);
          if (!(Math.abs(c0 * x + c4 * y + c8 * z + c12) <= guard &&
                Math.abs(c1 * x + c5 * y + c9 * z + c13) <= guard)) {
            continue;
          }
        }
        const depth = Math.fround(x * dx + y * dy + z * dz);
        ids[kept] = s;
        if (coarse) {
          depths[kept] = depth;
          if (depth < nearest) nearest = depth;
          if (depth > farthest) farthest = depth;
        } else {
          // sortableBits, then inverted so ascending is farthest first.
          float[0] = depth;
          let b = bits[0];
          b = (b & 0x80000000) ? ~b : (b | 0x80000000);
          const key = ~b >>> 0;
          keys[kept] = key;
          counts[key & 0xff]++;
          counts[256 + ((key >>> 8) & 0xff)]++;
          counts[512 + ((key >>> 16) & 0xff)]++;
          counts[768 + (key >>> 24)]++;
        }
        kept++;
      }

      let passes = 4;
      if (coarse) {
        passes = 2;
        const span = farthest - nearest;
        const scale = span > 0 ? 65535 / span : 0;
        for (let s = 0; s < kept; s++) {
          const q = (depths[s] - nearest) * scale;
          const level = q >= 0 ? (q < 65535 ? Math.floor(q) : 65535) : 0;
          const key = 65535 - level;
          keys[s] = key;
          counts[key & 0xff]++;
          counts[256 + (key >>> 8)]++;
        }
      }

      for (let pass = 0; pass < passes; pass++) {
        const shift = pass * 8;
        const base = pass * 256;
        // Every key agrees on this byte, so the pass would move nothing.
        if (kept === 0 || counts[base + ((keys[0] >>> shift) & 0xff)] === kept) {
          continue;
        }
        let total = 0;
        for (let b = 0; b < 256; b++) {
          offsets[b] = total;
          total += counts[base + b];
        }
        for (let s = 0; s < kept; s++) {
          const key = keys[s];
          const at = offsets[(key >>> shift) & 0xff]++;
          keysOut[at] = key;
          idsOut[at] = ids[s];
        }
        [keys, keysOut] = [keysOut, keys];
        [ids, idsOut] = [idsOut, ids];
      }
      return ids.slice(0, kept);
    }
  }

  let source = null;
  function sourceUrl() {
    if (source === null) {
      source = URL.createObjectURL(new Blob(
        ['(' + sortingWorker.toString() + ')();'],
        { type: 'text/javascript' },
      ));
    }
    return source;
  }

  const running = new Map();
  let next = 1;

  // Answers to state(), as OrblitSplatSorterWeb.cpp reads them.
  const IDLE = 0;
  const WAITING = 1;
  const FAILED = 2;

  return {
    // A worker holding a copy of `positions`, or 0 when the page will not
    // start one — no Worker at all, or a content security policy that refuses
    // a blob: script. The renderer sorts inline then, and says so.
    start(positions, count) {
      if (typeof Worker === 'undefined' || typeof Blob === 'undefined' ||
          typeof URL === 'undefined') {
        return 0;
      }
      let thread;
      try {
        thread = new Worker(sourceUrl());
      } catch (error) {
        console.warn('[orblit] splats: no sorting worker: ' + error);
        return 0;
      }
      const id = next++;
      const one = { thread, waiting: false, failed: false, order: null, milliseconds: 0 };
      thread.onmessage = (event) => {
        one.waiting = false;
        one.order = event.data.order;
        one.milliseconds = event.data.milliseconds;
      };
      thread.onerror = (event) => {
        one.waiting = false;
        one.failed = true;
        console.warn('[orblit] splats: the sorting worker failed: ' +
          (event.message || 'no reason given'));
      };
      // Copied out of the module's memory, which can grow and move, and
      // handed over rather than cloned a second time.
      const copy = positions.slice();
      thread.postMessage({ positions: copy, count }, [copy.buffer]);
      running.set(id, one);
      return id;
    },

    request(id, numbers, flags) {
      const one = running.get(id);
      if (!one || one.failed) return;
      one.waiting = true;
      one.thread.postMessage({ numbers, flags }, [numbers.buffer]);
    },

    state(id) {
      const one = running.get(id);
      if (!one || one.failed) return FAILED;
      return one.waiting ? WAITING : IDLE;
    },

    // How many splats the finished order has, or -1 when none has arrived.
    ready(id) {
      const one = running.get(id);
      return one && one.order ? one.order.length : -1;
    },

    // Copies the finished order into the module's memory at `at`, a word
    // index, and answers what the sort took.
    take(id, heap, at) {
      const one = running.get(id);
      if (!one || !one.order) return 0;
      heap.set(one.order, at);
      one.order = null;
      return one.milliseconds;
    },

    // Stops waiting on a worker that has taken too long, so that the renderer
    // can sort where it stands and ask again. Its answer is still taken if it
    // turns up later; this only ends the waiting.
    giveUp(id) {
      const one = running.get(id);
      if (one) one.waiting = false;
    },

    stop(id) {
      const one = running.get(id);
      if (!one) return;
      one.thread.terminate();
      running.delete(id);
    },
  };
})();
