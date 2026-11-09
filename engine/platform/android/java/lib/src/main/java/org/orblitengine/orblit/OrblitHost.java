/**************************************************************************/
/*  OrblitHost.java                                                       */

package org.orblitengine.orblit;

import org.orblitengine.orblit.error.Error;
import org.orblitengine.orblit.plugin.OrblitPlugin;

import android.app.Activity;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import java.util.Collections;
import java.util.List;
import java.util.Set;

/**
 * Denotate a component (e.g: Activity, Fragment) that hosts the {@link Orblit} engine.
 */
public interface OrblitHost {
	/**
	 * Provides a set of command line parameters to setup the {@link Orblit} engine.
	 */
	default List<String> getCommandLine() {
		return Collections.emptyList();
	}

	/**
	 * Invoked on the render thread when setup of the {@link Orblit} engine is complete.
	 */
	default void onOrblitSetupCompleted() {}

	/**
	 * Invoked on the render thread when the {@link Orblit} engine main loop has started.
	 */
	default void onOrblitMainLoopStarted() {}

	/**
	 * Invoked on the render thread to terminate the given {@link Orblit} engine instance.
	 */
	default void onOrblitForceQuit(Orblit instance) {}

	/**
	 * Invoked on the render thread to terminate the {@link Orblit} engine instance with the given id.
	 * @param orblitInstanceId id of the Orblit instance to terminate. See {@code onNewOrblitInstanceRequested}
	 *
	 * @return true if successful, false otherwise.
	 */
	default boolean onOrblitForceQuit(int orblitInstanceId) {
		return false;
	}

	/**
	 * Invoked on the render thread when the Orblit instance wants to be restarted. It's up to the host
	 * to perform the appropriate action(s).
	 */
	default void onOrblitRestartRequested(Orblit instance) {}

	/**
	 * Invoked on the render thread when a new Orblit instance is requested. It's up to the host to
	 * perform the appropriate action(s).
	 *
	 * @param args Arguments used to initialize the new instance.
	 *
	 * @return the id of the new instance. See {@code onOrblitForceQuit}
	 */
	default int onNewOrblitInstanceRequested(String[] args) {
		return -1;
	}

	/**
	 * Provide access to the Activity hosting the {@link Orblit} engine if any.
	 */
	@Nullable
	Activity getActivity();

	/**
	 * Provide access to the hosted {@link Orblit} engine.
	 */
	Orblit getOrblit();

	/**
	 * Returns a set of {@link OrblitPlugin} to be registered with the hosted {@link Orblit} engine.
	 */
	default Set<OrblitPlugin> getHostPlugins(Orblit engine) {
		return Collections.emptySet();
	}

	/**
	 * Signs the given Android apk
	 *
	 * @param inputPath Path to the apk that should be signed
	 * @param outputPath Path for the signed output apk; can be the same as inputPath
	 * @param keystorePath Path to the keystore to use for signing the apk
	 * @param keystoreUser Keystore user credential
	 * @param keystorePassword Keystore password credential
	 *
	 * @return {@link Error#OK} if signing is successful
	 */
	default Error signApk(@NonNull String inputPath, @NonNull String outputPath, @NonNull String keystorePath, @NonNull String keystoreUser, @NonNull String keystorePassword) {
		return Error.ERR_UNAVAILABLE;
	}

	/**
	 * Verifies the given Android apk is signed
	 *
	 * @param apkPath Path to the apk that should be verified
	 * @return {@link Error#OK} if verification was successful
	 */
	default Error verifyApk(@NonNull String apkPath) {
		return Error.ERR_UNAVAILABLE;
	}

	/**
	 * Returns whether the given feature tag is supported.
	 */
	default boolean supportsFeature(String featureTag) {
		return false;
	}

	/**
	 * Invoked on the render thread when an editor workspace has been selected.
	 */
	default void onEditorWorkspaceSelected(String workspace) {}

	/**
	 * Invoked on the render thread to request showing the editor game dock.
	 */
	default void showEditorGameDock() {}

	/**
	 * Triggered when the editor's distraction-free mode changes.
	 */
	default void onDistractionFreeModeChanged(Boolean enabled) {}

	/**
	 * Runs the specified action on a host provided thread.
	 */
	default void runOnHostThread(Runnable action) {
		if (action == null) {
			return;
		}

		Activity activity = getActivity();
		if (activity != null) {
			activity.runOnUiThread(action);
		}
	}

	/**
	 * Gets the build provider, if available.
	 *
	 * @return the build provider, if available; otherwise, null.
	 */
	default @Nullable BuildProvider getBuildProvider() {
		return null;
	}
}
