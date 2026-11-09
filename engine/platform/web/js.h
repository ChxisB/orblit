/**************************************************************************/
/*  js.h                                                                  */

#pragma once

#define WASM_EXPORT __attribute__((visibility("default")))

#include <cstdint>

#ifdef __cplusplus
extern "C" {
#endif

// Emscripten
extern char *orblit_js_emscripten_get_version();

// Config
extern int orblit_js_config_pid_get();
extern void orblit_js_config_locale_get(char *p_ptr, int p_ptr_max);
extern void orblit_js_config_canvas_id_get(char *p_ptr, int p_ptr_max);

// Game
extern void orblit_js_game_frame(double p_delta, int p_frame);
// Kind 0 attaches a C# script to the entity, with its position. Kind 1 ends it.
extern void orblit_js_script_event(int p_kind, int p_handle, const char *p_class_name, float p_x, float p_y, float p_z);

// OS
extern void orblit_js_os_finish_async(void (*p_callback)());
extern void orblit_js_os_request_quit_cb(void (*p_callback)());
extern int orblit_js_os_fs_is_persistent();
extern void orblit_js_os_fs_sync(void (*p_callback)());
extern int orblit_js_os_execute(const char *p_path, const char *p_json_args);
extern int orblit_js_os_kill(int p_pid);
extern void orblit_js_os_shell_open(const char *p_uri);
extern int orblit_js_os_hw_concurrency_get();
extern int orblit_js_os_thread_pool_size_get();
extern int orblit_js_os_has_feature(const char *p_ftr);
extern int orblit_js_pwa_cb(void (*p_callback)());
extern int orblit_js_pwa_update();

// Input
extern void orblit_js_input_mouse_button_cb(int (*p_callback)(int p_pressed, int p_button, double p_x, double p_y, int p_modifiers));
extern void orblit_js_input_mouse_move_cb(void (*p_callback)(double p_x, double p_y, double p_rel_x, double p_rel_y, int p_modifiers, double p_pressure));
extern void orblit_js_input_mouse_wheel_cb(int (*p_callback)(int p_delta_mode, double p_delta_x, double p_delta_y));
extern void orblit_js_input_touch_cb(void (*p_callback)(int p_type, int p_count), uint32_t *r_identifiers, double *r_coords);
extern void orblit_js_input_key_cb(void (*p_callback)(int p_type, int p_repeat, int p_modifiers), char r_code[32], char r_key[32]);
extern void orblit_js_input_vibrate_handheld(int p_duration_ms);

extern void orblit_js_set_ime_active(int p_active);
extern void orblit_js_set_ime_position(int p_x, int p_y);
extern void orblit_js_set_ime_cb(void (*p_input)(int p_type, const char *p_text), void (*p_callback)(int p_type, int p_repeat, int p_modifiers), char r_code[32], char r_key[32]);
extern int orblit_js_is_ime_focused();

// Input gamepad
extern void orblit_js_input_gamepad_cb(void (*p_on_change)(int p_index, int p_connected, const char *p_id, const char *p_guid));
extern int orblit_js_input_gamepad_sample();
extern int orblit_js_input_gamepad_sample_count();
extern int orblit_js_input_gamepad_sample_get(int p_idx, float r_btns[16], int32_t *r_btns_num, float r_axes[10], int32_t *r_axes_num, int32_t *r_standard);
extern void orblit_js_input_paste_cb(void (*p_callback)(const char *p_text));
extern void orblit_js_input_drop_files_cb(void (*p_callback)(const char **p_filev, int p_filec));

// TTS
extern int orblit_js_tts_is_speaking();
extern int orblit_js_tts_is_paused();
extern int orblit_js_tts_get_voices(void (*p_callback)(int p_size, const char **p_voices));
extern void orblit_js_tts_speak(const char *p_text, const char *p_voice, int p_volume, float p_pitch, float p_rate, int64_t p_utterance_id, void (*p_callback)(int p_event, int64_t p_id, int p_pos));
extern void orblit_js_tts_pause();
extern void orblit_js_tts_resume();
extern void orblit_js_tts_stop();

// Display
extern int orblit_js_display_screen_dpi_get();
extern double orblit_js_display_pixel_ratio_get();
extern void orblit_js_display_alert(const char *p_text);
extern int orblit_js_display_touchscreen_is_available();
extern int orblit_js_display_is_swap_ok_cancel();
extern void orblit_js_display_setup_canvas(int p_width, int p_height, int p_fullscreen, int p_hidpi);

// Display canvas
extern void orblit_js_display_canvas_focus();
extern int orblit_js_display_canvas_is_focused();

// Display window
extern void orblit_js_display_desired_size_set(int p_width, int p_height);
extern int orblit_js_display_size_update();
extern void orblit_js_display_window_size_get(int32_t *p_x, int32_t *p_y);
extern void orblit_js_display_screen_size_get(int32_t *p_x, int32_t *p_y);
extern int orblit_js_display_fullscreen_request();
extern int orblit_js_display_fullscreen_exit();
extern void orblit_js_display_window_title_set(const char *p_text);
extern void orblit_js_display_window_icon_set(const uint8_t *p_ptr, int p_len);
extern int orblit_js_display_has_webgl(int p_version);

// Display clipboard
extern int orblit_js_display_clipboard_set(const char *p_text);
extern int orblit_js_display_clipboard_get(void (*p_callback)(const char *p_text));

// Display cursor
extern void orblit_js_display_cursor_set_shape(const char *p_cursor);
extern int orblit_js_display_cursor_is_hidden();
extern void orblit_js_display_cursor_set_custom_shape(const char *p_shape, const uint8_t *p_ptr, int p_len, int p_hotspot_x, int p_hotspot_y);
extern void orblit_js_display_cursor_set_visible(int p_visible);
extern void orblit_js_display_cursor_lock_set(int p_lock);
extern int orblit_js_display_cursor_is_locked();

// Display listeners
extern void orblit_js_display_fullscreen_cb(void (*p_callback)(int p_fullscreen));
extern void orblit_js_display_window_blur_cb(void (*p_callback)());
extern void orblit_js_display_notification_cb(void (*p_callback)(int p_notification), int p_enter, int p_exit, int p_in, int p_out);

// Display Virtual Keyboard
extern int orblit_js_display_vk_available();
extern int orblit_js_display_tts_available();
extern void orblit_js_display_vk_cb(void (*p_input)(const char *p_text, int p_cursor));
extern void orblit_js_display_vk_show(const char *p_text, int p_type, int p_start, int p_end);
extern void orblit_js_display_vk_hide();

#ifdef __cplusplus
}
#endif
