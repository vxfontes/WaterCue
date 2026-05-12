import AppKit
import SwiftUI

@MainActor
final class LockService: ObservableObject {
    static let shared = LockService()

    @Published var isLocked = false

    private var overlayWindows: [NSWindow] = []
    private var eventMonitors: [Any] = []
    private var reactivationTimer: Timer?

    func engage(appState: AppState) {
        guard !isLocked else { return }
        isLocked = true

        for screen in NSScreen.screens {
            let window = buildWindow(for: screen, appState: appState, isPrimary: screen == NSScreen.main)
            overlayWindows.append(window)
            window.makeKeyAndOrderFront(nil)
        }

        NSApp.activate(ignoringOtherApps: true)
        installEventMonitors()
        startReactivation()
        observeScreenChanges(appState: appState)
    }

    func release() {
        guard isLocked else { return }
        isLocked = false

        stopReactivation()
        removeEventMonitors()
        NotificationCenter.default.removeObserver(self, name: NSApplication.didChangeScreenParametersNotification, object: nil)

        overlayWindows.forEach { $0.orderOut(nil) }
        overlayWindows.removeAll()
    }

    private func buildWindow(for screen: NSScreen, appState: AppState, isPrimary: Bool) -> NSWindow {
        let window = NSWindow(
            contentRect: screen.frame,
            styleMask: [.borderless],
            backing: .buffered,
            defer: false
        )
        window.isOpaque = true
        window.backgroundColor = .black
        window.level = .screenSaver
        window.collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle, .fullScreenAuxiliary]
        window.hidesOnDeactivate = false
        window.canHide = false
        window.isMovable = false
        window.ignoresMouseEvents = false

        let content = LockOverlayView(isPrimaryScreen: isPrimary)
        window.contentView = NSHostingView(rootView: content.environmentObject(appState))
        return window
    }

    private func installEventMonitors() {
        let consumedKeys: Set<String> = ["q", "w", "h", "m"]
        let localMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            if event.modifierFlags.contains(.command) {
                let key = event.charactersIgnoringModifiers ?? ""
                if consumedKeys.contains(key) { return nil }
                // cmd-tab (keyCode 48)
                if event.keyCode == 48 { return nil }
            }
            return event
        }
        if let m = localMonitor { eventMonitors.append(m) }
    }

    private func removeEventMonitors() {
        eventMonitors.forEach { NSEvent.removeMonitor($0) }
        eventMonitors.removeAll()
    }

    private func startReactivation() {
        reactivationTimer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                guard let self, self.isLocked else { return }
                if !NSApp.isActive {
                    NSApp.activate(ignoringOtherApps: true)
                }
                self.overlayWindows.forEach { $0.makeKeyAndOrderFront(nil) }
            }
        }
    }

    private func stopReactivation() {
        reactivationTimer?.invalidate()
        reactivationTimer = nil
    }

    private func observeScreenChanges(appState: AppState) {
        NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor [weak self] in
                guard let self, self.isLocked else { return }
                self.overlayWindows.forEach { $0.orderOut(nil) }
                self.overlayWindows.removeAll()
                for screen in NSScreen.screens {
                    let w = self.buildWindow(for: screen, appState: appState, isPrimary: screen == NSScreen.main)
                    self.overlayWindows.append(w)
                    w.makeKeyAndOrderFront(nil)
                }
            }
        }
    }
}
