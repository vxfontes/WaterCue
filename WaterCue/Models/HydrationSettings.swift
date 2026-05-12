import Foundation

struct HydrationSettings: Codable, Equatable {
    var intervalMinutes: Int = 60
    var emergencyDelaySeconds: Int = 60
    var warnBeforeLock: Bool = true
    var warningMinutes: Int = 2
    var groqModel: String = "meta-llama/llama-4-scout-17b-16e-instruct"
    var dailyGoal: Int = 8
    var startHour: Int = 7
    var endHour: Int = 22
    var cameraDeviceID: String = ""   // empty = auto

    static let `default` = HydrationSettings()

    private static let key = "watercue_settings"

    static func load() -> HydrationSettings {
        guard let data = UserDefaults.standard.data(forKey: key),
              let decoded = try? JSONDecoder().decode(HydrationSettings.self, from: data) else {
            return .default
        }
        return decoded
    }

    func save() {
        guard let data = try? JSONEncoder().encode(self) else { return }
        UserDefaults.standard.set(data, forKey: HydrationSettings.key)
    }

    var intervalSeconds: TimeInterval {
        TimeInterval(intervalMinutes * 60)
    }

    var warningSeconds: TimeInterval {
        TimeInterval(warningMinutes * 60)
    }
}
