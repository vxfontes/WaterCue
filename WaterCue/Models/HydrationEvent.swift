import Foundation

struct HydrationEvent {
    var id: Int64?
    var timestamp: Date
    var validatedByAI: Bool
    var groqModel: String?
    var emergencyBypass: Bool
    var photoHash: String?
    var reason: String?

    init(timestamp: Date = Date(),
         validatedByAI: Bool,
         groqModel: String? = nil,
         emergencyBypass: Bool = false,
         photoHash: String? = nil,
         reason: String? = nil) {
        self.timestamp = timestamp
        self.validatedByAI = validatedByAI
        self.groqModel = groqModel
        self.emergencyBypass = emergencyBypass
        self.photoHash = photoHash
        self.reason = reason
    }
}

struct DailyCount {
    let day: String
    let count: Int
}
