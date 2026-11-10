/**************************************************************************/
/*  freedesktop_at_spi_monitor.cpp                                        */

#include "freedesktop_at_spi_monitor.h"

#ifdef DBUS_ENABLED

#ifdef SOWRAP_ENABLED
#include "dbus-so_wrap.h"
#else
#include <dbus/dbus.h>
#endif

#include <unistd.h>

#define BUS_OBJECT_NAME "org.a11y.Bus"
#define BUS_OBJECT_PATH "/org/a11y/bus"

#define BUS_INTERFACE_PROPERTIES "org.freedesktop.DBus.Properties"

void FreeDesktopAtSPIMonitor::monitor_thread_func(void *p_userdata) {
	FreeDesktopAtSPIMonitor *mon = (FreeDesktopAtSPIMonitor *)p_userdata;

	DBusError error;
	dbus_error_init(&error);

	DBusConnection *bus = dbus_bus_get(DBUS_BUS_SESSION, &error);
	if (dbus_error_is_set(&error)) {
		dbus_error_free(&error);
		mon->supported.clear();
		return;
	}

	static const char *iface = "org.a11y.Status";
	static const char *member_ac = "IsEnabled";
	static const char *member_sr = "ScreenReaderEnabled";

	while (!mon->exit_thread.is_set()) {
		DBusMessage *message = dbus_message_new_method_call(BUS_OBJECT_NAME, BUS_OBJECT_PATH, BUS_INTERFACE_PROPERTIES, "Get");

		dbus_message_append_args(
				message,
				DBUS_TYPE_STRING, &iface,
				DBUS_TYPE_STRING, &member_ac,
				DBUS_TYPE_INVALID);

		DBusMessage *reply = dbus_connection_send_with_reply_and_block(bus, message, 50, &error);
		dbus_message_unref(message);

		if (!dbus_error_is_set(&error)) {
			DBusMessageIter iter, iter_variant, iter_struct;
			dbus_bool_t result;
			dbus_message_iter_init(reply, &iter);
			dbus_message_iter_recurse(&iter, &iter_variant);
			switch (dbus_message_iter_get_arg_type(&iter_variant)) {
				case DBUS_TYPE_STRUCT: {
					dbus_message_iter_recurse(&iter_variant, &iter_struct);
					if (dbus_message_iter_get_arg_type(&iter_struct) == DBUS_TYPE_BOOLEAN) {
						dbus_message_iter_get_basic(&iter_struct, &result);
						if (result) {
							mon->ac_enabled.set();
						} else {
							mon->ac_enabled.clear();
						}
					}
				} break;
				case DBUS_TYPE_BOOLEAN: {
					dbus_message_iter_get_basic(&iter_variant, &result);
					if (result) {
						mon->ac_enabled.set();
					} else {
						mon->ac_enabled.clear();
					}
				} break;
				default:
					break;
			}
			dbus_message_unref(reply);
		} else {
			dbus_error_free(&error);
		}

		message = dbus_message_new_method_call(BUS_OBJECT_NAME, BUS_OBJECT_PATH, BUS_INTERFACE_PROPERTIES, "Get");

		dbus_message_append_args(
				message,
				DBUS_TYPE_STRING, &iface,
				DBUS_TYPE_STRING, &member_sr,
				DBUS_TYPE_INVALID);

		reply = dbus_connection_send_with_reply_and_block(bus, message, 50, &error);
		dbus_message_unref(message);

		if (!dbus_error_is_set(&error)) {
			DBusMessageIter iter, iter_variant, iter_struct;
			dbus_bool_t result;
			dbus_message_iter_init(reply, &iter);
			dbus_message_iter_recurse(&iter, &iter_variant);
			switch (dbus_message_iter_get_arg_type(&iter_variant)) {
				case DBUS_TYPE_STRUCT: {
					dbus_message_iter_recurse(&iter_variant, &iter_struct);
					if (dbus_message_iter_get_arg_type(&iter_struct) == DBUS_TYPE_BOOLEAN) {
						dbus_message_iter_get_basic(&iter_struct, &result);
						if (result) {
							mon->sr_enabled.set();
						} else {
							mon->sr_enabled.clear();
						}
					}
				} break;
				case DBUS_TYPE_BOOLEAN: {
					dbus_message_iter_get_basic(&iter_variant, &result);
					if (result) {
						mon->sr_enabled.set();
					} else {
						mon->sr_enabled.clear();
					}
				} break;
				default:
					break;
			}
			dbus_message_unref(reply);
		} else {
			dbus_error_free(&error);
		}

		usleep(50000);
	}

	dbus_connection_unref(bus);
}

FreeDesktopAtSPIMonitor::FreeDesktopAtSPIMonitor() {
	supported.set();
	sr_enabled.clear();
	exit_thread.clear();

	thread.start(FreeDesktopAtSPIMonitor::monitor_thread_func, this);
}

FreeDesktopAtSPIMonitor::~FreeDesktopAtSPIMonitor() {
	exit_thread.set();
	if (thread.is_started()) {
		thread.wait_to_finish();
	}
}

#endif // DBUS_ENABLED
