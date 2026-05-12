import SwiftUI
import AVFoundation

struct LockOverlayView: View {
    @EnvironmentObject var appState: AppState
    @ObservedObject private var camera = CameraService.shared
    let isPrimaryScreen: Bool

    var body: some View {
        ZStack {
            backgroundGradient

            if isPrimaryScreen {
                primaryContent
            } else {
                secondaryContent
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onAppear {
            guard isPrimaryScreen else { return }
            Task { try? await appState.camera.start() }
        }
        .onDisappear {
            guard isPrimaryScreen else { return }
            appState.camera.stop()
        }
    }

    // MARK: - Background

    private var backgroundGradient: some View {
        ZStack {
            Color(red: 0.04, green: 0.06, blue: 0.12).ignoresSafeArea()
            RadialGradient(
                colors: [
                    Color(red: 0.05, green: 0.18, blue: 0.40).opacity(0.6),
                    Color.clear
                ],
                center: .top,
                startRadius: 0,
                endRadius: 600
            )
            .ignoresSafeArea()
        }
    }

    // MARK: - Primary Screen

    private var primaryContent: some View {
        VStack(spacing: 0) {
            Spacer()

            headerSection
                .padding(.bottom, 32)

            if appState.lockState == .validating {
                ValidationView(isValidating: true, errorMessage: nil)
                    .environmentObject(appState)
            } else if case .validationFailed(let msg) = appState.lockState {
                ValidationView(isValidating: false, errorMessage: msg)
                    .environmentObject(appState)
            } else {
                cameraSection
            }

            Spacer()

            EmergencyButton(emergencyDelaySeconds: appState.settings.emergencyDelaySeconds)
                .environmentObject(appState)
                .padding(.bottom, 36)
        }
        .padding(.horizontal, 48)
    }

    private var headerSection: some View {
        VStack(spacing: 12) {
            Image("AppIcon")
                .resizable()
                .interpolation(.high)
                .frame(width: 64, height: 64)
                .shadow(color: Color(red: 0.15, green: 0.60, blue: 1.0).opacity(0.7), radius: 20)

            Text("Hora de beber água!")
                .font(.system(size: 34, weight: .bold, design: .rounded))
                .foregroundStyle(.white)

            Text("Segure seu copo ou garrafa e tire uma foto para desbloquear.")
                .font(.callout)
                .foregroundStyle(.white.opacity(0.55))
                .multilineTextAlignment(.center)
                .frame(maxWidth: 420)
        }
    }

    private var cameraSection: some View {
        GeometryReader { geo in
            let previewW = min(geo.size.width - 48, geo.size.height * 0.72)
            let previewH = previewW * 0.72

            VStack(spacing: 20) {
                ZStack {
                    CameraPreviewView()
                        .frame(width: previewW, height: previewH)
                        .clipShape(RoundedRectangle(cornerRadius: 20))

                    if !camera.isRunning {
                        RoundedRectangle(cornerRadius: 20)
                            .fill(Color.black.opacity(0.75))
                            .frame(width: previewW, height: previewH)
                        VStack(spacing: 16) {
                            if camera.error != nil {
                                Image(systemName: "camera.slash.fill")
                                    .font(.system(size: 44))
                                    .foregroundStyle(.red.opacity(0.8))
                                Text("Câmera bloqueada.")
                                    .font(.callout.bold())
                                    .foregroundStyle(.white.opacity(0.9))
                                Button("Abrir Configurações do Sistema") {
                                    NSWorkspace.shared.open(
                                        URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Camera")!
                                    )
                                }
                                .buttonStyle(.borderedProminent)
                                .controlSize(.small)
                                Button("Tentar novamente") {
                                    Task {
                                        appState.camera.stop()
                                        try? await appState.camera.start()
                                    }
                                }
                                .font(.caption)
                                .foregroundStyle(.white.opacity(0.5))
                                .buttonStyle(.plain)
                            } else {
                                ProgressView()
                                    .controlSize(.large)
                                    .tint(.white)
                                Text("Iniciando câmera…")
                                    .font(.callout)
                                    .foregroundStyle(.white.opacity(0.6))
                            }
                        }
                    }
                }
                .overlay(
                    RoundedRectangle(cornerRadius: 20)
                        .stroke(
                            camera.isRunning
                                ? Color(red: 0.15, green: 0.60, blue: 1.0).opacity(0.5)
                                : Color.white.opacity(0.12),
                            lineWidth: 1.5
                        )
                )
                .shadow(
                    color: camera.isRunning
                        ? Color(red: 0.15, green: 0.60, blue: 1.0).opacity(0.25)
                        : Color.clear,
                    radius: 24
                )
                .animation(.easeInOut(duration: 0.4), value: camera.isRunning)

                captureButton
            }
            .frame(maxWidth: .infinity)
        }
    }

    private var captureButton: some View {
        Button(action: { appState.captureAndValidate() }) {
            HStack(spacing: 10) {
                Image(systemName: camera.isRunning ? "camera.fill" : "camera")
                Text(camera.isRunning ? "Capturar foto" : "Aguardando câmera…")
            }
            .font(.system(size: 16, weight: .semibold))
            .foregroundStyle(camera.isRunning ? .black : .white.opacity(0.4))
            .padding(.horizontal, 44)
            .padding(.vertical, 15)
            .background(
                camera.isRunning
                    ? LinearGradient(
                        colors: [Color(red: 0.55, green: 0.85, blue: 1.0), Color(red: 0.20, green: 0.65, blue: 1.0)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                      )
                    : LinearGradient(
                        colors: [Color.white.opacity(0.08), Color.white.opacity(0.08)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                      )
            )
            .clipShape(Capsule())
            .shadow(
                color: camera.isRunning ? Color(red: 0.20, green: 0.65, blue: 1.0).opacity(0.5) : .clear,
                radius: 12, y: 4
            )
        }
        .buttonStyle(.plain)
        .disabled(!camera.isRunning || appState.lockState == .validating)
        .animation(.easeInOut(duration: 0.35), value: camera.isRunning)
    }

    // MARK: - Secondary Screen

    private var secondaryContent: some View {
        VStack(spacing: 16) {
            Image("AppIcon")
                .resizable()
                .interpolation(.high)
                .frame(width: 56, height: 56)
                .opacity(0.7)

            Text("Vá para o monitor principal")
                .font(.system(size: 26, weight: .semibold, design: .rounded))
                .foregroundStyle(.white)

            Text("Tire a foto no outro monitor para desbloquear.")
                .font(.callout)
                .foregroundStyle(.white.opacity(0.5))
        }
    }
}

// MARK: - Camera Preview

struct CameraPreviewView: NSViewRepresentable {
    @ObservedObject private var camera = CameraService.shared

    func makeNSView(context: Context) -> CameraPreviewNSView {
        CameraPreviewNSView()
    }

    func updateNSView(_ nsView: CameraPreviewNSView, context: Context) {
        nsView.updateSession(camera.previewLayer)
    }
}

final class CameraPreviewNSView: NSView {
    private var previewLayer: AVCaptureVideoPreviewLayer?

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        layer?.backgroundColor = NSColor.black.cgColor
    }

    required init?(coder: NSCoder) { fatalError() }

    func updateSession(_ newLayer: AVCaptureVideoPreviewLayer?) {
        previewLayer?.removeFromSuperlayer()
        guard let newLayer else { return }
        newLayer.frame = bounds
        newLayer.videoGravity = .resizeAspectFill
        layer?.addSublayer(newLayer)
        previewLayer = newLayer
    }

    override func layout() {
        super.layout()
        previewLayer?.frame = bounds
    }
}
