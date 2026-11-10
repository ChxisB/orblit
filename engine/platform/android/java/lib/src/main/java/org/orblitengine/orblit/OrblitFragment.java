/**************************************************************************/
/*  OrblitFragment.java                                                   */

package org.orblitengine.orblit;

import org.orblitengine.orblit.error.Error;
import org.orblitengine.orblit.plugin.OrblitPlugin;
import org.orblitengine.orblit.utils.BenchmarkUtils;

import android.content.Context;
import android.content.Intent;
import android.content.res.Configuration;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;

import androidx.annotation.CallSuper;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;

import java.util.Collections;
import java.util.List;
import java.util.Set;

/**
 * Base fragment for Android apps intending to use Orblit for part of the app's UI.
 */
public class OrblitFragment extends Fragment implements OrblitHost {
	private static final String TAG = OrblitFragment.class.getSimpleName();

	private FrameLayout orblitContainerLayout;

	@Nullable
	private OrblitHost parentHost;
	private Orblit orblit;

	@Override
	public Orblit getOrblit() {
		return orblit;
	}

	@Override
	public void onAttach(@NonNull Context context) {
		super.onAttach(context);
		if (getParentFragment() instanceof OrblitHost) {
			parentHost = (OrblitHost)getParentFragment();
		} else if (getActivity() instanceof OrblitHost) {
			parentHost = (OrblitHost)getActivity();
		}
	}

	@Override
	public void onDetach() {
		if (orblitContainerLayout != null && orblitContainerLayout.getParent() != null) {
			Log.d(TAG, "Cleaning up Orblit container layout during detach.");
			((ViewGroup)orblitContainerLayout.getParent()).removeView(orblitContainerLayout);
		}

		super.onDetach();
		parentHost = null;
	}

	@CallSuper
	@Override
	public void onConfigurationChanged(Configuration newConfig) {
		super.onConfigurationChanged(newConfig);
		orblit.onConfigurationChanged(newConfig);
	}

	@CallSuper
	@Override
	public void onActivityResult(int requestCode, int resultCode, Intent data) {
		super.onActivityResult(requestCode, resultCode, data);
		orblit.onActivityResult(requestCode, resultCode, data);
	}

	@CallSuper
	@Override
	public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
		super.onRequestPermissionsResult(requestCode, permissions, grantResults);
		orblit.onRequestPermissionsResult(requestCode, permissions, grantResults);
	}

	@Override
	public void onCreate(Bundle icicle) {
		BenchmarkUtils.beginBenchmarkMeasure("Startup", "OrblitFragment::onCreate");
		super.onCreate(icicle);

		if (parentHost != null) {
			orblit = parentHost.getOrblit();
		}
		if (orblit == null) {
			orblit = Orblit.getInstance(requireContext());
		}
		performEngineInitialization();
		BenchmarkUtils.endBenchmarkMeasure("Startup", "OrblitFragment::onCreate");
	}

	private void performEngineInitialization() {
		try {
			if (!orblit.initEngine(this, getCommandLine(), getHostPlugins(orblit))) {
				throw new IllegalStateException("Unable to initialize Orblit engine");
			}

			orblitContainerLayout = orblit.onInitRenderView(this);
			if (orblitContainerLayout == null) {
				throw new IllegalStateException("Unable to initialize engine render view");
			}
		} catch (Exception e) {
			Log.e(TAG, "Engine initialization failed", e);
			final String errorMessage = TextUtils.isEmpty(e.getMessage())
					? getString(R.string.error_engine_setup_message)
					: e.getMessage();
			orblit.alert(errorMessage, getString(R.string.text_error_title), orblit::destroyAndKillProcess);
		}
	}

	@Override
	public View onCreateView(@NonNull LayoutInflater inflater, ViewGroup container, Bundle icicle) {
		if (orblitContainerLayout != null && orblitContainerLayout.getParent() != null) {
			Log.w(TAG, "Orblit container layout already has a parent, removing it.");
			((ViewGroup)orblitContainerLayout.getParent()).removeView(orblitContainerLayout);
		}

		return orblitContainerLayout;
	}

	@Override
	public void onDestroy() {
		if (orblitContainerLayout != null && orblitContainerLayout.getParent() != null) {
			Log.w(TAG, "Removing Orblit container layout from parent during destruction.");
			((ViewGroup)orblitContainerLayout.getParent()).removeView(orblitContainerLayout);
		}

		orblit.onDestroy(this);
		super.onDestroy();
	}

	@Override
	public void onPause() {
		super.onPause();
		orblit.onPause(this);
	}

	@Override
	public void onStop() {
		super.onStop();
		orblit.onStop(this);
	}

	@Override
	public void onStart() {
		super.onStart();
		orblit.onStart(this);
	}

	@Override
	public void onResume() {
		super.onResume();
		orblit.onResume(this);
	}

	public void onBackPressed() {
		orblit.onBackPressed();
	}

	@CallSuper
	@Override
	public List<String> getCommandLine() {
		return parentHost != null ? parentHost.getCommandLine() : Collections.emptyList();
	}

	@CallSuper
	@Override
	public void onOrblitSetupCompleted() {
		if (parentHost != null) {
			parentHost.onOrblitSetupCompleted();
		}
	}

	@CallSuper
	@Override
	public void onOrblitMainLoopStarted() {
		if (parentHost != null) {
			parentHost.onOrblitMainLoopStarted();
		}
	}

	@Override
	public void onOrblitForceQuit(Orblit instance) {
		if (parentHost != null) {
			parentHost.onOrblitForceQuit(instance);
		}
	}

	@Override
	public boolean onOrblitForceQuit(int orblitInstanceId) {
		return parentHost != null && parentHost.onOrblitForceQuit(orblitInstanceId);
	}

	@Override
	public void onOrblitRestartRequested(Orblit instance) {
		if (parentHost != null) {
			parentHost.onOrblitRestartRequested(instance);
		}
	}

	@Override
	public int onNewOrblitInstanceRequested(String[] args) {
		if (parentHost != null) {
			return parentHost.onNewOrblitInstanceRequested(args);
		}
		return -1;
	}

	@Override
	@CallSuper
	public Set<OrblitPlugin> getHostPlugins(Orblit engine) {
		if (parentHost != null) {
			return parentHost.getHostPlugins(engine);
		}
		return Collections.emptySet();
	}

	@Override
	public Error signApk(@NonNull String inputPath, @NonNull String outputPath, @NonNull String keystorePath, @NonNull String keystoreUser, @NonNull String keystorePassword) {
		if (parentHost != null) {
			return parentHost.signApk(inputPath, outputPath, keystorePath, keystoreUser, keystorePassword);
		}
		return Error.ERR_UNAVAILABLE;
	}

	@Override
	public Error verifyApk(@NonNull String apkPath) {
		if (parentHost != null) {
			return parentHost.verifyApk(apkPath);
		}
		return Error.ERR_UNAVAILABLE;
	}

	@Override
	public boolean supportsFeature(String featureTag) {
		if (parentHost != null) {
			return parentHost.supportsFeature(featureTag);
		}
		return false;
	}

	@Override
	public void onEditorWorkspaceSelected(String workspace) {
		if (parentHost != null) {
			parentHost.onEditorWorkspaceSelected(workspace);
		}
	}

	@Override
	public void showEditorGameDock() {
		if (parentHost != null) {
			parentHost.showEditorGameDock();
		}
	}

	@Override
	public void onDistractionFreeModeChanged(Boolean enabled) {
		if (parentHost != null) {
			parentHost.onDistractionFreeModeChanged(enabled);
		}
	}

	@Override
	public BuildProvider getBuildProvider() {
		if (parentHost != null) {
			return parentHost.getBuildProvider();
		}
		return null;
	}
}
