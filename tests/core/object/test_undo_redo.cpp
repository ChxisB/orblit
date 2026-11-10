/**************************************************************************/
/*  test_undo_redo.cpp                                                    */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_undo_redo)

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "core/object/undo_redo.h"

namespace TestUndoRedo {

class _TestUndoRedoObject : public Object {
	ORBLIT_CLASS(_TestUndoRedoObject, Object);
	int property_value = 0;

protected:
	static void register_members() {
		ClassRegistry::bind_method(D_METHOD("set_property", "property"), &_TestUndoRedoObject::set_property);
		ClassRegistry::bind_method(D_METHOD("get_property"), &_TestUndoRedoObject::get_property);
		ADD_PROPERTY(PropertyInfo(Variant::INT, "property"), "set_property", "get_property");
	}

public:
	void set_property(int value) { property_value = value; }
	int get_property() const { return property_value; }
	void add_to_property(int value) { property_value += value; }
	void subtract_from_property(int value) { property_value -= value; }
};

void set_property_action(UndoRedo *undo_redo, const String &name, _TestUndoRedoObject *test_object, int value, UndoRedo::MergeMode merge_mode = UndoRedo::MERGE_DISABLE) {
	undo_redo->create_action(name, merge_mode);
	undo_redo->add_do_property(test_object, "property", value);
	undo_redo->add_undo_property(test_object, "property", test_object->get_property());
	undo_redo->commit_action();
}

void increment_property_action(UndoRedo *undo_redo, const String &name, _TestUndoRedoObject *test_object, int value, UndoRedo::MergeMode merge_mode = UndoRedo::MERGE_DISABLE) {
	undo_redo->create_action(name, merge_mode);
	undo_redo->add_do_method(callable_mp(test_object, &_TestUndoRedoObject::add_to_property).bind(value));
	undo_redo->add_undo_method(callable_mp(test_object, &_TestUndoRedoObject::subtract_from_property).bind(value));
	undo_redo->commit_action();
}

TEST_CASE("[UndoRedo] Simple Property UndoRedo") {
	ORBLIT_REGISTER_CLASS(_TestUndoRedoObject);
	UndoRedo *undo_redo = memnew(UndoRedo());

	_TestUndoRedoObject *test_object = memnew(_TestUndoRedoObject());

	CHECK(test_object->get_property() == 0);
	CHECK(undo_redo->get_version() == 1);
	CHECK(undo_redo->get_history_count() == 0);

	set_property_action(undo_redo, "Set Property", test_object, 10);

	CHECK(test_object->get_property() == 10);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	undo_redo->undo();

	CHECK(test_object->get_property() == 0);
	CHECK(undo_redo->get_version() == 1);
	CHECK(undo_redo->get_history_count() == 1);

	undo_redo->redo();

	CHECK(test_object->get_property() == 10);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	set_property_action(undo_redo, "Set Property", test_object, 100);

	CHECK(test_object->get_property() == 100);
	CHECK(undo_redo->get_version() == 3);
	CHECK(undo_redo->get_history_count() == 2);

	set_property_action(undo_redo, "Set Property", test_object, 1000);

	CHECK(test_object->get_property() == 1000);
	CHECK(undo_redo->get_version() == 4);
	CHECK(undo_redo->get_history_count() == 3);

	undo_redo->undo();

	CHECK(test_object->get_property() == 100);
	CHECK(undo_redo->get_version() == 3);
	CHECK(undo_redo->get_history_count() == 3);

	memdelete(test_object);
	memdelete(undo_redo);
}

TEST_CASE("[UndoRedo] Merge Property UndoRedo") {
	ORBLIT_REGISTER_CLASS(_TestUndoRedoObject);
	UndoRedo *undo_redo = memnew(UndoRedo());

	_TestUndoRedoObject *test_object = memnew(_TestUndoRedoObject());

	CHECK(test_object->get_property() == 0);
	CHECK(undo_redo->get_version() == 1);
	CHECK(undo_redo->get_history_count() == 0);

	set_property_action(undo_redo, "Merge Action 1", test_object, 10, UndoRedo::MERGE_ALL);

	CHECK(test_object->get_property() == 10);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	set_property_action(undo_redo, "Merge Action 1", test_object, 100, UndoRedo::MERGE_ALL);

	CHECK(test_object->get_property() == 100);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	set_property_action(undo_redo, "Merge Action 1", test_object, 1000, UndoRedo::MERGE_ALL);

	CHECK(test_object->get_property() == 1000);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	memdelete(test_object);
	memdelete(undo_redo);
}

TEST_CASE("[UndoRedo] Merge Method UndoRedo") {
	ORBLIT_REGISTER_CLASS(_TestUndoRedoObject);
	UndoRedo *undo_redo = memnew(UndoRedo());

	_TestUndoRedoObject *test_object = memnew(_TestUndoRedoObject());

	CHECK(test_object->get_property() == 0);
	CHECK(undo_redo->get_version() == 1);
	CHECK(undo_redo->get_history_count() == 0);

	increment_property_action(undo_redo, "Merge Increment 1", test_object, 10, UndoRedo::MERGE_ALL);

	CHECK(test_object->get_property() == 10);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	increment_property_action(undo_redo, "Merge Increment 1", test_object, 10, UndoRedo::MERGE_ALL);

	CHECK(test_object->get_property() == 20);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	increment_property_action(undo_redo, "Merge Increment 1", test_object, 10, UndoRedo::MERGE_ALL);

	CHECK(test_object->get_property() == 30);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	undo_redo->undo();

	CHECK(test_object->get_property() == 0);
	CHECK(undo_redo->get_version() == 1);
	CHECK(undo_redo->get_history_count() == 1);

	undo_redo->redo();

	CHECK(test_object->get_property() == 30);
	CHECK(undo_redo->get_version() == 2);
	CHECK(undo_redo->get_history_count() == 1);

	memdelete(test_object);
	memdelete(undo_redo);
}

} // namespace TestUndoRedo
