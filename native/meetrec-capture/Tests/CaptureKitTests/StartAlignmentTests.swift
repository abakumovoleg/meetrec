import Testing
@testable import CaptureKit

struct StartAlignmentTests {
    @Test func compute_micStartsLater_reportsPositiveOffset() {
        // Arrange
        let system = 100.0
        let mic = 100.0125

        // Act
        let alignment = StartAlignment.compute(systemStartSeconds: system, micStartSeconds: mic, sampleRate: 16_000)

        // Assert
        #expect(alignment == StartAlignment(offsetMs: 13, micSamplesToDrop: 0))
    }

    @Test func compute_micStartsEarlier_trimsMicInsteadOfNegativeOffset() {
        // Arrange
        let system = 100.0
        let mic = 99.75

        // Act
        let alignment = StartAlignment.compute(systemStartSeconds: system, micStartSeconds: mic, sampleRate: 16_000)

        // Assert
        #expect(alignment == StartAlignment(offsetMs: 0, micSamplesToDrop: 4_000))
    }
}
