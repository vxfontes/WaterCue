import AVFoundation
import AppKit
import Combine

@MainActor
final class CameraService: NSObject, ObservableObject {
    static let shared = CameraService()

    @Published var isRunning = false
    @Published var error: String?
    @Published var capturedImage: NSImage?

    private let sessionQueue = DispatchQueue(label: "com.watercue.camera.session", qos: .userInitiated)
    private var isStarting = false
    private var session: AVCaptureSession?
    private var photoOutput: AVCapturePhotoOutput?
    private var continuation: CheckedContinuation<Data, Error>?

    @Published private(set) var previewLayer: AVCaptureVideoPreviewLayer?

    func start() async throws {
        if isRunning || isStarting {
            return
        }

        error = nil
        capturedImage = nil

        let status = AVCaptureDevice.authorizationStatus(for: .video)
        if status == .denied || status == .restricted {
            let msg = "Câmera bloqueada. Autorize em Configurações do Sistema."
            error = msg
            throw CameraError.permissionDenied
        }
        if status == .notDetermined {
            let granted = await AVCaptureDevice.requestAccess(for: .video)
            if !granted {
                let msg = "Câmera bloqueada. Autorize em Configurações do Sistema."
                error = msg
                throw CameraError.permissionDenied
            }
        }

        let newSession = AVCaptureSession()
        newSession.sessionPreset = .photo

        let preferredID = HydrationSettings.load().cameraDeviceID
        let device = CameraService.availableCameras().first(where: { $0.uniqueID == preferredID })
            ?? AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .front)
            ?? AVCaptureDevice.default(for: .video)
        guard let device else {
            error = "Nenhuma câmera encontrada."
            throw CameraError.noDevice
        }

        let input = try AVCaptureDeviceInput(device: device)
        guard newSession.canAddInput(input) else {
            error = "Não conseguiu adicionar input de câmera."
            throw CameraError.configFailed
        }
        newSession.addInput(input)

        let output = AVCapturePhotoOutput()
        guard newSession.canAddOutput(output) else {
            error = "Não conseguiu adicionar output de câmera."
            throw CameraError.configFailed
        }
        newSession.addOutput(output)

        session = newSession
        photoOutput = output

        let layer = AVCaptureVideoPreviewLayer(session: newSession)
        previewLayer = layer

        isStarting = true
        isRunning = false

        sessionQueue.async { [weak self, newSession] in
            newSession.startRunning()
            Task { @MainActor [weak self] in
                guard let self, self.session === newSession else { return }
                self.isStarting = false
                self.isRunning = newSession.isRunning
                if !newSession.isRunning && self.error == nil {
                    self.error = "Falha ao iniciar câmera."
                }
            }
        }
    }

    func stop() {
        let s = session
        sessionQueue.async { s?.stopRunning() }
        isStarting = false
        session = nil
        photoOutput = nil
        previewLayer = nil
        isRunning = false
        capturedImage = nil
    }

    func capturePhoto() async throws -> Data {
        guard let output = photoOutput else {
            throw CameraError.notRunning
        }
        return try await withCheckedThrowingContinuation { cont in
            self.continuation = cont
            let settings = AVCapturePhotoSettings()
            settings.flashMode = .off
            output.capturePhoto(with: settings, delegate: self)
        }
    }

    static func availableCameras() -> [AVCaptureDevice] {
        AVCaptureDevice.DiscoverySession(
            deviceTypes: [.builtInWideAngleCamera, .external, .continuityCamera],
            mediaType: .video,
            position: .unspecified
        ).devices
    }

    enum CameraError: LocalizedError {
        case permissionDenied, noDevice, configFailed, notRunning, captureFailed

        var errorDescription: String? {
            switch self {
            case .permissionDenied: return "Câmera bloqueada. Autorize em Configurações do Sistema."
            case .noDevice:         return "Nenhuma câmera encontrada."
            case .configFailed:     return "Falha ao configurar câmera."
            case .notRunning:       return "Câmera não está ativa."
            case .captureFailed:    return "Falha ao capturar foto."
            }
        }
    }
}

extension CameraService: AVCapturePhotoCaptureDelegate {
    nonisolated func photoOutput(_ output: AVCapturePhotoOutput,
                                 didFinishProcessingPhoto photo: AVCapturePhoto,
                                 error: Error?) {
        Task { @MainActor in
            if let error {
                self.continuation?.resume(throwing: error)
                self.continuation = nil
                return
            }
            guard let data = photo.fileDataRepresentation() else {
                self.continuation?.resume(throwing: CameraError.captureFailed)
                self.continuation = nil
                return
            }
            if let image = NSImage(data: data) {
                self.capturedImage = image
            }
            self.continuation?.resume(returning: data)
            self.continuation = nil
        }
    }
}
