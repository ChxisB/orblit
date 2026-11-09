/**************************************************************************/
/*  jolt_broad_phase_layer.h                                              */

#pragma once

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>

#include <cstdint>

namespace JoltBroadPhaseLayer {

constexpr JPH::BroadPhaseLayer BODY_STATIC(0);
constexpr JPH::BroadPhaseLayer BODY_STATIC_BIG(1);
constexpr JPH::BroadPhaseLayer BODY_DYNAMIC(2);
constexpr JPH::BroadPhaseLayer AREA_DETECTABLE(3);
constexpr JPH::BroadPhaseLayer AREA_UNDETECTABLE(4);

constexpr uint32_t COUNT = 5;

} // namespace JoltBroadPhaseLayer
