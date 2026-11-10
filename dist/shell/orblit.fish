# Fish completion for the Orblit editor
# To use it, install this file in `~/.config/fish/completions` then restart your shell.
# You can also `source` this file directly in your shell startup file.

function orblit_rendering_method_args
    # Use a function instead of a fixed string to customize the argument descriptions.
    echo -e "forward_plus\tHigh-end desktop renderer"
    echo -e "mobile\tHigh-end mobile/desktop renderer"
    echo -e "gl_compatibility\tLow-end desktop, mobile and web renderer"
end

function orblit_rendering_driver_args
    # Use a function instead of a fixed string to customize the argument descriptions.
    echo -e "vulkan\tVulkan renderer"
    echo -e "opengl3\tOpenGL ES 3.0 renderer"
    echo -e "dummy\tDummy renderer"
end

# Erase existing completions for Orblit.
complete -c orblit -e

# General options:
complete -c orblit -s h -l help -d "Display the full help message"
complete -c orblit -l version -d "Display the version string"
complete -c orblit -s v -l verbose -d "Use verbose stdout mode"
complete -c orblit -s q -l quiet -d "Quiet mode, silences stdout messages (errors are still displayed)"

# Run options:
complete -c orblit -s e -l editor -d "Start the editor instead of running the scene"
complete -c orblit -s p -l project-manager -d "Start the project manager, even if a project is auto-detected"
complete -c orblit -l debug-server -d "Start the editor debug server (<protocol>://<host/IP>[:<port>] address)" -x
complete -c orblit -l quit -d "Quit after the first iteration"
complete -c orblit -s l -l language -d "Use a specific locale (<locale> being a two-letter code)" -x
complete -c orblit -l path -d "Path to a project (<directory> must contain a 'project.orblit' file)" -r
complete -c orblit -l main-pack -d "Path to a pack (.opack) file to load" -r
complete -c orblit -l render-thread -d "Set the render thread mode" -x -a "unsafe safe separate"
complete -c orblit -l remote-fs -d "Use a remote filesystem (<host/IP>[:<port>] address)" -x
complete -c orblit -l remote-fs-password -d "Password for remote filesystem" -x
complete -c orblit -l audio-driver -d "Set the audio driver" -x
complete -c orblit -l audio-output-latency -d "Override audio output latency in milliseconds (default is 15 ms)" -x
complete -c orblit -l display-driver -d "Set the display driver" -x
complete -c orblit -l rendering-method -d "Set the renderer" -x -a "(orblit_rendering_method_args)"
complete -c orblit -l rendering-driver -d "Set the rendering driver" -x -a "(orblit_rendering_driver_args)"
complete -c orblit -l gpu-index -d "Use a specific GPU (run with --verbose to get available device list)" -x
complete -c orblit -l text-driver -d "Set the text driver" -x
complete -c orblit -l tablet-driver -d "Set the pen tablet input driver" -x
complete -c orblit -l headless -d "Enable headless mode (--display-driver headless --audio-driver Dummy). Useful for servers and with --script"
complete -c orblit -l log-file -d "Write output/error log to the specified path instead of the default location defined by the project" -x
complete -c orblit -l write-movie -d "Write a video to the specified path (usually with .avi or .png extension). --fixed-fps is forced when enabled" -x

# Display options:
complete -c orblit -s f -l fullscreen -d "Request fullscreen mode"
complete -c orblit -s m -l maximized -d "Request a maximized window"
complete -c orblit -s w -l windowed -d "Request windowed mode"
complete -c orblit -s t -l always-on-top -d "Request an always-on-top window"
complete -c orblit -l resolution -d "Request window resolution" -x
complete -c orblit -l position -d "Request window position" -x
complete -c orblit -l single-window -d "Use a single window (no separate subwindows)"
complete -c orblit -l xr-mode -d "Select Extended Reality (XR) mode" -a "default off on"

# Debug options:
complete -c orblit -s d -l debug -d "Debug (local stdout debugger)"
complete -c orblit -s b -l breakpoints -d "Specify the breakpoint list as source::line comma-separated pairs, no spaces (use %20 instead)" -x
complete -c orblit -l profiling -d "Enable profiling in the script debugger"
complete -c orblit -l gpu-profile -d "Show a GPU profile of the tasks that took the most time during frame rendering"
complete -c orblit -l gpu-validation -d "Enable graphics API validation layers for debugging"
complete -c orblit -l gpu-abort -d "Abort on graphics API usage errors (usually validation layer errors)"
complete -c orblit -l remote-debug -d "Enable remote debugging"
complete -c orblit -l debug-collisions -d "Show collision shapes when running the scene"
complete -c orblit -l debug-navigation -d "Show navigation polygons when running the scene"
complete -c orblit -l debug-stringnames -d "Print all StringName allocations to stdout when the engine quits"
complete -c orblit -l max-fps -d "Set a maximum number of frames per second rendered (can be used to limit power usage), a value of 0 results in unlimited framerate" -x
complete -c orblit -l frame-delay -d "Simulate high CPU load (delay each frame by the given number of milliseconds)" -x
complete -c orblit -l time-scale -d "Force time scale (higher values are faster, 1.0 is normal speed)" -x
complete -c orblit -l disable-render-loop -d "Disable render loop so rendering only occurs when called explicitly from script"
complete -c orblit -l disable-crash-handler -d "Disable crash handler when supported by the platform code"
complete -c orblit -l fixed-fps -d "Force a fixed number of frames per second (this setting disables real-time synchronization)" -x
complete -c orblit -l print-fps -d "Print the frames per second to the stdout"

# Standalone tools:
complete -c orblit -s s -l script -d "Run a script" -r
complete -c orblit -l check-only -d "Only parse for errors and quit (use with --script)"
complete -c orblit -l export-release -d "Export the project in release mode using the given preset and output path" -x
complete -c orblit -l export-debug -d "Export the project in debug mode using the given preset and output path" -x
complete -c orblit -l export-pack -d "Export the project data only as a PCK or ZIP file using the given preset and output path" -x
complete -c orblit -l doctool -d "Dump the engine API reference to the given path in XML format, merging if existing files are found" -r
complete -c orblit -l no-docbase -d "Disallow dumping the base types (used with --doctool)"
complete -c orblit -l build-solutions -d "Build the scripting solutions (e.g. for C# projects)"
complete -c orblit -l dump-native-extension-interface -d "Generate NativeExtension header file 'native_extension_interface.h' in the current folder. This file is the base file required to implement a NativeExtension"
complete -c orblit -l dump-extension-api -d "Generate JSON dump of the Orblit API for NativeExtension bindings named 'extension_api.json' in the current folder"
complete -c orblit -l benchmark -d "Benchmark the run time and print it to console"
complete -c orblit -l benchmark-file -d "Benchmark the run time and save it to a given file in JSON format" -x
complete -c orblit -l test -d "Run all unit tests; run with '--test --help' for more information" -x
