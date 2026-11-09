/**************************************************************************/
/*  upnp_device_miniupnp.h                                                */

#pragma once

#ifndef WEB_ENABLED

#include "upnp_device.h"

class UPNPDeviceMiniUPNP : public UPNPDevice {
	ORBLIT_CLASS(UPNPDeviceMiniUPNP, UPNPDevice);

private:
	static UPNPDevice *_create(bool p_notify_postinitialize);

	String description_url;
	String service_type;
	String igd_control_url;
	String igd_service_type;
	String igd_our_addr;
	IGDStatus igd_status = IGD_STATUS_UNKNOWN_ERROR;

public:
	static void make_default();

	virtual void set_description_url(const String &url) override;
	virtual String get_description_url() const override;

	virtual void set_service_type(const String &type) override;
	virtual String get_service_type() const override;

	virtual void set_igd_control_url(const String &url) override;
	virtual String get_igd_control_url() const override;

	virtual void set_igd_service_type(const String &type) override;
	virtual String get_igd_service_type() const override;

	virtual void set_igd_our_addr(const String &addr) override;
	virtual String get_igd_our_addr() const override;

	virtual void set_igd_status(IGDStatus status) override;
	virtual IGDStatus get_igd_status() const override;

	virtual bool is_valid_gateway() const override;
	virtual String query_external_address() const override;
	virtual int add_port_mapping(int port, int port_internal = 0, String desc = "", String proto = "UDP", int duration = 0) const override;
	virtual int delete_port_mapping(int port, String proto = "UDP") const override;

	UPNPDeviceMiniUPNP() {}
	virtual ~UPNPDeviceMiniUPNP() {}
};

#endif // WEB_ENABLED
