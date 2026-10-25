# Builds the device wrapper for the .NET packages. The Dart build uses
# hook/build.dart and does not read this file. Both compile the same two
# sources with the same definitions.
#
# Usage: cmake -S packages/orblit_audio_device -B <build dir> -DORBLIT_RID=osx-arm64
#        cmake --build <build dir> --config Release
#        cmake --install <build dir> --config Release --prefix dotnet/native
cmake_minimum_required(VERSION 3.20)

set(ORBLIT_RID "" CACHE STRING "Runtime id, for example osx-arm64, win-x64 or linux-x64")
if(ORBLIT_RID STREQUAL "")
  message(FATAL_ERROR "Pass -DORBLIT_RID=<runtime id>.")
endif()

if(APPLE)
  project(orblit_audio_device LANGUAGES C OBJC)
else()
  project(orblit_audio_device LANGUAGES C)
endif()

add_library(orblit_audio_device SHARED
  src/orblit_audio_device.c
  src/miniaudio_impl.c)
target_include_directories(orblit_audio_device PUBLIC include PRIVATE third_party)
target_compile_definitions(orblit_audio_device PRIVATE
  MA_NO_ENCODING
  MA_NO_GENERATION
  # With no device, the engine fails to open. miniaudio would otherwise fall
  # back to a null device that plays nothing, and the game would never hear
  # that it is silent.
  MA_NO_NULL)
target_compile_options(orblit_audio_device PRIVATE -O2)

if(APPLE)
  # Linking the frameworks, not opening them at run time, is what Apple's
  # notarization accepts. miniaudio needs the Objective-C compiler on iOS, and
  # macOS takes the same flag so Apple has one build.
  target_compile_definitions(orblit_audio_device PRIVATE MA_NO_RUNTIME_LINKING)
  set_source_files_properties(src/miniaudio_impl.c PROPERTIES LANGUAGE OBJC)
  # The vendored decoder compares a pointer with itself on purpose. Its own code is not ours to edit.
  set_property(SOURCE src/miniaudio_impl.c APPEND PROPERTY COMPILE_OPTIONS -Wno-tautological-compare)
  target_link_libraries(orblit_audio_device PRIVATE
    "-framework Foundation"
    "-framework CoreFoundation"
    "-framework CoreAudio"
    "-framework AudioToolbox")
elseif(UNIX)
  target_link_libraries(orblit_audio_device PRIVATE m dl pthread)
endif()

install(TARGETS orblit_audio_device
  LIBRARY DESTINATION runtimes/${ORBLIT_RID}/native
  RUNTIME DESTINATION runtimes/${ORBLIT_RID}/native)
