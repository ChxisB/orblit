/**************************************************************************/
/*  RemoteOrblitFragment.kt                                               */

package org.orblitengine.orblit.service

import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.IBinder
import android.os.Message
import android.os.Messenger
import android.os.RemoteException
import android.util.Log
import android.view.LayoutInflater
import android.view.SurfaceControlViewHost
import android.view.SurfaceView
import android.view.View
import android.view.ViewGroup
import androidx.annotation.RequiresApi
import androidx.fragment.app.Fragment
import org.orblitengine.orblit.OrblitHost
import org.orblitengine.orblit.R
import org.orblitengine.orblit.service.OrblitService.EngineStatus.*
import org.orblitengine.orblit.service.OrblitService.EngineError.*
import java.lang.ref.WeakReference

/**
 * Orblit [Fragment] component showcasing how to drive rendering from another process using a [OrblitService] instance.
 */
@RequiresApi(Build.VERSION_CODES.R)
class RemoteOrblitFragment: Fragment() {

	companion object {
		internal val TAG = RemoteOrblitFragment::class.java.simpleName
	}

	/**
	 * Target we publish for receiving messages from the service.
	 */
	private val messengerForReply = Messenger(IncomingHandler(WeakReference<RemoteOrblitFragment>(this)))

	/**
	 * Messenger for sending messages to the [OrblitService] implementation.
	 */
	private var serviceMessenger: Messenger? = null

	private var remoteSurface : SurfaceView? = null

	private var engineInitialized = false
	private var fragmentStarted = false
	private var serviceBound = false
	private var remoteGameArgs = arrayOf<String>()

	private var orblitHost : OrblitHost? = null

	private val serviceConnection = object : ServiceConnection {
		override fun onServiceConnected(name: ComponentName?, service: IBinder?) {
			Log.d(TAG, "Connected to service $name")
			serviceMessenger = Messenger(service)

			// Initialize the Orblit engine
			initOrblitEngine()
		}

		override fun onServiceDisconnected(name: ComponentName?) {
			Log.d(TAG, "Disconnected from service $name")
			serviceMessenger = null
		}
	}

	/**
	 * Handler of incoming messages from [OrblitService] implementations.
	 */
	private class IncomingHandler(private val fragmentRef: WeakReference<RemoteOrblitFragment>) : Handler() {

		override fun handleMessage(msg: Message) {
			val fragment = fragmentRef.get() ?: return

			try {
				Log.d(TAG, "HandleMessage: $msg")

				when (msg.what) {
					OrblitService.MSG_ENGINE_STATUS_UPDATE -> {
						try {
							val engineStatus = OrblitService.EngineStatus.valueOf(
								msg.data.getString(OrblitService.KEY_ENGINE_STATUS, "")
							)
							Log.d(TAG, "Received engine status $engineStatus")

							when (engineStatus) {
								INITIALIZED -> {
									Log.d(TAG, "Engine initialized!")

									try {
										Log.i(TAG, "Creating SurfaceControlViewHost...")
										fragment.remoteSurface?.let {
											fragment.serviceMessenger?.send(Message.obtain().apply {
												what = OrblitService.MSG_WRAP_ENGINE_WITH_SCVH
												data.apply {
													putBinder(OrblitService.KEY_HOST_TOKEN, it.hostToken)
													putInt(OrblitService.KEY_DISPLAY_ID, it.display.displayId)
													putInt(OrblitService.KEY_WIDTH, it.width)
													putInt(OrblitService.KEY_HEIGHT, it.height)
												}
												replyTo = fragment.messengerForReply
											})
										}
									} catch (e: RemoteException) {
										Log.e(TAG, "Unable to set up SurfaceControlViewHost", e)
									}
								}

								STARTED -> {
									Log.d(TAG, "Engine started!")
								}

								STOPPED -> {
									Log.d(TAG, "Engine stopped!")
								}

								DESTROYED -> {
									Log.d(TAG, "Engine destroyed!")
									fragment.engineInitialized = false
								}

								SCVH_CREATED -> {
									Log.d(TAG, "SurfaceControlViewHost created!")

									val surfacePackage = msg.data.getParcelable<SurfaceControlViewHost.SurfacePackage>(
										OrblitService.KEY_SURFACE_PACKAGE)
									if (surfacePackage == null) {
										Log.e(TAG, "Unable to retrieve surface package from OrblitService")
									} else {
										fragment.remoteSurface?.setChildSurfacePackage(surfacePackage)
										fragment.engineInitialized = true
										fragment.startOrblitEngine()
									}
								}
							}
						} catch (e: IllegalArgumentException) {
							Log.e(TAG, "Unable to retrieve engine status update from $msg")
						}
					}

					OrblitService.MSG_ENGINE_ERROR -> {
						try {
							val engineError = OrblitService.EngineError.valueOf(
								msg.data.getString(OrblitService.KEY_ENGINE_ERROR, "")
							)
							Log.d(TAG, "Received engine error $engineError")

							when (engineError) {
								ALREADY_BOUND -> {
									// Engine is already connected to another client, unbind for now
									fragment.stopRemoteGame(false)
								}

								INIT_FAILED -> {
									Log.e(TAG, "Engine initialization failed")
								}

								SCVH_CREATION_FAILED -> {
									Log.e(TAG, "SurfaceControlViewHost creation failed")
								}
							}
						} catch (e: IllegalArgumentException) {
							Log.e(TAG, "Unable to retrieve engine error from message $msg", e)
						}
					}

					OrblitService.MSG_ENGINE_RESTART_REQUESTED -> {
						Log.d(TAG, "Engine restart requested")
						// Validate the engine is actually running
						if (!fragment.serviceBound || !fragment.engineInitialized) {
							return
						}

						// Retrieve the current game args since stopping the engine will clear them out
						val currentArgs = fragment.remoteGameArgs

						// Stop the engine
						fragment.stopRemoteGame()

						// Restart the engine
						fragment.startRemoteGame(currentArgs)
					}

					else -> super.handleMessage(msg)
				}
			} catch (e: RemoteException) {
				Log.e(TAG, "Unable to handle message $msg", e)
			}
		}
	}

	override fun onAttach(context: Context) {
		super.onAttach(context)
		val parentActivity = activity
		if (parentActivity is OrblitHost) {
			orblitHost = parentActivity
		} else {
			val parentFragment = parentFragment
			if (parentFragment is OrblitHost) {
				orblitHost = parentFragment
			}
		}
	}

	override fun onDetach() {
		super.onDetach()
		orblitHost = null
	}

	override fun onCreateView(inflater: LayoutInflater, container: ViewGroup?, bundle: Bundle?): View? {
		return inflater.inflate(R.layout.remote_fragment_layout, container, false)
	}

	override fun onViewCreated(view: View, bundle: Bundle?) {
		super.onViewCreated(view, bundle)
		remoteSurface = view.findViewById(R.id.remote_orblit_window_surface)
		remoteSurface?.setZOrderOnTop(false)

		initOrblitEngine()
	}

	fun startRemoteGame(args: Array<String>) {
		Log.d(TAG, "Starting remote game with args: ${args.contentToString()}")
		remoteSurface?.setZOrderOnTop(true)
		remoteGameArgs = args
		context?.bindService(
			Intent(context, OrblitService::class.java),
			serviceConnection,
			Context.BIND_AUTO_CREATE
		)
		serviceBound = true
	}

	fun stopRemoteGame(destroyEngine: Boolean = true) {
		Log.d(TAG, "Stopping remote game")
		remoteSurface?.setZOrderOnTop(false)
		remoteGameArgs = arrayOf()

		if (serviceBound) {
			if (destroyEngine) {
				serviceMessenger?.send(Message.obtain().apply {
					what = OrblitService.MSG_DESTROY_ENGINE
					replyTo = messengerForReply
				})
			}
			context?.unbindService(serviceConnection)
			serviceBound = false
		}
	}

	private fun initOrblitEngine() {
		if (!serviceBound) {
			return
		}

		try {
			serviceMessenger?.send(Message.obtain().apply {
				what = OrblitService.MSG_INIT_ENGINE
				data.apply {
					putStringArray(OrblitService.KEY_COMMAND_LINE_PARAMETERS, remoteGameArgs)
				}
				replyTo = messengerForReply
			})
		} catch (e: RemoteException) {
			Log.e(TAG, "Unable to initialize Orblit engine", e)
		}
	}

	private fun startOrblitEngine() {
		if (!serviceBound || !engineInitialized || !fragmentStarted) {
			return
		}
		try {
			serviceMessenger?.send(Message.obtain().apply {
				what = OrblitService.MSG_START_ENGINE
				replyTo = messengerForReply
			})
		} catch (e: RemoteException) {
			Log.e(TAG, "Unable to start Orblit engine", e)
		}
	}

	private fun stopOrblitEngine() {
		if (!serviceBound || !engineInitialized || fragmentStarted) {
			return
		}
		try {
			serviceMessenger?.send(Message.obtain().apply {
				what = OrblitService.MSG_STOP_ENGINE
				replyTo = messengerForReply
			})
		} catch (e: RemoteException) {
			Log.e(TAG, "Unable to stop Orblit engine", e)
		}
	}

	override fun onStart() {
		super.onStart()
		fragmentStarted = true
		startOrblitEngine()
	}

	override fun onStop() {
		super.onStop()
		fragmentStarted = false
		stopOrblitEngine()
	}

	override fun onDestroy() {
		stopRemoteGame()
		super.onDestroy()
	}
}
