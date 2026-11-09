/**************************************************************************/
/*  jolt_custom_shape_type.h                                              */

#pragma once

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Collision/Shape/Shape.h>

namespace JoltCustomShapeSubType {

constexpr JPH::EShapeSubType INSTANCE_OVERRIDES = JPH::EShapeSubType::User1;
constexpr JPH::EShapeSubType RAY = JPH::EShapeSubType::UserConvex1;
constexpr JPH::EShapeSubType MOTION = JPH::EShapeSubType::UserConvex2;

} // namespace JoltCustomShapeSubType
