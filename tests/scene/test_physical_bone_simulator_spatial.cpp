/**************************************************************************/
/*  test_physical_bone_simulator_spatial.cpp                                  */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_physical_bone_simulator_spatial)

#ifndef PHYSICS_SPATIAL_DISABLED

#include "scene/spatial/physics/physical_bone_simulator_spatial.h"
#include "tests/test_tools.h"

namespace TestPhysicalBoneSimulator3D {

TEST_CASE("[Stage][PhysicalBoneSimulator3D] An unassigned bone id has no physical bone") {
	PhysicalBoneSimulator3D *simulator = memnew(PhysicalBoneSimulator3D);

	ErrorDetector detector;
	CHECK(simulator->get_physical_bone(-1) == nullptr);
	CHECK(simulator->get_physical_bone_parent(-1) == nullptr);
	CHECK_FALSE(detector.has_error);

	memdelete(simulator);
}

TEST_CASE("[Stage][PhysicalBoneSimulator3D] A bone id past the end is still an error") {
	PhysicalBoneSimulator3D *simulator = memnew(PhysicalBoneSimulator3D);

	ErrorDetector detector;
	CHECK(simulator->get_physical_bone(0) == nullptr);
	CHECK(detector.has_error);

	memdelete(simulator);
}

} // namespace TestPhysicalBoneSimulator3D

#endif // PHYSICS_SPATIAL_DISABLED
