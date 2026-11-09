/**************************************************************************/
/*  os_linuxbsd.h                                                         */

#pragma once

#include "crash_handler_linuxbsd.h"

#include "backends/alsa/audio_driver_alsa.h"
#include "backends/alsamidi/midi_driver_alsamidi.h"
#include "backends/pulseaudio/audio_driver_pulseaudio.h"
#include "backends/unix/os_unix.h"
#include "core/input/input_event.h"

class JoypadSDL;

#ifdef FONTCONFIG_ENABLED
typedef struct _FcConfig FcConfig;
typedef struct _FcObjectSet FcObjectSet;
#endif

class OS_LinuxBSD : public OS_Unix {
	virtual void delete_main_loop() override;

#ifdef FONTCONFIG_ENABLED
	bool font_config_initialized = false;
	FcConfig *config = nullptr;
	FcObjectSet *object_set = nullptr;

	int _weight_to_fc(int p_weight) const;
	int _stretch_to_fc(int p_stretch) const;
#endif

#ifdef SDL_ENABLED
	JoypadSDL *joypad_sdl = nullptr;
#endif

#ifdef ALSA_ENABLED
	AudioDriverALSA driver_alsa;
#endif

#ifdef ALSAMIDI_ENABLED
	MIDIDriverALSAMidi driver_alsamidi;
#endif

#ifdef PULSEAUDIO_ENABLED
	AudioDriverPulseAudio driver_pulseaudio;
#endif

	CrashHandler crash_handler;

	MainLoop *main_loop = nullptr;

	String get_systemd_os_release_info_value(const String &key) const;

	Vector<String> lspci_device_filter(Vector<String> vendor_device_id_mapping, String class_suffix, String check_column, String whitelist) const;
	Vector<String> lspci_get_device_value(Vector<String> vendor_device_id_mapping, String check_column, String blacklist) const;

	String system_dir_desktop_cache;

protected:
	virtual void initialize() override;
	virtual void finalize() override;

	virtual void initialize_joypads() override;

	virtual void set_main_loop(MainLoop *p_main_loop) override;

public:
	virtual String get_identifier() const override;
	virtual String get_name() const override;
	virtual String get_distribution_name() const override;
	virtual String get_version() const override;

	virtual Vector<String> get_video_adapter_driver_info() const override;

	virtual MainLoop *get_main_loop() const override;

	virtual uint64_t get_embedded_pck_offset() const override;

	virtual Vector<String> get_system_fonts() const override;
	virtual String get_system_font_path(const String &p_font_name, int p_weight = 400, int p_stretch = 100, bool p_italic = false) const override;
	virtual Vector<String> get_system_font_path_for_text(const String &p_font_name, const String &p_text, const String &p_locale = String(), const String &p_script = String(), int p_weight = 400, int p_stretch = 100, bool p_italic = false) const override;

	virtual String get_config_path() const override;
	virtual String get_data_path() const override;
	virtual String get_cache_path() const override;

	virtual String get_system_dir(SystemDir p_dir, bool p_shared_storage = true) const override;

	virtual Error shell_open(const String &p_uri) override;

	virtual String get_unique_id() const override;
	virtual String get_processor_name() const override;

	virtual bool is_sandboxed() const override;

	virtual void alert(const String &p_alert, const String &p_title = "ALERT!") override;

	virtual bool _check_internal_feature_support(const String &p_feature) override;

	void run();

	virtual void disable_crash_handler() override;
	virtual bool is_disable_crash_handler() const override;

	virtual Error move_to_trash(const String &p_path) override;

	virtual String get_system_ca_certificates() override;
	virtual Error get_entropy(uint8_t *r_buffer, int p_bytes) override;

#ifdef TOOLS_ENABLED
	virtual bool _test_create_rendering_device_and_gl(const String &p_display_driver) const override;
	virtual bool _test_create_rendering_device(const String &p_display_driver) const override;
#endif

	OS_LinuxBSD();
	~OS_LinuxBSD();
};
