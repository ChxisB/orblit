# The Linux plugin: the same portable renderer core the Apple and Android
# builds compile, behind Flutter's GTK embedder instead of Swift's or
# Kotlin's. The counterpart to ../android/src/main/cpp/CMakeLists.txt, which
# this follows closely -- same sources, same archive list, same reasons.
cmake_minimum_required(VERSION 3.13)
set(PROJECT_NAME "orblit_filament")
project(${PROJECT_NAME} LANGUAGES CXX)

set(PLUGIN_NAME "orblit_filament_plugin")

# Stages the Filament Linux release and compiles this package's materials --
# setup.sh, the Linux counterpart to ../darwin/setup.sh and
# ../android/setup.sh. Run at configure time rather than as a build step
# because the archives and the generated headers it produces are inputs to
# this file's own find/compile rules: CMake has to see them while it is
# configuring, not later while it is building. It is idempotent and, once
# the symlink and the materials exist, fast, so paying for it on every
# configure is cheaper than a manual step somebody forgets -- the same
# argument ../android/build.gradle.kts makes for its own Exec task.
execute_process(
  COMMAND bash "${CMAKE_CURRENT_SOURCE_DIR}/setup.sh"
  WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
  RESULT_VARIABLE ORBLIT_SETUP_RESULT
  OUTPUT_VARIABLE ORBLIT_SETUP_OUTPUT
  ERROR_VARIABLE ORBLIT_SETUP_OUTPUT)
message(STATUS "orblit_filament/linux setup.sh:\n${ORBLIT_SETUP_OUTPUT}")
if(NOT ORBLIT_SETUP_RESULT EQUAL 0)
  message(FATAL_ERROR "packages/orblit_filament/linux/setup.sh failed.")
endif()

# third_party/filament is the symlink (or fetched copy) setup.sh just made
# onto Filament's unpacked Linux release for this architecture.
set(FILAMENT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/filament")
get_filename_component(FILAMENT_DIR "${FILAMENT_DIR}" ABSOLUTE)
if(NOT EXISTS "${FILAMENT_DIR}/include/filament/Engine.h")
  message(FATAL_ERROR "No Filament Linux release at ${FILAMENT_DIR}.")
endif()

# Google publishes the aarch64 release as "arm-linux", unpacking to
# lib/aarch64, and the x86_64 one as "linux", unpacking to lib/x86_64 --
# setup.sh picks the tarball and this picks the matching slice of it, both
# from the same machine fact rather than from a flag either could get wrong.
if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
  set(FILAMENT_LIB_DIR "${FILAMENT_DIR}/lib/aarch64")
else()
  set(FILAMENT_LIB_DIR "${FILAMENT_DIR}/lib/x86_64")
endif()
if(NOT EXISTS "${FILAMENT_LIB_DIR}")
  message(FATAL_ERROR "Filament's Linux release has no libraries at ${FILAMENT_LIB_DIR}")
endif()

# The portable renderer core. Referenced from where it lives under darwin/ --
# not copied -- so the same source compiles for every platform and a change
# to one is a change for all of them. The list is
# ../android/src/main/cpp/CMakeLists.txt's, unchanged: OrblitRenderer.mm,
# OrblitSurfaceApple.mm, OrblitPlatformApple.mm and OrblitTexture.m are Apple's
# Objective-C++ wrapper and are never built here. See PORTING.md.
set(ORBLIT_CORE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../darwin/orblit_filament/Sources/orblit_filament_native")
get_filename_component(ORBLIT_CORE_DIR "${ORBLIT_CORE_DIR}" ABSOLUTE)
if(NOT EXISTS "${ORBLIT_CORE_DIR}/generated/lit_opaque_material.h")
  message(FATAL_ERROR "No compiled materials at ${ORBLIT_CORE_DIR}/generated.")
endif()

add_library(${PLUGIN_NAME} SHARED
  # This directory's own: the GTK plugin, one viewport per texture, and the
  # decoder that turns a `setScene` message into C ABI calls.
  "orblit_filament_plugin.cc"
  "orblit_viewport.cc"
  "orblit_scene.cc"

  # The portable core and its C ABI.
  "${ORBLIT_CORE_DIR}/OrblitRendererCore.cpp"
  "${ORBLIT_CORE_DIR}/OrblitRendererC.cpp"
  "${ORBLIT_CORE_DIR}/OrblitBackend.cpp"
  "${ORBLIT_CORE_DIR}/OrblitPlatform.cpp"
  "${ORBLIT_CORE_DIR}/OrblitSurfaceHeadless.cpp"
  "${ORBLIT_CORE_DIR}/OrblitDecals.cpp"
  "${ORBLIT_CORE_DIR}/OrblitOutline.cpp"
  "${ORBLIT_CORE_DIR}/OrblitShadows.cpp"
  "${ORBLIT_CORE_DIR}/OrblitSplats.cpp"
  "${ORBLIT_CORE_DIR}/OrblitSplatSet.cpp"
  "${ORBLIT_CORE_DIR}/OrblitMotionBlur.cpp"
  "${ORBLIT_CORE_DIR}/ScreenEffects.cpp"
)

# Not apply_standard_settings(): that helper pins C++14 and turns on -Werror,
# and this target is Filament's headers and four thousand lines of renderer
# that want C++17 and do not compile clean under another project's warning
# policy. The plugin's own sources are held to -Wall -Wextra below instead.
set_target_properties(${PLUGIN_NAME} PROPERTIES
  CXX_STANDARD 17
  CXX_STANDARD_REQUIRED ON
  CXX_VISIBILITY_PRESET hidden)

# libc++, not libstdc++, and on the compile line as well as the link one.
#
# Filament's Linux release is built against LLVM's standard library, so its
# archives name symbols in namespace std::__1 -- __next_prime and the rest of
# the hash-table machinery. Linked against GNU's libstdc++ instead, every one
# of those is undefined, and the error names template instantiations a page
# long rather than the one-word cause. Found the hard way by
# ../native/headless/build_linux.sh, which carries the same two flags.
#
# Safe to disagree with the rest of the build about this, which is the
# obvious worry: Flutter's Linux embedder, GTK and GLib are all C APIs, and
# no C++ type crosses out of this shared library in either direction. Two C++
# runtimes in one process is only undefined behaviour when they exchange C++
# objects, and nothing here does.
target_compile_options(${PLUGIN_NAME} PRIVATE -stdlib=libc++)
target_link_options(${PLUGIN_NAME} PRIVATE -stdlib=libc++)

target_compile_definitions(${PLUGIN_NAME} PRIVATE FLUTTER_PLUGIN_IMPL)
target_compile_options(${PLUGIN_NAME} PRIVATE
  -Wall -Wextra -Wno-unused-parameter -Wno-deprecated-declarations
  -Wno-unused-private-field)

target_include_directories(${PLUGIN_NAME} PRIVATE
  "${FILAMENT_DIR}/include"
  "${ORBLIT_CORE_DIR}"
  "${ORBLIT_CORE_DIR}/include"
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

# The archive list is ../darwin/setup.sh's own LIBS -- what the renderer core
# actually reaches -- plus bluegl and bluevk, the runtime loaders for OpenGL
# and Vulkan. They dlopen libGL.so and libvulkan.so.1 rather than linking
# them, which is what lets this build run against a software rasteriser
# (llvmpipe, lavapipe) with no driver present at build time. Unlike Android,
# which is GLES and has no bluegl build, a Linux desktop needs both.
set(FILAMENT_STATIC_LIBS
    filament backend bluegl bluevk filabridge filaflat utils geometry smol-v
    ibl image abseil zstd filament-iblprefilter gltfio_core uberarchive
    uberzlib dracodec meshoptimizer ktxreader stb basis_transcoder mikktspace)

set(FILAMENT_LINK_LINE "")
foreach(lib ${FILAMENT_STATIC_LIBS})
  list(APPEND FILAMENT_LINK_LINE "${FILAMENT_LIB_DIR}/lib${lib}.a")
endforeach()

# --start-group: Filament's archives refer to each other both ways round, and
# ld on Linux reads an archive once unless told otherwise. Without this the
# link fails on symbols that are plainly present in the libraries -- the
# third of the three fixes build_linux.sh records.
target_link_libraries(${PLUGIN_NAME} PRIVATE
  -Wl,--start-group
  ${FILAMENT_LINK_LINE}
  -Wl,--end-group
  pthread dl m)

target_link_libraries(${PLUGIN_NAME} PRIVATE flutter)
target_link_libraries(${PLUGIN_NAME} PRIVATE PkgConfig::GTK)

# Nothing of ours to copy beside the executable: the Filament archives are
# static and are already inside this shared library, and libc++ is the
# system's. Set anyway, because the runner's CMakeLists reads this variable
# for every plugin and an unset one is a configure error there.
set(orblit_filament_bundled_libraries "" PARENT_SCOPE)
