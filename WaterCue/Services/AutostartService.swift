import ServiceManagement
import Foundation

final class AutostartService {
    static let shared = AutostartService()

    var isRegistered: Bool {
        SMAppService.mainApp.status == .enabled
    }

    var status: SMAppService.Status {
        SMAppService.mainApp.status
    }

    func register() throws {
        try SMAppService.mainApp.register()
    }

    func unregister() throws {
        try SMAppService.mainApp.unregister()
    }

    func openLoginItemsSettings() {
        SMAppService.openSystemSettingsLoginItems()
    }
}
