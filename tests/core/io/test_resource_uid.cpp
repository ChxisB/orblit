/**************************************************************************/
/*  test_resource_uid.cpp                                                 */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_resource_uid)

#include "core/io/resource_uid.h"

namespace TestResourceUID {

TEST_CASE("[ResourceUID] Must encode/decode maximum/minimum UID correctly") {
	CHECK_MESSAGE(ResourceUID::get_singleton()->id_to_text(0x7fffffffffffffff) == "uid://d4n4ub6itg400", "Maximum UID must encode correctly.");
	CHECK_MESSAGE(ResourceUID::get_singleton()->text_to_id("uid://d4n4ub6itg400") == 0x7fffffffffffffff, "Maximum UID must decode correctly.");

	CHECK_MESSAGE(ResourceUID::get_singleton()->id_to_text(0) == "uid://a", "Minimum UID must encode correctly.");
	CHECK_MESSAGE(ResourceUID::get_singleton()->text_to_id("uid://a") == 0, "Minimum UID must decode correctly.");
}

TEST_CASE("[ResourceUID] Must encode and decode invalid UIDs correctly") {
	ResourceUID *rid = ResourceUID::get_singleton();
	CHECK_MESSAGE(rid->id_to_text(-1) == "uid://<invalid>", "Invalid UID -1 must encode correctly.");
	CHECK_MESSAGE(rid->text_to_id("uid://<invalid>") == -1, "Invalid UID -1 must decode correctly.");

	CHECK_MESSAGE(rid->id_to_text(-2) == rid->id_to_text(-1), "Invalid UID -2 must encode to the same as -1.");

	CHECK_MESSAGE(rid->text_to_id("dm3rdgs30kfci") == -1, "UID without scheme must decode correctly.");
}

TEST_CASE("[ResourceUID] Must encode and decode various UIDs correctly") {
	ResourceUID *rid = ResourceUID::get_singleton();
	CHECK_MESSAGE(rid->id_to_text(1) == "uid://b", "UID 1 must encode correctly.");
	CHECK_MESSAGE(rid->text_to_id("uid://b") == 1, "UID 1 must decode correctly.");

	CHECK_MESSAGE(rid->id_to_text(8060368642360689600) == "uid://dm3rdgs30kfci", "A normal UID must encode correctly.");
	CHECK_MESSAGE(rid->text_to_id("uid://dm3rdgs30kfci") == 8060368642360689600, "A normal UID must decode correctly.");
}

} // namespace TestResourceUID
