/**************************************************************************/
/*  test_scene_multiplayer.h                                              */

#pragma once

#include "../scene_multiplayer.h"
#include "../scene_replication_config.h"

#include "core/object/callable_mp.h"
#include "core/os/os.h"
#include "scene/planar/entity_planar.h"
#include "scene/stage/stage.h"
#include "tests/signal_watcher.h"
#include "tests/test_macros.h"
#include "tests/test_utils.h"

namespace TestSceneMultiplayer {
TEST_CASE("[Multiplayer][SceneMultiplayer] Defaults") {
	Ref<SceneMultiplayer> scene_multiplayer;
	scene_multiplayer.instantiate();

	REQUIRE(scene_multiplayer->has_multiplayer_peer());
	Ref<MultiplayerPeer> multiplayer_peer = scene_multiplayer->get_multiplayer_peer();
	REQUIRE_MESSAGE(Object::cast_to<OfflineMultiplayerPeer>(multiplayer_peer.ptr()) != nullptr, "By default it must be an OfflineMultiplayerPeer instance.");
	CHECK_EQ(scene_multiplayer->poll(), Error::OK);
	CHECK_EQ(scene_multiplayer->get_unique_id(), MultiplayerPeer::TARGET_PEER_SERVER);
	CHECK_EQ(scene_multiplayer->get_peer_ids(), Vector<int>());
	CHECK_EQ(scene_multiplayer->get_remote_sender_id(), 0);
	CHECK_EQ(scene_multiplayer->get_root_path(), EntityPath());
	CHECK(scene_multiplayer->get_connected_peers().is_empty());
	CHECK_FALSE(scene_multiplayer->is_refusing_new_connections());
	CHECK_FALSE(scene_multiplayer->is_object_decoding_allowed());
	CHECK(scene_multiplayer->is_server_relay_enabled());
	CHECK_EQ(scene_multiplayer->get_max_sync_packet_size(), 1350);
	CHECK_EQ(scene_multiplayer->get_max_delta_packet_size(), 65535);
	CHECK(scene_multiplayer->is_server());
}

TEST_CASE("[Multiplayer][SceneMultiplayer][Stage] Stage has a OfflineMultiplayerPeer by default") {
	Ref<SceneMultiplayer> scene_multiplayer = Stage::get_singleton()->get_multiplayer();
	REQUIRE(scene_multiplayer->has_multiplayer_peer());

	Ref<MultiplayerPeer> multiplayer_peer = scene_multiplayer->get_multiplayer_peer();
	REQUIRE_MESSAGE(Object::cast_to<OfflineMultiplayerPeer>(multiplayer_peer.ptr()) != nullptr, "By default it must be an OfflineMultiplayerPeer instance.");
}

TEST_CASE("[Multiplayer][SceneMultiplayer][Stage] Object configuration add/remove") {
	Ref<SceneMultiplayer> scene_multiplayer;
	scene_multiplayer.instantiate();

	SUBCASE("Returns invalid parameter") {
		CHECK_EQ(scene_multiplayer->object_configuration_add(nullptr, "ImInvalid"), Error::ERR_INVALID_PARAMETER);
		CHECK_EQ(scene_multiplayer->object_configuration_remove(nullptr, "ImInvalid"), Error::ERR_INVALID_PARAMETER);

		EntityPath foo_path("/Foo");
		EntityPath bar_path("/Bar");
		CHECK_EQ(scene_multiplayer->object_configuration_add(nullptr, foo_path), Error::OK);
		ERR_PRINT_OFF;
		CHECK_EQ(scene_multiplayer->object_configuration_remove(nullptr, bar_path), Error::ERR_INVALID_PARAMETER);
		ERR_PRINT_ON;
	}

	SUBCASE("Sets root path") {
		EntityPath foo_path("/Foo");
		CHECK_EQ(scene_multiplayer->object_configuration_add(nullptr, foo_path), Error::OK);

		CHECK_EQ(scene_multiplayer->get_root_path(), foo_path);
	}

	SUBCASE("Unsets root path") {
		EntityPath foo_path("/Foo");
		CHECK_EQ(scene_multiplayer->object_configuration_add(nullptr, foo_path), Error::OK);

		CHECK_EQ(scene_multiplayer->object_configuration_remove(nullptr, foo_path), Error::OK);
		CHECK_EQ(scene_multiplayer->get_root_path(), EntityPath());
	}

	SUBCASE("Add/Remove a MultiplayerSpawner") {
		Entity2D *node = memnew(Entity2D);
		MultiplayerSpawner *spawner = memnew(MultiplayerSpawner);

		CHECK_EQ(scene_multiplayer->object_configuration_add(node, spawner), Error::OK);
		CHECK_EQ(scene_multiplayer->object_configuration_remove(node, spawner), Error::OK);

		memdelete(spawner);
		memdelete(node);
	}

	SUBCASE("Add/Remove a MultiplayerSynchronizer") {
		Entity2D *node = memnew(Entity2D);
		MultiplayerSynchronizer *synchronizer = memnew(MultiplayerSynchronizer);

		CHECK_EQ(scene_multiplayer->object_configuration_add(node, synchronizer), Error::OK);
		CHECK_EQ(scene_multiplayer->object_configuration_remove(node, synchronizer), Error::OK);

		memdelete(synchronizer);
		memdelete(node);
	}
}

TEST_CASE("[Multiplayer][SceneMultiplayer] Root Path") {
	Ref<SceneMultiplayer> scene_multiplayer;
	scene_multiplayer.instantiate();

	SUBCASE("Is set") {
		EntityPath foo_path("/Foo");
		scene_multiplayer->set_root_path(foo_path);

		CHECK_EQ(scene_multiplayer->get_root_path(), foo_path);
	}

	SUBCASE("Fails when path is empty") {
		ERR_PRINT_OFF;
		scene_multiplayer->set_root_path(EntityPath());
		ERR_PRINT_ON;
	}

	SUBCASE("Fails when path is relative") {
		EntityPath foo_path("Foo");
		ERR_PRINT_OFF;
		scene_multiplayer->set_root_path(foo_path);
		ERR_PRINT_ON;

		CHECK_EQ(scene_multiplayer->get_root_path(), EntityPath());
	}
}

// This one could be a dummy callback because the current set of test is not actually testing the full auth flow.
static Variant auth_callback(Variant sv, Variant pvav) {
	return Variant();
}

TEST_CASE("[Multiplayer][SceneMultiplayer][Stage] Send Authentication") {
	Ref<SceneMultiplayer> scene_multiplayer;
	scene_multiplayer.instantiate();
	Stage::get_singleton()->set_multiplayer(scene_multiplayer);
	scene_multiplayer->set_auth_callback(callable_mp_static(auth_callback));

	SUBCASE("Is properly sent") {
		SIGNAL_WATCH(scene_multiplayer.ptr(), "peer_authenticating");

		// Adding a peer to MultiplayerPeer.
		Ref<MultiplayerPeer> multiplayer_peer = scene_multiplayer->get_multiplayer_peer();
		int peer_id = 42;
		multiplayer_peer->emit_signal(SNAME("peer_connected"), peer_id);
		SIGNAL_CHECK("peer_authenticating", { { peer_id } });

		CHECK_EQ(scene_multiplayer->send_auth(peer_id, String("It's me").to_ascii_buffer()), Error::OK);

		Vector<int> expected_peer_ids = { peer_id };
		CHECK_EQ(scene_multiplayer->get_authenticating_peer_ids(), expected_peer_ids);

		SIGNAL_UNWATCH(scene_multiplayer.ptr(), "peer_authenticating");
	}

	SUBCASE("peer_authentication_failed is emitted when a peer is deleted before authentication is completed") {
		SIGNAL_WATCH(scene_multiplayer.ptr(), "peer_authentication_failed");

		// Adding a peer to MultiplayerPeer.
		Ref<MultiplayerPeer> multiplayer_peer = scene_multiplayer->get_multiplayer_peer();
		int peer_id = 42;
		multiplayer_peer->emit_signal(SNAME("peer_connected"), peer_id);
		multiplayer_peer->emit_signal(SNAME("peer_disconnected"), peer_id);
		SIGNAL_CHECK("peer_authentication_failed", { { peer_id } });

		SIGNAL_UNWATCH(scene_multiplayer.ptr(), "peer_authentication_failed");
	}

	SUBCASE("peer_authentication_failed is emitted when authentication timeout") {
		SIGNAL_WATCH(scene_multiplayer.ptr(), "peer_authentication_failed");
		scene_multiplayer->set_auth_timeout(0.01);
		CHECK_EQ(scene_multiplayer->get_auth_timeout(), 0.01);

		// Adding two peesr to MultiplayerPeer.
		Ref<MultiplayerPeer> multiplayer_peer = scene_multiplayer->get_multiplayer_peer();
		int first_peer_id = 42;
		int second_peer_id = 84;
		multiplayer_peer->emit_signal(SNAME("peer_connected"), first_peer_id);
		multiplayer_peer->emit_signal(SNAME("peer_connected"), second_peer_id);

		// Let timeout happens.
		OS::get_singleton()->delay_usec(500000);

		CHECK_EQ(scene_multiplayer->poll(), Error::OK);

		SIGNAL_CHECK("peer_authentication_failed", Array({ { first_peer_id }, { second_peer_id } }));

		SIGNAL_UNWATCH(scene_multiplayer.ptr(), "peer_authentication_failed");
	}

	SUBCASE("Fails when there is no MultiplayerPeer configured") {
		scene_multiplayer->set_multiplayer_peer(nullptr);

		ERR_PRINT_OFF;
		CHECK_EQ(scene_multiplayer->send_auth(42, Vector<uint8_t>()), Error::ERR_UNCONFIGURED);
		ERR_PRINT_ON;
	}

	SUBCASE("Fails when the peer to send the auth is not pending") {
		ERR_PRINT_OFF;
		CHECK_EQ(scene_multiplayer->send_auth(42, String("It's me").to_ascii_buffer()), Error::ERR_INVALID_PARAMETER);
		ERR_PRINT_ON;
	}
}

TEST_CASE("[Multiplayer][SceneMultiplayer][Stage] Complete Authentication") {
	Ref<SceneMultiplayer> scene_multiplayer;
	scene_multiplayer.instantiate();
	Stage::get_singleton()->set_multiplayer(scene_multiplayer);
	scene_multiplayer->set_auth_callback(callable_mp_static(auth_callback));

	SUBCASE("Is properly completed") {
		Ref<MultiplayerPeer> multiplayer_peer = scene_multiplayer->get_multiplayer_peer();
		int peer_id = 42;
		multiplayer_peer->emit_signal(SNAME("peer_connected"), peer_id);
		CHECK_EQ(scene_multiplayer->send_auth(peer_id, String("It's me").to_ascii_buffer()), Error::OK);

		CHECK_EQ(scene_multiplayer->complete_auth(peer_id), Error::OK);
	}

	SUBCASE("Fails when there is no MultiplayerPeer configured") {
		scene_multiplayer->set_multiplayer_peer(nullptr);

		ERR_PRINT_OFF;
		CHECK_EQ(scene_multiplayer->complete_auth(42), Error::ERR_UNCONFIGURED);
		ERR_PRINT_ON;
	}

	SUBCASE("Fails when the peer to complete the auth is not pending") {
		ERR_PRINT_OFF;
		CHECK_EQ(scene_multiplayer->complete_auth(42), Error::ERR_INVALID_PARAMETER);
		ERR_PRINT_ON;
	}

	SUBCASE("Fails to send auth or completed for a second time") {
		Ref<MultiplayerPeer> multiplayer_peer = scene_multiplayer->get_multiplayer_peer();
		int peer_id = 42;
		multiplayer_peer->emit_signal(SNAME("peer_connected"), peer_id);
		CHECK_EQ(scene_multiplayer->send_auth(peer_id, String("It's me").to_ascii_buffer()), Error::OK);
		CHECK_EQ(scene_multiplayer->complete_auth(peer_id), Error::OK);

		ERR_PRINT_OFF;
		CHECK_EQ(scene_multiplayer->send_auth(peer_id, String("It's me").to_ascii_buffer()), Error::ERR_FILE_CANT_WRITE);
		CHECK_EQ(scene_multiplayer->complete_auth(peer_id), Error::ERR_FILE_CANT_WRITE);
		ERR_PRINT_ON;
	}
}

TEST_CASE("[SceneReplicationConfig] The removed sync and watch flags are refused") {
	Ref<SceneReplicationConfig> config;
	config.instantiate();
	const EntityPath path(":position");
	config->add_property(path);

	bool valid = true;
	config->set("properties/0/sync", true, &valid);
	CHECK_FALSE(valid);

	valid = true;
	config->set("properties/0/watch", true, &valid);
	CHECK_FALSE(valid);

	CHECK_EQ(config->property_get_replication_mode(path), SceneReplicationConfig::REPLICATION_MODE_ALWAYS);
}

} // namespace TestSceneMultiplayer
