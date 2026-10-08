import Testing
@testable import CaptureKit

struct EventsTests {
    @Test func level_withMic_matchesSpecFormat() {
        // Arrange
        let mic: Float = 0.12
        let sys: Float = 0.4

        // Act
        let line = Events.level(mic: mic, sys: sys)

        // Assert
        #expect(line == #"{"t":"level","mic":0.120,"sys":0.400}"#)
    }

    @Test func level_withoutMic_omitsMicAndClampsToOne() {
        // Arrange
        let sys: Float = 1.7

        // Act
        let line = Events.level(mic: nil, sys: sys)

        // Assert
        #expect(line == #"{"t":"level","sys":1.000}"#)
    }

    @Test func started_writesOffset() {
        // Arrange
        let offset = 12

        // Act
        let line = Events.started(offsetMs: offset)

        // Assert
        #expect(line == #"{"t":"started","offsetMs":12}"#)
    }

    @Test func device_escapesName() {
        // Arrange
        let name = #"Микрофон "USB""#

        // Act
        let line = Events.device(uid: "a/b", name: name, isDefault: true)

        // Assert
        #expect(line == #"{"t":"device","uid":"a/b","name":"Микрофон \"USB\"","default":true}"#)
    }
}
