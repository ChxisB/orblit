/**************************************************************************/
/*  app_ios.swift                                                         */

import SwiftUI

@main
struct SwiftUIApp: App {
	@UIApplicationDelegateAdaptor(GDTAppDelegateIOS.self) var appDelegate
	@Environment(\.scenePhase) private var scenePhase

	var body: some Scene {
		OrblitWindowScene()
	}
}
