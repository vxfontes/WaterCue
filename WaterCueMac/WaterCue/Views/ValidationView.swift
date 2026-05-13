import SwiftUI

struct ValidationView: View {
    @EnvironmentObject var appState: AppState
    let isValidating: Bool
    let errorMessage: String?

    var body: some View {
        VStack(spacing: 24) {
            if isValidating {
                VStack(spacing: 16) {
                    ProgressView()
                        .progressViewStyle(.circular)
                        .scaleEffect(1.5)
                        .tint(.white)

                    Text("Validando com IA...")
                        .font(.headline)
                        .foregroundStyle(.white)
                }
            } else if let msg = errorMessage {
                VStack(spacing: 16) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 48))
                        .foregroundStyle(.red.opacity(0.8))

                    Text(msg)
                        .font(.callout)
                        .foregroundStyle(.white.opacity(0.85))
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)

                    Button(action: { appState.retryCapture() }) {
                        Label("Tentar de novo", systemImage: "camera.fill")
                            .padding(.horizontal, 24)
                            .padding(.vertical, 10)
                            .background(Color.white.opacity(0.2))
                            .foregroundStyle(.white)
                            .clipShape(RoundedRectangle(cornerRadius: 10))
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .padding(32)
        .background(Color.black.opacity(0.7))
        .clipShape(RoundedRectangle(cornerRadius: 20))
    }
}
