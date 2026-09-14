#ifndef ORBLIT_FILAMENT_PLUGIN_H_
#define ORBLIT_FILAMENT_PLUGIN_H_

// The plugin's whole public surface: one function, called once, by the
// registrant Flutter generates into the application.
//
// This header exists because of how Flutter's Windows tooling wires a plugin
// up. `pluginClass: OrblitFilamentPlugin` in pubspec.yaml makes the tool write,
// into the runner's generated_plugin_registrant.cc,
//
//     #include <orblit_filament/orblit_filament_plugin.h>
//     OrblitFilamentPluginRegisterWithRegistrar(
//         registry->GetRegistrarForPlugin("OrblitFilamentPlugin"));
//
// so the include path and the symbol are both derived from that one line and
// neither is ours to choose: the tool snake-cases the plugin class to get the
// header's name (`_filenameForCppClass` in flutter_tools) and appends
// `RegisterWithRegistrar` to the class itself to get the symbol. That is the
// one naming difference from linux/, whose registrant calls a fully
// snake-cased C function instead; the pubspec key is the same word on both.
//
// Everything else the plugin does is private to windows/ and reached through
// the `orblit_filament` method channel, exactly as on the other four platforms.

#include <flutter_plugin_registrar.h>

// Set by the plugin's own build (see CMakeLists.txt) and not by whoever
// includes this, which is how the registrant gets the import side of the
// declaration rather than the export side.
#ifdef FLUTTER_PLUGIN_IMPL
#define FLUTTER_PLUGIN_EXPORT __declspec(dllexport)
#else
#define FLUTTER_PLUGIN_EXPORT __declspec(dllimport)
#endif

#if defined(__cplusplus)
extern "C" {
#endif

FLUTTER_PLUGIN_EXPORT void OrblitFilamentPluginRegisterWithRegistrar(
    FlutterDesktopPluginRegistrarRef registrar);

#if defined(__cplusplus)
}  // extern "C"
#endif

#endif  // ORBLIT_FILAMENT_PLUGIN_H_
