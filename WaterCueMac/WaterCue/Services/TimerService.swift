import Foundation
import Combine

enum TimerPhase {
    case warning
    case lock
}

@MainActor
final class TimerService: ObservableObject {
    static let shared = TimerService()

    @Published var nextLockDate: Date = Date()
    @Published var timeUntilLock: TimeInterval = 0

    var onPhase: ((TimerPhase) -> Void)?

    private var cancellables = Set<AnyCancellable>()
    private var lockTimer: AnyCancellable?
    private var warnTimer: AnyCancellable?
    private var countdownTimer: AnyCancellable?

    private static let nextLockKey = "watercue_next_lock"

    init() {
        restoreOrSchedule()
    }

    func schedule(settings: HydrationSettings) {
        cancelAll()
        let nextLock = Date().addingTimeInterval(settings.intervalSeconds)
        nextLockDate = nextLock
        UserDefaults.standard.set(nextLock.timeIntervalSince1970, forKey: TimerService.nextLockKey)
        arm(settings: settings, nextLock: nextLock)
        startCountdown()
    }

    func reset(settings: HydrationSettings) {
        schedule(settings: settings)
    }

    func snooze(extraSeconds: TimeInterval, settings: HydrationSettings) {
        cancelAll()
        let newLockDate = nextLockDate.addingTimeInterval(extraSeconds)
        nextLockDate = newLockDate
        UserDefaults.standard.set(newLockDate.timeIntervalSince1970, forKey: TimerService.nextLockKey)
        arm(settings: settings, nextLock: newLockDate)
        startCountdown()
    }

    func cancelAll() {
        lockTimer?.cancel()
        warnTimer?.cancel()
        countdownTimer?.cancel()
        lockTimer = nil
        warnTimer = nil
        countdownTimer = nil
    }

    private func restoreOrSchedule() {
        let stored = UserDefaults.standard.double(forKey: TimerService.nextLockKey)
        if stored > 0 {
            let restoredDate = Date(timeIntervalSince1970: stored)
            if restoredDate > Date() {
                nextLockDate = restoredDate
                let settings = HydrationSettings.load()
                arm(settings: settings, nextLock: restoredDate)
                startCountdown()
                return
            }
        }
        // No stored date or it's past — schedule fresh
        let settings = HydrationSettings.load()
        schedule(settings: settings)
    }

    private func arm(settings: HydrationSettings, nextLock: Date) {
        let now = Date()
        let lockDelay = nextLock.timeIntervalSince(now)

        if lockDelay <= 0 {
            onPhase?(.lock)
            return
        }

        // Warning timer (fires warningSeconds before lock)
        if settings.warnBeforeLock {
            let warnDelay = lockDelay - settings.warningSeconds
            if warnDelay > 0 {
                warnTimer = Just(())
                    .delay(for: .seconds(warnDelay), scheduler: RunLoop.main)
                    .sink { [weak self] in
                        guard let self else { return }
                        self.onPhase?(.warning)
                    }
            }
        }

        // Lock timer
        lockTimer = Just(())
            .delay(for: .seconds(lockDelay), scheduler: RunLoop.main)
            .sink { [weak self] in
                guard let self else { return }
                self.onPhase?(.lock)
            }
    }

    private func startCountdown() {
        countdownTimer = Timer.publish(every: 1, on: .main, in: .common)
            .autoconnect()
            .sink { [weak self] _ in
                guard let self else { return }
                self.timeUntilLock = max(0, self.nextLockDate.timeIntervalSince(Date()))
            }
    }

    var timeUntilLockFormatted: String {
        let t = Int(timeUntilLock)
        if t <= 0 { return "Agora" }
        let h = t / 3600
        let m = (t % 3600) / 60
        let s = t % 60
        if h > 0 { return String(format: "%dh %02dm", h, m) }
        if m > 0 { return String(format: "%dm %02ds", m, s) }
        return String(format: "%ds", s)
    }
}
