/**************************************************************************/
/*  entity_spatial_editor_constants.h                                          */

#pragma once

#include "core/math/math_defs.h"

namespace Entity3DEditorConstants {

constexpr real_t GIZMO_ARROW_RADIUS = 0.065;
constexpr real_t GIZMO_ARROW_SIZE = 0.35;
constexpr real_t GIZMO_RING_HALF_WIDTH = 0.1;
constexpr real_t GIZMO_PLANE_SIZE = 0.25;
constexpr real_t GIZMO_PLANE_DST = 0.2;
constexpr real_t GIZMO_CIRCLE_SIZE = 1.1;
constexpr real_t GIZMO_SCALE_SIZE = 0.15;

constexpr real_t GIZMO_SCALE_OFFSET = GIZMO_CIRCLE_SIZE + 0.3;
constexpr real_t GIZMO_ARROW_OFFSET = GIZMO_CIRCLE_SIZE + 0.3;

constexpr real_t TRACKBALL_SENSITIVITY = 0.005;
constexpr int TRACKBALL_SPHERE_RINGS = 16;
constexpr int TRACKBALL_SPHERE_SECTORS = 32;
constexpr real_t TRACKBALL_HIGHLIGHT_ALPHA = 0.01;
constexpr int GIZMO_HIGHLIGHT_AXIS_VIEW_ROTATION = 15;

constexpr float VERTEX_SNAP_THRESHOLD = 30.0f;
constexpr int GIZMO_HIGHLIGHT_AXIS_TRACKBALL = 16;

constexpr real_t ZOOM_FREELOOK_INDICATOR_DELAY_S = 1.5;

constexpr real_t MIN_Z = 0.01;
constexpr real_t MAX_Z = 1000000.0;

constexpr real_t MIN_FOV = 0.01;
constexpr real_t MAX_FOV = 179;

} // namespace Entity3DEditorConstants
