import SwiftUI

struct WarningView: View {
    @EnvironmentObject var appState: AppState

    var body: some View {
        VStack(spacing: 24) {
            // Header
            VStack(spacing: 10) {
                Image("AppIcon")
                    .resizable()
                    .interpolation(.high)
                    .frame(width: 56, height: 56)
                    .shadow(color: Color(red: 0.15, green: 0.60, blue: 1.0).opacity(0.5), radius: 12)

                Text("Hora de beber água!")
                    .font(.system(size: 20, weight: .bold, design: .rounded))

                Text("O Mac vai travar em \(appState.timer.timeUntilLockFormatted).")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }

            // Buttons
            VStack(spacing: 10) {
                Button(action: { appState.drinkNowFromWarning() }) {
                    Label("Já bebi!", systemImage: "drop.fill")
                        .font(.system(size: 15, weight: .semibold))
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .keyboardShortcut(.return, modifiers: [])

                Button(action: { appState.snoozeWarning() }) {
                    Label("Não posso agora  (+5 min)", systemImage: "moon.zzz.fill")
                        .font(.callout)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 8)
                }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .keyboardShortcut(.escape, modifiers: [])
            }
        }
        .padding(28)
        .frame(width: 340)
    }
}
