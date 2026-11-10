/**************************************************************************/
/*  EditorBenchmarks.kt                                                   */


package org.orblitengine.editor.v4.macrobenchmark

import android.Manifest
import android.util.Log
import androidx.benchmark.macro.ExperimentalMetricApi
import androidx.benchmark.macro.FrameTimingGfxInfoMetric
import androidx.benchmark.macro.MemoryUsageMetric
import androidx.benchmark.macro.StartupMode
import androidx.benchmark.macro.StartupTimingMetric
import androidx.benchmark.macro.junit4.MacrobenchmarkRule
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.rule.GrantPermissionRule
import androidx.test.uiautomator.By
import androidx.test.uiautomator.StaleObjectException
import androidx.test.uiautomator.Until
import androidx.test.uiautomator.uiAutomator
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Set of editor macro benchmarks.
 *
 * Before running, switch the editor's active build variant to 'benchmark'
 */
@RunWith(AndroidJUnit4::class)
class EditorBenchmarks {

	companion object {
		private val TAG = EditorBenchmarks::class.java.simpleName

		private const val PACKAGE_NAME = "org.orblitengine.editor.v4.benchmark"
		private const val ITERATION_COUNT = 5
		private const val EXTRA_LOAD_EMPTY_BENCHMARK_PROJECT = "load_empty_benchmark_project"
		private const val EXTRA_BENCHMARK_RENDERING_METHOD = "benchmark_rendering_method"

		private const val MOBILE_RENDERING_METHOD = "mobile"
		private const val GL_COMPATIBILITY_RENDERING_METHOD = "gl_compatibility"
	}

	@get:Rule
    val benchmarkRule = MacrobenchmarkRule()
	@get:Rule
    val grantPermissionRule = GrantPermissionRule.grant(
		Manifest.permission.READ_EXTERNAL_STORAGE,
		Manifest.permission.WRITE_EXTERNAL_STORAGE)

	/**
	 * Navigates to the device's home screen, and launches the Project Manager.
	 */
	@OptIn(ExperimentalMetricApi::class)
	@Test
	fun startupProjectManagerUsingOpenGL() = benchmarkRule.measureRepeated(
			packageName = PACKAGE_NAME,
			metrics = listOf(
				StartupTimingMetric(),
				MemoryUsageMetric(MemoryUsageMetric.Mode.Max),
				FrameTimingGfxInfoMetric(),
			),
			iterations = ITERATION_COUNT,
			startupMode = StartupMode.COLD
	) {
		pressHome()
		startActivityAndWait { intent ->
			intent.putExtra(EXTRA_BENCHMARK_RENDERING_METHOD, GL_COMPATIBILITY_RENDERING_METHOD)
		}
	}

	/**
	 * Navigates to the device's home screen, and launches the Project Manager using the Vulkan renderer.
	 */
	@OptIn(ExperimentalMetricApi::class)
	@Test
	fun startupProjectManagerUsingVulkan() = benchmarkRule.measureRepeated(
		packageName = PACKAGE_NAME,
		metrics = listOf(
			StartupTimingMetric(),
			MemoryUsageMetric(MemoryUsageMetric.Mode.Max),
			FrameTimingGfxInfoMetric(),
		),
		iterations = ITERATION_COUNT,
		startupMode = StartupMode.COLD
	) {
		pressHome()
		startActivityAndWait { intent ->
			intent.putExtra(EXTRA_BENCHMARK_RENDERING_METHOD, MOBILE_RENDERING_METHOD)
		}
	}

	/**
	 * Navigates to the device's home screen, launches the editor and loads an empty project.
	 */
	@OptIn(ExperimentalMetricApi::class)
	@Test
	fun startupEditorWindowUsingOpenGL() = benchmarkRule.measureRepeated(
		packageName = PACKAGE_NAME,
		metrics = listOf(
			StartupTimingMetric(),
			MemoryUsageMetric(MemoryUsageMetric.Mode.Max),
			FrameTimingGfxInfoMetric(),
		),
		iterations = ITERATION_COUNT,
		startupMode = StartupMode.COLD,
	) {
		pressHome()
		startActivityAndWait { intent ->
			intent.putExtra(EXTRA_LOAD_EMPTY_BENCHMARK_PROJECT, true)
			intent.putExtra(EXTRA_BENCHMARK_RENDERING_METHOD, GL_COMPATIBILITY_RENDERING_METHOD)
		}

		waitForEditorLoadingIndicator()
	}

	/**
	 * Navigates to the device's home screen, launches the editor and loads an empty project using the Vulkan renderer.
	 */
	@OptIn(ExperimentalMetricApi::class)
	@Test
	fun startupEditorWindowUsingVulkan() = benchmarkRule.measureRepeated(
		packageName = PACKAGE_NAME,
		metrics = listOf(
			StartupTimingMetric(),
			MemoryUsageMetric(MemoryUsageMetric.Mode.Max),
			FrameTimingGfxInfoMetric(),
		),
		iterations = ITERATION_COUNT,
		startupMode = StartupMode.COLD,
	) {
		pressHome()
		startActivityAndWait { intent ->
			intent.putExtra(EXTRA_LOAD_EMPTY_BENCHMARK_PROJECT, true)
			intent.putExtra(EXTRA_BENCHMARK_RENDERING_METHOD, MOBILE_RENDERING_METHOD)
		}

		waitForEditorLoadingIndicator()
	}

	private fun waitForEditorLoadingIndicator() {
		uiAutomator {
			try {
				val selector = By.res("$PACKAGE_NAME:id/editor_loading_indicator")
				val editorLoadingIndicator = device.findObject(selector)
				editorLoadingIndicator?.wait(Until.gone(selector), 60_000L)
			} catch (e: StaleObjectException) {
				Log.w(TAG, "Error while waiting for editor loading indicator.", e)
			}
		}
	}
}
