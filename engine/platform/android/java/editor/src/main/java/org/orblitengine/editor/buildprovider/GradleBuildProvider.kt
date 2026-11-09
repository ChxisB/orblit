/**************************************************************************/
/*  GradleBuildProvider.kt                                                */

package org.orblitengine.editor.buildprovider

import android.content.Context
import org.orblitengine.orblit.BuildProvider
import org.orblitengine.orblit.OrblitHost
import org.orblitengine.orblit.variant.Callable

internal class GradleBuildProvider(
	val context: Context,
	val host: OrblitHost,
) : BuildProvider {

	val gradleBuildEnvironmentClient = GradleBuildEnvironmentClient(context)

	val orblit get() = host.orblit

	override fun buildEnvConnect(callback: Callable): Boolean {
		return gradleBuildEnvironmentClient.connect {
			orblit?.runOnRenderThread {
				callback.call()
			}
		}
	}

	override fun buildEnvDisconnect() {
		gradleBuildEnvironmentClient.disconnect()
	}

	override fun buildEnvExecute(
		buildTool: String,
		arguments: Array<String>,
		projectPath: String,
		buildDir: String,
		outputCallback: Callable,
		resultCallback: Callable
	): Int {
		if (buildTool != "gradle") {
			return -1;
		}
		val outputCb: (Int, String) -> Unit = { outputType, line ->
			orblit?.runOnRenderThread {
				outputCallback.call(outputType, line)
			}
		}
		val resultCb: (Int) -> Unit = { exitCode ->
			orblit?.runOnRenderThread {
				resultCallback.call(exitCode)
			}
		}
		return gradleBuildEnvironmentClient.execute(arguments, projectPath, buildDir, outputCb, resultCb)
	}

	override fun buildEnvCancel(jobId: Int) {
		gradleBuildEnvironmentClient.cancel(jobId)
	}

	override fun buildEnvCleanProject(projectPath: String, buildDir: String, callback: Callable) {
		val cb: (Int) -> Unit = { exitCode ->
			orblit?.runOnRenderThread {
				callback.call()
			}
		}
		gradleBuildEnvironmentClient.cleanProject(projectPath, buildDir, cb)
	}
}
