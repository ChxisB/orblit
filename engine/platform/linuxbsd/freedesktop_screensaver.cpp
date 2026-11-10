/**************************************************************************/
/*  freedesktop_screensaver.cpp                                           */

#include "freedesktop_screensaver.h"

#ifdef DBUS_ENABLED

#include "core/config/project_settings.h"

#ifdef SOWRAP_ENABLED
#include "dbus-so_wrap.h"
#else
#include <dbus/dbus.h>
#endif

#define BUS_OBJECT_NAME "org.freedesktop.ScreenSaver"
#define BUS_OBJECT_PATH "/org/freedesktop/ScreenSaver"
#define BUS_INTERFACE "org.freedesktop.ScreenSaver"

void FreeDesktopScreenSaver::inhibit() {
	if (unsupported) {
		return;
	}

	DBusError error;
	dbus_error_init(&error);

	DBusConnection *bus = dbus_bus_get(DBUS_BUS_SESSION, &error);
	if (dbus_error_is_set(&error)) {
		dbus_error_free(&error);
		unsupported = true;
		return;
	}

	String app_name_string = GLOBAL_GET("application/config/name");
	CharString app_name_utf8 = app_name_string.utf8();
	const char *app_name = app_name_string.is_empty() ? "Orblit Engine" : app_name_utf8.get_data();

	const char *reason = "Running Orblit Engine project";

	DBusMessage *message = dbus_message_new_method_call(
			BUS_OBJECT_NAME, BUS_OBJECT_PATH, BUS_INTERFACE,
			"Inhibit");
	dbus_message_append_args(
			message,
			DBUS_TYPE_STRING, &app_name,
			DBUS_TYPE_STRING, &reason,
			DBUS_TYPE_INVALID);

	DBusMessage *reply = dbus_connection_send_with_reply_and_block(bus, message, 50, &error);
	dbus_message_unref(message);
	if (dbus_error_is_set(&error)) {
		dbus_error_free(&error);
		dbus_connection_unref(bus);
		unsupported = true;
		return;
	}

	DBusMessageIter reply_iter;
	dbus_message_iter_init(reply, &reply_iter);
	dbus_message_iter_get_basic(&reply_iter, &cookie);
	print_verbose("FreeDesktopScreenSaver: Acquired screensaver inhibition cookie: " + uitos(cookie));

	dbus_message_unref(reply);
	dbus_connection_unref(bus);
}

void FreeDesktopScreenSaver::uninhibit() {
	if (unsupported) {
		return;
	}

	DBusError error;
	dbus_error_init(&error);

	DBusConnection *bus = dbus_bus_get(DBUS_BUS_SESSION, &error);
	if (dbus_error_is_set(&error)) {
		dbus_error_free(&error);
		unsupported = true;
		return;
	}

	DBusMessage *message = dbus_message_new_method_call(
			BUS_OBJECT_NAME, BUS_OBJECT_PATH, BUS_INTERFACE,
			"UnInhibit");
	dbus_message_append_args(
			message,
			DBUS_TYPE_UINT32, &cookie,
			DBUS_TYPE_INVALID);

	DBusMessage *reply = dbus_connection_send_with_reply_and_block(bus, message, 50, &error);
	dbus_message_unref(message);
	if (dbus_error_is_set(&error)) {
		dbus_error_free(&error);
		dbus_connection_unref(bus);
		unsupported = true;
		return;
	}

	print_verbose("FreeDesktopScreenSaver: Released screensaver inhibition cookie: " + uitos(cookie));

	dbus_message_unref(reply);
	dbus_connection_unref(bus);
}

FreeDesktopScreenSaver::FreeDesktopScreenSaver() {
}

#endif // DBUS_ENABLED
