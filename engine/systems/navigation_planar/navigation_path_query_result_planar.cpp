/**************************************************************************/
/*  navigation_path_query_result_planar.cpp                                   */

#include "navigation_path_query_result_planar.h"

#include "core/object/class_registry.h"

void NavigationPathQueryResult2D::set_path(const Vector<Vector2> &p_path) {
	path = p_path;
}

const Vector<Vector2> &NavigationPathQueryResult2D::get_path() const {
	return path;
}

void NavigationPathQueryResult2D::set_path_types(const Vector<int32_t> &p_path_types) {
	path_types = p_path_types;
}

const Vector<int32_t> &NavigationPathQueryResult2D::get_path_types() const {
	return path_types;
}

void NavigationPathQueryResult2D::set_path_rids(const TypedArray<RID> &p_path_rids) {
	path_rids = p_path_rids;
}

TypedArray<RID> NavigationPathQueryResult2D::get_path_rids() const {
	return path_rids;
}

void NavigationPathQueryResult2D::set_path_owner_ids(const Vector<int64_t> &p_path_owner_ids) {
	path_owner_ids = p_path_owner_ids;
}

const Vector<int64_t> &NavigationPathQueryResult2D::get_path_owner_ids() const {
	return path_owner_ids;
}

void NavigationPathQueryResult2D::reset() {
	path.clear();
	path_types.clear();
	path_rids.clear();
	path_owner_ids.clear();
}

void NavigationPathQueryResult2D::set_data(const LocalVector<Vector2> &p_path, const LocalVector<int32_t> &p_path_types, const LocalVector<RID> &p_path_rids, const LocalVector<int64_t> &p_path_owner_ids) {
	path.clear();
	path_types.clear();
	path_rids.clear();
	path_owner_ids.clear();

	{
		path.resize(p_path.size());
		Vector2 *w = path.ptrw();
		const Vector2 *r = p_path.ptr();
		for (uint32_t i = 0; i < p_path.size(); i++) {
			w[i] = r[i];
		}
	}

	{
		path_types.resize(p_path_types.size());
		int32_t *w = path_types.ptrw();
		const int32_t *r = p_path_types.ptr();
		for (uint32_t i = 0; i < p_path_types.size(); i++) {
			w[i] = r[i];
		}
	}

	{
		path_rids.resize(p_path_rids.size());
		for (uint32_t i = 0; i < p_path_rids.size(); i++) {
			path_rids[i] = p_path_rids[i];
		}
	}

	{
		path_owner_ids.resize(p_path_owner_ids.size());
		int64_t *w = path_owner_ids.ptrw();
		const int64_t *r = p_path_owner_ids.ptr();
		for (uint32_t i = 0; i < p_path_owner_ids.size(); i++) {
			w[i] = r[i];
		}
	}
}

void NavigationPathQueryResult2D::set_path_length(float p_length) {
	path_length = p_length;
}

float NavigationPathQueryResult2D::get_path_length() const {
	return path_length;
}

void NavigationPathQueryResult2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_path", "path"), &NavigationPathQueryResult2D::set_path);
	ClassRegistry::bind_method(D_METHOD("get_path"), &NavigationPathQueryResult2D::get_path);

	ClassRegistry::bind_method(D_METHOD("set_path_types", "path_types"), &NavigationPathQueryResult2D::set_path_types);
	ClassRegistry::bind_method(D_METHOD("get_path_types"), &NavigationPathQueryResult2D::get_path_types);

	ClassRegistry::bind_method(D_METHOD("set_path_rids", "path_rids"), &NavigationPathQueryResult2D::set_path_rids);
	ClassRegistry::bind_method(D_METHOD("get_path_rids"), &NavigationPathQueryResult2D::get_path_rids);

	ClassRegistry::bind_method(D_METHOD("set_path_owner_ids", "path_owner_ids"), &NavigationPathQueryResult2D::set_path_owner_ids);
	ClassRegistry::bind_method(D_METHOD("get_path_owner_ids"), &NavigationPathQueryResult2D::get_path_owner_ids);

	ClassRegistry::bind_method(D_METHOD("set_path_length", "length"), &NavigationPathQueryResult2D::set_path_length);
	ClassRegistry::bind_method(D_METHOD("get_path_length"), &NavigationPathQueryResult2D::get_path_length);

	ClassRegistry::bind_method(D_METHOD("reset"), &NavigationPathQueryResult2D::reset);

	ADD_PROPERTY(PropertyInfo(Variant::PACKED_VECTOR2_ARRAY, "path"), "set_path", "get_path");
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT32_ARRAY, "path_types"), "set_path_types", "get_path_types");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "path_rids", PROPERTY_HINT_ARRAY_TYPE, "RID"), "set_path_rids", "get_path_rids");
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_INT64_ARRAY, "path_owner_ids"), "set_path_owner_ids", "get_path_owner_ids");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "path_length"), "set_path_length", "get_path_length");

	BIND_ENUM_CONSTANT(PATH_SEGMENT_TYPE_REGION);
	BIND_ENUM_CONSTANT(PATH_SEGMENT_TYPE_LINK);
}
