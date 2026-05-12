import XCTest
@testable import WaterCue

final class DatabaseServiceTests: XCTestCase {
    var db: DatabaseService!

    override func setUpWithError() throws {
        db = DatabaseService(inMemory: true)
    }

    func test_insert_and_cupsToday() {
        db.insert(HydrationEvent(validatedByAI: true))
        XCTAssertEqual(db.cupsToday(), 1)
    }

    func test_emergencyBypass_notCounted() {
        db.insert(HydrationEvent(validatedByAI: false, emergencyBypass: true))
        XCTAssertEqual(db.cupsToday(), 0)
    }

    func test_multiple_cups() {
        for _ in 0..<5 {
            db.insert(HydrationEvent(validatedByAI: true))
        }
        XCTAssertEqual(db.cupsToday(), 5)
    }

    func test_photoHash_replayDetected() {
        db.insert(HydrationEvent(validatedByAI: true, photoHash: "deadbeef"))
        XCTAssertTrue(db.isReplayedPhoto(hash: "deadbeef"))
    }

    func test_photoHash_differentHash_notReplay() {
        db.insert(HydrationEvent(validatedByAI: true, photoHash: "deadbeef"))
        XCTAssertFalse(db.isReplayedPhoto(hash: "cafebabe"))
    }

    func test_streak_singleDayCups() {
        db.insert(HydrationEvent(validatedByAI: true))
        XCTAssertEqual(db.currentStreak(dailyGoal: 1), 1)
    }

    func test_streak_zeroCups() {
        XCTAssertEqual(db.currentStreak(dailyGoal: 8), 0)
    }

    func test_weeklyCounts_empty() {
        XCTAssertTrue(db.weeklyCounts().isEmpty)
    }

    func test_weeklyCounts_withData() {
        db.insert(HydrationEvent(validatedByAI: true))
        db.insert(HydrationEvent(validatedByAI: true))
        let counts = db.weeklyCounts()
        XCTAssertFalse(counts.isEmpty)
        XCTAssertEqual(counts.last?.count, 2)
    }
}
