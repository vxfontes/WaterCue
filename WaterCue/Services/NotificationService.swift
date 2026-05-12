import UserNotifications
import Foundation

@MainActor
final class NotificationService: NSObject, ObservableObject {
    static let shared = NotificationService()

    private let center = UNUserNotificationCenter.current()
    var onDrinkNow: (() -> Void)?

    override init() {
        super.init()
        center.delegate = self
        setupCategories()
    }

    func requestPermission() async -> Bool {
        do {
            return try await center.requestAuthorization(options: [.alert, .sound, .badge])
        } catch {
            return false
        }
    }

    var isAuthorized: Bool {
        get async {
            let settings = await center.notificationSettings()
            return settings.authorizationStatus == .authorized
        }
    }

    private func setupCategories() {
        let drinkNow = UNNotificationAction(
            identifier: "DRINK_NOW",
            title: "Já vou beber",
            options: [.foreground]
        )
        let category = UNNotificationCategory(
            identifier: "PRE_LOCK",
            actions: [drinkNow],
            intentIdentifiers: [],
            options: []
        )
        center.setNotificationCategories([category])
    }

    func scheduleWarning(in seconds: TimeInterval) {
        center.removePendingNotificationRequests(withIdentifiers: ["watercue-warning"])

        let content = UNMutableNotificationContent()
        content.title = "WaterCue"
        content.body = "Beba água agora — tela trava em \(Int(seconds / 60)) minuto(s)!"
        content.sound = .default
        content.categoryIdentifier = "PRE_LOCK"

        let trigger = UNTimeIntervalNotificationTrigger(timeInterval: max(1, seconds), repeats: false)
        let request = UNNotificationRequest(identifier: "watercue-warning", content: content, trigger: trigger)
        center.add(request)
    }

    func cancelWarning() {
        center.removePendingNotificationRequests(withIdentifiers: ["watercue-warning"])
    }

    func sendUnlockCelebration() {
        let content = UNMutableNotificationContent()
        content.title = "WaterCue"
        content.body = "Boa! Hidratação validada. Até a próxima."
        content.sound = .default

        let trigger = UNTimeIntervalNotificationTrigger(timeInterval: 0.1, repeats: false)
        let request = UNNotificationRequest(identifier: "watercue-unlock-\(Date().timeIntervalSince1970)",
                                            content: content, trigger: trigger)
        center.add(request)
    }
}

extension NotificationService: UNUserNotificationCenterDelegate {
    nonisolated func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        didReceive response: UNNotificationResponse,
        withCompletionHandler completionHandler: @escaping () -> Void
    ) {
        if response.actionIdentifier == "DRINK_NOW" {
            Task { @MainActor in
                self.onDrinkNow?()
            }
        }
        completionHandler()
    }

    nonisolated func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        completionHandler([.banner, .sound])
    }
}
