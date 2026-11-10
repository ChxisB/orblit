/**************************************************************************/
/*  upnp_device.cpp                                                       */

#include "upnp_device.h"

#include "core/object/class_registry.h"

UPNPDevice *(*UPNPDevice::_create)(bool p_notify_postinitialize) = nullptr;

void UPNPDevice::register_members() {
	ClassRegistry::bind_method(D_METHOD("is_valid_gateway"), &UPNPDevice::is_valid_gateway);
	ClassRegistry::bind_method(D_METHOD("query_external_address"), &UPNPDevice::query_external_address);
	ClassRegistry::bind_method(D_METHOD("add_port_mapping", "port", "port_internal", "desc", "proto", "duration"), &UPNPDevice::add_port_mapping, DEFVAL(0), DEFVAL(""), DEFVAL("UDP"), DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("delete_port_mapping", "port", "proto"), &UPNPDevice::delete_port_mapping, DEFVAL("UDP"));

	ClassRegistry::bind_method(D_METHOD("set_description_url", "url"), &UPNPDevice::set_description_url);
	ClassRegistry::bind_method(D_METHOD("get_description_url"), &UPNPDevice::get_description_url);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "description_url"), "set_description_url", "get_description_url");

	ClassRegistry::bind_method(D_METHOD("set_service_type", "type"), &UPNPDevice::set_service_type);
	ClassRegistry::bind_method(D_METHOD("get_service_type"), &UPNPDevice::get_service_type);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "service_type"), "set_service_type", "get_service_type");

	ClassRegistry::bind_method(D_METHOD("set_igd_control_url", "url"), &UPNPDevice::set_igd_control_url);
	ClassRegistry::bind_method(D_METHOD("get_igd_control_url"), &UPNPDevice::get_igd_control_url);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "igd_control_url"), "set_igd_control_url", "get_igd_control_url");

	ClassRegistry::bind_method(D_METHOD("set_igd_service_type", "type"), &UPNPDevice::set_igd_service_type);
	ClassRegistry::bind_method(D_METHOD("get_igd_service_type"), &UPNPDevice::get_igd_service_type);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "igd_service_type"), "set_igd_service_type", "get_igd_service_type");

	ClassRegistry::bind_method(D_METHOD("set_igd_our_addr", "addr"), &UPNPDevice::set_igd_our_addr);
	ClassRegistry::bind_method(D_METHOD("get_igd_our_addr"), &UPNPDevice::get_igd_our_addr);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "igd_our_addr"), "set_igd_our_addr", "get_igd_our_addr");

	ClassRegistry::bind_method(D_METHOD("set_igd_status", "status"), &UPNPDevice::set_igd_status);
	ClassRegistry::bind_method(D_METHOD("get_igd_status"), &UPNPDevice::get_igd_status);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "igd_status", PROPERTY_HINT_ENUM), "set_igd_status", "get_igd_status");

	BIND_ENUM_CONSTANT(IGD_STATUS_OK);
	BIND_ENUM_CONSTANT(IGD_STATUS_HTTP_ERROR);
	BIND_ENUM_CONSTANT(IGD_STATUS_HTTP_EMPTY);
	BIND_ENUM_CONSTANT(IGD_STATUS_NO_IGD);
	BIND_ENUM_CONSTANT(IGD_STATUS_DISCONNECTED);
	BIND_ENUM_CONSTANT(IGD_STATUS_UNKNOWN_DEVICE);
	BIND_ENUM_CONSTANT(IGD_STATUS_INVALID_CONTROL);
	BIND_ENUM_CONSTANT(IGD_STATUS_UNKNOWN_ERROR);
}
