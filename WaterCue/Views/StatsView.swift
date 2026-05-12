import SwiftUI
import Charts

struct StatsView: View {
    @EnvironmentObject var appState: AppState
    @State private var weeklyCounts: [DailyCount] = []

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            Text("Hidratação")
                .font(.title2.bold())

            todayRow
            streakRow

            if !weeklyCounts.isEmpty {
                weeklyChart
            }

            Spacer()
        }
        .padding()
        .frame(minWidth: 320, minHeight: 280)
        .onAppear { loadData() }
    }

    private var todayRow: some View {
        HStack {
            Label("Hoje", systemImage: "drop.fill")
                .foregroundStyle(.blue)
            Spacer()
            Text("\(appState.cupsToday) / \(appState.settings.dailyGoal) copos")
                .monospacedDigit()
                .foregroundStyle(appState.cupsToday >= appState.settings.dailyGoal ? .green : .primary)
        }
        .padding()
        .background(Color.secondary.opacity(0.1))
        .clipShape(RoundedRectangle(cornerRadius: 10))
    }

    private var streakRow: some View {
        HStack {
            Label("Sequência", systemImage: "flame.fill")
                .foregroundStyle(.orange)
            Spacer()
            Text("\(appState.currentStreak) dias")
                .monospacedDigit()
                .foregroundStyle(appState.currentStreak > 0 ? .orange : .secondary)
        }
        .padding()
        .background(Color.secondary.opacity(0.1))
        .clipShape(RoundedRectangle(cornerRadius: 10))
    }

    private var weeklyChart: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Últimos 7 dias")
                .font(.subheadline)
                .foregroundStyle(.secondary)

            Chart(weeklyCounts, id: \.day) { item in
                BarMark(
                    x: .value("Dia", formatDay(item.day)),
                    y: .value("Copos", item.count)
                )
                .foregroundStyle(item.count >= appState.settings.dailyGoal ? Color.blue : Color.blue.opacity(0.4))
                .cornerRadius(4)
            }
            .chartYAxis { AxisMarks(values: .stride(by: 2)) }
            .frame(height: 120)
        }
    }

    private func loadData() {
        weeklyCounts = (try? appState.db.weeklyCounts()) ?? []
    }

    private func formatDay(_ day: String) -> String {
        // day format: "YYYY-MM-DD" — show "Mon", "Tue" etc
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd"
        if let date = formatter.date(from: day) {
            let out = DateFormatter()
            out.dateFormat = "EEE"
            out.locale = Locale(identifier: "pt_BR")
            return out.string(from: date)
        }
        return day
    }
}
