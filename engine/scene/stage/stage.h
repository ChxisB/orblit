/**************************************************************************/
/*  stage.h                                                               */

#pragma once

#include "core/containers/paged_allocator.h"
#include "core/containers/self_list.h"
#include "core/object/message_queue.h"
#include "core/object/ref_counted.h"
#include "core/os/main_loop.h"
#include "core/os/thread_safe.h"
#include "scene/stage/stage_fti.h"

#include <cstdlib>

class ArrayMesh;
class AudioStream;
class AudioStreamPlayback;
class AudioStreamPlayer;
class InputEvent;
class Material;
class MultiplayerAPI;
class Entity;
class Prefab;
class Tween;
class Viewport;
class Window;

#ifndef SPATIAL_DISABLED
class Entity3D;
#endif

class StageTimer : public RefCounted {
	ORBLIT_CLASS(StageTimer, RefCounted);

	double time_left = 0.0;
	bool process_always = true;
	bool process_in_physics = false;
	bool ignore_time_scale = false;

protected:
	static void register_members();

public:
	void set_time_left(double p_time);
	double get_time_left() const;

	void set_process_always(bool p_process_always);
	bool is_process_always();

	void set_process_in_physics(bool p_process_in_physics);
	bool is_process_in_physics();

	void set_ignore_time_scale(bool p_ignore);
	bool is_ignoring_time_scale();

	void release_connections();
};

struct StageGroup {
	Vector<Entity *> nodes;
	bool changed = false;
};

class Stage : public MainLoop {
	_THREAD_SAFE_CLASS_

	ORBLIT_CLASS(Stage, MainLoop);

public:
	typedef void (*IdleCallback)();

private:
	CallQueue::Allocator *process_group_call_queue_allocator = nullptr;

	struct ProcessGroup {
		CallQueue call_queue;
		Vector<Entity *> nodes;
		Vector<Entity *> physics_nodes;
		bool node_order_dirty = true;
		bool physics_node_order_dirty = true;
		bool removed = false;
		Entity *owner = nullptr;
		uint64_t last_pass = 0;
	};

	struct ProcessGroupSort {
		_FORCE_INLINE_ bool operator()(const ProcessGroup *p_left, const ProcessGroup *p_right) const;
	};

	PagedAllocator<ProcessGroup, true> group_allocator; // Allocate groups on pages, to enhance cache usage.

	LocalVector<ProcessGroup *> process_groups;
	bool process_groups_dirty = true;
	LocalVector<ProcessGroup *> local_process_group_cache; // Used when processing to group what needs to
	uint64_t process_last_pass = 1;

	ProcessGroup default_process_group;

	bool node_threading_disabled = false;

	Window *root = nullptr;

	double physics_process_time = 0.0;
	double process_time = 0.0;
	bool accept_quit = true;
	bool quit_on_go_back = true;

#ifdef DEBUG_ENABLED
	bool debug_paths_hint = false;
	bool debug_navigation_hint = false;
#endif
	bool paused = false;
	bool suspended = false;

	HashMap<StringName, StageGroup> group_map;
	bool _quit = false;

	// Static so we can get directly instead of via Stage pointer.
	static bool _physics_interpolation_enabled;

	// Note that physics interpolation is hard coded to OFF in the editor,
	// therefore we have a second bool to enable e.g. configuration warnings
	// to only take effect when the project is using physics interpolation.
	static bool _physics_interpolation_enabled_in_project;

	StageFTI scene_tree_fti;

	StringName stage_changed_name = "stage_changed";
	StringName node_added_name = "entity_added";
	StringName node_removed_name = "entity_removed";
	StringName node_renamed_name = "entity_renamed";

	int64_t current_frame = 0;
	int nodes_in_tree_count = 0;

#ifdef TOOLS_ENABLED
	Entity *edited_scene_root = nullptr;
#endif
	struct UGCall {
		StringName group;
		StringName call;

		static uint32_t hash(const UGCall &p_val) {
			return p_val.group.hash() ^ p_val.call.hash();
		}
		bool operator==(const UGCall &p_with) const { return group == p_with.group && call == p_with.call; }
		bool operator<(const UGCall &p_with) const { return group == p_with.group ? call < p_with.call : group < p_with.group; }
	};

	// Safety for when a node is deleted while a group is being called.

	int nodes_removed_on_group_call_lock = 0;
	HashSet<Entity *> nodes_removed_on_group_call; // Skip erased nodes.

	List<ObjectID> delete_queue;

	uint64_t accessibility_upd_per_sec = 0;
	bool accessibility_force_update = true;
	HashSet<ObjectID> accessibility_change_queue;
	uint64_t accessibility_last_update = 0;
	StringName gui_theme_bus;

	HashMap<UGCall, Vector<Variant>, UGCall> unique_group_calls;
	bool ugc_locked = false;
	void _flush_ugc();

	_FORCE_INLINE_ void _update_group_order(StageGroup &g);

	TypedArray<Entity> _get_nodes_in_group(const StringName &p_group);

	Entity *current_scene = nullptr;
	ObjectID prev_scene_id;
	ObjectID pending_new_scene_id;

	Color debug_collisions_color;
	Color debug_collision_contact_color;
	Color debug_paths_color;
	float debug_paths_width = 1.0f;
	Ref<ArrayMesh> debug_contact_mesh;
	Ref<Material> debug_paths_material;
	Ref<Material> collision_material;
	int collision_debug_contacts;

	void _flush_scene_change();

	List<Ref<StageTimer>> timers;
	List<Ref<Tween>> tweens;

	///network///

	Ref<MultiplayerAPI> multiplayer;
	HashMap<EntityPath, Ref<MultiplayerAPI>> custom_multiplayers;
	bool multiplayer_poll = true;

	static Stage *singleton;
	friend class Entity;

	void stage_changed();
	void entity_added(Entity *p_node);
	void entity_removed(Entity *p_node);
	void entity_renamed(Entity *p_node);
	void process_timers(double p_delta, bool p_physics_frame);
	void process_tweens(double p_delta, bool p_physics_frame);

	StageGroup *add_to_group(const StringName &p_group, Entity *p_node);
	void remove_from_group(const StringName &p_group, Entity *p_node);

	void _process_group(ProcessGroup *p_group, bool p_physics);
	void _process_groups_thread(uint32_t p_index, bool p_physics);
	void _process(bool p_physics);

	void _remove_process_group(Entity *p_node);
	void _add_process_group(Entity *p_node);
	void _remove_node_from_process_group(Entity *p_node, Entity *p_owner);
	void _add_node_to_process_group(Entity *p_node, Entity *p_owner);

	void _call_group_flags(const Variant **p_args, int p_argcount, Callable::CallError &r_error);
	void _call_group(const Variant **p_args, int p_argcount, Callable::CallError &r_error);

	void _flush_delete_queue();
	// Optimization.
	friend class CanvasItem;
	friend class Entity3D;
	friend class Viewport;

	SelfList<Entity>::List xform_change_list;

#ifdef DEBUG_ENABLED // No live editor in release build.
	friend class LiveEditor;
#endif

	enum {
		MAX_IDLE_CALLBACKS = 256
	};

	static IdleCallback idle_callbacks[MAX_IDLE_CALLBACKS];
	static int idle_callback_count;
	void _call_idle_callbacks();

	void _main_window_focus_in();
	void _main_window_close();
	void _main_window_go_back();

	enum CallInputType {
		CALL_INPUT_TYPE_INPUT,
		CALL_INPUT_TYPE_SHORTCUT_INPUT,
		CALL_INPUT_TYPE_UNHANDLED_INPUT,
		CALL_INPUT_TYPE_UNHANDLED_KEY_INPUT,
	};

	//used by viewport
	void _call_input_pause(const StringName &p_group, CallInputType p_call_type, const Ref<InputEvent> &p_input, Viewport *p_viewport);

	void _project_settings_changed();

protected:
	void _notification(int p_notification);
	static void register_members();

public:
	enum {
		// Keep in sync with CanvasItem and Entity3D.
		NOTIFICATION_TRANSFORM_CHANGED = 2000
	};

	enum GroupCallFlags {
		GROUP_CALL_DEFAULT = 0,
		GROUP_CALL_REVERSE = 1,
		GROUP_CALL_DEFERRED = 2,
		GROUP_CALL_UNIQUE = 4,
	};

	RequiredResult<Window> get_root() const;

	void call_group_flagsp(uint32_t p_call_flags, const StringName &p_group, const StringName &p_function, const Variant **p_args, int p_argcount);
	void notify_group_flags(uint32_t p_call_flags, const StringName &p_group, int p_notification);
	void set_group_flags(uint32_t p_call_flags, const StringName &p_group, const String &p_name, const Variant &p_value);

	// `notify_group()` is immediate by default since Orblit 4.0.
	void notify_group(const StringName &p_group, int p_notification);
	// `set_group()` is immediate by default since Orblit 4.0.
	void set_group(const StringName &p_group, const String &p_name, const Variant &p_value);

	template <typename... VarArgs>
	// `call_group()` is immediate by default since Orblit 4.0.
	void call_group(const StringName &p_group, const StringName &p_function, VarArgs... p_args) {
		Variant args[sizeof...(p_args) + 1] = { p_args..., Variant() }; // +1 makes sure zero sized arrays are also supported.
		const Variant *argptrs[sizeof...(p_args) + 1];
		for (uint32_t i = 0; i < sizeof...(p_args); i++) {
			argptrs[i] = &args[i];
		}
		call_group_flagsp(GROUP_CALL_DEFAULT, p_group, p_function, sizeof...(p_args) == 0 ? nullptr : (const Variant **)argptrs, sizeof...(p_args));
	}

	template <typename... VarArgs>
	void call_group_flags(uint32_t p_flags, const StringName &p_group, const StringName &p_function, VarArgs... p_args) {
		Variant args[sizeof...(p_args) + 1] = { p_args..., Variant() }; // +1 makes sure zero sized arrays are also supported.
		const Variant *argptrs[sizeof...(p_args) + 1];
		for (uint32_t i = 0; i < sizeof...(p_args); i++) {
			argptrs[i] = &args[i];
		}
		call_group_flagsp(p_flags, p_group, p_function, sizeof...(p_args) == 0 ? nullptr : (const Variant **)argptrs, sizeof...(p_args));
	}

	void flush_transform_notifications();

	bool is_accessibility_enabled() const;
	bool is_accessibility_supported() const;
	void _accessibility_force_update();
	void _accessibility_notify_change(const Entity *p_node, bool p_remove = false);
	void _flush_accessibility_changes();
	void _process_accessibility_changes(int p_window_id); // Effectively DisplaySystemEnums::WindowID

	virtual void initialize() override;

	virtual void iteration_prepare() override;

	virtual bool physics_process(double p_time) override;
	virtual void iteration_end() override;
	virtual bool process(double p_time) override;

	virtual void finalize() override;

	bool is_auto_accept_quit() const;
	void set_auto_accept_quit(bool p_enable);

	bool is_quit_on_go_back() const;
	void set_quit_on_go_back(bool p_enable);

	void quit(int p_exit_code = EXIT_SUCCESS);

	_FORCE_INLINE_ double get_physics_process_time() const { return physics_process_time; }
	_FORCE_INLINE_ double get_process_time() const { return process_time; }

	void set_pause(bool p_enabled);
	bool is_paused() const;
	void set_suspend(bool p_enabled);
	bool is_suspended() const;

#ifdef DEBUG_ENABLED
	void set_debug_collisions_hint(bool p_enabled);
	bool is_debugging_collisions_hint() const;

	void set_debug_paths_hint(bool p_enabled);
	bool is_debugging_paths_hint() const;

	void set_debug_navigation_hint(bool p_enabled);
	bool is_debugging_navigation_hint() const;
#else
	void set_debug_collisions_hint(bool p_enabled) {}
	bool is_debugging_collisions_hint() const { return false; }

	void set_debug_paths_hint(bool p_enabled) {}
	bool is_debugging_paths_hint() const { return false; }

	void set_debug_navigation_hint(bool p_enabled) {}
	bool is_debugging_navigation_hint() const { return false; }
#endif

	void set_debug_collisions_color(const Color &p_color);
	Color get_debug_collisions_color() const;

	void set_debug_collision_contact_color(const Color &p_color);
	Color get_debug_collision_contact_color() const;

	void set_debug_paths_color(const Color &p_color);
	Color get_debug_paths_color() const;

	void set_debug_paths_width(float p_width);
	float get_debug_paths_width() const;

	Ref<Material> get_debug_paths_material();
	Ref<Material> get_debug_collision_material();
	Ref<ArrayMesh> get_debug_contact_mesh();

	int get_collision_debug_contact_count() { return collision_debug_contacts; }

	int64_t get_frame() const;

	int get_entity_count() const;

	void queue_delete(RequiredParam<Object> p_object);

	Vector<Entity *> get_entities_in_group(const StringName &p_group);
	Entity *get_first_entity_in_group(const StringName &p_group);
	bool has_group(const StringName &p_identifier) const;
	int get_entity_count_in_group(const StringName &p_group) const;

	//void change_scene(const String& p_path);
	//Entity *get_loaded_scene();

	void set_edited_scene_root(Entity *p_node);
	Entity *get_edited_scene_root() const;

	void set_current_scene(Entity *p_scene);
	Entity *get_current_scene() const;
	Error change_scene_to_file(const String &p_path);
	Error change_scene_to_prefab(RequiredParam<Prefab> p_scene);
	Error change_scene_to_entity(RequiredParam<Entity> p_node);
	Error reload_current_scene();
	void unload_current_scene();

	RequiredResult<StageTimer> create_timer(double p_delay_sec, bool p_process_always = true, bool p_process_in_physics = false, bool p_ignore_time_scale = false);
	RequiredResult<Tween> create_tween();
	void remove_tween(const Ref<Tween> &p_tween);
	TypedArray<Tween> get_processed_tweens();

	void play_theme_sound(const Ref<AudioStream> &p_stream);

	//used by Main::start, don't use otherwise
	void add_current_scene(Entity *p_current);

	static Stage *get_singleton() { return singleton; }

#ifdef TOOLS_ENABLED
	void get_argument_options(const StringName &p_function, int p_idx, List<String> *r_options) const override;
#endif

	//network API

	RequiredResult<MultiplayerAPI> get_multiplayer(const EntityPath &p_for_path = EntityPath()) const;
	void set_multiplayer(Ref<MultiplayerAPI> p_multiplayer, const EntityPath &p_root_path = EntityPath());
	void set_multiplayer_poll_enabled(bool p_enabled);
	bool is_multiplayer_poll_enabled() const;

	static void add_idle_callback(IdleCallback p_callback);

	void set_disable_node_threading(bool p_disable);
	//default texture settings

	void set_physics_interpolation_enabled(bool p_enabled);
	bool is_physics_interpolation_enabled() const { return _physics_interpolation_enabled; }

	// Different name to disambiguate fast static versions from the user bound versions.
	static bool is_fti_enabled() { return _physics_interpolation_enabled; }
	static bool is_fti_enabled_in_project() { return _physics_interpolation_enabled_in_project; }

	StageFTI &get_scene_tree_fti() { return scene_tree_fti; }

	Stage();
	~Stage();
};

VARIANT_ENUM_CAST(Stage::GroupCallFlags);
