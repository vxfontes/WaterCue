import Foundation

final class KeychainService {
    static let shared = KeychainService()

    private let key = "watercue_groq_api_key"

    func getGroqKey() throws -> String {
        guard let k = UserDefaults.standard.string(forKey: key), !k.isEmpty else {
            throw KeychainError.noKey
        }
        return k
    }

    func setGroqKey(_ value: String) throws {
        UserDefaults.standard.set(value, forKey: key)
    }

    func deleteGroqKey() throws {
        UserDefaults.standard.removeObject(forKey: key)
    }

    var hasGroqKey: Bool {
        let v = UserDefaults.standard.string(forKey: key) ?? ""
        return !v.isEmpty
    }

    enum KeychainError: LocalizedError {
        case noKey
        var errorDescription: String? {
            "Groq API key não encontrada. Configure em Preferências."
        }
    }
}
