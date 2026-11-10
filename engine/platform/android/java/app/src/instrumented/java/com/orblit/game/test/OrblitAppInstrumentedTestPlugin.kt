/**************************************************************************/
/*  OrblitAppInstrumentedTestPlugin.kt                                    */

package com.orblit.game.test

import android.util.Log
import android.widget.Toast
import org.orblitengine.orblit.Orblit
import org.orblitengine.orblit.plugin.OrblitPlugin
import org.orblitengine.orblit.plugin.UsedByOrblit
import org.orblitengine.orblit.plugin.SignalInfo
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

/**
 * [OrblitPlugin] used to drive instrumented tests.
 */
class OrblitAppInstrumentedTestPlugin(orblit: Orblit) : OrblitPlugin(orblit) {

	companion object {
		private val TAG = OrblitAppInstrumentedTestPlugin::class.java.simpleName
		private const val MAIN_LOOP_STARTED_LATCH_KEY = "main_loop_started_latch"
		private const val ENGINE_TERMINATING_LATCH_KEY = "engine_terminating_latch"

		private const val JAVACLASSWRAPPER_TESTS = "javaclasswrapper_tests"
		private const val FILE_ACCESS_TESTS = "file_access_tests"
		private const val PLUGIN_SIGNAL_TESTS = "android_plugin_signal_tests"

		private val LAUNCH_TESTS_SIGNAL = SignalInfo("launch_tests", String::class.java)
		private val UPDATE_QUIT_ON_GO_BACK_SIGNAL = SignalInfo("update_quit_on_go_back", java.lang.Boolean::class.java)

		private val SIGNALS = setOf(
			LAUNCH_TESTS_SIGNAL,
			UPDATE_QUIT_ON_GO_BACK_SIGNAL
		)
	}

	private val testResults = ConcurrentHashMap<String, Result<Any>>()
	private val latches = ConcurrentHashMap<String, CountDownLatch>()

	init {
		// Add a countdown latch that is triggered when `onOrblitMainLoopStarted` is fired.
		// This will be used by tests to wait until the engine is ready.
		latches[MAIN_LOOP_STARTED_LATCH_KEY] = CountDownLatch(1)
		// Add a countdown latch that is triggered when the engine terminates.
		latches[ENGINE_TERMINATING_LATCH_KEY] = CountDownLatch(1)
	}

	override fun getPluginName() = "OrblitAppInstrumentedTestPlugin"

	override fun getPluginSignals() = SIGNALS

	override fun onOrblitMainLoopStarted() {
		super.onOrblitMainLoopStarted()
		latches.remove(MAIN_LOOP_STARTED_LATCH_KEY)?.countDown()
	}

	override fun onOrblitTerminating() {
		super.onOrblitTerminating()
		latches.remove(ENGINE_TERMINATING_LATCH_KEY)?.countDown()
	}

	/**
	 * Used by the instrumented test to wait until the Orblit main loop is up and running.
	 */
	internal fun waitForOrblitMainLoopStarted() {
		// Wait on the CountDownLatch for `onOrblitMainLoopStarted`
		try {
			latches[MAIN_LOOP_STARTED_LATCH_KEY]?.await()
		} catch (e: InterruptedException) {
			Log.e(TAG, "Unable to wait for Orblit main loop started event.", e)
		}
	}

	internal fun waitForEngineTermination(timeoutInMs: Long) {
		// Wait on the CountDownLatch for `onOrblitTerminating`.
		try {
			latches[ENGINE_TERMINATING_LATCH_KEY]?.await(timeoutInMs, TimeUnit.MILLISECONDS)
		} catch (e: InterruptedException) {
			Log.e(TAG, "Unable to wait for engine termination event.", e)
		}
	}

	internal fun updateQuitOnGoBack(quitOnGoBack: Boolean) {
		emitSignal(UPDATE_QUIT_ON_GO_BACK_SIGNAL, quitOnGoBack)
	}

	/**
	 * This launches the JavaClassWrapper tests, and wait until the tests are complete before returning.
	 */
	internal fun runJavaClassWrapperTests(): Result<Any>? {
		return launchTests(JAVACLASSWRAPPER_TESTS)
	}

	/**
	 * Launches the FileAccess tests, and wait until the tests are complete before returning.
	 */
	internal fun runFileAccessTests(): Result<Any>? {
		return launchTests(FILE_ACCESS_TESTS)
	}

	internal fun runPluginSignalTests(): Result<Any>? {
		return launchTests(PLUGIN_SIGNAL_TESTS)
	}

	private fun launchTests(testLabel: String): Result<Any>? {
		val latch = latches.getOrPut(testLabel) { CountDownLatch(1) }
		emitSignal(LAUNCH_TESTS_SIGNAL, testLabel)
		return try {
			latch.await()
			val result = testResults.remove(testLabel)
			result
		} catch (e: InterruptedException) {
			Log.e(TAG, "Unable to wait for completion for $testLabel", e)
			null
		}
	}

	/**
	 * Callback invoked from scripts when the tests are completed.
	 */
	@UsedByOrblit
	fun onTestsCompleted(testLabel: String, passes: Int, failures: Int) {
		Log.d(TAG, "$testLabel tests completed")
		val result = if (failures == 0) {
			Result.success(passes)
		} else {
			Result.failure(AssertionError("$failures tests failed!"))
		}

		completeTest(testLabel, result)
	}

	@UsedByOrblit
	fun onTestsFailed(testLabel: String, failureMessage: String) {
		Log.d(TAG, "$testLabel tests failed")
		val result: Result<Any> = Result.failure(AssertionError(failureMessage))
		completeTest(testLabel, result)
	}

	private fun completeTest(testKey: String, result: Result<Any>) {
		testResults[testKey] = result
		latches.remove(testKey)?.countDown()
	}

	@UsedByOrblit
	fun helloWorld() {
		runOnHostThread {
			Toast.makeText(activity, "Toast from Android plugin", Toast.LENGTH_LONG).show()
			Log.v(pluginName, "Hello World")
		}
	}
}
