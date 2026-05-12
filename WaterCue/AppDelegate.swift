import AppKit
import SwiftUI

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private var popover: NSPopover?
    private var onboardingWindow: NSWindow?
    private var settingsWindow: NSWindow?
    private var statsWindow: NSWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        setupMenuBar()
        setupWindowNotifications()

        if !AppState.shared.isOnboardingComplete {
            showOnboarding()
        }
    }

    private func setupWindowNotifications() {
        NotificationCenter.default.addObserver(forName: .openSettings, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor [weak self] in self?.showSettings() }
        }
        NotificationCenter.default.addObserver(forName: .openStats, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor [weak self] in self?.showStats() }
        }
    }

    private func showSettings() {
        popover?.performClose(nil)
        if let w = settingsWindow, w.isVisible { w.makeKeyAndOrderFront(nil); return }
        let w = makeWindow(size: NSSize(width: 460, height: 580), title: "Preferências — WaterCue") {
            SettingsView().environmentObject(AppState.shared)
        }
        settingsWindow = w
        presentWindow(w)
    }

    private func showStats() {
        popover?.performClose(nil)
        if let w = statsWindow, w.isVisible { w.makeKeyAndOrderFront(nil); return }
        let w = makeWindow(size: NSSize(width: 380, height: 460), title: "Estatísticas — WaterCue") {
            StatsView().environmentObject(AppState.shared).padding()
        }
        statsWindow = w
        presentWindow(w)
    }

    private func makeWindow<V: View>(size: NSSize, title: String, @ViewBuilder content: () -> V) -> NSWindow {
        let w = NSWindow(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.titled, .closable, .resizable],
            backing: .buffered, defer: false
        )
        w.title = title
        w.center()
        w.isReleasedWhenClosed = false
        w.contentView = NSHostingView(rootView: content())
        NotificationCenter.default.addObserver(forName: NSWindow.willCloseNotification, object: w, queue: .main) { [weak self] _ in
            self?.updateActivationPolicy()
        }
        return w
    }

    private func presentWindow(_ w: NSWindow) {
        NSApp.setActivationPolicy(.regular)
        w.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    private func updateActivationPolicy() {
        let hasVisibleWindow = [settingsWindow, statsWindow, onboardingWindow].compactMap { $0 }.contains { $0.isVisible }
        if !hasVisibleWindow && AppState.shared.lockState == .idle {
            NSApp.setActivationPolicy(.accessory)
        }
    }

    private func setupMenuBar() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let button = item.button {
            button.image = NSImage(systemSymbolName: "drop.fill", accessibilityDescription: "WaterCue")
            button.image?.isTemplate = true
            button.action = #selector(togglePopover)
            button.target = self
        }
        statusItem = item

        let pop = NSPopover()
        pop.contentSize = NSSize(width: 280, height: 320)
        pop.behavior = .transient
        pop.contentViewController = NSHostingController(
            rootView: MenuBarView().environmentObject(AppState.shared)
        )
        popover = pop
    }

    @objc private func togglePopover() {
        guard let button = statusItem?.button, let pop = popover else { return }
        if pop.isShown {
            pop.performClose(nil)
        } else {
            AppState.shared.refreshStats()
            pop.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        }
    }

    private func showOnboarding() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 520, height: 480),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        window.title = "WaterCue — Configuração"
        window.center()
        window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(
            rootView: OnboardingView().environmentObject(AppState.shared)
        )
        NSApp.setActivationPolicy(.regular)
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        onboardingWindow = window

        NotificationCenter.default.addObserver(
            forName: .onboardingCompleted,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.onboardingWindow?.orderOut(nil)
                self?.updateActivationPolicy()
            }
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }
}

extension Notification.Name {
    static let onboardingCompleted = Notification.Name("WaterCueOnboardingCompleted")
}
