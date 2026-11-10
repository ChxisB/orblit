/**************************************************************************/
/*  BuildProvider.java                                                    */

package org.orblitengine.orblit;

import org.orblitengine.orblit.variant.Callable;

import androidx.annotation.NonNull;

/**
 * Provides an environment for executing build commands.
 */
public interface BuildProvider {
	/**
	 * Connects to the build environment.
	 *
	 * @param callback The callback to call when connected
	 * @return Whether or not connecting is possible
	 */
	boolean buildEnvConnect(@NonNull Callable callback);

	/**
	 * Disconnects from the build environment.
	 */
	void buildEnvDisconnect();

	/**
	 * Executes a command via the build environment.
	 *
	 * @param buildTool      The build tool to execute (for example, "gradle")
	 * @param arguments      The argument for the command
	 * @param projectPath    The working directory to use when executing the command
	 * @param buildDir       The build directory within the project
	 * @param outputCallback The callback to call for each line of output from the command
	 * @param resultCallback The callback to call when the command is finished running
	 * @return A positive job id, if successful; otherwise, a negative number
	 */
	int buildEnvExecute(String buildTool, @NonNull String[] arguments, @NonNull String projectPath, @NonNull String buildDir, @NonNull Callable outputCallback, @NonNull Callable resultCallback);

	/**
	 * Cancels a command executed via the build environment.
	 *
	 * @param jobId The job id returned from buildEnvExecute()
	 */
	void buildEnvCancel(int jobId);

	/**
	 * Requests that a project be cleaned up via the build environment.
	 *
	 * @param projectPath The working directory to use when executing the command
	 * @param buildDir    The build directory within the project
	 */
	void buildEnvCleanProject(@NonNull String projectPath, @NonNull String buildDir, @NonNull Callable callback);
}
