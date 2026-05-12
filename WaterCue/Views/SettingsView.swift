import SwiftUI
import ServiceManagement
import AVFoundation

struct SettingsView: View {
    @EnvironmentObject var appState: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var draft: HydrationSettings = HydrationSettings.load()
    @State private var apiKey: String = ""
    @State private var apiKeySaved = false
    @State private var apiKeyError: String?
    @State private var autostartOn: Bool = false
    @State private var cameras: [AVCaptureDevice] = []

    var body: some View {
        Form {
            timerSection
            cameraSection
            apiKeySection
            autostartSection
            dangerSection
        }
        .formStyle(.grouped)
        .frame(minWidth: 420, minHeight: 520)
        .navigationTitle("Preferências")
        .toolbar {
            ToolbarItem(placement: .confirmationAction) {
                Button("Salvar") { save() }
            }
            ToolbarItem(placement: .cancellationAction) {
                Button("Cancelar") { dismiss() }
            }
        }
        .onAppear { loadCurrent() }
    }

    private var timerSection: some View {
        Section("Intervalo") {
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text("Intervalo entre travas")
                    Spacer()
                    Text("\(draft.intervalMinutes) min")
                        .monospacedDigit()
                        .foregroundStyle(.secondary)
                }
                Slider(value: Binding(
                    get: { Double(draft.intervalMinutes) },
                    set: { draft.intervalMinutes = Int($0) }
                ), in: 15...240, step: 5)
            }

            Toggle("Aviso antes de travar", isOn: $draft.warnBeforeLock)

            if draft.warnBeforeLock {
                VStack(alignment: .leading, spacing: 4) {
                    HStack {
                        Text("Aviso com antecedência de")
                        Spacer()
                        Text("\(draft.warningMinutes) min")
                            .monospacedDigit()
                            .foregroundStyle(.secondary)
                    }
                    Slider(value: Binding(
                        get: { Double(draft.warningMinutes) },
                        set: { draft.warningMinutes = Int($0) }
                    ), in: 1...10, step: 1)
                }
            }

            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text("Emergência disponível após")
                    Spacer()
                    Text("\(draft.emergencyDelaySeconds) s")
                        .monospacedDigit()
                        .foregroundStyle(.secondary)
                }
                Slider(value: Binding(
                    get: { Double(draft.emergencyDelaySeconds) },
                    set: { draft.emergencyDelaySeconds = Int($0) }
                ), in: 10...300, step: 10)
            }

            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text("Meta diária de copos")
                    Spacer()
                    Text("\(draft.dailyGoal) copos")
                        .monospacedDigit()
                        .foregroundStyle(.secondary)
                }
                Slider(value: Binding(
                    get: { Double(draft.dailyGoal) },
                    set: { draft.dailyGoal = Int($0) }
                ), in: 4...16, step: 1)
            }
        }
    }

    private var cameraSection: some View {
        Section("Câmera") {
            if cameras.isEmpty {
                Text("Nenhuma câmera detectada")
                    .foregroundStyle(.secondary)
            } else {
                Picker("Câmera", selection: $draft.cameraDeviceID) {
                    Text("Automático").tag("")
                    ForEach(cameras, id: \.uniqueID) { cam in
                        Text(cam.localizedName).tag(cam.uniqueID)
                    }
                }
            }
        }
    }

    private var apiKeySection: some View {
        Section("Groq API") {
            VStack(alignment: .leading, spacing: 8) {
                SecureField("Groq API key (gsk_...)", text: $apiKey)
                    .textFieldStyle(.roundedBorder)

                if let err = apiKeyError {
                    Text(err).font(.caption).foregroundStyle(.red)
                }
                if apiKeySaved {
                    Label("Chave salva com sucesso!", systemImage: "checkmark.circle.fill")
                        .font(.caption)
                        .foregroundStyle(.green)
                }

                HStack {
                    Button("Salvar chave") { saveAPIKey() }
                        .disabled(apiKey.trimmingCharacters(in: .whitespaces).isEmpty)
                    Spacer()
                    Link("Obter chave gratuita", destination: URL(string: "https://console.groq.com/keys")!)
                        .font(.caption)
                }
            }

            Picker("Modelo", selection: $draft.groqModel) {
                Text("Llama 4 Scout (rápido)").tag("meta-llama/llama-4-scout-17b-16e-instruct")
                Text("Llama 4 Maverick (preciso)").tag("meta-llama/llama-4-maverick-17b-128e-instruct")
                Text("Llama 3.2 90B Vision").tag("llama-3.2-90b-vision-preview")
                Text("Llama 3.2 11B Vision (econômico)").tag("llama-3.2-11b-vision-preview")
            }
        }
    }

    private var autostartSection: some View {
        Section("Sistema") {
            HStack {
                Toggle("Iniciar com o Mac", isOn: $autostartOn)
                    .onChange(of: autostartOn) { _, new in
                        toggleAutostart(new)
                    }
                Spacer()
                if appState.autostart.status == .requiresApproval {
                    Button("Aprovar em Configurações") {
                        appState.autostart.openLoginItemsSettings()
                    }
                    .font(.caption)
                }
            }
        }
    }

    private var dangerSection: some View {
        Section("Avançado") {
            Button("Refazer onboarding") {
                UserDefaults.standard.set(false, forKey: "watercue_onboarding_done")
                appState.isOnboardingComplete = false
                dismiss()
            }
            .foregroundStyle(.orange)
        }
    }

    private func loadCurrent() {
        draft = appState.settings
        apiKey = (try? KeychainService.shared.getGroqKey()) ?? ""
        autostartOn = appState.autostart.isRegistered
        cameras = CameraService.availableCameras()
    }

    private func saveAPIKey() {
        let trimmed = apiKey.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return }
        do {
            try KeychainService.shared.setGroqKey(trimmed)
            apiKeySaved = true
            apiKeyError = nil
            DispatchQueue.main.asyncAfter(deadline: .now() + 2) {
                apiKeySaved = false
            }
        } catch {
            apiKeyError = error.localizedDescription
        }
    }

    private func save() {
        appState.saveSettings(draft)
        dismiss()
    }

    private func toggleAutostart(_ on: Bool) {
        do {
            if on { try appState.autostart.register() }
            else   { try appState.autostart.unregister() }
        } catch {
            autostartOn = !on
        }
    }
}
