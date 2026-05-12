import SwiftUI

extension Notification.Name {
    static let openSettings = Notification.Name("WaterCueOpenSettings")
    static let openStats    = Notification.Name("WaterCueOpenStats")
}

struct MenuBarView: View {
    @EnvironmentObject var appState: AppState

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            statusRow
            cupsRow
            Divider().padding(.vertical, 4)
            actionButtons
            Divider().padding(.vertical, 4)
            bottomButtons
        }
        .padding(12)
        .frame(width: 280)
    }

    private var header: some View {
        HStack(spacing: 8) {
            Text("💧 WaterCue")
                .font(.headline)
            Spacer()
        }
        .padding(.bottom, 10)
    }

    private var statusRow: some View {
        HStack {
            Image(systemName: "clock")
                .foregroundStyle(.secondary)
                .frame(width: 16)
            Text("Próxima trava")
                .foregroundStyle(.secondary)
                .font(.callout)
            Spacer()
            Text(appState.timer.timeUntilLockFormatted)
                .monospacedDigit()
                .font(.callout)
                .foregroundStyle(appState.timer.timeUntilLock < 120 ? .red : .primary)
        }
        .padding(.vertical, 6)
    }

    private var cupsRow: some View {
        HStack {
            Image(systemName: "drop.fill")
                .foregroundStyle(.blue)
                .frame(width: 16)
            Text("Hoje")
                .foregroundStyle(.secondary)
                .font(.callout)
            Spacer()
            Text("\(appState.cupsToday) / \(appState.settings.dailyGoal)")
                .monospacedDigit()
                .font(.callout)
                .foregroundStyle(appState.cupsToday >= appState.settings.dailyGoal ? .green : .primary)
        }
        .padding(.vertical, 6)
    }

    private var actionButtons: some View {
        VStack(spacing: 4) {
            Button {
                appState.openOverlayImmediately()
            } label: {
                Label("Beber água agora", systemImage: "drop.fill")
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)
            .padding(.vertical, 5)
            .padding(.horizontal, 8)
            .background(Color.blue.opacity(0.1))
            .clipShape(RoundedRectangle(cornerRadius: 6))

            Button {
                NotificationCenter.default.post(name: .openStats, object: nil)
            } label: {
                Label("Ver estatísticas", systemImage: "chart.bar.fill")
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)
            .padding(.vertical, 5)
            .padding(.horizontal, 8)
        }
    }

    private var bottomButtons: some View {
        HStack {
            Button {
                NotificationCenter.default.post(name: .openSettings, object: nil)
            } label: {
                Label("Preferências", systemImage: "gear")
            }
            .buttonStyle(.plain)

            Spacer()

            Button {
                NSApp.terminate(nil)
            } label: {
                Text("Sair")
                    .foregroundStyle(.secondary)
            }
            .buttonStyle(.plain)
        }
        .font(.callout)
    }
}
