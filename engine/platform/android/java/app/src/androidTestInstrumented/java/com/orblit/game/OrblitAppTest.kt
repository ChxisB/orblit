/**************************************************************************/
/*  OrblitAppTest.kt                                                      */

package com.orblit.game

import android.content.ComponentName
import android.content.Intent
import android.util.Log
import androidx.test.core.app.ActivityScenario
import androidx.test.espresso.Espresso
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.orblit.game.app.BuildConfig
import com.orblit.game.test.OrblitAppInstrumentedTestPlugin
import org.orblitengine.orblit.Orblit
import org.orblitengine.orblit.OrblitActivity.Companion.EXTRA_COMMAND_LINE_PARAMS
import org.orblitengine.orblit.plugin.OrblitPluginRegistry
import org.junit.Test
import org.junit.runner.RunWith
import kotlin.collections.contentEquals
import kotlin.test.assertEquals
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertTrue

/**
 * This instrumented test will launch the `instrumented` version of OrblitApp and run a set of tests against it.
 */
@RunWith(AndroidJUnit4::class)
class OrblitAppTest {

	companion object {
		private val TAG = OrblitAppTest::class.java.simpleName

		private const val ORBLIT_APP_LAUNCHER_CLASS_NAME = "com.orblit.game.OrblitAppLauncher"
		private const val ORBLIT_APP_CLASS_NAME = "com.orblit.game.OrblitApp"

		private val TEST_COMMAND_LINE_PARAMS = arrayOf("This is a test")
		private val VULKAN_RENDERER_COMMAND_LINE_PARAMS = arrayOf("--rendering-method","mobile")
	}

	private fun getTestPlugin(): OrblitAppInstrumentedTestPlugin? {
		return OrblitPluginRegistry.getPluginRegistry()
			.getPlugin("OrblitAppInstrumentedTestPlugin") as OrblitAppInstrumentedTestPlugin?
	}

	/**
	 * Runs the JavaClassWrapper tests via the OrblitAppInstrumentedTestPlugin.
	 */
	@Test
	fun runJavaClassWrapperTests() {
		ActivityScenario.launch(OrblitApp::class.java).use { scenario ->
			scenario.onActivity { activity ->
				val testPlugin = getTestPlugin()
				assertNotNull(testPlugin)

				Log.d(TAG, "Waiting for the Orblit main loop to start...")
				testPlugin.waitForOrblitMainLoopStarted()

				Log.d(TAG, "Running JavaClassWrapper tests...")
				val result = testPlugin.runJavaClassWrapperTests()
				assertNotNull(result)
				result.exceptionOrNull()?.let { throw it }
				assertTrue(result.isSuccess)
				Log.d(TAG, "Passed ${result.getOrNull()} tests")
			}
		}
	}

	/**
	 * Runs file access related tests.
	 */
	@Test
	fun runFileAccessTests() {
		ActivityScenario.launch(OrblitApp::class.java).use { scenario ->
			scenario.onActivity { activity ->
				val testPlugin = getTestPlugin()
				assertNotNull(testPlugin)

				Log.d(TAG, "Waiting for the Orblit main loop to start...")
				testPlugin.waitForOrblitMainLoopStarted()

				Log.d(TAG, "Running FileAccess tests...")
				val result = testPlugin.runFileAccessTests()
				assertNotNull(result)
				result.exceptionOrNull()?.let { throw it }
				assertTrue(result.isSuccess)
			}
		}
	}

	/**
	 * Runs test to validate android plugin signals.
	 */
	@Test
	fun runPluginSignalTests() {
		ActivityScenario.launch(OrblitApp::class.java).use { scenario ->
			scenario.onActivity { activity ->
				val testPlugin = getTestPlugin()
				assertNotNull(testPlugin)

				Log.d(TAG, "Waiting for the Orblit main loop to start...")
				testPlugin.waitForOrblitMainLoopStarted()

				Log.d(TAG, "Running Android plugin signal tests...")
				val result = testPlugin.runPluginSignalTests()
				assertNotNull(result)
				result.exceptionOrNull()?.let { throw it }
				assertTrue(result.isSuccess)
			}
		}
	}

	/**
	 * Test implicit launch of the Orblit app, and validates this resolves to the `OrblitAppLauncher` activity alias.
	 */
	@Test
	fun testImplicitOrblitAppLauncherLaunch() {
		val implicitLaunchIntent = Intent().apply {
			setPackage(BuildConfig.APPLICATION_ID)
			action = Intent.ACTION_MAIN
			addCategory(Intent.CATEGORY_LAUNCHER)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, TEST_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitApp>(implicitLaunchIntent).use { scenario ->
			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, ORBLIT_APP_LAUNCHER_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNull(commandLineParams)
			}
		}
	}

	/**
	 * Test explicit launch of the Orblit app via its activity-alias launcher, and validates it resolves properly.
	 */
	@Test
	fun testExplicitOrblitAppLauncherLaunch() {
		val explicitIntent = Intent().apply {
			component = ComponentName(BuildConfig.APPLICATION_ID, ORBLIT_APP_LAUNCHER_CLASS_NAME)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, TEST_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitApp>(explicitIntent).use { scenario ->
			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, ORBLIT_APP_LAUNCHER_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNull(commandLineParams)
			}
		}
	}

	/**
	 * Test explicit launch of the `OrblitApp` activity.
	 */
	@Test
	fun testExplicitOrblitAppLaunch() {
		val explicitIntent = Intent().apply {
			component = ComponentName(BuildConfig.APPLICATION_ID, ORBLIT_APP_CLASS_NAME)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, TEST_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitApp>(explicitIntent).use { scenario ->
			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, ORBLIT_APP_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNotNull(commandLineParams)
				assertTrue(commandLineParams.contentEquals(TEST_COMMAND_LINE_PARAMS))
			}
		}
	}

	/**
	 * Test that launching using the Vulkan renderer doesn't crash the app.
	 */
	@Test
	fun testVulkanOrblitAppLaunch() {
		val appIntent = Intent().apply {
			component = ComponentName(BuildConfig.APPLICATION_ID, ORBLIT_APP_CLASS_NAME)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, VULKAN_RENDERER_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitApp>(appIntent).use { scenario ->
			val testPlugin = getTestPlugin()
			assertNotNull(testPlugin)

			Log.d(TAG, "Waiting for the Orblit main loop to start...")
			testPlugin.waitForOrblitMainLoopStarted()

			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, ORBLIT_APP_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNotNull(commandLineParams)
				assertTrue(commandLineParams.contentEquals(VULKAN_RENDERER_COMMAND_LINE_PARAMS))
			}
		}
	}

	/**
	 * Validate that the back press does not quit the game when 'quit_on_go_back' is disabled.
	 */
	@Test
	fun testGameNotQuittingOnBackPress() {
		ActivityScenario.launch(OrblitApp::class.java).use { scenario ->
			val testPlugin = getTestPlugin()
			assertNotNull(testPlugin)

			Log.d(TAG, "Waiting for the Orblit main loop to start...")
			testPlugin.waitForOrblitMainLoopStarted()

			// Disable 'quit_on_go_back'.
			testPlugin.updateQuitOnGoBack(false)

			// Trigger the back press event.
			Espresso.pressBackUnconditionally()

			Log.d(TAG, "Waiting for the engine to terminate...")
			testPlugin.waitForEngineTermination(5_000L)

			val orblit = Orblit.getInstance(InstrumentationRegistry.getInstrumentation().targetContext)
			assertTrue { orblit.runStatus != Orblit.RunStatus.TERMINATING }
		}
	}

	/**
	 * Validate that the back press event quits the game when 'quit_on_go_back' is enabled.
	 */
	@Test
	fun testGameQuittingOnBackPress() {
		ActivityScenario.launch(OrblitApp::class.java).use { scenario ->
			val testPlugin = getTestPlugin()
			assertNotNull(testPlugin)

			Log.d(TAG, "Waiting for the Orblit main loop to start...")
			testPlugin.waitForOrblitMainLoopStarted()

			// Enable 'quit_on_go_back'.
			testPlugin.updateQuitOnGoBack(true)

			// Trigger the back press event.
			Espresso.pressBackUnconditionally()

			Log.d(TAG, "Waiting for the engine to terminate...")
			testPlugin.waitForEngineTermination(5_000L)

			val orblit = Orblit.getInstance(InstrumentationRegistry.getInstrumentation().targetContext)
			assertTrue { orblit.runStatus == Orblit.RunStatus.TERMINATING }
		}
	}
}
