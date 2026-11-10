/**************************************************************************/
/*  OrblitApp.java                                                        */

package com.orblit.game;

import org.orblitengine.orblit.Orblit;
import org.orblitengine.orblit.OrblitActivity;

import android.os.Bundle;

import androidx.activity.EdgeToEdge;
import androidx.core.splashscreen.SplashScreen;

/**
 * Template activity for Orblit Android builds.
 * Feel free to extend and modify this class for your custom logic.
 */
public class OrblitApp extends OrblitActivity {
	private final Runnable updateWindowAppearance = () -> {
		Orblit orblit = getOrblit();
		if (orblit != null) {
			orblit.enableImmersiveMode(orblit.isInImmersiveMode(), true);
			orblit.enableEdgeToEdge(orblit.isInEdgeToEdgeMode(), true);
			orblit.setSystemBarsAppearance();
		}
	};

	@Override
	public void onCreate(Bundle savedInstanceState) {
		SplashScreen splashScreen = SplashScreen.installSplashScreen(this);
		EdgeToEdge.enable(this);
		super.onCreate(savedInstanceState);

		Orblit orblit = getOrblit();
		if (orblit != null && orblit.getDisableOrblitSplash()) {
			splashScreen.setKeepOnScreenCondition(() -> orblit.getRunStatus() != Orblit.RunStatus.STARTED);
		}
	}

	@Override
	public void onResume() {
		super.onResume();
		updateWindowAppearance.run();
	}

	@Override
	public void onOrblitMainLoopStarted() {
		super.onOrblitMainLoopStarted();
		runOnUiThread(updateWindowAppearance);
	}

	@Override
	public void onOrblitForceQuit(Orblit instance) {
		if (!getPackageName().equals("com.orblit.game.instrumented")) {
			// For instrumented builds, we disable force-quitting to allow the instrumented tests to complete
			// successfully, otherwise they fail when the process crashes.
			super.onOrblitForceQuit(instance);
		}
	}

	@Override
	protected boolean isPiPEnabled() {
		return true;
	}
}
