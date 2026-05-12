import Foundation
import AppKit

struct ValidationResult {
    let valid: Bool
    let reason: String
}

enum GroqError: LocalizedError {
    case noAPIKey
    case unauthorized
    case rateLimited
    case serverError(Int)
    case networkError(Error)
    case timeout
    case unparseable(String)
    case replayedPhoto

    var errorDescription: String? {
        switch self {
        case .noAPIKey:         return "Configure sua Groq API key em Preferências."
        case .unauthorized:     return "API key inválida. Verifique em Preferências."
        case .rateLimited:      return "Servidor ocupado. Tente novamente em instantes."
        case .serverError(let c): return "Erro no servidor Groq (\(c)). Tente novamente."
        case .networkError:     return "Sem conexão. Verifique o Wi-Fi."
        case .timeout:          return "Groq demorou muito. Tente novamente."
        case .unparseable:      return "Resposta inesperada. Tente outra foto."
        case .replayedPhoto:    return "Essa foto já foi usada recentemente. Tire uma foto nova."
        }
    }
}

actor GroqVisionService {
    static let shared = GroqVisionService()

    private let baseURL = URL(string: "https://api.groq.com/openai/v1/chat/completions")!
    private let session: URLSession = {
        let config = URLSessionConfiguration.default
        config.timeoutIntervalForRequest = 15
        config.timeoutIntervalForResource = 20
        return URLSession(configuration: config)
    }()

    private let systemPrompt = "Você é um validador de hidratação. Responda APENAS JSON válido no formato {\"valid\":bool,\"reason\":string}. Não escreva nada antes ou depois do JSON."

    private let userPrompt = "Analise a imagem. valid=true se: há uma pessoa ou mão segurando ou tocando um recipiente de bebida (copo, caneca, garrafa, squeeze) — pode estar vazio, pode ter qualquer líquido, o que importa é o recipiente estar presente e a pessoa interagindo com ele. valid=false apenas se: não há recipiente visível, a imagem é claramente uma foto de tela ou foto antiga não tirada ao vivo, ou não há nenhuma pessoa/mão na imagem. reason: justifique em 1 frase em português."

    func validate(jpegData: Data, model: String) async throws -> ValidationResult {
        let key = try KeychainService.shared.getGroqKey()
        let resized = try resize(jpegData, maxDim: 1024)
        let b64 = resized.base64EncodedString()

        let body = GroqRequestBody(
            model: model,
            messages: [
                .init(role: "system", content: .text(systemPrompt)),
                .init(role: "user", content: .parts([
                    .init(type: "image_url", imageURL: .init(url: "data:image/jpeg;base64,\(b64)")),
                    .init(type: "text", text: userPrompt)
                ]))
            ],
            temperature: 0.1,
            maxTokens: 150,
            responseFormat: .init(type: "json_object")
        )

        var req = URLRequest(url: baseURL)
        req.httpMethod = "POST"
        req.setValue("Bearer \(key)", forHTTPHeaderField: "Authorization")
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        req.httpBody = try JSONEncoder().encode(body)

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await session.data(for: req)
        } catch let urlError as URLError where urlError.code == .timedOut {
            throw GroqError.timeout
        } catch {
            throw GroqError.networkError(error)
        }

        guard let http = response as? HTTPURLResponse else {
            throw GroqError.unparseable("no http response")
        }
        switch http.statusCode {
        case 200: break
        case 401: throw GroqError.unauthorized
        case 429: throw GroqError.rateLimited
        default:  throw GroqError.serverError(http.statusCode)
        }

        return try parseResponse(data)
    }

    private func parseResponse(_ data: Data) throws -> ValidationResult {
        struct Choice: Decodable {
            struct Message: Decodable { let content: String }
            let message: Message
        }
        struct GroqResponse: Decodable { let choices: [Choice] }
        struct Inner: Decodable { let valid: Bool; let reason: String }

        let outer = try JSONDecoder().decode(GroqResponse.self, from: data)
        guard let content = outer.choices.first?.message.content else {
            throw GroqError.unparseable("no choices")
        }

        let innerData = content.data(using: .utf8) ?? Data()
        if let inner = try? JSONDecoder().decode(Inner.self, from: innerData) {
            return ValidationResult(valid: inner.valid, reason: inner.reason)
        }

        // fallback: find JSON object in text
        if let range = content.range(of: #"\{[^}]+\}"#, options: .regularExpression),
           let extracted = content[range].data(using: .utf8),
           let inner = try? JSONDecoder().decode(Inner.self, from: extracted) {
            return ValidationResult(valid: inner.valid, reason: inner.reason)
        }

        throw GroqError.unparseable(content)
    }

    private func resize(_ data: Data, maxDim: CGFloat) throws -> Data {
        guard let image = NSImage(data: data) else {
            throw GroqError.unparseable("cannot decode image")
        }
        let original = image.size
        let scale = min(maxDim / original.width, maxDim / original.height, 1.0)
        if scale >= 1.0 {
            return data
        }
        let newSize = NSSize(width: original.width * scale, height: original.height * scale)
        let resized = NSImage(size: newSize)
        resized.lockFocus()
        image.draw(in: NSRect(origin: .zero, size: newSize),
                   from: NSRect(origin: .zero, size: original),
                   operation: .copy, fraction: 1.0)
        resized.unlockFocus()

        guard let tiff = resized.tiffRepresentation,
              let bitmap = NSBitmapImageRep(data: tiff),
              let jpeg = bitmap.representation(using: .jpeg, properties: [.compressionFactor: 0.7]) else {
            return data
        }
        return jpeg
    }
}

// MARK: - Request types

private struct GroqRequestBody: Encodable {
    let model: String
    let messages: [Message]
    let temperature: Double
    let maxTokens: Int
    let responseFormat: ResponseFormat

    enum CodingKeys: String, CodingKey {
        case model, messages, temperature
        case maxTokens = "max_tokens"
        case responseFormat = "response_format"
    }

    struct Message: Encodable {
        let role: String
        let content: ContentValue

        enum CodingKeys: String, CodingKey { case role, content }
    }

    enum ContentValue: Encodable {
        case text(String)
        case parts([ContentPart])

        func encode(to encoder: Encoder) throws {
            switch self {
            case .text(let s):
                var c = encoder.singleValueContainer()
                try c.encode(s)
            case .parts(let p):
                var c = encoder.singleValueContainer()
                try c.encode(p)
            }
        }
    }

    struct ContentPart: Encodable {
        let type: String
        var imageURL: ImageURL?
        var text: String?

        enum CodingKeys: String, CodingKey {
            case type
            case imageURL = "image_url"
            case text
        }
    }

    struct ImageURL: Encodable {
        let url: String
    }

    struct ResponseFormat: Encodable {
        let type: String
    }
}
