import Foundation
import Combine
import AppKit
import SwiftUI

enum LockState: Equatable {
    case idle
    case warning
    case locked
    case validating
    case validationFailed(String)
}

@MainActor
final class AppState: ObservableObject {
    static let shared = AppState()

    @Published var settings: HydrationSettings = HydrationSettings.load()
    @Published var lockState: LockState = .idle
    @Published var cupsToday: Int = 0
    @Published var currentStreak: Int = 0
    @Published var isOnboardingComplete: Bool

    let timer = TimerService.shared
    let lock = LockService.shared
    let camera = CameraService.shared
    let notifications = NotificationService.shared
    let db = DatabaseService.shared
    let autostart = AutostartService.shared

    private var cancellables = Set<AnyCancellable>()
    private var warningWindow: NSWindow?
    private var snoozeWorkItem: DispatchWorkItem?

    init() {
        isOnboardingComplete = UserDefaults.standard.bool(forKey: "watercue_onboarding_done")
        setupTimerCallbacks()
        setupNotificationCallbacks()
        refreshStats()
        observeSleepWake()
    }

    private func setupTimerCallbacks() {
        timer.onPhase = { [weak self] (phase: TimerPhase) in
            Task { @MainActor [weak self] in
                guard let self else { return }
                switch phase {
                case .warning:
                    self.lockState = .warning
                    self.notifications.scheduleWarning(in: self.settings.warningSeconds)
                    self.showWarningModal()
                case .lock:
                    self.notifications.cancelWarning()
                    self.engageLock()
                }
            }
        }
    }

    private func setupNotificationCallbacks() {
        notifications.onDrinkNow = { [weak self] in
            Task { @MainActor [weak self] in
                self?.openOverlayImmediately()
            }
        }
    }

    func openOverlayImmediately() {
        notifications.cancelWarning()
        timer.cancelAll()
        engageLock()
    }

    // MARK: - Warning modal

    func showWarningModal() {
        snoozeWorkItem?.cancel()
        snoozeWorkItem = nil
        warningWindow?.close()

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 340, height: 260),
            styleMask: [.titled, .closable, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        window.titlebarAppearsTransparent = true
        window.title = ""
        window.isMovableByWindowBackground = true
        window.center()
        window.isReleasedWhenClosed = false
        window.level = .floating
        window.contentView = NSHostingView(rootView: WarningView().environmentObject(self))
        NSApp.setActivationPolicy(.regular)
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        warningWindow = window
    }

    func dismissWarningModal() {
        snoozeWorkItem?.cancel()
        snoozeWorkItem = nil
        warningWindow?.close()
        warningWindow = nil
    }

    func drinkNowFromWarning() {
        dismissWarningModal()
        notifications.cancelWarning()
        timer.cancelAll()
        engageLock()
    }

    func snoozeWarning() {
        dismissWarningModal()
        // push lock forward 5 min + warning window so warning re-fires in ~5 min
        let snoozeSecs: TimeInterval = 5 * 60 + settings.warningSeconds
        timer.snooze(extraSeconds: snoozeSecs, settings: settings)
        lockState = .idle

        // show modal again in exactly 5 min regardless of warning timing
        let work = DispatchWorkItem { [weak self] in
            guard let self, self.lockState != .locked else { return }
            self.lockState = .warning
            self.showWarningModal()
        }
        snoozeWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 300, execute: work)
    }

    private func engageLock() {
        dismissWarningModal()
        lockState = .locked
        NSApp.setActivationPolicy(.regular)
        lock.engage(appState: self)
    }

    func captureAndValidate() {
        lockState = .validating
        Task {
            do {
                let jpeg = try await camera.capturePhoto()
                let hash = DatabaseService.photoHash(from: jpeg)

                if db.isReplayedPhoto(hash: hash) {
                    lockState = .validationFailed("Foto já usada recentemente. Tire uma nova.")
                    return
                }

                let result = try await GroqVisionService.shared.validate(jpegData: jpeg, model: settings.groqModel)

                if result.valid {
                    unlockAfterValidation(hash: hash, result: result)
                } else {
                    lockState = .validationFailed(result.reason)
                }
            } catch {
                lockState = .validationFailed(error.localizedDescription)
            }
        }
    }

    private func unlockAfterValidation(hash: String, result: ValidationResult) {
        let event = HydrationEvent(
            validatedByAI: true,
            groqModel: settings.groqModel,
            emergencyBypass: false,
            photoHash: hash,
            reason: result.reason
        )
        db.insert(event)
        unlock()
        notifications.sendUnlockCelebration()
        refreshStats()
    }

    func emergencyUnlock() {
        let event = HydrationEvent(validatedByAI: false, emergencyBypass: true)
        db.insert(event)
        unlock()
    }

    private func unlock() {
        lock.release()
        lockState = .idle
        NSApp.setActivationPolicy(.accessory)
        timer.schedule(settings: settings)
    }

    func retryCapture() {
        lockState = .locked
    }

    func saveSettings(_ newSettings: HydrationSettings) {
        settings = newSettings
        newSettings.save()
        if lockState == .idle {
            timer.schedule(settings: newSettings)
        }
    }

    func completeOnboarding() {
        isOnboardingComplete = true
        UserDefaults.standard.set(true, forKey: "watercue_onboarding_done")
        NotificationCenter.default.post(name: .onboardingCompleted, object: nil)
        timer.schedule(settings: settings)
    }

    func refreshStats() {
        cupsToday = db.cupsToday()
        currentStreak = db.currentStreak(dailyGoal: settings.dailyGoal)
    }

    private func observeSleepWake() {
        NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didWakeNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor [weak self] in
                guard let self else { return }
                if self.lockState == .locked {
                    try? await self.camera.start()
                }
            }
        }
    }
}
