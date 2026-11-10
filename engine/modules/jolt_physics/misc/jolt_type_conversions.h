/**************************************************************************/
/*  jolt_type_conversions.h                                               */

#pragma once

#include "core/math/aabb.h"
#include "core/math/color.h"
#include "core/math/plane.h"
#include "core/math/quaternion.h"
#include "core/math/transform_spatial.h"
#include "core/string/ustring.h"

#include <Jolt/Jolt.h>

#include <Jolt/Core/Color.h>
#include <Jolt/Geometry/AABox.h>
#include <Jolt/Geometry/Plane.h>
#include <Jolt/Math/Mat44.h>
#include <Jolt/Math/Quat.h>
#include <Jolt/Math/Vec3.h>

_FORCE_INLINE_ Vector3 to_orblit(const JPH::Vec3 &p_vec) {
	return Vector3((real_t)p_vec.GetX(), (real_t)p_vec.GetY(), (real_t)p_vec.GetZ());
}

_FORCE_INLINE_ Vector3 to_orblit(const JPH::DVec3 &p_vec) {
	return Vector3((real_t)p_vec.GetX(), (real_t)p_vec.GetY(), (real_t)p_vec.GetZ());
}

_FORCE_INLINE_ Basis to_orblit(const JPH::Quat &p_quat) {
	return Basis(Quaternion(p_quat.GetX(), p_quat.GetY(), p_quat.GetZ(), p_quat.GetW()));
}

_FORCE_INLINE_ Transform3D to_orblit(const JPH::Mat44 &p_mat) {
	return Transform3D(
			Vector3(p_mat(0, 0), p_mat(1, 0), p_mat(2, 0)),
			Vector3(p_mat(0, 1), p_mat(1, 1), p_mat(2, 1)),
			Vector3(p_mat(0, 2), p_mat(1, 2), p_mat(2, 2)),
			Vector3(p_mat(0, 3), p_mat(1, 3), p_mat(2, 3)));
}

_FORCE_INLINE_ Color to_orblit(const JPH::Color &p_color) {
	const float r = (float)p_color.r;
	const float g = (float)p_color.g;
	const float b = (float)p_color.b;
	const float a = (float)p_color.a;

	return Color(
			r == 0.0f ? 0.0f : 255.0f / r,
			g == 0.0f ? 0.0f : 255.0f / g,
			b == 0.0f ? 0.0f : 255.0f / b,
			a == 0.0f ? 0.0f : 255.0f / a);
}

_FORCE_INLINE_ String to_orblit(const JPH::String &p_str) {
	return String::utf8(p_str.c_str(), (int)p_str.length());
}

_FORCE_INLINE_ AABB to_orblit(const JPH::AABox &p_aabb) {
	return AABB(to_orblit(p_aabb.mMin), to_orblit(p_aabb.mMax - p_aabb.mMin));
}

_FORCE_INLINE_ Plane to_orblit(const JPH::Plane &p_plane) {
	return Plane(to_orblit(p_plane.GetNormal()), (real_t)p_plane.GetConstant());
}

_FORCE_INLINE_ JPH::Vec3 to_jolt(const Vector3 &p_vec) {
	return JPH::Vec3((float)p_vec.x, (float)p_vec.y, (float)p_vec.z);
}

_FORCE_INLINE_ JPH::Quat to_jolt(const Quaternion &p_quat) {
	return JPH::Quat((float)p_quat.x, (float)p_quat.y, (float)p_quat.z, (float)p_quat.w);
}

_FORCE_INLINE_ JPH::Quat to_jolt(const Basis &p_basis) {
	return to_jolt(p_basis.get_quaternion().normalized());
}

_FORCE_INLINE_ JPH::Mat44 to_jolt(const Transform3D &p_transform) {
	const Basis &b = p_transform.basis;
	const Vector3 &o = p_transform.origin;

	return JPH::Mat44(
			JPH::Vec4(b[0][0], b[1][0], b[2][0], 0.0f),
			JPH::Vec4(b[0][1], b[1][1], b[2][1], 0.0f),
			JPH::Vec4(b[0][2], b[1][2], b[2][2], 0.0f),
			JPH::Vec3(o.x, o.y, o.z));
}

_FORCE_INLINE_ JPH::Color to_jolt(const Color &p_color) {
	return JPH::Color((JPH::uint32)p_color.to_abgr32());
}

_FORCE_INLINE_ JPH::String to_jolt(const String &p_str) {
	const CharString str_utf8 = p_str.utf8();
	return JPH::String(str_utf8.get_data(), (size_t)str_utf8.length());
}

_FORCE_INLINE_ JPH::AABox to_jolt(const AABB &p_aabb) {
	return JPH::AABox(to_jolt(p_aabb.position), to_jolt(p_aabb.position + p_aabb.size));
}

_FORCE_INLINE_ JPH::Plane to_jolt(const Plane &p_plane) {
	return JPH::Plane(to_jolt(p_plane.normal), (float)-p_plane.d);
}

_FORCE_INLINE_ JPH::RVec3 to_jolt_r(const Vector3 &p_vec) {
	return JPH::RVec3(p_vec.x, p_vec.y, p_vec.z);
}

_FORCE_INLINE_ JPH::RMat44 to_jolt_r(const Transform3D &p_transform) {
	const Basis &b = p_transform.basis;
	const Vector3 &o = p_transform.origin;

	return JPH::RMat44(
			JPH::Vec4(b[0][0], b[1][0], b[2][0], 0.0f),
			JPH::Vec4(b[0][1], b[1][1], b[2][1], 0.0f),
			JPH::Vec4(b[0][2], b[1][2], b[2][2], 0.0f),
			JPH::RVec3(o.x, o.y, o.z));
}
