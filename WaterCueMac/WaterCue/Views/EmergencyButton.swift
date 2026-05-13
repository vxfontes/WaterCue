import SwiftUI

struct EmergencyButton: View {
    @EnvironmentObject var appState: AppState
    let emergencyDelaySeconds: Int

    @State private var elapsed: Int = 0
    @State private var showConfirmation = false
    @State private var timer: Timer?

    private var remaining: Int { max(0, emergencyDelaySeconds - elapsed) }
    private var isVisible: Bool { elapsed >= emergencyDelaySeconds }

    var body: some View {
        VStack(spacing: 8) {
            if isVisible {
                Button(action: { showConfirmation = true }) {
                    HStack(spacing: 8) {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundStyle(.yellow)
                        Text("Desbloquear (emergência)")
                            .foregroundStyle(.white)
                    }
                    .padding(.horizontal, 20)
                    .padding(.vertical, 10)
                    .background(Color.white.opacity(0.15))
                    .clipShape(RoundedRectangle(cornerRadius: 10))
                    .overlay(
                        RoundedRectangle(cornerRadius: 10)
                            .stroke(Color.yellow.opacity(0.5), lineWidth: 1)
                    )
                }
                .buttonStyle(.plain)
                .transition(.opacity.combined(with: .scale))
            } else {
                Text("Emergência disponível em \(remaining)s")
                    .font(.caption)
                    .foregroundStyle(.white.opacity(0.4))
            }
        }
        .onAppear { startTimer() }
        .onDisappear { timer?.invalidate() }
        .animation(.easeIn(duration: 0.4), value: isVisible)
        .alert("Desbloquear sem beber água?", isPresented: $showConfirmation) {
            Button("Cancelar", role: .cancel) {}
            Button("Sim, desbloquear", role: .destructive) {
                appState.emergencyUnlock()
            }
        } message: {
            Text("O desbloqueio de emergência não conta como hidratação. Use só se realmente precisar.")
        }
    }

    private func startTimer() {
        elapsed = 0
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { _ in
            Task { @MainActor in
                self.elapsed += 1
            }
        }
    }
}
