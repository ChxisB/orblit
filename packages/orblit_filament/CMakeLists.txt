# Builds the renderer as one library for the .NET packages.
#
# The Filament release is not in the repository. Point ORBLIT_FILAMENT_SDK at
# the directory that holds include/ and lib/. On macOS it defaults to the one
# darwin/setup.sh stages. The compiled materials must exist in
# ORBLIT_GENERATED_DIR. CI builds all three branches. The Linux branch
# follows native/headless/build_linux.sh. The Windows branch links the
# Filament release's MSVC libraries.
#
# Usage: cmake -S packages/orblit_filament -B <build dir> -DORBLIT_RID=osx-arm64
#        cmake --build <build dir> --config Release
#        cmake --install <build dir> --config Release --prefix dotnet/native
cmake_minimum_required(VERSION 3.20)
project(orblit_filament LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(ORBLIT_RID "" CACHE STRING "Runtime id, for example osx-arm64, win-x64 or linux-x64")
if(ORBLIT_RID STREQUAL "")
  message(FATAL_ERROR "Pass -DORBLIT_RID=<runtime id>.")
endif()

set(ORBLIT_SOURCES_DIR "${CMAKE_CURRENT_SOURCE_DIR}/darwin/orblit_filament/Sources/orblit_filament_native")
set(ORBLIT_FILAMENT_SDK "${CMAKE_CURRENT_SOURCE_DIR}/darwin/third_party/filament-mac/filament"
  CACHE PATH "A Filament release: the directory holding include/ and lib/")
set(ORBLIT_GENERATED_DIR "${ORBLIT_SOURCES_DIR}/generated/darwin-release"
  CACHE PATH "The compiled materials, the lit_opaque_material.h folder")

if(NOT EXISTS "${ORBLIT_FILAMENT_SDK}/include/filament/Engine.h")
  message(FATAL_ERROR "No Filament release at ${ORBLIT_FILAMENT_SDK}. Pass -DORBLIT_FILAMENT_SDK=<path>.")
endif()
if(NOT EXISTS "${ORBLIT_GENERATED_DIR}/lit_opaque_material.h")
  message(FATAL_ERROR "No compiled materials at ${ORBLIT_GENERATED_DIR}. Run darwin/setup.sh or pass -DORBLIT_GENERATED_DIR=<path>.")
endif()

if(ORBLIT_RID MATCHES "arm64$")
  set(ORBLIT_ARCH arm64)
else()
  set(ORBLIT_ARCH x86_64)
endif()

# Every plain C++ file beside the renderer, so a helper added later is built
# without anyone remembering to list it.
file(GLOB ORBLIT_CPP CONFIGURE_DEPENDS "${ORBLIT_SOURCES_DIR}/*.cpp")

set(ORBLIT_LIBS
  filament backend filabridge filaflat utils geometry smol-v ibl image
  abseil zstd filament-iblprefilter gltfio_core uberarchive uberzlib
  dracodec meshoptimizer ktxreader stb basis_transcoder mikktspace
  bluegl bluevk)

add_library(orblit_filament SHARED ${ORBLIT_CPP})
target_include_directories(orblit_filament PRIVATE
  "${ORBLIT_FILAMENT_SDK}/include"
  "${ORBLIT_SOURCES_DIR}"
  "${ORBLIT_SOURCES_DIR}/include"
  "${ORBLIT_GENERATED_DIR}")
if(NOT MSVC)
  # The flags the headless build uses. A Release default of -O3 with NDEBUG
  # is not what the renderer has been measured with.
  set(CMAKE_CXX_FLAGS_RELEASE "-O2")
  target_compile_options(orblit_filament PRIVATE
    -Wno-deprecated-declarations -Wno-unused-private-field)
endif()

if(APPLE)
  enable_language(OBJCXX)
  target_sources(orblit_filament PRIVATE
    "${ORBLIT_SOURCES_DIR}/OrblitSurfaceApple.mm"
    "${ORBLIT_SOURCES_DIR}/OrblitPlatformApple.mm")
  set_source_files_properties(
    "${ORBLIT_SOURCES_DIR}/OrblitSurfaceApple.mm"
    "${ORBLIT_SOURCES_DIR}/OrblitPlatformApple.mm"
    PROPERTIES COMPILE_OPTIONS "-fobjc-arc")
  foreach(lib IN LISTS ORBLIT_LIBS)
    target_link_libraries(orblit_filament PRIVATE "${ORBLIT_FILAMENT_SDK}/lib/${ORBLIT_ARCH}/lib${lib}.a")
  endforeach()
  if(EXISTS "${ORBLIT_FILAMENT_SDK}/lib/${ORBLIT_ARCH}/libwebgpu_dawn.a")
    target_link_libraries(orblit_filament PRIVATE "${ORBLIT_FILAMENT_SDK}/lib/${ORBLIT_ARCH}/libwebgpu_dawn.a")
  endif()
  target_link_libraries(orblit_filament PRIVATE
    "-framework Cocoa" "-framework Metal" "-framework QuartzCore"
    "-framework CoreVideo" "-framework IOSurface" "-framework OpenGL"
    "-framework CoreFoundation" "-framework ImageIO" "-framework AVFoundation"
    "-framework CoreMedia" "-framework CoreGraphics" "-framework IOKit")
elseif(WIN32)
  # Filament publishes a Windows release for x64 only.
  if(ORBLIT_ARCH STREQUAL "arm64")
    message(FATAL_ERROR "Filament has no Windows arm64 release, so the renderer cannot be built for ${ORBLIT_RID}.")
  endif()
  # The header has no export macro, so the exports are the functions it
  # declares. Exporting every symbol would pass the 65,535 limit.
  file(STRINGS "${ORBLIT_SOURCES_DIR}/include/orblit_renderer.h" ORBLIT_DECLS REGEX "orblit_renderer_[a-z_]+\\(")
  set(ORBLIT_DEF "${CMAKE_CURRENT_BINARY_DIR}/orblit_filament.def")
  set(ORBLIT_DEF_TEXT "EXPORTS\n")
  foreach(decl IN LISTS ORBLIT_DECLS)
    string(REGEX MATCH "orblit_renderer_[a-z_]+" name "${decl}")
    string(APPEND ORBLIT_DEF_TEXT "  ${name}\n")
  endforeach()
  file(WRITE "${ORBLIT_DEF}" "${ORBLIT_DEF_TEXT}")
  target_sources(orblit_filament PRIVATE "${ORBLIT_DEF}")
  target_compile_definitions(orblit_filament PRIVATE ORBLIT_PLATFORM_PORTABLE)
  # Filament's headers use designated initializers. Clang takes them in C++17
  # as an extension, and MSVC only in C++20.
  set_property(TARGET orblit_filament PROPERTY CXX_STANDARD 20)
  foreach(lib IN LISTS ORBLIT_LIBS)
    target_link_libraries(orblit_filament PRIVATE
      "${ORBLIT_FILAMENT_SDK}/lib/x86_64/$<IF:$<CONFIG:Debug>,mdd,md>/${lib}.lib")
  endforeach()
  # Filament's archives call WGL and a shell path helper, and the compiler's
  # default libraries have neither.
  target_link_libraries(orblit_filament PRIVATE opengl32 shlwapi)
else()
  # Filament's Linux release is built against LLVM's standard library, so the
  # compile and the link both take libc++. The archives refer to each other
  # both ways round, hence the group.
  if(NOT CMAKE_CXX_COMPILER_ID MATCHES "Clang")
    message(FATAL_ERROR "The renderer links Filament's libc++ archives, so it needs clang. Install clang and libc++-dev, then set CXX=clang++.")
  endif()
  target_compile_definitions(orblit_filament PRIVATE ORBLIT_PLATFORM_PORTABLE)
  target_compile_options(orblit_filament PRIVATE -stdlib=libc++)
  # Google unpacks its arm64 Linux release to lib/aarch64, where the macOS
  # release uses lib/arm64.
  set(ORBLIT_LIB_ARCH ${ORBLIT_ARCH})
  if(ORBLIT_ARCH STREQUAL "arm64")
    set(ORBLIT_LIB_ARCH aarch64)
  endif()
  set(ORBLIT_ARCHIVES "")
  foreach(lib IN LISTS ORBLIT_LIBS)
    list(APPEND ORBLIT_ARCHIVES "${ORBLIT_FILAMENT_SDK}/lib/${ORBLIT_LIB_ARCH}/lib${lib}.a")
  endforeach()
  target_link_libraries(orblit_filament PRIVATE
    -stdlib=libc++ -Wl,--start-group ${ORBLIT_ARCHIVES} -Wl,--end-group pthread dl m)
endif()

set(ORBLIT_TARGETS orblit_filament)
if(APPLE)
  # Metal events for the Avalonia viewport. A library of its own, because the
  # renderer's C API is shared by every host and must not grow Metal calls
  # only one host uses.
  add_library(orblit_filament_apple SHARED "${CMAKE_CURRENT_SOURCE_DIR}/native/apple/orblit_filament_apple.mm")
  set_source_files_properties("${CMAKE_CURRENT_SOURCE_DIR}/native/apple/orblit_filament_apple.mm"
    PROPERTIES COMPILE_OPTIONS "-fobjc-arc")
  target_link_libraries(orblit_filament_apple PRIVATE "-framework Metal" "-framework Foundation")
  list(APPEND ORBLIT_TARGETS orblit_filament_apple)
endif()

install(TARGETS ${ORBLIT_TARGETS}
  LIBRARY DESTINATION runtimes/${ORBLIT_RID}/native
  RUNTIME DESTINATION runtimes/${ORBLIT_RID}/native)
