# Builds the core for the .NET packages.
#
# Usage: cmake -S packages/orblit_core -B <build dir> -DORBLIT_RID=osx-arm64
#        cmake --build <build dir> --config Release
#        cmake --install <build dir> --config Release --prefix dotnet/native
cmake_minimum_required(VERSION 3.20)
project(orblit_core LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# The header has no export macro. MSVC exports nothing without one.
set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

set(ORBLIT_RID "" CACHE STRING "Runtime id, for example osx-arm64, win-x64 or linux-x64")
if(ORBLIT_RID STREQUAL "")
  message(FATAL_ERROR "Pass -DORBLIT_RID=<runtime id>.")
endif()

add_library(orblit_core SHARED
  src/world.cpp
  src/orblit_core.cpp
  src/transform.cpp)
target_include_directories(orblit_core PUBLIC include PRIVATE src)

install(TARGETS orblit_core
  LIBRARY DESTINATION runtimes/${ORBLIT_RID}/native
  RUNTIME DESTINATION runtimes/${ORBLIT_RID}/native)
