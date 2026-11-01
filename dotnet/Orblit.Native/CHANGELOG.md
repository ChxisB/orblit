# Changelog

## 0.1.1

`orblit_script.h` marks the five functions the host looks up with
`ORBLIT_EXPORT`. A script built on Windows exported none of them, so
the host refused it as having no `orblit_script_abi`.

## 0.1.0

Port of `orblit_native` 0.2.0. A script's library is opened
and closed with `NativeLibrary`, which is `dlopen` and `LoadLibraryW` behind
one call. `NativeLibrary` passes
the runtime's own `dlopen` flags, not `RTLD_NOW | RTLD_LOCAL`.

The headers a script compiles against are found in `ORBLIT_INCLUDE`, then in
the `include` folder beside the assembly.

`ScriptRunner.Watch` rebuilds on a worker thread, so the runner takes a lock.
`ScriptError` is `ScriptException`. `ScriptValues` is `IScriptValues`.
