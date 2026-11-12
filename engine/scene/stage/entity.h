/**************************************************************************/
/*  entity.h                                                              */

#pragma once

#include "core/containers/iterable.h"
#include "core/input/input_event.h"
#include "core/object/object.h"
#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"
#include "core/os/thread_safe.h"
#include "scene/scene_string_names.h" // IWYU pragma: export. Make available to all Nodes.

class MultiplayerAPI;
class EntityPath;
class Resource;
class SceneState;
class Stage;
class Tween;
class Viewport;
class Window;
struct StageGroup;

SAFE_FLAG_TYPE_PUN_GUARANTEES
SAFE_NUMERIC_TYPE_PUN_GUARANTEES(uint32_t)

class Entity : public Object {
	ORBLIT_CLASS(Entity, Object);

protected:
	// During group processing, these are thread-safe.
	// Outside group processing, these avoid the cost of sync by working as plain primitive types.
	union MTFlag {
		SafeFlag mt;
		bool st;
		MTFlag() :
				mt{} {}
	};
	template <typename T>
	union MTNumeric {
		SafeNumeric<T> mt;
		T st;
		MTNumeric() :
				mt{} {}
	};

public:
	static constexpr AncestralClass static_ancestral_class = AncestralClass::NODE;

	// N.B. Any enum stored as a bitfield should be specified as UNSIGNED to work around
	// some compilers trying to store it as signed, and requiring 1 more bit than necessary.
	enum RunMode : unsigned int {
		RUN_MODE_INHERIT, // same as parent node
		RUN_MODE_PAUSABLE, // process only if not paused
		RUN_MODE_WHEN_PAUSED, // process only if paused
		RUN_MODE_ALWAYS, // process always
		RUN_MODE_DISABLED, // never process
	};

	enum RunThreadGroup {
		RUN_THREAD_GROUP_INHERIT,
		RUN_THREAD_GROUP_MAIN_THREAD,
		RUN_THREAD_GROUP_SUB_THREAD,
	};

	enum RunThreadMessages {
		FLAG_RUN_THREAD_MESSAGES = 1,
		FLAG_RUN_THREAD_MESSAGES_PHYSICS = 2,
		FLAG_RUN_THREAD_MESSAGES_ALL = 3,
	};

	enum PhysicsInterpolationMode : unsigned int {
		PHYSICS_INTERPOLATION_MODE_INHERIT,
		PHYSICS_INTERPOLATION_MODE_ON,
		PHYSICS_INTERPOLATION_MODE_OFF,
	};

	enum DuplicateFlags {
		DUPLICATE_SIGNALS = 1,
		DUPLICATE_GROUPS = 2,
		DUPLICATE_SCRIPTS = 4,
		DUPLICATE_USE_INSTANTIATION = 8,
		DUPLICATE_INTERNAL_STATE = 16,
		DUPLICATE_DEFAULT = DUPLICATE_SIGNALS | DUPLICATE_GROUPS | DUPLICATE_SCRIPTS | DUPLICATE_USE_INSTANTIATION,
#ifdef TOOLS_ENABLED
		DUPLICATE_FROM_EDITOR = 32,
#endif
	};

	enum NameCasing {
		NAME_CASING_PASCAL_CASE,
		NAME_CASING_CAMEL_CASE,
		NAME_CASING_SNAKE_CASE,
		NAME_CASING_KEBAB_CASE,
	};

	enum InternalMode {
		INTERNAL_MODE_DISABLED,
		INTERNAL_MODE_FRONT,
		INTERNAL_MODE_BACK,
	};

	enum AutoTranslateMode : unsigned int {
		AUTO_TRANSLATE_MODE_INHERIT,
		AUTO_TRANSLATE_MODE_ALWAYS,
		AUTO_TRANSLATE_MODE_DISABLED,
	};

	struct Comparator {
		bool operator()(const Entity *p_a, const Entity *p_b) const { return p_b->is_greater_than(p_a); }
	};

#ifdef DEBUG_ENABLED
	static SafeNumeric<uint64_t> total_node_count;
#endif
	enum {
		UNIQUE_SCENE_ID_UNASSIGNED = 0
	};

	void _update_process(bool p_enable, bool p_for_children);

	struct ChildrenIterator {
		_FORCE_INLINE_ Entity *&operator*() const { return *_ptr; }
		_FORCE_INLINE_ Entity **operator->() const { return _ptr; }
		_FORCE_INLINE_ ChildrenIterator &operator++() {
			_ptr++;
			return *this;
		}
		_FORCE_INLINE_ ChildrenIterator &operator--() {
			_ptr--;
			return *this;
		}

		_FORCE_INLINE_ bool operator==(const ChildrenIterator &b) const { return _ptr == b._ptr; }
		_FORCE_INLINE_ bool operator!=(const ChildrenIterator &b) const { return _ptr != b._ptr; }

		ChildrenIterator(Entity **p_ptr) { _ptr = p_ptr; }
		ChildrenIterator() {}
		ChildrenIterator(const ChildrenIterator &p_it) { _ptr = p_it._ptr; }

	private:
		Entity **_ptr = nullptr;
	};

private:
	struct GroupData {
		bool persistent = false;
		StageGroup *group = nullptr;
	};

	struct ComparatorByIndex {
		bool operator()(const Entity *p_left, const Entity *p_right) const {
			static const uint32_t order[3] = { 1, 0, 2 };
			uint32_t order_left = order[p_left->data.internal_mode];
			uint32_t order_right = order[p_right->data.internal_mode];
			if (order_left == order_right) {
				return p_left->data.index < p_right->data.index;
			}
			return order_left < order_right;
		}
	};

	struct ComparatorWithPriority {
		bool operator()(const Entity *p_a, const Entity *p_b) const { return p_b->data.run_priority == p_a->data.run_priority ? p_b->is_greater_than(p_a) : p_b->data.run_priority > p_a->data.run_priority; }
	};

	struct ComparatorWithPhysicsPriority {
		bool operator()(const Entity *p_a, const Entity *p_b) const { return p_b->data.run_physics_priority == p_a->data.run_physics_priority ? p_b->is_greater_than(p_a) : p_b->data.run_physics_priority > p_a->data.run_physics_priority; }
	};

	// This Data struct is to avoid namespace pollution in derived classes.
	struct Data {
		String scene_file_path;
		Ref<SceneState> instance_state;
		Ref<SceneState> inherited_state;

		Entity *parent = nullptr;
		Entity *owner = nullptr;
		HashMap<StringName, Entity *> children;
		mutable bool children_cache_dirty = false;
		mutable LocalVector<Entity *> children_cache;
		HashMap<StringName, Entity *> owned_unique_nodes;
		bool unique_name_in_owner = false;
		InternalMode internal_mode = INTERNAL_MODE_DISABLED;
		mutable int internal_children_front_count_cache = 0;
		mutable int internal_children_back_count_cache = 0;
		mutable int external_children_count_cache = 0;
		mutable int index = -1; // relative to front, normal or back.
		int32_t depth = -1;
		int blocked = 0; // Safeguard that throws an error when attempting to modify the tree in a harmful way while being traversed.
		StringName name;
		Stage *tree = nullptr;

		String editor_description;

		Viewport *viewport = nullptr;

		mutable RID accessibility_element;

		HashMap<StringName, GroupData> grouped;
		List<Entity *>::Element *OW = nullptr; // Owned element.
		List<Entity *> owned;

		Entity *process_owner = nullptr;
		RunThreadGroup run_thread_group = RUN_THREAD_GROUP_INHERIT;
		Entity *run_thread_group_owner = nullptr;
		int run_thread_group_order = 0;
		BitField<RunThreadMessages> run_thread_messages = {};
		void *process_group = nullptr; // to avoid cyclic dependency

		int multiplayer_authority = 1; // Server by default.
		Variant rpc_config;

		// Variables used to properly sort the node when processing, ignored otherwise.
		int run_priority = 0;
		int run_physics_priority = 0;

		// Keep bitpacked values together to get better packing.
		RunMode run_mode : 3;
		PhysicsInterpolationMode physics_interpolation_mode : 2;
		AutoTranslateMode auto_translate_mode : 2;

		bool physics_process : 1;
		bool process : 1;

		bool physics_process_internal : 1;
		bool process_internal : 1;

		bool input : 1;
		bool shortcut_input : 1;
		bool unhandled_input : 1;
		bool unhandled_key_input : 1;

		// Physics interpolation can be turned on and off on a per node basis.
		// This only takes effect when the Stage (or project setting) physics interpolation
		// is switched on.
		bool physics_interpolated : 1;

		// We can auto-reset physics interpolation when e.g. adding a node for the first time.
		bool physics_interpolation_reset_requested : 1;

		// For certain nodes (e.g. CPU particles in global mode)
		// it can be useful to not send the instance transform to the
		// RenderingSystem, and specify the mesh in world space.
		bool use_identity_transform : 1;

		bool use_placeholder : 1;

		bool display_folded : 1;
		bool editable_instance : 1;

		bool ready_notified : 1;
		bool ready_first : 1;

		mutable bool is_auto_translating : 1;
		mutable bool is_auto_translate_dirty : 1;

		mutable bool is_translation_domain_inherited : 1;
		mutable bool is_translation_domain_dirty : 1;

		int32_t unique_scene_id = UNIQUE_SCENE_ID_UNASSIGNED;

		mutable EntityPath *path_cache = nullptr;

	} data;

	String _get_entity_tree_string_pretty(const String &p_prefix, bool p_last);
	String _get_entity_tree_string(const Entity *p_node);

	Entity *_get_child_by_name(const StringName &p_name) const;
	void _find_children_into(const String &p_pattern, const String &p_type, bool p_recursive, bool p_owned, TypedArray<Entity> &r_found) const;

	void _replace_connections_target(Entity *p_new_target);

	void _validate_child_name(Entity *p_child, bool p_force_human_readable = false);
	void _generate_serial_child_name(const Entity *p_child, StringName &name) const;

	void _propagate_enter_tree();
	void _propagate_ready();
	void _propagate_exit_tree();
	void _propagate_after_exit_tree();
	void _propagate_physics_interpolated(bool p_interpolated);
	void _propagate_physics_interpolation_reset_requested(bool p_requested);
	void _propagate_process_owner(Entity *p_owner, int p_pause_notification, int p_enabled_notification);
	void _propagate_groups_dirty();
	void _propagate_translation_domain_dirty();
	Array _get_node_and_resource(const EntityPath &p_path);

	void _duplicate_scripts(const Entity *p_original, Entity *p_copy, int p_flags) const;
	void _duplicate_properties(const Entity *p_root, const Entity *p_original, Entity *p_copy, int p_flags) const;
	void _duplicate_signals(const Entity *p_original, Entity *p_copy) const;
	Entity *_duplicate(int p_flags, HashMap<const Entity *, Entity *> *r_duplimap = nullptr) const;

	TypedArray<StringName> _get_groups() const;

	Error _rpc_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error);
	Error _rpc_id_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error);

	friend class Stage;

	void _set_tree(Stage *p_tree);
	void _propagate_pause_notification(bool p_enable);
	void _propagate_suspend_notification(bool p_enable);

	_FORCE_INLINE_ bool _can_run(bool p_paused) const;
	_FORCE_INLINE_ bool _is_enabled() const;

	void _release_unique_name_in_owner();
	void _acquire_unique_name_in_owner();

	void _clean_up_owner();

	_FORCE_INLINE_ void _update_children_cache() const {
		if (unlikely(data.children_cache_dirty)) {
			_update_children_cache_impl();
		}
	}

	void _update_children_cache_impl() const;

	// Process group management
	void _add_process_group();
	void _remove_process_group();
	void _add_to_run_thread_group();
	void _remove_from_run_thread_group();
	void _remove_tree_from_run_thread_group();
	void _add_tree_to_run_thread_group(Entity *p_owner);

	static thread_local Entity *current_run_thread_group;

	Variant _call_deferred_thread_group_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error);
	Variant _call_thread_safe_bind(const Variant **p_args, int p_argcount, Callable::CallError &r_error);

	// Editor only signal to keep the StageEditor in sync.
#ifdef TOOLS_ENABLED
	void _emit_editor_state_changed();
#else
	void _emit_editor_state_changed() {}
#endif

protected:
	void _block() { data.blocked++; }
	void _unblock() { data.blocked--; }

	void _notification(int p_notification);

	virtual void _physics_interpolated_changed();

	virtual void add_child_notify(Entity *p_child);
	virtual void remove_child_notify(Entity *p_child);
	virtual void move_child_notify(Entity *p_child);
	virtual void owner_changed_notify();

	void _propagate_replace_owner(Entity *p_owner, Entity *p_by_owner);

	static void register_members();
	static String _get_name_num_separator();

	friend class SceneState;

	void _add_child_nocheck(Entity *p_child, const StringName &p_name, InternalMode p_internal_mode = INTERNAL_MODE_DISABLED);
	void _set_owner_nocheck(Entity *p_owner);
	void _set_name_nocheck(const StringName &p_name);

	void _set_physics_interpolation_reset_requested(bool p_enable) { data.physics_interpolation_reset_requested = p_enable; }
	bool _is_physics_interpolation_reset_requested() const { return data.physics_interpolation_reset_requested; }

	void _set_use_identity_transform(bool p_enable) { data.use_identity_transform = p_enable; }
	bool _is_using_identity_transform() const { return data.use_identity_transform; }
	int32_t _get_scene_tree_depth() const { return data.depth; }

	//call from Stage
	void _call_input(const Ref<InputEvent> &p_event);
	void _call_shortcut_input(const Ref<InputEvent> &p_event);
	void _call_unhandled_input(const Ref<InputEvent> &p_event);
	void _call_unhandled_key_input(const Ref<InputEvent> &p_event);

	void _validate_property(PropertyInfo &p_property) const;
	virtual String _to_string() override;

	// Localization

	virtual StringName _get_translation_context_with_override(const StringName &p_context) const { return p_context; }

	Variant _get_node_rpc_config_bind() const {
		return get_entity_rpc_config().duplicate(true);
	}

protected:
	virtual bool _uses_signal_mutex() const override { return false; } // Entity uses thread guards instead.

	virtual void input(const Ref<InputEvent> &p_event);
	virtual void shortcut_input(const Ref<InputEvent> &p_key_event);
	virtual void unhandled_input(const Ref<InputEvent> &p_event);
	virtual void unhandled_key_input(const Ref<InputEvent> &p_key_event);

	ORBLIT_VIRTUAL1(_frame, double)
	ORBLIT_VIRTUAL1(_step, double)
	ORBLIT_VIRTUAL0(_enter_stage)
	ORBLIT_VIRTUAL0(_exit_stage)
	ORBLIT_VIRTUAL0(_start)
	ORBLIT_VIRTUAL0RC(Vector<String>, _get_accessibility_configuration_warnings)
	ORBLIT_VIRTUAL0RC(Vector<String>, _get_configuration_warnings)

	ORBLIT_VIRTUAL1(_input, RequiredParam<InputEvent>)
	ORBLIT_VIRTUAL1(_shortcut_input, RequiredParam<InputEvent>)
	ORBLIT_VIRTUAL1(_unhandled_input, RequiredParam<InputEvent>)
	ORBLIT_VIRTUAL1(_unhandled_key_input, RequiredParam<InputEvent>)

	ORBLIT_VIRTUAL0RC(RID, _get_focused_accessibility_element)

public:
	enum {
		// You can make your own, but don't use the same numbers as other notifications in other nodes.
		NOTIFICATION_ENTER_STAGE = 10,
		NOTIFICATION_EXIT_STAGE = 11,
		NOTIFICATION_START = 13,
		NOTIFICATION_PAUSED = 14,
		NOTIFICATION_UNPAUSED = 15,
		NOTIFICATION_STEP = 16,
		NOTIFICATION_FRAME = 17,
		NOTIFICATION_PARENTED = 18,
		NOTIFICATION_UNPARENTED = 19,
		NOTIFICATION_SCENE_INSTANTIATED = 20,
		NOTIFICATION_DRAG_BEGIN = 21,
		NOTIFICATION_DRAG_END = 22,
		NOTIFICATION_PATH_RENAMED = 23,
		NOTIFICATION_CHILD_ORDER_CHANGED = 24,
		NOTIFICATION_INTERNAL_FRAME = 25,
		NOTIFICATION_INTERNAL_STEP = 26,
		NOTIFICATION_POST_ENTER_STAGE = 27,
		NOTIFICATION_DISABLED = 28,
		NOTIFICATION_ENABLED = 29,
		NOTIFICATION_RESET_PHYSICS_INTERPOLATION = 2001, // A OrblitSpace Odyssey.
		// Keep these linked to Entity.

		NOTIFICATION_ACCESSIBILITY_UPDATE = 3000,
		NOTIFICATION_ACCESSIBILITY_INVALIDATE = 3001,

		NOTIFICATION_WM_MOUSE_ENTER = 1002,
		NOTIFICATION_WM_MOUSE_EXIT = 1003,
		NOTIFICATION_WM_WINDOW_FOCUS_IN = 1004,
		NOTIFICATION_WM_WINDOW_FOCUS_OUT = 1005,
		NOTIFICATION_WM_CLOSE_REQUEST = 1006,
		NOTIFICATION_WM_GO_BACK_REQUEST = 1007,
		NOTIFICATION_WM_SIZE_CHANGED = 1008,
		NOTIFICATION_WM_DPI_CHANGE = 1009,
		NOTIFICATION_VP_MOUSE_ENTER = 1010,
		NOTIFICATION_VP_MOUSE_EXIT = 1011,
		NOTIFICATION_WM_POSITION_CHANGED = 1012,
		NOTIFICATION_WM_OUTPUT_MAX_LINEAR_VALUE_CHANGED = 1013,

		// Keep these in sync with MainLoop.
		NOTIFICATION_OS_MEMORY_WARNING = 2009,
		NOTIFICATION_TRANSLATION_CHANGED = 2010,
		NOTIFICATION_WM_ABOUT = 2011,
		NOTIFICATION_CRASH = 2012,
		NOTIFICATION_OS_IME_UPDATE = 2013,
		NOTIFICATION_APPLICATION_RESUMED = 2014,
		NOTIFICATION_APPLICATION_PAUSED = 2015,
		NOTIFICATION_APPLICATION_FOCUS_IN = 2016,
		NOTIFICATION_APPLICATION_FOCUS_OUT = 2017,
		NOTIFICATION_TEXT_SYSTEM_CHANGED = 2018,
		NOTIFICATION_APPLICATION_PIP_MODE_ENTERED = 2019,
		NOTIFICATION_APPLICATION_PIP_MODE_EXITED = 2020,

		// Editor specific node notifications.
		NOTIFICATION_EDITOR_PRE_SAVE = 9001,
		NOTIFICATION_EDITOR_POST_SAVE = 9002,
		NOTIFICATION_SUSPENDED = 9003,
		NOTIFICATION_UNSUSPENDED = 9004
	};

	/* NODE/TREE */

	StringName get_name() const;
	String get_description(bool p_show_not_in_tree = false) const;
	void set_name(const StringName &p_name);

	InternalMode get_internal_mode() const;

	void add_child(RequiredParam<Entity> p_child, bool p_force_readable_name = false, InternalMode p_internal = INTERNAL_MODE_DISABLED);
	void add_sibling(RequiredParam<Entity> p_sibling, bool p_force_readable_name = false);
	void remove_child(RequiredParam<Entity> p_child);

	/// Optimal way to iterate the children of this node.
	/// The caller is responsible to ensure:
	/// - The thread has the rights to access the node (is_accessible_from_caller_thread() == true).
	/// - No children are inserted, removed, or have their index changed during iteration.
	template <bool p_include_internal = true>
	Iterable<ChildrenIterator> iterate_children() const;

	int get_child_count(bool p_include_internal = true) const;
	Entity *get_child(int p_index, bool p_include_internal = true) const;
	TypedArray<Entity> get_children(bool p_include_internal = true) const;
	bool has_entity(const EntityPath &p_path) const;
	Entity *get_entity(const EntityPath &p_path) const;
	Entity *get_entity_or_null(const EntityPath &p_path) const;
	Entity *find_child(const String &p_pattern, bool p_recursive = true, bool p_owned = true) const;
	TypedArray<Entity> find_children(const String &p_pattern, const String &p_type = "", bool p_recursive = true, bool p_owned = true) const;
	bool has_entity_and_resource(const EntityPath &p_path) const;
	Entity *get_entity_and_resource(const EntityPath &p_path, Ref<Resource> &r_res, Vector<StringName> &r_leftover_subpath, bool p_last_is_property = true) const;

	virtual void reparent(RequiredParam<Entity> p_parent, bool p_keep_global_transform = true);
	Entity *get_parent() const;
	Entity *find_parent(const String &p_pattern) const;

	void set_unique_scene_id(int32_t p_unique_id);
	int32_t get_unique_scene_id() const;

	Window *get_window() const;
	Window *get_non_popup_window() const;
	Window *get_last_exclusive_window() const;

	_FORCE_INLINE_ Stage *get_stage() const {
		ERR_FAIL_NULL_V(data.tree, nullptr);
		return data.tree;
	}

	_FORCE_INLINE_ bool is_inside_stage() const { return data.tree; }
	bool is_internal() const { return data.internal_mode != INTERNAL_MODE_DISABLED; }

	bool is_ancestor_of(RequiredParam<const Entity> p_node) const;
	bool is_greater_than(RequiredParam<const Entity> p_node) const;

	EntityPath get_path() const;
	EntityPath get_path_to(RequiredParam<const Entity> p_node, bool p_use_unique_path = false) const;
	Entity *find_common_parent_with(const Entity *p_node) const;

	void add_to_group(const StringName &p_identifier, bool p_persistent = false);
	void remove_from_group(const StringName &p_identifier);
	bool is_in_group(const StringName &p_identifier) const;

	struct GroupInfo {
		StringName name;
		bool persistent = false;
	};

	void get_groups(List<GroupInfo> *p_groups) const;
	int get_persistent_group_count() const;

	void move_child(RequiredParam<Entity> p_child, int p_index);
	void _move_child(Entity *p_child, int p_index, bool p_ignore_end = false);

	void set_owner(Entity *p_owner);
	Entity *get_owner() const;
	void get_owned_by(Entity *p_by, List<Entity *> *p_owned);

	void set_unique_name_in_owner(bool p_enabled);
	bool is_unique_name_in_owner() const;

	_FORCE_INLINE_ int get_index(bool p_include_internal = true) const {
		// p_include_internal = false doesn't make sense if the node is internal.
		ERR_FAIL_COND_V_MSG(!p_include_internal && data.internal_mode != INTERNAL_MODE_DISABLED, -1, "Entity is internal. Can't get index with 'include_internal' being false.");
		if (!data.parent) {
			return data.index;
		}
		data.parent->_update_children_cache();

		if (!p_include_internal) {
			return data.index;
		} else {
			switch (data.internal_mode) {
				case INTERNAL_MODE_DISABLED: {
					return data.parent->data.internal_children_front_count_cache + data.index;
				} break;
				case INTERNAL_MODE_FRONT: {
					return data.index;
				} break;
				case INTERNAL_MODE_BACK: {
					return data.parent->data.internal_children_front_count_cache + data.parent->data.external_children_count_cache + data.index;
				} break;
			}
			return -1;
		}
	}

	RequiredResult<Tween> create_tween();

	void print_entity_tree();
	void print_entity_tree_pretty();
	String get_entity_tree_string();
	String get_entity_tree_string_pretty();

	void set_scene_file_path(const String &p_scene_file_path);
	String get_scene_file_path() const;

	void set_editor_description(const String &p_editor_description);
	String get_editor_description() const;

	void set_editable_instance(RequiredParam<Entity> p_node, bool p_editable);
	bool is_editable_instance(const Entity *p_node) const;
	Entity *get_deepest_editable_node(Entity *p_start_node) const;

#ifdef TOOLS_ENABLED
	void set_property_pinned(const String &p_property, bool p_pinned);
	bool is_property_pinned(const StringName &p_property) const;
	virtual StringName get_property_store_alias(const StringName &p_property) const;
	bool is_part_of_edited_scene() const;
#else
	bool is_part_of_edited_scene() const { return false; }
#endif
	void get_storable_properties(HashSet<StringName> &r_storable_properties) const;

	/* NOTIFICATIONS */

	void propagate_notification(int p_notification);

	void propagate_call(const StringName &p_method, const Array &p_args = Array(), const bool p_parent_first = false);

	/* PROCESSING */

	void set_step_enabled(bool p_process);
	double get_step_delta() const;
	bool is_step_enabled() const;

	void set_frame_enabled(bool p_process);
	double get_frame_delta() const;
	bool is_frame_enabled() const;

	void set_internal_step_enabled(bool p_process_internal);
	bool is_internal_step_enabled() const;

	void set_internal_frame_enabled(bool p_process_internal);
	bool is_internal_frame_enabled() const;

	void set_run_priority(int p_priority);
	int get_run_priority() const;

	void set_run_thread_group_order(int p_order);
	int get_run_thread_group_order() const;

	void set_run_physics_priority(int p_priority);
	int get_run_physics_priority() const;

	void set_input_enabled(bool p_enable);
	bool is_input_enabled() const;

	void set_shortcut_input_enabled(bool p_enable);
	bool is_shortcut_input_enabled() const;

	void set_unhandled_input_enabled(bool p_enable);
	bool is_unhandled_input_enabled() const;

	void set_unhandled_key_input_enabled(bool p_enable);
	bool is_unhandled_key_input_enabled() const;

	_FORCE_INLINE_ bool _is_any_processing() const {
		return data.process || data.process_internal || data.physics_process || data.physics_process_internal;
	}
	_FORCE_INLINE_ bool is_accessible_from_caller_thread() const {
		if (current_run_thread_group == nullptr) {
			// No thread processing.
			// Only accessible if node is outside the scene tree
			// or access will happen from a node-safe thread.
			return !data.tree || is_current_thread_safe_for_nodes();
		} else {
			// Thread processing.
			return current_run_thread_group == data.run_thread_group_owner;
		}
	}

	_FORCE_INLINE_ bool is_readable_from_caller_thread() const {
		if (current_run_thread_group == nullptr) {
			// No thread processing.
			// Only accessible if node is outside the scene tree
			// or access will happen from a node-safe thread.
			return is_current_thread_safe_for_nodes() || unlikely(!data.tree);
		} else {
			// Thread processing.
			return true;
		}
	}

	_FORCE_INLINE_ static bool is_group_processing() { return current_run_thread_group; }

	void set_run_thread_messages(BitField<RunThreadMessages> p_flags);
	BitField<RunThreadMessages> get_run_thread_messages() const;

	void queue_accessibility_update();

	virtual RID get_accessibility_element() const;
	virtual RID get_focused_accessibility_element() const;
	virtual bool accessibility_override_tree_hierarchy() const { return false; }
	virtual Transform2D get_accessibility_transform() const;

	virtual PackedStringArray get_accessibility_configuration_warnings() const;

	Entity *duplicate(int p_flags = DUPLICATE_GROUPS | DUPLICATE_SIGNALS | DUPLICATE_SCRIPTS) const;
#ifdef TOOLS_ENABLED
	Entity *duplicate_from_editor(HashMap<const Entity *, Entity *> &r_duplimap) const;
	Entity *duplicate_from_editor(HashMap<const Entity *, Entity *> &r_duplimap, Entity *p_scene_root, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_resource_remap) const;
	void remap_node_resources(Entity *p_node, Entity *p_scene_root, HashMap<Entity *, HashMap<Ref<Resource>, Ref<Resource>>> &p_resource_remap) const;
	void remap_nested_resources(Ref<Resource> p_resource, HashMap<Ref<Resource>, Ref<Resource>> &p_resource_remap) const;
#endif

	// used by editors, to save what has changed only
	void set_scene_instance_state(const Ref<SceneState> &p_state);
	Ref<SceneState> get_scene_instance_state() const;

	void set_scene_inherited_state(const Ref<SceneState> &p_state);
	Ref<SceneState> get_scene_inherited_state() const;

	void set_scene_instance_load_placeholder(bool p_enable);
	bool get_scene_instance_load_placeholder() const;

	template <typename... VarArgs>
	Vector<Variant> make_binds(VarArgs... p_args) {
		Vector<Variant> binds = { p_args... };
		return binds;
	}

	void replace_by(RequiredParam<Entity> p_node, bool p_keep_groups = false);

	void set_run_mode(RunMode p_mode);
	RunMode get_run_mode() const;
	bool can_run() const;
	bool can_process_notification(int p_what) const;

	void set_physics_interpolation_mode(PhysicsInterpolationMode p_mode);
	PhysicsInterpolationMode get_physics_interpolation_mode() const { return data.physics_interpolation_mode; }
	_FORCE_INLINE_ bool is_physics_interpolated() const { return data.physics_interpolated; }
	bool is_physics_interpolated_and_enabled() const;
	void reset_physics_interpolation();

	bool is_enabled() const;
	bool is_ready() const;

	void request_start();

	void set_run_thread_group(RunThreadGroup p_mode);
	RunThreadGroup get_run_thread_group() const;

	static void print_orphan_entities();
	static TypedArray<int> get_orphan_entity_ids();

#ifdef TOOLS_ENABLED
	String validate_child_name(Entity *p_child);
	String prevalidate_child_name(Entity *p_child, StringName p_name);
	void get_argument_options(const StringName &p_function, int p_idx, List<String> *r_options) const override;
#endif
	static String adjust_name_casing(const String &p_name);

	void queue_destroy();

	//hacks for speed
	static void init_node_hrcr();

	bool is_owned_by_parent() const;

	void clear_internal_tree_resource_paths();

	_FORCE_INLINE_ Viewport *get_viewport() const { return data.viewport; }

	virtual PackedStringArray get_configuration_warnings() const;

	void update_configuration_warnings();

	void set_display_folded(bool p_folded);
	bool is_displayed_folded() const;

	/* NETWORK */

	virtual void set_multiplayer_authority(int p_peer_id, bool p_recursive = true);
	int get_multiplayer_authority() const;
	bool is_multiplayer_authority() const;

	void rpc_config(const StringName &p_method, const Variant &p_config); // config a local method for RPC
	const Variant get_entity_rpc_config() const;

	template <typename... VarArgs>
	Error rpc(const StringName &p_method, VarArgs... p_args);

	template <typename... VarArgs>
	Error rpc_id(int p_peer_id, const StringName &p_method, VarArgs... p_args);

	Error rpcp(int p_peer_id, const StringName &p_method, const Variant **p_arg, int p_argcount);

	Ref<MultiplayerAPI> get_multiplayer() const;

	/* INTERNATIONALIZATION */

	void set_auto_translate_mode(AutoTranslateMode p_mode);
	AutoTranslateMode get_auto_translate_mode() const;
	bool can_auto_translate() const;

	virtual StringName get_translation_domain() const override;
	virtual void set_translation_domain(const StringName &p_domain) override;
	void set_translation_domain_inherited();

	_FORCE_INLINE_ String atr(const String &p_message, const StringName &p_context = "") const {
		return can_auto_translate() ? tr(p_message, _get_translation_context_with_override(p_context)) : p_message;
	}
	_FORCE_INLINE_ String atr_n(const String &p_message, const StringName &p_message_plural, int p_n, const StringName &p_context = "") const {
		if (can_auto_translate()) {
			return tr_n(p_message, p_message_plural, p_n, _get_translation_context_with_override(p_context));
		}
		return p_n == 1 ? p_message : String(p_message_plural);
	}

	/* THREADING */

	void call_deferred_thread_groupp(const StringName &p_method, const Variant **p_args, int p_argcount, bool p_show_error = false);
	template <typename... VarArgs>
	void call_deferred_thread_group(const StringName &p_method, VarArgs... p_args) {
		Variant args[sizeof...(p_args) + 1] = { p_args..., Variant() }; // +1 makes sure zero sized arrays are also supported.
		const Variant *argptrs[sizeof...(p_args) + 1];
		for (uint32_t i = 0; i < sizeof...(p_args); i++) {
			argptrs[i] = &args[i];
		}
		call_deferred_thread_groupp(p_method, sizeof...(p_args) == 0 ? nullptr : (const Variant **)argptrs, sizeof...(p_args));
	}
	void set_deferred_thread_group(const StringName &p_property, const Variant &p_value);
	void notify_deferred_thread_group(int p_notification);

	void call_thread_safep(const StringName &p_method, const Variant **p_args, int p_argcount, bool p_show_error = false);
	template <typename... VarArgs>
	void call_thread_safe(const StringName &p_method, VarArgs... p_args) {
		Variant args[sizeof...(p_args) + 1] = { p_args..., Variant() }; // +1 makes sure zero sized arrays are also supported.
		const Variant *argptrs[sizeof...(p_args) + 1];
		for (uint32_t i = 0; i < sizeof...(p_args); i++) {
			argptrs[i] = &args[i];
		}
		call_deferred_thread_groupp(p_method, sizeof...(p_args) == 0 ? nullptr : (const Variant **)argptrs, sizeof...(p_args));
	}
	void set_thread_safe(const StringName &p_property, const Variant &p_value);
	void notify_thread_safe(int p_notification);

	/* HELPER */

	bool is_instance() const { return !data.scene_file_path.is_empty(); }

	// These inherited functions need proper multithread locking when overridden in Entity.
#ifdef DEBUG_ENABLED

	virtual void set_script(const Variant &p_script) override;
	virtual Variant get_script() const override;

	virtual bool has_meta(const StringName &p_name) const override;
	virtual void set_meta(const StringName &p_name, const Variant &p_value) override;
	virtual void remove_meta(const StringName &p_name) override;
	virtual Variant get_meta(const StringName &p_name, const Variant &p_default = Variant()) const override;
	virtual void get_meta_list(List<StringName> *p_list) const override;

	virtual Error emit_signalp(const StringName &p_name, const Variant **p_args, int p_argcount) override;
	virtual bool has_signal(const StringName &p_name) const override;
	virtual void get_signal_list(List<MethodInfo> *p_signals) const override;
	virtual void get_signal_connection_list(const StringName &p_signal, List<Connection> *p_connections) const override;
	virtual void get_all_signal_connections(List<Connection> *p_connections) const override;
	virtual int get_persistent_signal_connection_count() const override;
	virtual uint32_t get_signal_connection_flags(const StringName &p_name, const Callable &p_callable) const override;
	virtual void get_signals_connected_to_this(List<Connection> *p_connections) const override;

	virtual Error connect(const StringName &p_signal, const Callable &p_callable, uint32_t p_flags = 0) override;
	virtual void disconnect(const StringName &p_signal, const Callable &p_callable) override;
	virtual bool is_connected(const StringName &p_signal, const Callable &p_callable) const override;
	virtual bool has_connections(const StringName &p_signal) const override;
#endif
	Entity();
	~Entity();
};

VARIANT_ENUM_CAST(Entity::DuplicateFlags);
VARIANT_ENUM_CAST(Entity::RunMode);
VARIANT_ENUM_CAST(Entity::RunThreadGroup);
VARIANT_BITFIELD_CAST(Entity::RunThreadMessages);
VARIANT_ENUM_CAST(Entity::InternalMode);
VARIANT_ENUM_CAST(Entity::PhysicsInterpolationMode);
VARIANT_ENUM_CAST(Entity::AutoTranslateMode);

typedef HashSet<Entity *, Entity::Comparator> NodeSet;

// Template definitions must be in the header so they are always fully initialized before their usage.
// See this StackOverflow question for more information: https://stackoverflow.com/questions/495021/why-can-templates-only-be-implemented-in-the-header-file

template <typename... VarArgs>
Error Entity::rpc(const StringName &p_method, VarArgs... p_args) {
	return rpc_id(0, p_method, p_args...);
}

template <typename... VarArgs>
Error Entity::rpc_id(int p_peer_id, const StringName &p_method, VarArgs... p_args) {
	Variant args[sizeof...(p_args) + 1] = { p_args..., Variant() }; // +1 makes sure zero sized arrays are also supported.
	const Variant *argptrs[sizeof...(p_args) + 1];
	for (uint32_t i = 0; i < sizeof...(p_args); i++) {
		argptrs[i] = &args[i];
	}
	return rpcp(p_peer_id, p_method, sizeof...(p_args) == 0 ? nullptr : (const Variant **)argptrs, sizeof...(p_args));
}

#ifdef DEBUG_ENABLED
#define ERR_THREAD_GUARD ERR_FAIL_COND_MSG(!is_accessible_from_caller_thread(), vformat("%s: The caller thread can't call the function `%s()` on this node. Use `call_deferred()` or `call_deferred_thread_group()` instead.", get_description(), FUNCTION_STR));
#define ERR_THREAD_GUARD_V(m_ret) ERR_FAIL_COND_V_MSG(!is_accessible_from_caller_thread(), (m_ret), vformat("%s: The caller thread can't call the function `%s()` on this node. Use `call_deferred()` or `call_deferred_thread_group()` instead.", get_description(), FUNCTION_STR));
#define ERR_MAIN_THREAD_GUARD ERR_FAIL_COND_MSG(is_inside_stage() && !is_current_thread_safe_for_nodes(), vformat("%s: The function `%s()` on this node can only be accessed from the main thread. Use `call_deferred()` instead.", get_description(), FUNCTION_STR));
#define ERR_MAIN_THREAD_GUARD_V(m_ret) ERR_FAIL_COND_V_MSG(is_inside_stage() && !is_current_thread_safe_for_nodes(), (m_ret), vformat("%s: The function `%s()` on this node can only be accessed from the main thread. Use `call_deferred()` instead.", get_description(), FUNCTION_STR));
#define ERR_READ_THREAD_GUARD ERR_FAIL_COND_MSG(!is_readable_from_caller_thread(), vformat("%s: The function `%s()` on this node can only be accessed from either the main thread or a thread group. Use `call_deferred()` instead.", get_description(), FUNCTION_STR));
#define ERR_READ_THREAD_GUARD_V(m_ret) ERR_FAIL_COND_V_MSG(!is_readable_from_caller_thread(), (m_ret), vformat("%s: The function `%s()` on this node can only be accessed from either the main thread or a thread group. Use `call_deferred()` instead.", get_description(), FUNCTION_STR));
#else
#define ERR_THREAD_GUARD
#define ERR_THREAD_GUARD_V(m_ret)
#define ERR_MAIN_THREAD_GUARD
#define ERR_MAIN_THREAD_GUARD_V(m_ret)
#define ERR_READ_THREAD_GUARD
#define ERR_READ_THREAD_GUARD_V(m_ret)
#endif

// Add these macro to your class's 'get_configuration_warnings' function to have warnings show up in the scene tree inspector.
#define EXPERIMENTAL_NODE_WARNING warnings.push_back(RTR("This node is marked as experimental and may be subject to removal or major changes in future versions."));
