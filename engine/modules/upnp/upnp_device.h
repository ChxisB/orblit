/**************************************************************************/
/*  upnp_device.h                                                         */

#pragma once

#include "core/object/ref_counted.h"
#include "core/variant/type_info.h"

class UPNPDevice : public RefCounted {
	ORBLIT_CLASS(UPNPDevice, RefCounted);

protected:
	static void register_members();

	static UPNPDevice *(*_create)(bool p_notify_postinitialize);

public:
	enum IGDStatus {
		IGD_STATUS_OK,
		IGD_STATUS_HTTP_ERROR,
		IGD_STATUS_HTTP_EMPTY,
		IGD_STATUS_NO_IGD,
		IGD_STATUS_DISCONNECTED,
		IGD_STATUS_UNKNOWN_DEVICE,
		IGD_STATUS_INVALID_CONTROL,
		IGD_STATUS_UNKNOWN_ERROR,
	};

	static UPNPDevice *create(bool p_notify_postinitialize = true) {
		if (!_create) {
			return nullptr;
		}
		return _create(p_notify_postinitialize);
	}

	virtual void set_description_url(const String &url) = 0;
	virtual String get_description_url() const = 0;

	virtual void set_service_type(const String &type) = 0;
	virtual String get_service_type() const = 0;

	virtual void set_igd_control_url(const String &url) = 0;
	virtual String get_igd_control_url() const = 0;

	virtual void set_igd_service_type(const String &type) = 0;
	virtual String get_igd_service_type() const = 0;

	virtual void set_igd_our_addr(const String &addr) = 0;
	virtual String get_igd_our_addr() const = 0;

	virtual void set_igd_status(IGDStatus status) = 0;
	virtual IGDStatus get_igd_status() const = 0;

	virtual bool is_valid_gateway() const = 0;
	virtual String query_external_address() const = 0;
	virtual int add_port_mapping(int port, int port_internal = 0, String desc = "", String proto = "UDP", int duration = 0) const = 0;
	virtual int delete_port_mapping(int port, String proto = "UDP") const = 0;

	UPNPDevice() {}
	virtual ~UPNPDevice() {}
};

VARIANT_ENUM_CAST(UPNPDevice::IGDStatus)
