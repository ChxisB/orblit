/**************************************************************************/
/*  swiftui_view_controller.swift                                         */

import SwiftUI
import UIKit

struct OrblitSwiftUIViewController: UIViewControllerRepresentable {

	func makeUIViewController(context: Context) -> GDTViewController {
		let viewController = GDTViewController()
		GDTAppDelegateService.viewController = viewController
		return viewController
	}

	func updateUIViewController(_ uiViewController: GDTViewController, context: Context) {
		// NOOP
	}

}

struct OrblitWindowScene: Scene {
	var body: some Scene {
		WindowGroup {
			OrblitSwiftUIViewController()
				.ignoresSafeArea()
		}
	}
}
