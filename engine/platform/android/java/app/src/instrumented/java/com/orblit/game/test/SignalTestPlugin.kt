package com.orblit.game.test

import org.orblitengine.orblit.Orblit
import org.orblitengine.orblit.plugin.OrblitPlugin
import org.orblitengine.orblit.plugin.SignalInfo
import org.orblitengine.orblit.plugin.UsedByOrblit

class SignalTestPlugin(orblit: Orblit) : OrblitPlugin(orblit) {

	companion object {
		private val EMISSION_TEST_SIGNAL = SignalInfo("emission_test_signal")
		private val LAUNCH_TESTS_SIGNAL = SignalInfo("launch_tests", java.lang.Boolean::class.java, String::class.java)
	}

	override fun getPluginName() = "SignalTestPlugin"

	override fun getPluginSignals(): Set<SignalInfo?> {
		return setOf(
			EMISSION_TEST_SIGNAL,
			LAUNCH_TESTS_SIGNAL
		)
	}

	@UsedByOrblit
	fun triggerTestSignal1() {
		emitSignal(EMISSION_TEST_SIGNAL)
	}

	@UsedByOrblit
	fun triggerLaunchTestSignal() {
		emitSignal(LAUNCH_TESTS_SIGNAL, true, "second message")
	}
}
