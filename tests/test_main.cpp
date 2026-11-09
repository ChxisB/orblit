/**************************************************************************/
/*  test_main.cpp                                                         */

#include "test_main.h"

#include "core/config/engine.h"
#include "core/input/input.h"
#include "core/input/input_map.h"
#include "core/io/dir_access.h"
#include "core/object/worker_thread_pool.h"
#include "core/os/os.h"
#include "core/string/translation_system.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/theme/theme_db.h"
#include "systems/audio/audio_driver.h"
#include "systems/audio/audio_system.h"
#include "systems/display/accessibility_system.h"
#include "systems/rendering/rendering_system.h"
#include "tests/display_system_mock.h"
#include "tests/force_link.gen.h"
#include "tests/signal_watcher.h"
#include "tests/test_macros.h"
#include "tests/test_utils.h"

#ifdef TOOLS_ENABLED
#include "editor/file_system/editor_paths.h"
#include "editor/settings/editor_settings.h"
#endif // TOOLS_ENABLED

#ifndef NAVIGATION_PLANAR_DISABLED
#include "systems/navigation_planar/navigation_system_planar.h"
#include "systems/navigation_planar/navigation_system_planar_manager.h"
#endif // NAVIGATION_PLANAR_DISABLED
#ifndef NAVIGATION_SPATIAL_DISABLED
#include "systems/navigation_spatial/navigation_system_spatial.h"
#include "systems/navigation_spatial/navigation_system_spatial_manager.h"
#endif // NAVIGATION_SPATIAL_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
#include "systems/physics_planar/physics_system_planar.h"
#include "systems/physics_planar/physics_system_planar_dummy.h"
#include "systems/physics_planar/physics_system_planar_manager.h"
#endif // PHYSICS_PLANAR_DISABLED
#ifndef PHYSICS_SPATIAL_DISABLED
#include "systems/physics_spatial/physics_system_spatial.h"
#include "systems/physics_spatial/physics_system_spatial_dummy.h"
#include "systems/physics_spatial/physics_system_spatial_manager.h"
#endif // PHYSICS_SPATIAL_DISABLED

#include "modules/modules_tests.gen.h" // IWYU pragma: keep // TODO: Migrate module tests to compilation files.

int test_main(int argc, char *argv[]) {
	ForceLink::force_link_tests();

	bool run_tests = true;

	// Convert arguments to Orblit's command-line.
	List<String> args;

	for (int i = 0; i < argc; i++) {
		args.push_back(String::utf8(argv[i]));
	}
	OS::get_singleton()->set_cmdline("", args, List<String>());
	DisplaySystemMock::register_mock_driver();

	WorkerThreadPool::get_singleton()->init();

	{
		const String test_path = TestUtils::get_temp_path("");
		Ref<DirAccess> da = DirAccess::open(test_path); // get_temp_path() automatically creates the folder.
		ERR_FAIL_COND_V(da.is_null(), 0);
		ERR_FAIL_COND_V_MSG(da->erase_contents_recursive() != OK, 0, "Failed to delete files");
	}

	// Run custom test tools.
	if (test_commands) {
		int res = 0;
		for (const KeyValue<String, TestFunc> &E : (*test_commands)) {
			if (args.find(E.key)) {
				const TestFunc &test_func = E.value;
				res = test_func();
				run_tests = false;
				break;
			}
		}
		if (!run_tests) {
			delete test_commands;
			return res;
		}
	}
	// Doctest runner.
	doctest::Context test_context;
	LocalVector<String> test_args;

	// Clean arguments of "--test" from the args.
	for (int x = 0; x < argc; x++) {
		String arg = String(argv[x]);
		if (arg != "--test") {
			test_args.push_back(arg);
		}
	}

	if (test_args.size() > 0) {
		// Convert Orblit command line arguments back to standard arguments.
		char **doctest_args = new char *[test_args.size()];
		for (uint32_t x = 0; x < test_args.size(); x++) {
			// Operation to convert Orblit string to non wchar string.
			CharString cs = test_args[x].utf8();
			const char *str = cs.get_data();
			// Allocate the string copy.
			doctest_args[x] = new char[strlen(str) + 1];
			// Copy this into memory.
			memcpy(doctest_args[x], str, strlen(str) + 1);
		}

		test_context.applyCommandLine(test_args.size(), doctest_args);

		for (uint32_t x = 0; x < test_args.size(); x++) {
			delete[] doctest_args[x];
		}
		delete[] doctest_args;
	}

	return test_context.run();
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

struct OrblitTestCaseListener : public doctest::IReporter {
	OrblitTestCaseListener(const doctest::ContextOptions &p_in) {}

	SignalWatcher *signal_watcher = nullptr;

#ifndef PHYSICS_PLANAR_DISABLED
	PhysicsSystem2D *physics_system_2d = nullptr;
#endif // PHYSICS_PLANAR_DISABLED
#ifndef PHYSICS_SPATIAL_DISABLED
	PhysicsSystem3D *physics_system_3d = nullptr;
#endif // PHYSICS_SPATIAL_DISABLED

#ifndef NAVIGATION_PLANAR_DISABLED
	NavigationSystem2D *navigation_system_2d = nullptr;
#endif // NAVIGATION_PLANAR_DISABLED
#ifndef NAVIGATION_SPATIAL_DISABLED
	NavigationSystem3D *navigation_system_3d = nullptr;
#endif // NAVIGATION_SPATIAL_DISABLED

	void test_case_start(const doctest::TestCaseData &p_in) override {
		reinitialize();

		String name = String(p_in.m_name);
		[[maybe_unused]] String suite_name = String(p_in.m_test_suite);

		if (name.contains("[Stage]") || name.contains("[Editor]")) {
			memnew(Input);
			Input::get_singleton()->set_use_accumulated_input(false);

			Error err = OK;
			OS::get_singleton()->set_has_server_feature_callback(nullptr);

			for (int i = 0; i < AccessibilitySystem::get_create_function_count(); i++) {
				if (String("dummy") == AccessibilitySystem::get_create_function_name(i)) {
					AccessibilitySystem::create(i, err);
					break;
				}
			}

			for (int i = 0; i < DisplaySystem::get_create_function_count(); i++) {
				if (String("mock") == DisplaySystem::get_create_function_name(i)) {
					DisplaySystem::create(i, "", DisplaySystemEnums::WindowMode::WINDOW_MODE_MINIMIZED, DisplaySystemEnums::VSyncMode::VSYNC_ENABLED, 0, nullptr, Vector2i(0, 0), DisplaySystemEnums::SCREEN_PRIMARY, DisplaySystemEnums::CONTEXT_EDITOR, 0, err);
					break;
				}
			}

			// ThemeDB requires RenderingSystem to initialize the default theme.
			// So we have to do this for each test case. Also make sure there is
			// no residual theme from something else.
			ThemeDB::get_singleton()->finalize_theme();
			ThemeDB::get_singleton()->initialize_theme();

#ifndef PHYSICS_SPATIAL_DISABLED
			physics_system_3d = PhysicsSystem3DManager::get_singleton()->new_default_system();
			if (!physics_system_3d) {
				physics_system_3d = memnew(PhysicsSystem3DDummy);
			}
			physics_system_3d->init();
#endif // PHYSICS_SPATIAL_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
			physics_system_2d = PhysicsSystem2DManager::get_singleton()->new_default_system();
			if (!physics_system_2d) {
				physics_system_2d = memnew(PhysicsSystem2DDummy);
			}
			physics_system_2d->init();
#endif // PHYSICS_PLANAR_DISABLED

			ERR_PRINT_OFF;
#ifndef NAVIGATION_SPATIAL_DISABLED
			navigation_system_3d = NavigationSystem3DManager::get_singleton()->new_default_system();
#endif // NAVIGATION_SPATIAL_DISABLED
#ifndef NAVIGATION_PLANAR_DISABLED
			navigation_system_2d = NavigationSystem2DManager::get_singleton()->new_default_system();
#endif // NAVIGATION_PLANAR_DISABLED
			ERR_PRINT_ON;

			memnew(InputMap);
			InputMap::get_singleton()->load_default();

			memnew(Stage);
			Stage::get_singleton()->initialize();
			if (!DisplaySystem::get_singleton()->has_feature(DisplaySystemEnums::FEATURE_SUBWINDOWS)) {
				Stage::get_singleton()->get_root()->set_embedding_subwindows(true);
			}

#ifdef TOOLS_ENABLED
			if (name.contains("[Editor]")) {
				Engine::get_singleton()->set_editor_hint(true);
				EditorPaths::create();
				EditorSettings::create();
			}
#endif // TOOLS_ENABLED

			return;
		}

		if (name.contains("[Audio]")) {
			// The last driver index should always be the dummy driver.
			int dummy_idx = AudioDriverManager::get_driver_count() - 1;
			AudioDriverManager::initialize(dummy_idx);
			AudioSystem *audio_system = memnew(AudioSystem);
			audio_system->init();
			return;
		}

#ifndef NAVIGATION_SPATIAL_DISABLED
		if (suite_name.contains("[Navigation3D]") && navigation_system_3d == nullptr) {
			ERR_PRINT_OFF;
			navigation_system_3d = NavigationSystem3DManager::get_singleton()->new_default_system();
			ERR_PRINT_ON;
			return;
		}
#endif // NAVIGATION_SPATIAL_DISABLED

#ifndef NAVIGATION_PLANAR_DISABLED
		if (suite_name.contains("[Navigation2D]") && navigation_system_2d == nullptr) {
			ERR_PRINT_OFF;
			navigation_system_2d = NavigationSystem2DManager::get_singleton()->new_default_system();
			ERR_PRINT_ON;
			return;
		}
#endif // NAVIGATION_PLANAR_DISABLED
	}

	void test_case_end(const doctest::CurrentTestCaseStats &) override {
#ifdef TOOLS_ENABLED
		if (EditorSettings::get_singleton()) {
			EditorSettings::destroy();

			// Instantiating the EditorSettings singleton sets the locale to the editor's language.
			TranslationSystem::get_singleton()->set_locale("en");
		}
		if (EditorPaths::get_singleton()) {
			EditorPaths::free();
		}
#endif // TOOLS_ENABLED

		Engine::get_singleton()->set_editor_hint(false);

		if (Stage::get_singleton()) {
			Stage::get_singleton()->finalize();
		}

		if (MessageQueue::get_singleton()) {
			MessageQueue::get_singleton()->flush();
		}

		memdelete(Stage::get_singleton());

#ifndef NAVIGATION_SPATIAL_DISABLED
		if (navigation_system_3d) {
			memdelete(navigation_system_3d);
			navigation_system_3d = nullptr;
		}
#endif // NAVIGATION_SPATIAL_DISABLED

#ifndef NAVIGATION_PLANAR_DISABLED
		if (navigation_system_2d) {
			memdelete(navigation_system_2d);
			navigation_system_2d = nullptr;
		}
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
		if (physics_system_3d) {
			physics_system_3d->finish();
			memdelete(physics_system_3d);
			physics_system_3d = nullptr;
		}
#endif // PHYSICS_SPATIAL_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
		if (physics_system_2d) {
			physics_system_2d->finish();
			memdelete(physics_system_2d);
			physics_system_2d = nullptr;
		}
#endif // PHYSICS_PLANAR_DISABLED

		memdelete(Input::get_singleton());

		if (RenderingSystem::get_singleton()) {
			ThemeDB::get_singleton()->finalize_theme();
		}

		memdelete(AccessibilitySystem::get_singleton());
		memdelete(DisplaySystem::get_singleton());
		memdelete(InputMap::get_singleton());

		if (AudioSystem::get_singleton()) {
			AudioSystem::get_singleton()->finish();
			memdelete(AudioSystem::get_singleton());
		}
	}

	void test_run_start() override {
		signal_watcher = memnew(SignalWatcher);
	}

	void test_run_end(const doctest::TestRunStats &) override {
		memdelete(signal_watcher);
	}

	void test_case_reenter(const doctest::TestCaseData &) override {
		reinitialize();
	}

	void subcase_start(const doctest::SubcaseSignature &) override {
		reinitialize();
	}

	void report_query(const doctest::QueryData &) override {}
	void test_case_exception(const doctest::TestCaseException &) override {}
	void subcase_end() override {}

	void log_assert(const doctest::AssertData &in) override {}
	void log_message(const doctest::MessageData &) override {}
	void test_case_skipped(const doctest::TestCaseData &) override {}

private:
	void reinitialize() {
		Math::seed(0x60d07);
		SignalWatcher::get_singleton()->_clear_signals();
	}
};

REGISTER_LISTENER("OrblitTestCaseListener", 1, OrblitTestCaseListener);
