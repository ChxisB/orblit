/**************************************************************************/
/*  test_multiplayer_spawner.h                                            */

#pragma once

#include "../multiplayer_spawner.h"

#include "core/object/class_registry.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "tests/test_macros.h"
#include "tests/test_utils.h"

namespace TestMultiplayerSpawner {
class Wasp : public Entity {
	ORBLIT_CLASS(Wasp, Entity);

	int _size = 0;

public:
	int get_size() const {
		return _size;
	}
	void set_size(int p_size) {
		_size = p_size;
	}

	Wasp() {
		set_name("Wasp");
		set_scene_file_path("wasp.oscn");
	}
};

class SpawnWasps : public Object {
	ORBLIT_CLASS(SpawnWasps, Object);

protected:
	static void register_members() {
		ClassRegistry::bind_method(D_METHOD("wasp", "size"), &SpawnWasps::create_wasps);
		{
			MethodInfo mi;
			mi.name = "wasp_error";
			mi.arguments.push_back(PropertyInfo(Variant::INT, "size"));

			ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "wasp_error", &SpawnWasps::create_wasps_error, mi, varray(), false);
		}
		ClassRegistry::bind_method(D_METHOD("echo", "size"), &SpawnWasps::echo_size);
	}

public:
	Wasp *create_wasps(int p_size) {
		Wasp *wasp = memnew(Wasp);
		wasp->set_size(p_size);
		return wasp;
	}

	Wasp *create_wasps_error(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
		return nullptr;
	}

	int echo_size(int p_size) {
		return p_size;
	}
};

TEST_CASE("[Multiplayer][MultiplayerSpawner] Defaults") {
	MultiplayerSpawner *multiplayer_spawner = memnew(MultiplayerSpawner);

	CHECK_EQ(multiplayer_spawner->get_configuration_warnings().size(), 1);
	CHECK_EQ(multiplayer_spawner->get_spawn_node(), nullptr);
	CHECK_EQ(multiplayer_spawner->get_spawnable_scene_count(), 0);
	CHECK_EQ(multiplayer_spawner->get_spawn_path(), EntityPath());
	CHECK_EQ(multiplayer_spawner->get_spawn_limit(), 0);
	CHECK_EQ(multiplayer_spawner->get_spawn_function(), Callable());

	memdelete(multiplayer_spawner);
}

TEST_CASE("[Multiplayer][MultiplayerSpawner][Stage] Spawn Path warning") {
	MultiplayerSpawner *multiplayer_spawner = memnew(MultiplayerSpawner);
	Stage::get_singleton()->get_root()->add_child(multiplayer_spawner);

	// If there is no spawn path, there should be a warning.
	PackedStringArray warning_messages = multiplayer_spawner->get_configuration_warnings();
	REQUIRE_EQ(warning_messages.size(), 1);
	CHECK_MESSAGE(warning_messages[0].contains("\"Spawn Path\""), "Invalid configuration warning");

	// If there is a spawn path, but it doesn't exist a node on it, there should be a warning.
	multiplayer_spawner->set_spawn_path(EntityPath("/root/Foo"));
	warning_messages = multiplayer_spawner->get_configuration_warnings();
	REQUIRE_EQ(warning_messages.size(), 1);
	CHECK_MESSAGE(warning_messages[0].contains("\"Spawn Path\""), "Invalid configuration warning");

	// If there is a spawn path and a node on it, shouldn't be a warning.
	Entity *foo = memnew(Entity);
	foo->set_name("Foo");
	Stage::get_singleton()->get_root()->add_child(foo);
	warning_messages = multiplayer_spawner->get_configuration_warnings();
	CHECK_EQ(warning_messages.size(), 0);

	memdelete(foo);
	memdelete(multiplayer_spawner);
}

TEST_CASE("[Multiplayer][MultiplayerSpawner][Stage] Spawn node") {
	MultiplayerSpawner *multiplayer_spawner = memnew(MultiplayerSpawner);
	Stage::get_singleton()->get_root()->add_child(multiplayer_spawner);
	CHECK_EQ(multiplayer_spawner->get_spawn_node(), nullptr);

	Entity *foo = memnew(Entity);
	foo->set_name("Foo");
	Stage::get_singleton()->get_root()->add_child(foo);

	SUBCASE("nullptr if spawn path doesn't exists") {
		multiplayer_spawner->set_spawn_path(EntityPath("/root/NotExists"));

		CHECK_EQ(multiplayer_spawner->get_spawn_node(), nullptr);
	}

	SUBCASE("Get it after setting spawn path with no signal connections") {
		multiplayer_spawner->set_spawn_path(EntityPath("/root/Foo"));

		CHECK_EQ(multiplayer_spawner->get_spawn_node(), foo);
		CHECK_FALSE(foo->has_connections("child_entered_stage"));
	}

	SUBCASE("Get it after setting spawn path with signal connections") {
		multiplayer_spawner->add_spawnable_scene("scene.oscn");
		multiplayer_spawner->set_spawn_path(EntityPath("/root/Foo"));

		CHECK_EQ(multiplayer_spawner->get_spawn_node(), foo);
		CHECK(foo->has_connections("child_entered_stage"));
	}

	SUBCASE("Set a new one should disconnect signals from the old one") {
		multiplayer_spawner->add_spawnable_scene("scene.oscn");
		multiplayer_spawner->set_spawn_path(EntityPath("/root/Foo"));

		CHECK_EQ(multiplayer_spawner->get_spawn_node(), foo);
		CHECK(foo->has_connections("child_entered_stage"));

		Entity *bar = memnew(Entity);
		bar->set_name("Bar");
		Stage::get_singleton()->get_root()->add_child(bar);
		multiplayer_spawner->set_spawn_path(EntityPath("/root/Bar"));

		CHECK_EQ(multiplayer_spawner->get_spawn_node(), bar);
		CHECK(bar->has_connections("child_entered_stage"));
		CHECK_FALSE(foo->has_connections("child_entered_stage"));

		memdelete(bar);
	}

	memdelete(foo);
	memdelete(multiplayer_spawner);
}

TEST_CASE("[Multiplayer][MultiplayerSpawner][Stage] Spawnable scene") {
	MultiplayerSpawner *multiplayer_spawner = memnew(MultiplayerSpawner);
	Stage::get_singleton()->get_root()->add_child(multiplayer_spawner);
	CHECK_EQ(multiplayer_spawner->get_spawnable_scene_count(), 0);

	SUBCASE("Add one") {
		multiplayer_spawner->add_spawnable_scene("scene.oscn");

		CHECK_EQ(multiplayer_spawner->get_spawnable_scene_count(), 1);
		CHECK_EQ(multiplayer_spawner->get_spawnable_scene(0), "scene.oscn");
	}

	SUBCASE("Add one and if there is a valid spawn path add a connection to it") {
		Entity *foo = memnew(Entity);
		foo->set_name("Foo");
		multiplayer_spawner->set_spawn_path(EntityPath("/root/Foo"));
		CHECK_FALSE(foo->has_connections("child_entered_stage"));

		// Adding now foo to the tree to avoid set_spawn_path() making the connection.
		Stage::get_singleton()->get_root()->add_child(foo);
		multiplayer_spawner->notification(Entity::NOTIFICATION_POST_ENTER_STAGE);
		CHECK_FALSE(foo->has_connections("child_entered_stage"));
		multiplayer_spawner->add_spawnable_scene("scene.oscn");
		CHECK(foo->has_connections("child_entered_stage"));

		memdelete(foo);
	}

	SUBCASE("Add multiple") {
		multiplayer_spawner->add_spawnable_scene("scene.oscn");
		multiplayer_spawner->add_spawnable_scene("other_scene.oscn");
		multiplayer_spawner->add_spawnable_scene("yet_another_scene.oscn");

		CHECK_EQ(multiplayer_spawner->get_spawnable_scene_count(), 3);
		CHECK_EQ(multiplayer_spawner->get_spawnable_scene(0), "scene.oscn");
		CHECK_EQ(multiplayer_spawner->get_spawnable_scene(1), "other_scene.oscn");
		CHECK_EQ(multiplayer_spawner->get_spawnable_scene(2), "yet_another_scene.oscn");
	}

	SUBCASE("Clear") {
		Entity *foo = memnew(Entity);
		foo->set_name("Foo");
		Stage::get_singleton()->get_root()->add_child(foo);
		multiplayer_spawner->set_spawn_path(EntityPath("/root/Foo"));

		multiplayer_spawner->add_spawnable_scene("scene.oscn");
		multiplayer_spawner->add_spawnable_scene("other_scene.oscn");
		multiplayer_spawner->add_spawnable_scene("yet_another_scene.oscn");
		CHECK_EQ(multiplayer_spawner->get_spawnable_scene_count(), 3);
		CHECK(foo->has_connections("child_entered_stage"));

		multiplayer_spawner->clear_spawnable_scenes();

		CHECK_EQ(multiplayer_spawner->get_spawnable_scene_count(), 0);
		CHECK_FALSE(foo->has_connections("child_entered_stage"));
	}

	memdelete(multiplayer_spawner);
}

TEST_CASE("[Multiplayer][MultiplayerSpawner][Stage] Instantiate custom") {
	MultiplayerSpawner *multiplayer_spawner = memnew(MultiplayerSpawner);
	Stage::get_singleton()->get_root()->add_child(multiplayer_spawner);
	CHECK_EQ(multiplayer_spawner->get_spawn_node(), nullptr);

	Entity *nest = memnew(Entity);
	nest->set_name("Nest");
	Stage::get_singleton()->get_root()->add_child(nest);
	multiplayer_spawner->set_spawn_path(EntityPath("/root/Nest"));

	SpawnWasps *spawn_wasps = memnew(SpawnWasps);

	SUBCASE("Instantiates a node properly") {
		multiplayer_spawner->add_spawnable_scene("wasp.oscn");

		multiplayer_spawner->set_spawn_limit(1);
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp"));
		Wasp *wasp = Object::cast_to<Wasp>(multiplayer_spawner->instantiate_custom(Variant(42)));
		CHECK_NE(wasp, nullptr);
		CHECK_EQ(wasp->get_name(), "Wasp");
		CHECK_EQ(wasp->get_size(), 42);

		memdelete(wasp);
	}

	SUBCASE("Instantiates multiple nodes properly if there is no spawn limit") {
		multiplayer_spawner->add_spawnable_scene("wasp.oscn");
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp"));

		for (int i = 0; i < 10; i++) {
			Wasp *wasp = Object::cast_to<Wasp>(multiplayer_spawner->instantiate_custom(Variant(i)));
			CHECK_NE(wasp, nullptr);
			CHECK_EQ(wasp->get_name(), "Wasp");
			CHECK_EQ(wasp->get_size(), i);
			nest->add_child(wasp, true);
		}
	}

	SUBCASE("Fails if spawn limit is reached") {
		multiplayer_spawner->add_spawnable_scene("wasp.oscn");

		multiplayer_spawner->set_spawn_limit(1);
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp"));

		// This one works.
		Wasp *wasp = Object::cast_to<Wasp>(multiplayer_spawner->instantiate_custom(Variant(42)));
		CHECK_NE(wasp, nullptr);
		CHECK_EQ(wasp->get_name(), "Wasp");
		CHECK_EQ(wasp->get_size(), 42);
		// Adding to the spawner node to get it tracked.
		nest->add_child(wasp);

		// This one fails because spawn limit is reached.
		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->instantiate_custom(Variant(255)), nullptr);
		ERR_PRINT_ON;

		memdelete(wasp);
	}

	SUBCASE("Fails if spawn function is not set") {
		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->instantiate_custom(Variant(42)), nullptr);
		ERR_PRINT_ON;
	}

	SUBCASE("Fails when spawn function fails") {
		multiplayer_spawner->add_spawnable_scene("wasp.oscn");

		multiplayer_spawner->set_spawn_limit(1);
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp_error"));

		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->instantiate_custom(Variant(42)), nullptr);
		ERR_PRINT_ON;
	}

	SUBCASE("Fails when spawn function not returns a node") {
		multiplayer_spawner->add_spawnable_scene("wasp.oscn");

		multiplayer_spawner->set_spawn_limit(1);
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "echo"));

		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->instantiate_custom(Variant(42)), nullptr);
		ERR_PRINT_ON;
	}

	memdelete(spawn_wasps);
	memdelete(nest);
	memdelete(multiplayer_spawner);
}

TEST_CASE("[Multiplayer][MultiplayerSpawner][Stage] Spawn") {
	MultiplayerSpawner *multiplayer_spawner = memnew(MultiplayerSpawner);

	SUBCASE("Fails because is not inside tree") {
		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->spawn(Variant(42)), nullptr);
		ERR_PRINT_ON;
	}

	Stage::get_singleton()->get_root()->add_child(multiplayer_spawner);
	CHECK_EQ(multiplayer_spawner->get_spawn_node(), nullptr);

	Entity *nest = memnew(Entity);
	nest->set_name("Nest");
	Stage::get_singleton()->get_root()->add_child(nest);
	multiplayer_spawner->set_spawn_path(EntityPath("/root/Nest"));

	SpawnWasps *spawn_wasps = memnew(SpawnWasps);
	multiplayer_spawner->add_spawnable_scene("wasp.oscn");

	SUBCASE("Spawns a node, track it and add it to spawn node") {
		multiplayer_spawner->set_spawn_limit(1);
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp"));
		Wasp *wasp = Object::cast_to<Wasp>(multiplayer_spawner->spawn(Variant(42)));
		CHECK_NE(wasp, nullptr);
		CHECK_EQ(wasp->get_name(), "Wasp");
		CHECK_EQ(wasp->get_size(), 42);
		CHECK_EQ(wasp->get_parent(), nest);
		CHECK_EQ(nest->get_child_count(), 1);
		CHECK_EQ(nest->get_child(0), wasp);
	}

	SUBCASE("Spawns multiple nodes properly if there is no spawn limit") {
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp"));

		for (int i = 0; i < 10; i++) {
			Wasp *wasp = Object::cast_to<Wasp>(multiplayer_spawner->spawn(Variant(i)));
			CHECK_NE(wasp, nullptr);
			CHECK_EQ(wasp->get_name(), "Wasp" + String((i == 0) ? "" : itos(i + 1)));
			CHECK_EQ(wasp->get_size(), i);
			CHECK_EQ(wasp->get_parent(), nest);
			CHECK_EQ(nest->get_child_count(), i + 1);
			CHECK_EQ(nest->get_child(i), wasp);
		}
	}

	SUBCASE("Fails if spawn limit is reached") {
		multiplayer_spawner->set_spawn_limit(1);
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp"));

		// This one works.
		Wasp *wasp = Object::cast_to<Wasp>(multiplayer_spawner->spawn(Variant(42)));
		CHECK_NE(wasp, nullptr);
		CHECK_EQ(wasp->get_name(), "Wasp");
		CHECK_EQ(wasp->get_size(), 42);
		CHECK_EQ(wasp->get_parent(), nest);
		CHECK_EQ(nest->get_child_count(), 1);
		CHECK_EQ(nest->get_child(0), wasp);

		// This one fails because spawn limit is reached.
		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->spawn(Variant(255)), nullptr);
		ERR_PRINT_ON;

		memdelete(wasp);
	}

	SUBCASE("Fails if spawn function is not set") {
		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->spawn(Variant(42)), nullptr);
		ERR_PRINT_ON;
	}

	SUBCASE("Fails if spawn node cannot be found") {
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "wasp"));
		multiplayer_spawner->set_spawn_path(EntityPath(""));

		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->spawn(Variant(42)), nullptr);
		ERR_PRINT_ON;
	}

	SUBCASE("Fails when instantiate_custom not returns a node") {
		multiplayer_spawner->add_spawnable_scene("wasp.oscn");

		multiplayer_spawner->set_spawn_limit(1);
		multiplayer_spawner->set_spawn_function(Callable(spawn_wasps, "echo"));

		ERR_PRINT_OFF;
		CHECK_EQ(multiplayer_spawner->spawn(Variant(42)), nullptr);
		ERR_PRINT_ON;
	}

	memdelete(spawn_wasps);
	memdelete(nest);
	memdelete(multiplayer_spawner);
}

} // namespace TestMultiplayerSpawner
