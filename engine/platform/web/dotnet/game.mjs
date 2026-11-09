// Connects a C# game running in .NET wasm to the engine running in its own
// wasm module. The engine side is web_game_bridge.cpp. The managed side is
// Orblit.Web. Call attachDotnetGame after the engine has started.

const RECORD_BYTES = 16; // Same as EntityPosition in web_game_bridge.cpp.

// The engine heap can grow on any engine call, which detaches an older
// HEAPU8 view. Take the view from the runtime each time it is used.
function createScratch(rt) {
	let pointer = 0;
	let capacity = 0;
	return (length) => {
		if (length > capacity) {
			rt._free(pointer);
			pointer = rt._malloc(length);
			capacity = length;
		}
		return pointer;
	};
}

function createEngineImports(rt) {
	const encoder = new TextEncoder();
	const scratchFor = createScratch(rt);
	return {
		bindEntity: (path) => {
			const bytes = encoder.encode(path);
			const pointer = rt._malloc(bytes.length + 1);
			rt.HEAPU8.set(bytes, pointer);
			rt.HEAPU8[pointer + bytes.length] = 0;
			const handle = rt._orblit_game_bind_entity(pointer);
			rt._free(pointer);
			return handle;
		},
		releaseEntity: (handle) => rt._orblit_game_release_entity(handle),
		setPositions: (view) => {
			const pointer = scratchFor(view.byteLength);
			view.copyTo(rt.HEAPU8.subarray(pointer, pointer + view.byteLength));
			rt._orblit_game_set_positions(pointer, view.byteLength / RECORD_BYTES);
		},
		getPositions: (view) => {
			const pointer = scratchFor(view.byteLength);
			view.copyTo(rt.HEAPU8.subarray(pointer, pointer + view.byteLength));
			rt._orblit_game_get_positions(pointer, view.byteLength / RECORD_BYTES);
			view.set(rt.HEAPU8.subarray(pointer, pointer + view.byteLength));
		},
	};
}

// rt is engine.rtenv. The export step puts the published game in _framework,
// next to this file.
export async function attachDotnetGame(rt) {
	const { dotnet } = await import(new URL('./_framework/dotnet.js', import.meta.url).href);
	const runtime = await dotnet.create();
	runtime.setModuleImports('orblit-engine', createEngineImports(rt));
	await runtime.runMain();
	const exports = await runtime.getAssemblyExports('Orblit.Web');
	const host = exports.Orblit.Web.WebHost;
	host.Start();
	rt.orblitGameFrame = (delta, frame) => host.Frame(delta, frame);
	rt.orblitScriptEvent = (kind, handle, className, x, y, z) => host.ScriptEvent(kind, handle, className, x, y, z);
	rt._orblit_game_start();
}
