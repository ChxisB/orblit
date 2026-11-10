/**************************************************************************/
/*  OrblitEditorTest.kt                                                   */

package org.orblitengine.editor

import android.content.ComponentName
import android.content.Intent
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.orblitengine.orblit.OrblitActivity.Companion.EXTRA_COMMAND_LINE_PARAMS
import org.junit.Test
import org.junit.runner.RunWith
import kotlin.test.assertEquals
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertTrue

/**
 * Instrumented test for the Orblit editor.
 */
@RunWith(AndroidJUnit4::class)
class OrblitEditorTest {
	companion object {
		private val TAG = OrblitEditorTest::class.simpleName

		private val TEST_COMMAND_LINE_PARAMS = arrayOf("This is a test")
		private val PM_LAUNCH_COMMAND_LINE_PARAMS = arrayOf("--project-manager")
		private val PM_VULKAN_LAUNCH_COMMAND_LINE_PARAMS = PM_LAUNCH_COMMAND_LINE_PARAMS + arrayOf("--rendering-method", "mobile")

		private const val PROJECT_MANAGER_CLASS_NAME = "org.orblitengine.editor.ProjectManager"
		private const val ORBLIT_EDITOR_CLASS_NAME = "org.orblitengine.editor.OrblitEditor"
	}

	/**
	 * Implicitly launch the project manager.
	 */
	@Test
	fun testImplicitProjectManagerLaunch() {
		val implicitLaunchIntent = Intent().apply {
			setPackage(BuildConfig.APPLICATION_ID)
			action = Intent.ACTION_MAIN
			addCategory(Intent.CATEGORY_LAUNCHER)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, TEST_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitEditor>(implicitLaunchIntent).use { scenario ->
			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, PROJECT_MANAGER_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNull(commandLineParams)
			}
		}
	}

	/**
	 * Explicitly launch the project manager.
	 */
	@Test
	fun testExplicitProjectManagerLaunch() {
		val explicitProjectManagerIntent = Intent().apply {
			component = ComponentName(BuildConfig.APPLICATION_ID, PROJECT_MANAGER_CLASS_NAME)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, TEST_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitEditor>(explicitProjectManagerIntent).use { scenario ->
			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, PROJECT_MANAGER_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNull(commandLineParams)
			}
		}
	}

	/**
	 * Explicitly launch the `OrblitEditor` activity.
	 */
	@Test
	fun testExplicitOrblitEditorLaunch() {
		val orblitEditorIntent = Intent().apply {
			component = ComponentName(BuildConfig.APPLICATION_ID, ORBLIT_EDITOR_CLASS_NAME)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, TEST_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitEditor>(orblitEditorIntent).use { scenario ->
			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, ORBLIT_EDITOR_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNotNull(commandLineParams)
				assertTrue(commandLineParams.contentEquals(TEST_COMMAND_LINE_PARAMS))
			}
		}
	}

	/**
	 * Check that the project manager can launch using vulkan (Forward mobile) without crashing.
	 */
	@Test
	fun testVulkanProjectManagerLaunch() {
		val orblitEditorIntent = Intent().apply {
			component = ComponentName(BuildConfig.APPLICATION_ID, ORBLIT_EDITOR_CLASS_NAME)
			putExtra(EXTRA_COMMAND_LINE_PARAMS, PM_VULKAN_LAUNCH_COMMAND_LINE_PARAMS)
		}
		ActivityScenario.launch<OrblitEditor>(orblitEditorIntent).use { scenario ->
			scenario.onActivity { activity ->
				assertEquals(activity.intent.component?.className, ORBLIT_EDITOR_CLASS_NAME)

				val commandLineParams = activity.intent.getStringArrayExtra(EXTRA_COMMAND_LINE_PARAMS)
				assertNotNull(commandLineParams)
				assertTrue(commandLineParams.contentEquals(PM_VULKAN_LAUNCH_COMMAND_LINE_PARAMS))
			}
		}
	}
}
