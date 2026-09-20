# The Windows plugin: the same portable renderer core the Apple, Android and
# Linux builds compile, behind Flutter's Windows embedder instead of Swift's,
# Kotlin's or GTK's. The counterpart to ../linux/CMakeLists.txt, which this
# follows closely -- same sources, same archive list, same reasons.
cmake_minimum_required(VERSION 3.14)
set(PROJECT_NAME "orblit_filament")
project(${PROJECT_NAME} LANGUAGES CXX)

# Named by the generated flutter/generated_plugins.cmake, which links
# ${plugin}_plugin and reads ${plugin}_bundled_libraries, so neither this nor
# the variable at the bottom is ours to rename.
set(PLUGIN_NAME "orblit_filament_plugin")

# Stages the Filament Windows release and compiles this package's materials --
# setup.sh, the Windows counterpart to ../darwin/setup.sh, ../android/setup.sh
# and ../linux/setup.sh. Run at configure time rather than as a build step
# because the archives and the generated headers it produces are inputs to
# this file's own find/compile rules: CMake has to see them while it is
# configuring, not later while it is building. It is idempotent and, once the
# release and the materials exist, fast.
#
# Through bash explicitly, because the script is one shell script shared in
# spirit with the other three and Windows has no shell that would run it.
# Git for Windows ships one and Flutter already requires that toolchain, so
# this is a dependency the machine already has rather than a new one.
find_program(BASH_PROGRAM bash)
if(NOT BASH_PROGRAM)
  message(FATAL_ERROR
    "No bash on PATH. packages/orblit_filament/windows/setup.sh stages the "
    "Filament release and compiles the materials, and needs the bash that "
    "Git for Windows installs.")
endif()

execute_process(
  COMMAND "${BASH_PROGRAM}" "${CMAKE_CURRENT_SOURCE_DIR}/setup.sh"
  WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
  RESULT_VARIABLE ORBLIT_SETUP_RESULT
  OUTPUT_VARIABLE ORBLIT_SETUP_OUTPUT
  ERROR_VARIABLE ORBLIT_SETUP_OUTPUT)
message(STATUS "orblit_filament/windows setup.sh:\n${ORBLIT_SETUP_OUTPUT}")
if(NOT ORBLIT_SETUP_RESULT EQUAL 0)
  message(FATAL_ERROR "packages/orblit_filament/windows/setup.sh failed.")
endif()

# third_party/filament is the Filament Windows release setup.sh just staged.
# Note it unpacks flat -- include/, lib/, bin/ at its root -- where the Linux
# and Android tarballs unpack under a filament/ directory of their own, which
# is why setup.sh extracts this one into a directory it makes itself.
set(FILAMENT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/filament")
get_filename_component(FILAMENT_DIR "${FILAMENT_DIR}" ABSOLUTE)
if(NOT EXISTS "${FILAMENT_DIR}/include/filament/Engine.h")
  message(FATAL_ERROR "No Filament Windows release at ${FILAMENT_DIR}.")
endif()

# Google publishes the Windows release for x86_64 and nothing else, so an
# arm64 Flutter build has no archives to link and is refused here with the
# reason rather than a page of unresolved symbols at the very last step.
if(CMAKE_SIZEOF_VOID_P EQUAL 8 AND
   (CMAKE_GENERATOR_PLATFORM STREQUAL "ARM64" OR
    CMAKE_SYSTEM_PROCESSOR MATCHES "ARM64|arm64"))
  message(FATAL_ERROR
    "Filament publishes no arm64 Windows release; orblit_filament builds for "
    "x64 only. Build the application with --target-platform windows-x64.")
endif()

# Which runtime the archives were built against has to match what MSVC is
# compiling this target with, or the link fails on LNK2038 with a mismatch
# between MD_DynamicRelease and MDd_DynamicDebug. Filament's Windows release
# ships all four combinations -- md, mdd, mt, mtd -- and CMake's own default
# for MSVC is the dynamic CRT, debug flavour in Debug and release flavour
# otherwise, so these two lines are the same choice stated twice rather than
# a preference. Flutter's Profile configuration takes the release CRT, which
# is why this asks whether the config is Debug rather than whether it is
# Release.
set(FILAMENT_LIB_DIR "${FILAMENT_DIR}/lib/x86_64/$<IF:$<CONFIG:Debug>,mdd,md>")

# The portable renderer core. Referenced from where it lives under darwin/ --
# not copied -- so the same source compiles for every platform and a change
# to one is a change for all of them. The list is ../linux/CMakeLists.txt's,
# unchanged: OrblitRenderer.mm, OrblitSurfaceApple.mm, OrblitPlatformApple.mm and
# OrblitTexture.m are Apple's Objective-C++ wrapper and are never built here.
# See PORTING.md.
set(ORBLIT_CORE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../darwin/orblit_filament/Sources/orblit_filament_native")
get_filename_component(ORBLIT_CORE_DIR "${ORBLIT_CORE_DIR}" ABSOLUTE)
if(NOT EXISTS "${ORBLIT_CORE_DIR}/generated/lit_opaque_material.h")
  message(FATAL_ERROR "No compiled materials at ${ORBLIT_CORE_DIR}/generated.")
endif()

# The half of the scene decoder that is not this embedder's: what a scene
# holds and how it is applied, which is the same here and in ../linux.
set(ORBLIT_COMMON_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../common")
get_filename_component(ORBLIT_COMMON_DIR "${ORBLIT_COMMON_DIR}" ABSOLUTE)

add_library(${PLUGIN_NAME} SHARED
  # This directory's own: the Windows plugin, one viewport per texture, and
  # this embedder's half of the `setScene` decoder.
  "orblit_filament_plugin.cpp"
  "orblit_viewport.cpp"
  "orblit_scene.cpp"

  # Its other half, shared with the sibling plugin.
  "${ORBLIT_COMMON_DIR}/orblit_scene_common.cc"

  # The portable core and its C ABI.
  "${ORBLIT_CORE_DIR}/OrblitRendererCore.cpp"

  # The rest of orblit::Renderer, one file a topic, then the helpers they
  # share and the one unit that holds the compiled materials.
  "${ORBLIT_CORE_DIR}/OrblitRendererFrame.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererWeather.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererMeshes.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererPopulations.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererSurfaces.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererEnvironment.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererGraph.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererEffects.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererObjects.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererBatching.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererLights.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererProbes.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererCamera.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererInternal.cpp"
  "${ORBLIT_CORE_DIR}/OrblitMaterialPackages.cpp"

  "${ORBLIT_CORE_DIR}/OrblitRendererC.cpp"
  "${ORBLIT_CORE_DIR}/OrblitBackend.cpp"
  "${ORBLIT_CORE_DIR}/OrblitPlatform.cpp"
  "${ORBLIT_CORE_DIR}/OrblitResources.cpp"
  "${ORBLIT_CORE_DIR}/OrblitSurfaceHeadless.cpp"
  "${ORBLIT_CORE_DIR}/OrblitDecals.cpp"
  "${ORBLIT_CORE_DIR}/OrblitOutline.cpp"
  "${ORBLIT_CORE_DIR}/OrblitShadows.cpp"
  "${ORBLIT_CORE_DIR}/OrblitSplats.cpp"
  "${ORBLIT_CORE_DIR}/OrblitSplatSet.cpp"
  "${ORBLIT_CORE_DIR}/OrblitSprites.cpp"
  "${ORBLIT_CORE_DIR}/OrblitMotionBlur.cpp"
  "${ORBLIT_CORE_DIR}/ScreenEffects.cpp"
  "${ORBLIT_CORE_DIR}/OrblitModels.cpp"
  # FBX and OBJ to GLB, and ufbx compiled as C++ through one file (see
  # OrblitUfbx.cpp for why a .cpp includes a .c).
  "${ORBLIT_CORE_DIR}/OrblitImport.cpp"
  "${ORBLIT_CORE_DIR}/OrblitImportMeshes.cpp"
  "${ORBLIT_CORE_DIR}/OrblitImportMaterials.cpp"
  "${ORBLIT_CORE_DIR}/OrblitImportScene.cpp"
  "${ORBLIT_CORE_DIR}/OrblitUfbx.cpp"
  # Environments from .hdr and .exr pictures: decoding, cmgen's arithmetic,
  # the run-time filter, and tinyexr with miniz compiled as C++ through one
  # file (see OrblitTinyExr.cpp).
  "${ORBLIT_CORE_DIR}/OrblitHdrImage.cpp"
  "${ORBLIT_CORE_DIR}/OrblitTinyExr.cpp"
  "${ORBLIT_CORE_DIR}/OrblitEnvironmentBake.cpp"
  "${ORBLIT_CORE_DIR}/OrblitEnvironment.cpp"
  # Textures: the GPU-ready KTX 2 reader, and the queue that decodes and
  # uploads every texture under a budget a frame.
  "${ORBLIT_CORE_DIR}/OrblitKtx2.cpp"
  "${ORBLIT_CORE_DIR}/OrblitTextures.cpp"
  "${ORBLIT_CORE_DIR}/OrblitTexturesPush.cpp"
  "${ORBLIT_CORE_DIR}/OrblitTexturesDecode.cpp"
)

# Not apply_standard_settings(), for the reason ../linux/CMakeLists.txt gives
# and one more that is Windows's alone. That helper turns on /W4 /WX, which
# four thousand lines of renderer and Filament's own headers do not compile
# clean under; and it defines _HAS_EXCEPTIONS=0, which changes what MSVC's
# standard library headers expand to. Filament's archives were built without
# it, so defining it here would put two different inflections of the same
# inline functions in one binary. Since the archives are the overwhelming
# bulk of what is linked, this target matches them and leaves exceptions on.
#
# The CRT is the opposite case and is not a choice: the linker checks it, so
# MSVC_RUNTIME_LIBRARY is deliberately left at CMake's default to agree with
# flutter_wrapper_plugin, and FILAMENT_LIB_DIR above selects the matching
# flavour of the archives instead.
# C++20, not the 17 every other platform's build asks for, and not a
# preference -- MSVC refuses to compile this target below it, for two separate
# reasons found by building it:
#
#   - Designated initializers (`{ .engine = ..., .gltfPath = ... }`) are a
#     C++20 feature that GCC and Clang have always accepted as an extension in
#     C++17, so they are already in Filament's own headers (DriverEnums.h,
#     Box.h) and in the renderer's own sources. MSVC does not: it says
#     "error C7555: use of designated initializers requires at least
#     '/std:c++20'" and stops. That is what makes this the smallest possible
#     fix -- raising the standard here changes no shared source at all, where
#     rewriting those initialisers would change the core for every platform.
#   - /std:c++20 turns on /permissive- (asked for explicitly below anyway),
#     and with it MSVC's two-phase name lookup. Without that, `friend class
#     TPanic;` inside `class PreconditionPanic final : public
#     TPanic<PreconditionPanic>` in Filament's utils/Panic.h resolves to the
#     class template rather than the base's injected-class-name, and is
#     rejected with "error C2990: non-class template has already been
#     declared as a class template".
set_target_properties(${PLUGIN_NAME} PROPERTIES
  CXX_STANDARD 20
  CXX_STANDARD_REQUIRED ON
  CXX_VISIBILITY_PRESET hidden)

target_compile_definitions(${PLUGIN_NAME} PRIVATE
  FLUTTER_PLUGIN_IMPL
  # windows.h defines min and max as macros, which every other line of
  # Filament's headers then fails to compile against.
  NOMINMAX
  # The renderer defines M_PI itself when the platform does not, but several
  # of Filament's headers expect the usual constants to be there.
  _USE_MATH_DEFINES
  # OrblitSplats.h's sortableBits() type-puns a float through
  # __builtin_memcpy, which is GCC and Clang's spelling and not a function
  # MSVC has: "error C3861: '__builtin_memcpy': identifier not found".
  #
  # Fixed here rather than in that header deliberately. The header is shared
  # C++ compiled by all five platforms and is being worked on elsewhere, so a
  # one-word edit there would be both a cross-platform change and a merge
  # conflict, to buy nothing this does not. std::memcpy is what the builtin
  # compiles to in a constant-foldable context anyway, and /FI below
  # guarantees it is declared. If the splat header ever spells this
  # portably, this define and that /FI can go.
  __builtin_memcpy=std::memcpy)

# /EHsc because exceptions are on (see above); /bigobj because the renderer
# instantiates enough of Filament's templates to pass the object format's
# default section limit -- it was one 7,675-line file when that was found, and
# the split into OrblitRenderer*.cpp may well have brought every unit back
# under the limit, but no Mac can tell and the flag costs nothing; /w because
# this target is held
# to Filament's warning policy rather than the application's, exactly as the
# GTK plugin holds the core apart from its own -Wall -Wextra.
#
# /permissive- states what /std:c++20 already implies, so the two-phase
# lookup Panic.h depends on does not quietly depend on a compiler default.
# /Zc:__cplusplus makes MSVC report the standard it is actually compiling,
# which is what any header that gates on __cplusplus reads. /FIcstring
# force-includes <cstring> into every translation unit, so the
# __builtin_memcpy define above always has std::memcpy in scope --
# OrblitSplats.h includes <cstdint> and <vector> but not <cstring>, and
# relying on it arriving transitively is the kind of thing that works until
# it does not.
target_compile_options(${PLUGIN_NAME} PRIVATE
  /EHsc /bigobj /w /permissive- /Zc:__cplusplus /FIcstring)

target_include_directories(${PLUGIN_NAME} PRIVATE
  "${FILAMENT_DIR}/include"
  "${ORBLIT_CORE_DIR}"
  "${ORBLIT_CORE_DIR}/include"
  "${ORBLIT_COMMON_DIR}"
  # The compiled materials. The core includes them by bare name so that the
  # include path chooses which set is linked — see the darwin and android
  # builds, which point this at generated/<set>. This plugin's setup.sh
  # writes the flat generated/ directory, so that is what it names here.
  "${ORBLIT_CORE_DIR}/generated"
  "${CMAKE_CURRENT_SOURCE_DIR}")

# The one public header, reached by the registrant Flutter generates into the
# application as <orblit_filament/orblit_filament_plugin.h>. INTERFACE, because
# it is the runner that includes it and not anything compiled here.
target_include_directories(${PLUGIN_NAME} INTERFACE
  "${CMAKE_CURRENT_SOURCE_DIR}/include")

# The archive list is ../linux/CMakeLists.txt's, which is ../darwin/setup.sh's
# own LIBS -- what the renderer core actually reaches -- plus bluegl and
# bluevk, the runtime loaders for OpenGL and Vulkan. They load opengl32.dll
# and vulkan-1.dll rather than linking them, which is what lets this build run
# on a machine with no Vulkan driver present at build time. All twenty-three
# are present in the Windows release under every runtime flavour.
set(FILAMENT_STATIC_LIBS
    filament backend bluegl bluevk filabridge filaflat utils geometry smol-v
    ibl image abseil zstd filament-iblprefilter gltfio_core uberarchive
    uberzlib dracodec meshoptimizer ktxreader stb basis_transcoder mikktspace)

set(FILAMENT_LINK_LINE "")
foreach(lib ${FILAMENT_STATIC_LIBS})
  list(APPEND FILAMENT_LINK_LINE "${FILAMENT_LIB_DIR}/${lib}.lib")
endforeach()

# No --start-group equivalent is needed or available here: unlike ld, MSVC's
# linker resolves across the whole set of input libraries rather than reading
# each once, so the mutual references between Filament's archives -- the third
# of the three fixes the Linux build records -- are not a problem on Windows.
target_link_libraries(${PLUGIN_NAME} PRIVATE ${FILAMENT_LINK_LINE})

# The Win32 libraries Filament's archives import from, taken from the
# dllimport symbols they actually name rather than from a guess: wgl* from
# opengl32, ChoosePixelFormat/SetPixelFormat/SwapBuffers from gdi32,
# CreateWindowExA/GetDC/AdjustWindowRect and the DPI-awareness calls from
# user32, PathIsRelativeA from shlwapi and SHGetFolderPathA from shell32.
# Everything else they need is in kernel32 and the CRT, which MSVC links by
# default.
target_link_libraries(${PLUGIN_NAME} PRIVATE
  opengl32.lib gdi32.lib user32.lib shlwapi.lib shell32.lib)

target_link_libraries(${PLUGIN_NAME} PRIVATE flutter flutter_wrapper_plugin)

# Nothing of ours to copy beside the executable: the Filament archives are
# static and are already inside this DLL, and the DLL itself is bundled by
# the generated plugins file through $<TARGET_FILE:...>. Set anyway, because
# that file reads this variable for every plugin and an unset one would
# quietly bundle nothing rather than fail.
set(orblit_filament_bundled_libraries "" PARENT_SCOPE)
