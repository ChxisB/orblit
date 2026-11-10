/**************************************************************************/
/*  jolt_body_activation_listener_spatial.h                                    */

#pragma once

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Body/BodyActivationListener.h>

class JoltBodyActivationListener3D final
		: public JPH::BodyActivationListener {
public:
	virtual void OnBodyActivated(const JPH::BodyID &p_body_id, JPH::uint64 p_body_user_data) override;
	virtual void OnBodyDeactivated(const JPH::BodyID &p_body_id, JPH::uint64 p_body_user_data) override {}
};
