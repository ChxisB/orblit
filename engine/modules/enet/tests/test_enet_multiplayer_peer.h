/**************************************************************************/
/*  test_enet_multiplayer_peer.h                                          */

#pragma once

#include "core/object/class_registry.h"
#include "scene/stage/multiplayer_peer.h"
#include "tests/test_macros.h"

namespace TestENetMultiplayerPeer {

// Uses ClassRegistry so the test doesn't need the ENet headers, which are only on the module's include path.
TEST_CASE("[ENetMultiplayerPeer] create_server sets the host channel limit") {
	Ref<MultiplayerPeer> peer = Object::cast_to<MultiplayerPeer>(ClassRegistry::instantiate("ENetMultiplayerPeer"));
	REQUIRE(peer.is_valid());

	SUBCASE("Default channels") {
		REQUIRE_EQ(int(peer->call("create_server", 0, 4)), OK);
		// 0 means the ENet maximum of 255.
		Object *host = peer->call("get_host");
		CHECK_EQ(int(host->call("get_max_channels")), 255);
	}

	SUBCASE("Custom channels") {
		REQUIRE_EQ(int(peer->call("create_server", 0, 4, 3)), OK);
		// 3 user channels plus the 2 reserved system channels.
		Object *host = peer->call("get_host");
		CHECK_EQ(int(host->call("get_max_channels")), 5);
	}

	peer->close();
}

} // namespace TestENetMultiplayerPeer
