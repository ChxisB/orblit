/**************************************************************************/
/*  upnp.cpp                                                              */

#include "upnp.h"

#include "core/object/class_registry.h"

UPNP *(*UPNP::_create)(bool p_notify_postinitialize) = nullptr;

void UPNP::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_device_count"), &UPNP::get_device_count);
	ClassRegistry::bind_method(D_METHOD("get_device", "index"), &UPNP::get_device);
	ClassRegistry::bind_method(D_METHOD("add_device", "device"), &UPNP::add_device);
	ClassRegistry::bind_method(D_METHOD("set_device", "index", "device"), &UPNP::set_device);
	ClassRegistry::bind_method(D_METHOD("remove_device", "index"), &UPNP::remove_device);
	ClassRegistry::bind_method(D_METHOD("clear_devices"), &UPNP::clear_devices);

	ClassRegistry::bind_method(D_METHOD("get_gateway"), &UPNP::get_gateway);

	ClassRegistry::bind_method(D_METHOD("discover", "timeout", "ttl", "device_filter"), &UPNP::discover, DEFVAL(2000), DEFVAL(2), DEFVAL("InternetGatewayDevice"));

	ClassRegistry::bind_method(D_METHOD("query_external_address"), &UPNP::query_external_address);

	ClassRegistry::bind_method(D_METHOD("add_port_mapping", "port", "port_internal", "desc", "proto", "duration"), &UPNP::add_port_mapping, DEFVAL(0), DEFVAL(""), DEFVAL("UDP"), DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("delete_port_mapping", "port", "proto"), &UPNP::delete_port_mapping, DEFVAL("UDP"));

	ClassRegistry::bind_method(D_METHOD("set_discover_multicast_if", "m_if"), &UPNP::set_discover_multicast_if);
	ClassRegistry::bind_method(D_METHOD("get_discover_multicast_if"), &UPNP::get_discover_multicast_if);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "discover_multicast_if"), "set_discover_multicast_if", "get_discover_multicast_if");

	ClassRegistry::bind_method(D_METHOD("set_discover_local_port", "port"), &UPNP::set_discover_local_port);
	ClassRegistry::bind_method(D_METHOD("get_discover_local_port"), &UPNP::get_discover_local_port);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "discover_local_port", PROPERTY_HINT_RANGE, "0,65535"), "set_discover_local_port", "get_discover_local_port");

	ClassRegistry::bind_method(D_METHOD("set_discover_ipv6", "ipv6"), &UPNP::set_discover_ipv6);
	ClassRegistry::bind_method(D_METHOD("is_discover_ipv6"), &UPNP::is_discover_ipv6);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "discover_ipv6"), "set_discover_ipv6", "is_discover_ipv6");

	BIND_ENUM_CONSTANT(UPNP_RESULT_SUCCESS);
	BIND_ENUM_CONSTANT(UPNP_RESULT_NOT_AUTHORIZED);
	BIND_ENUM_CONSTANT(UPNP_RESULT_PORT_MAPPING_NOT_FOUND);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INCONSISTENT_PARAMETERS);
	BIND_ENUM_CONSTANT(UPNP_RESULT_NO_SUCH_ENTRY_IN_ARRAY);
	BIND_ENUM_CONSTANT(UPNP_RESULT_ACTION_FAILED);
	BIND_ENUM_CONSTANT(UPNP_RESULT_SRC_IP_WILDCARD_NOT_PERMITTED);
	BIND_ENUM_CONSTANT(UPNP_RESULT_EXT_PORT_WILDCARD_NOT_PERMITTED);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INT_PORT_WILDCARD_NOT_PERMITTED);
	BIND_ENUM_CONSTANT(UPNP_RESULT_REMOTE_HOST_MUST_BE_WILDCARD);
	BIND_ENUM_CONSTANT(UPNP_RESULT_EXT_PORT_MUST_BE_WILDCARD);
	BIND_ENUM_CONSTANT(UPNP_RESULT_NO_PORT_MAPS_AVAILABLE);
	BIND_ENUM_CONSTANT(UPNP_RESULT_CONFLICT_WITH_OTHER_MECHANISM);
	BIND_ENUM_CONSTANT(UPNP_RESULT_CONFLICT_WITH_OTHER_MAPPING);
	BIND_ENUM_CONSTANT(UPNP_RESULT_SAME_PORT_VALUES_REQUIRED);
	BIND_ENUM_CONSTANT(UPNP_RESULT_ONLY_PERMANENT_LEASE_SUPPORTED);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INVALID_GATEWAY);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INVALID_PORT);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INVALID_PROTOCOL);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INVALID_DURATION);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INVALID_ARGS);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INVALID_RESPONSE);
	BIND_ENUM_CONSTANT(UPNP_RESULT_INVALID_PARAM);
	BIND_ENUM_CONSTANT(UPNP_RESULT_HTTP_ERROR);
	BIND_ENUM_CONSTANT(UPNP_RESULT_SOCKET_ERROR);
	BIND_ENUM_CONSTANT(UPNP_RESULT_MEM_ALLOC_ERROR);
	BIND_ENUM_CONSTANT(UPNP_RESULT_NO_GATEWAY);
	BIND_ENUM_CONSTANT(UPNP_RESULT_NO_DEVICES);
	BIND_ENUM_CONSTANT(UPNP_RESULT_UNKNOWN_ERROR);
}
