/**************************************************************************/
/*  jolt_body_activation_listener_spatial.cpp                                  */

#include "jolt_body_activation_listener_spatial.h"

#include "../objects/jolt_body_spatial.h"

void JoltBodyActivationListener3D::OnBodyActivated(const JPH::BodyID &p_body_id, JPH::uint64 p_body_user_data) {
	// This method will be called on multiple threads during the simulation step.

	if (JoltBody3D *body = reinterpret_cast<JoltObject3D *>(p_body_user_data)->as_body()) {
		body->_on_wake_up();
	}
}
