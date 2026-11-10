/**************************************************************************/
/*  upnp_device_miniupnp.cpp                                              */

#ifndef WEB_ENABLED

#include "upnp_device_miniupnp.h"

#include "upnp_miniupnp.h"

#include "core/object/class_registry.h"

#include <miniupnpc/upnpcommands.h>

UPNPDevice *UPNPDeviceMiniUPNP::_create(bool p_notify_postinitialize) {
	return static_cast<UPNPDevice *>(ClassRegistry::creator<UPNPDeviceMiniUPNP>(p_notify_postinitialize));
}

void UPNPDeviceMiniUPNP::make_default() {
	UPNPDevice::_create = UPNPDeviceMiniUPNP::_create;
}

String UPNPDeviceMiniUPNP::query_external_address() const {
	ERR_FAIL_COND_V_MSG(!is_valid_gateway(), "", "The Internet Gateway Device must be valid.");

	char addr[16];
	int i = UPNP_GetExternalIPAddress(
			igd_control_url.utf8().get_data(),
			igd_service_type.utf8().get_data(),
			(char *)&addr);

	ERR_FAIL_COND_V_MSG(i != UPNPCOMMAND_SUCCESS, "", "Couldn't get external IP address.");

	return String(addr);
}

int UPNPDeviceMiniUPNP::add_port_mapping(int port, int port_internal, String desc, String proto, int duration) const {
	ERR_FAIL_COND_V_MSG(!is_valid_gateway(), UPNP::UPNP_RESULT_INVALID_GATEWAY, "The Internet Gateway Device must be valid.");
	ERR_FAIL_COND_V_MSG(port < 1 || port > 65535, UPNP::UPNP_RESULT_INVALID_PORT, "The port number must be set between 1 and 65535 (inclusive).");
	ERR_FAIL_COND_V_MSG(port_internal < 0 || port_internal > 65535, UPNP::UPNP_RESULT_INVALID_PORT, "The port number must be set between 0 and 65535 (inclusive)."); // Needs to allow 0 because 0 signifies "use external port as internal port"
	ERR_FAIL_COND_V_MSG(proto != "UDP" && proto != "TCP", UPNP::UPNP_RESULT_INVALID_PROTOCOL, "The protocol must be either TCP or UDP.");
	ERR_FAIL_COND_V_MSG(duration < 0, UPNP::UPNP_RESULT_INVALID_DURATION, "The port mapping's lease duration can't be negative.");

	if (port_internal < 1) {
		port_internal = port;
	}

	int i = UPNP_AddPortMapping(
			igd_control_url.utf8().get_data(),
			igd_service_type.utf8().get_data(),
			itos(port).utf8().get_data(),
			itos(port_internal).utf8().get_data(),
			igd_our_addr.utf8().get_data(),
			desc.is_empty() ? nullptr : desc.utf8().get_data(),
			proto.utf8().get_data(),
			nullptr, // Remote host, always nullptr as IGDs don't support it
			duration > 0 ? itos(duration).utf8().get_data() : nullptr);

	ERR_FAIL_COND_V_MSG(i != UPNPCOMMAND_SUCCESS, UPNPMiniUPNP::upnp_result(i), "Couldn't add port mapping.");

	return UPNP::UPNP_RESULT_SUCCESS;
}

int UPNPDeviceMiniUPNP::delete_port_mapping(int port, String proto) const {
	ERR_FAIL_COND_V_MSG(port < 1 || port > 65535, UPNP::UPNP_RESULT_INVALID_PORT, "The port number must be set between 1 and 65535 (inclusive).");
	ERR_FAIL_COND_V_MSG(proto != "UDP" && proto != "TCP", UPNP::UPNP_RESULT_INVALID_PROTOCOL, "The protocol must be either TCP or UDP.");

	int i = UPNP_DeletePortMapping(
			igd_control_url.utf8().get_data(),
			igd_service_type.utf8().get_data(),
			itos(port).utf8().get_data(),
			proto.utf8().get_data(),
			nullptr // Remote host, always nullptr as IGDs don't support it
	);

	ERR_FAIL_COND_V_MSG(i != UPNPCOMMAND_SUCCESS, UPNPMiniUPNP::upnp_result(i), "Couldn't delete port mapping.");

	return UPNP::UPNP_RESULT_SUCCESS;
}

void UPNPDeviceMiniUPNP::set_description_url(const String &url) {
	description_url = url;
}

String UPNPDeviceMiniUPNP::get_description_url() const {
	return description_url;
}

void UPNPDeviceMiniUPNP::set_service_type(const String &type) {
	service_type = type;
}

String UPNPDeviceMiniUPNP::get_service_type() const {
	return service_type;
}

void UPNPDeviceMiniUPNP::set_igd_control_url(const String &url) {
	igd_control_url = url;
}

String UPNPDeviceMiniUPNP::get_igd_control_url() const {
	return igd_control_url;
}

void UPNPDeviceMiniUPNP::set_igd_service_type(const String &type) {
	igd_service_type = type;
}

String UPNPDeviceMiniUPNP::get_igd_service_type() const {
	return igd_service_type;
}

void UPNPDeviceMiniUPNP::set_igd_our_addr(const String &addr) {
	igd_our_addr = addr;
}

String UPNPDeviceMiniUPNP::get_igd_our_addr() const {
	return igd_our_addr;
}

void UPNPDeviceMiniUPNP::set_igd_status(IGDStatus status) {
	igd_status = status;
}

UPNPDeviceMiniUPNP::IGDStatus UPNPDeviceMiniUPNP::get_igd_status() const {
	return igd_status;
}

bool UPNPDeviceMiniUPNP::is_valid_gateway() const {
	return igd_status == IGD_STATUS_OK;
}

#endif // WEB_ENABLED
