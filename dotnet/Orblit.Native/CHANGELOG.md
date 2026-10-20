# Changelog

## 0.1.0

Port of `orblit_native` 0.2.0, the Dart package. A script's library is opened
and closed with `NativeLibrary`, which is `dlopen` and `LoadLibraryW` behind
one call, so the Dart package's two loaders are gone. `NativeLibrary` passes
the runtime's own `dlopen` flags, not `RTLD_NOW | RTLD_LOCAL`.

The headers a script compiles against are found in `ORBLIT_INCLUDE`, then in
the `include` folder beside the assembly. The Dart package also read them out
of `package_config.json`.

`ScriptRunner.Watch` rebuilds on a worker thread, so the runner takes a lock.
`ScriptError` is `ScriptException`. `ScriptValues` is `IScriptValues`.
