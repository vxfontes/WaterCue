import Foundation
import SQLite3
import CryptoKit

final class DatabaseService {
    static let shared = DatabaseService()

    private var db: OpaquePointer?

    init() {
        let fileManager = FileManager.default
        let appSupportURL = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let dbFolder = appSupportURL.appendingPathComponent("WaterCue")
        try? fileManager.createDirectory(at: dbFolder, withIntermediateDirectories: true)
        let dbURL = dbFolder.appendingPathComponent("hydration.sqlite")

        guard sqlite3_open(dbURL.path, &db) == SQLITE_OK else {
            fatalError("Cannot open SQLite database")
        }
        createSchema()
    }

    init(inMemory: Bool) {
        guard sqlite3_open(":memory:", &db) == SQLITE_OK else {
            fatalError("Cannot open in-memory SQLite database")
        }
        createSchema()
    }

    deinit {
        sqlite3_close(db)
    }

    private func createSchema() {
        let sql = """
            CREATE TABLE IF NOT EXISTS hydration_events (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp           REAL    NOT NULL,
                validated_by_ai     INTEGER NOT NULL DEFAULT 0,
                groq_model          TEXT,
                emergency_bypass    INTEGER NOT NULL DEFAULT 0,
                photo_hash          TEXT,
                reason              TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_events_timestamp ON hydration_events(timestamp);
        """
        var errMsg: UnsafeMutablePointer<CChar>?
        sqlite3_exec(db, sql, nil, nil, &errMsg)
    }

    func insert(_ event: HydrationEvent) {
        let sql = """
            INSERT INTO hydration_events
                (timestamp, validated_by_ai, groq_model, emergency_bypass, photo_hash, reason)
            VALUES (?, ?, ?, ?, ?, ?)
        """
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return }
        defer { sqlite3_finalize(stmt) }

        sqlite3_bind_double(stmt, 1, event.timestamp.timeIntervalSince1970)
        sqlite3_bind_int(stmt, 2, event.validatedByAI ? 1 : 0)
        if let m = event.groqModel {
            sqlite3_bind_text(stmt, 3, (m as NSString).utf8String, -1, nil)
        } else {
            sqlite3_bind_null(stmt, 3)
        }
        sqlite3_bind_int(stmt, 4, event.emergencyBypass ? 1 : 0)
        if let h = event.photoHash {
            sqlite3_bind_text(stmt, 5, (h as NSString).utf8String, -1, nil)
        } else {
            sqlite3_bind_null(stmt, 5)
        }
        if let r = event.reason {
            sqlite3_bind_text(stmt, 6, (r as NSString).utf8String, -1, nil)
        } else {
            sqlite3_bind_null(stmt, 6)
        }
        sqlite3_step(stmt)
    }

    func cupsToday() -> Int {
        let startOfDay = Calendar.current.startOfDay(for: Date()).timeIntervalSince1970
        let sql = """
            SELECT COUNT(*) FROM hydration_events
            WHERE timestamp >= ? AND emergency_bypass = 0
        """
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return 0 }
        defer { sqlite3_finalize(stmt) }
        sqlite3_bind_double(stmt, 1, startOfDay)
        return sqlite3_step(stmt) == SQLITE_ROW ? Int(sqlite3_column_int(stmt, 0)) : 0
    }

    func currentStreak(dailyGoal: Int) -> Int {
        let cal = Calendar.current
        var streak = 0
        var cursor = cal.startOfDay(for: Date())

        for _ in 0..<365 {
            let next = cal.date(byAdding: .day, value: 1, to: cursor)!
            let sql = """
                SELECT COUNT(*) FROM hydration_events
                WHERE timestamp >= ? AND timestamp < ? AND emergency_bypass = 0
            """
            var stmt: OpaquePointer?
            guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { break }
            sqlite3_bind_double(stmt, 1, cursor.timeIntervalSince1970)
            sqlite3_bind_double(stmt, 2, next.timeIntervalSince1970)
            let count = sqlite3_step(stmt) == SQLITE_ROW ? Int(sqlite3_column_int(stmt, 0)) : 0
            sqlite3_finalize(stmt)

            if count >= dailyGoal {
                streak += 1
            } else if cal.isDateInToday(cursor) {
                // today incomplete — don't break streak
            } else {
                break
            }
            cursor = cal.date(byAdding: .day, value: -1, to: cursor)!
        }
        return streak
    }

    func weeklyCounts() -> [DailyCount] {
        let sevenDaysAgo = Date().addingTimeInterval(-7 * 86400).timeIntervalSince1970
        let sql = """
            SELECT DATE(timestamp, 'unixepoch', 'localtime') AS day, COUNT(*) AS count
            FROM hydration_events
            WHERE emergency_bypass = 0 AND timestamp >= ?
            GROUP BY day ORDER BY day ASC
        """
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return [] }
        defer { sqlite3_finalize(stmt) }
        sqlite3_bind_double(stmt, 1, sevenDaysAgo)

        var results: [DailyCount] = []
        while sqlite3_step(stmt) == SQLITE_ROW {
            let day = String(cString: sqlite3_column_text(stmt, 0))
            let count = Int(sqlite3_column_int(stmt, 1))
            results.append(DailyCount(day: day, count: count))
        }
        return results
    }

    func isReplayedPhoto(hash: String) -> Bool {
        let oneHourAgo = Date().addingTimeInterval(-3600).timeIntervalSince1970
        let sql = """
            SELECT COUNT(*) FROM hydration_events
            WHERE photo_hash = ? AND timestamp > ?
        """
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(db, sql, -1, &stmt, nil) == SQLITE_OK else { return false }
        defer { sqlite3_finalize(stmt) }
        sqlite3_bind_text(stmt, 1, (hash as NSString).utf8String, -1, nil)
        sqlite3_bind_double(stmt, 2, oneHourAgo)
        return sqlite3_step(stmt) == SQLITE_ROW && sqlite3_column_int(stmt, 0) > 0
    }

    static func photoHash(from data: Data) -> String {
        let digest = SHA256.hash(data: data)
        return digest.compactMap { String(format: "%02x", $0) }.joined()
    }
}
