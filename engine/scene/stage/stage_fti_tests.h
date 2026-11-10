/**************************************************************************/
/*  stage_fti_tests.h                                                     */

#pragma once

#include <cstdint>

class Entity3D;
class Entity;
struct Transform3D;
class StageFTI;

class StageFTITests {
	StageFTI &_fti;

	void debug_verify_failed(const Entity3D *p_node, const Transform3D &p_test);

public:
	void update_dirty_nodes(Entity *p_node, uint32_t p_current_half_frame, float p_interpolation_fraction, bool p_active, const Transform3D *p_parent_global_xform = nullptr, int p_depth = 0);
	void frame_update(Entity *p_root, uint32_t p_half_frame, float p_interpolation_fraction);

	StageFTITests(StageFTI &p_fti);
};
