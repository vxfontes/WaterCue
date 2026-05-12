import SwiftUI
import AVFoundation
import UserNotifications

struct OnboardingView: View {
    @EnvironmentObject var appState: AppState
    @State private var step = 0
    @State private var cameraStatus: AVAuthorizationStatus = .notDetermined
    @State private var notifStatus: UNAuthorizationStatus = .notDetermined
    @State private var apiKey = ""
    @State private var apiKeyValid = false
    @State private var apiKeyError: String?
    @State private var isRequestingCamera = false
    @State private var isRequestingNotif = false
    @State private var availableCameras: [AVCaptureDevice] = []

    private var cameraGranted: Bool { cameraStatus == .authorized }
    private var notifGranted: Bool { notifStatus == .authorized || notifStatus == .provisional }

    var body: some View {
        ZStack {
            Color(nsColor: .windowBackgroundColor).ignoresSafeArea()

            VStack(spacing: 0) {
                progressIndicator

                Group {
                    switch step {
                    case 0: welcomeStep
                    case 1: cameraStep
                    case 2: cameraSelectorStep
                    case 3: notificationStep
                    case 4: apiKeyStep
                    case 5: intervalStep
                    default: doneStep
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .animation(.easeInOut(duration: 0.25), value: step)
            }
        }
        .frame(width: 540, height: 500)
        .onAppear { refreshPermissionStatuses() }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            refreshPermissionStatuses()
        }
        .onChange(of: notifGranted) { _, granted in
            if granted && step == 3 {
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { step += 1 }
            }
        }
        .onChange(of: cameraGranted) { _, granted in
            if granted && step == 1 {
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { step += 1 }
            }
        }
    }

    // MARK: - Progress

    private var progressIndicator: some View {
        HStack(spacing: 8) {
            ForEach(0..<7) { i in
                Capsule()
                    .fill(i <= step ? Color.accentColor : Color.secondary.opacity(0.25))
                    .frame(width: i == step ? 28 : 8, height: 6)
                    .animation(.spring(response: 0.35), value: step)
            }
        }
        .padding(.top, 24)
        .padding(.bottom, 4)
    }

    // MARK: - Steps

    private var welcomeStep: some View {
        stepWrapper {
            VStack(spacing: 20) {
                Image("AppIcon")
                    .resizable()
                    .interpolation(.high)
                    .frame(width: 80, height: 80)

                Text("Bem-vinda ao WaterCue!")
                    .font(.largeTitle.bold())

                Text("Vou travar seu Mac periodicamente até você beber água e comprovar com uma foto. Chega de 'vou agora' que nunca vai.")
                    .font(.body)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: 400)

                nextButton("Começar →")
            }
        }
    }

    private var cameraStep: some View {
        stepWrapper {
            VStack(spacing: 20) {
                Image(systemName: cameraGranted ? "camera.fill" : "camera")
                    .font(.system(size: 56))
                    .foregroundStyle(cameraGranted ? .green : .blue)
                    .symbolEffect(.bounce, value: cameraGranted)

                Text("Permissão de câmera")
                    .font(.title.bold())

                Text("Precisamos da câmera para validar a foto do seu copo de água antes de desbloquear o Mac.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: 400)

                if cameraStatus == .denied || cameraStatus == .restricted {
                    VStack(spacing: 12) {
                        Label("Câmera bloqueada", systemImage: "xmark.circle.fill")
                            .foregroundStyle(.red)
                        Text("Autorize em Ajustes do Sistema → Privacidade → Câmera")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                            .multilineTextAlignment(.center)
                        Button("Abrir Ajustes do Sistema") {
                            NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Camera")!)
                        }
                        .buttonStyle(.bordered)
                    }
                } else if cameraGranted {
                    Label("Câmera autorizada!", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                        .font(.headline)
                    nextButton("Continuar →")
                } else {
                    Button(isRequestingCamera ? "Aguardando..." : "Autorizar câmera") {
                        requestCamera()
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(isRequestingCamera)
                    .controlSize(.large)
                }
            }
        }
    }

    private var cameraSelectorStep: some View {
        stepWrapper {
            VStack(spacing: 20) {
                Image(systemName: "camera.viewfinder")
                    .font(.system(size: 56))
                    .foregroundStyle(.blue)

                Text("Qual câmera usar?")
                    .font(.title.bold())

                Text("Escolha a câmera que vai usar para tirar a foto de hidratação.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: 400)

                VStack(spacing: 8) {
                    if availableCameras.isEmpty {
                        Label("Nenhuma câmera detectada", systemImage: "camera.slash")
                            .foregroundStyle(.secondary)
                    } else {
                        // "Auto" row
                        Button {
                            var s = appState.settings
                            s.cameraDeviceID = ""
                            appState.saveSettings(s)
                        } label: {
                            HStack(spacing: 12) {
                                Image(systemName: appState.settings.cameraDeviceID.isEmpty ? "checkmark.circle.fill" : "circle")
                                    .foregroundStyle(appState.settings.cameraDeviceID.isEmpty ? .blue : .secondary)
                                    .font(.system(size: 20))
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("Automático")
                                        .fontWeight(appState.settings.cameraDeviceID.isEmpty ? .semibold : .regular)
                                    Text("Usa a câmera padrão do sistema")
                                        .font(.caption)
                                        .foregroundStyle(.secondary)
                                }
                                Spacer()
                            }
                            .padding(.vertical, 6)
                            .contentShape(Rectangle())
                        }
                        .buttonStyle(.plain)

                        Divider()

                        ForEach(availableCameras, id: \.uniqueID) { cam in
                            cameraRow(cam)
                        }
                    }
                }
                .padding()
                .background(Color.secondary.opacity(0.08))
                .clipShape(RoundedRectangle(cornerRadius: 12))
                .frame(maxWidth: 380)

                nextButton("Continuar →")
            }
        }
        .onAppear {
            availableCameras = CameraService.availableCameras()
            // auto-select first camera if nothing chosen yet
            if appState.settings.cameraDeviceID.isEmpty, let first = availableCameras.first {
                var s = appState.settings
                s.cameraDeviceID = first.uniqueID
                appState.saveSettings(s)
            }
        }
    }

    private func cameraRow(_ cam: AVCaptureDevice) -> some View {
        let isSelected = appState.settings.cameraDeviceID == cam.uniqueID
        return Button {
            var s = appState.settings
            s.cameraDeviceID = cam.uniqueID
            appState.saveSettings(s)
        } label: {
            HStack(spacing: 12) {
                Image(systemName: isSelected ? "checkmark.circle.fill" : "circle")
                    .foregroundStyle(isSelected ? .blue : .secondary)
                    .font(.system(size: 20))
                VStack(alignment: .leading, spacing: 2) {
                    Text(cam.localizedName)
                        .fontWeight(isSelected ? .semibold : .regular)
                    Text(cameraSubtitle(cam))
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
            }
            .padding(.vertical, 6)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    private var notificationStep: some View {
        stepWrapper {
            VStack(spacing: 20) {
                Image(systemName: notifGranted ? "bell.badge.fill" : "bell.fill")
                    .font(.system(size: 56))
                    .foregroundStyle(notifGranted ? .green : .orange)
                    .symbolEffect(.bounce, value: notifGranted)

                Text("Notificações")
                    .font(.title.bold())

                Text("Vou te avisar alguns minutos antes de travar. Assim você pode beber antes e o Mac nem chega a travar.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: 400)

                if notifStatus == .denied {
                    VStack(spacing: 12) {
                        Label("Notificações bloqueadas", systemImage: "xmark.circle.fill")
                            .foregroundStyle(.red)
                        Text("Autorize em Ajustes do Sistema → Notificações → WaterCue")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                            .multilineTextAlignment(.center)
                        Button("Abrir Ajustes do Sistema") {
                            NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.notifications")!)
                        }
                        .buttonStyle(.bordered)
                        skipButton
                    }
                } else if notifGranted {
                    Label("Notificações ativadas!", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                        .font(.headline)
                    nextButton("Continuar →")
                } else {
                    VStack(spacing: 12) {
                        Button(isRequestingNotif ? "Aguardando..." : "Ativar notificações") {
                            requestNotifications()
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(isRequestingNotif)
                        .controlSize(.large)

                        skipButton
                    }
                }
            }
        }
    }

    private var apiKeyStep: some View {
        stepWrapper {
            VStack(spacing: 20) {
                Image(systemName: "cpu.fill")
                    .font(.system(size: 56))
                    .foregroundStyle(.purple)

                Text("Chave Groq (IA)")
                    .font(.title.bold())

                Text("A IA do Groq analisa sua foto gratuitamente. Cole a chave abaixo — ela fica guardada só no Keychain do seu Mac.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: 400)

                VStack(alignment: .leading, spacing: 6) {
                    SecureField("gsk_...", text: $apiKey)
                        .textFieldStyle(.roundedBorder)
                        .frame(maxWidth: 380)

                    if let err = apiKeyError {
                        Label(err, systemImage: "exclamationmark.triangle.fill")
                            .font(.caption)
                            .foregroundStyle(.red)
                    }

                    Link("→ Criar chave grátis em console.groq.com",
                         destination: URL(string: "https://console.groq.com/keys")!)
                        .font(.caption)
                }

                if apiKeyValid {
                    Label("Chave salva no Keychain!", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                        .font(.headline)
                    nextButton("Continuar →")
                } else {
                    Button("Salvar e continuar") {
                        saveAPIKey()
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .disabled(apiKey.trimmingCharacters(in: .whitespaces).count < 20)
                }
            }
        }
    }

    private var intervalStep: some View {
        stepWrapper {
            VStack(spacing: 20) {
                Image(systemName: "timer.circle.fill")
                    .font(.system(size: 56))
                    .foregroundStyle(.teal)

                Text("Configurar intervalos")
                    .font(.title.bold())

                Text("Ajuste quando o Mac trava e quanto tempo antes o botão de emergência aparece.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: 400)

                VStack(spacing: 16) {
                    VStack(spacing: 8) {
                        HStack {
                            Label("Travar a cada", systemImage: "lock.fill")
                            Spacer()
                            Text(formatInterval(appState.settings.intervalMinutes))
                                .monospacedDigit()
                                .fontWeight(.semibold)
                        }
                        Slider(value: Binding(
                            get: { Double(appState.settings.intervalMinutes) },
                            set: { v in
                                var s = appState.settings
                                s.intervalMinutes = Int(v)
                                appState.saveSettings(s)
                            }
                        ), in: 15...240, step: 15)
                        HStack {
                            Text("15 min").font(.caption).foregroundStyle(.secondary)
                            Spacer()
                            Text("4 horas").font(.caption).foregroundStyle(.secondary)
                        }
                    }

                    Divider()

                    VStack(spacing: 8) {
                        HStack {
                            Label("Emergência após", systemImage: "exclamationmark.triangle.fill")
                                .foregroundStyle(.orange)
                            Spacer()
                            Text(formatSeconds(appState.settings.emergencyDelaySeconds))
                                .monospacedDigit()
                                .fontWeight(.semibold)
                        }
                        Slider(value: Binding(
                            get: { Double(appState.settings.emergencyDelaySeconds) },
                            set: { v in
                                var s = appState.settings
                                s.emergencyDelaySeconds = Int(v)
                                appState.saveSettings(s)
                            }
                        ), in: 10...300, step: 10)
                        HStack {
                            Text("10 s").font(.caption).foregroundStyle(.secondary)
                            Spacer()
                            Text("5 min").font(.caption).foregroundStyle(.secondary)
                        }
                        Text("Tempo para o botão de sair sem foto aparecer na tela de bloqueio.")
                            .font(.caption)
                            .foregroundStyle(.tertiary)
                            .multilineTextAlignment(.center)
                    }
                }
                .padding()
                .background(Color.secondary.opacity(0.08))
                .clipShape(RoundedRectangle(cornerRadius: 12))
                .frame(maxWidth: 380)

                nextButton("Continuar →")
            }
        }
    }

    private func cameraSubtitle(_ cam: AVCaptureDevice) -> String {
        if cam.deviceType == .continuityCamera { return "iPhone (Continuity Camera)" }
        if cam.deviceType == .external { return "Câmera externa" }
        if cam.position == .front { return "Câmera frontal integrada" }
        return "Câmera integrada"
    }

    private func formatSeconds(_ seconds: Int) -> String {
        if seconds < 60 { return "\(seconds)s" }
        let m = seconds / 60
        let s = seconds % 60
        return s == 0 ? "\(m)min" : "\(m)min\(s)s"
    }

    private var doneStep: some View {
        stepWrapper {
            VStack(spacing: 20) {
                Image(systemName: "checkmark.seal.fill")
                    .font(.system(size: 72))
                    .foregroundStyle(.green)
                    .symbolEffect(.bounce)

                Text("Tudo pronto!")
                    .font(.largeTitle.bold())

                Text("WaterCue vai travar seu Mac a cada \(formatInterval(appState.settings.intervalMinutes)).")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: 380)

                Text("Fique de olho na gotinha 💧 na barra de menus.")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)

                Button("Começar agora") {
                    appState.completeOnboarding()
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .keyboardShortcut(.return, modifiers: [])
            }
        }
    }

    // MARK: - Helpers

    private var skipButton: some View {
        Button("Pular (sem aviso prévio)") {
            step += 1
        }
        .font(.callout)
        .foregroundStyle(.secondary)
        .buttonStyle(.plain)
    }

    private func nextButton(_ label: String) -> some View {
        Button(label) { step += 1 }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .keyboardShortcut(.return, modifiers: [])
    }

    private func stepWrapper<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        ScrollView {
            VStack(spacing: 24) {
                Spacer(minLength: 20)
                content()
                Spacer(minLength: 20)
            }
            .padding(.horizontal, 48)
            .padding(.vertical, 24)
        }
    }

    // MARK: - Permission logic

    private func refreshPermissionStatuses() {
        cameraStatus = AVCaptureDevice.authorizationStatus(for: .video)
        Task {
            let settings = await UNUserNotificationCenter.current().notificationSettings()
            await MainActor.run { notifStatus = settings.authorizationStatus }
        }
    }

    private func requestCamera() {
        isRequestingCamera = true
        Task {
            let granted = await AVCaptureDevice.requestAccess(for: .video)
            await MainActor.run {
                cameraStatus = granted ? .authorized : .denied
                isRequestingCamera = false
                if granted { DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) { step += 1 } }
            }
        }
    }

    private func requestNotifications() {
        isRequestingNotif = true
        Task.detached {
            let granted = (try? await UNUserNotificationCenter.current()
                .requestAuthorization(options: [.alert, .sound, .badge])) ?? false
            let settings = await UNUserNotificationCenter.current().notificationSettings()
            await MainActor.run {
                notifStatus = settings.authorizationStatus
                isRequestingNotif = false
                if granted {
                    DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) { step += 1 }
                }
            }
        }
    }

    private func saveAPIKey() {
        let trimmed = apiKey.trimmingCharacters(in: .whitespaces)
        do {
            try KeychainService.shared.setGroqKey(trimmed)
            apiKeyValid = true
            apiKeyError = nil
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) { step += 1 }
        } catch {
            apiKeyError = error.localizedDescription
        }
    }

    private func formatInterval(_ minutes: Int) -> String {
        if minutes < 60 { return "\(minutes) min" }
        let h = minutes / 60
        let m = minutes % 60
        return m == 0 ? "\(h)h" : "\(h)h\(m)min"
    }
}
