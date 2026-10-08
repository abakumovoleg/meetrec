import Testing
@testable import CaptureKit

struct ArgumentsTests {
    @Test func parse_outAndMic_returnsRecord() throws {
        // Arrange
        let args = ["--out", "/tmp/session", "--mic", "BuiltInMicrophoneDevice"]

        // Act
        let command = try Arguments.parse(args)

        // Assert
        #expect(command == .record(outputDirectory: "/tmp/session", micUID: "BuiltInMicrophoneDevice", noMic: false))
    }

    @Test func parse_listDevices_ignoresMissingOut() throws {
        // Arrange
        let args = ["--list-devices"]

        // Act
        let command = try Arguments.parse(args)

        // Assert
        #expect(command == .listDevices)
    }

    @Test(arguments: [
        ["--mic", "x"],
        ["--out"],
        ["--out", "--no-mic"],
        ["--out", "/tmp/s", "--no-mic", "--mic", "x"],
        ["--out", "/tmp/s", "--bogus"],
    ])
    func parse_invalid_throws(args: [String]) {
        // Arrange
        let input = args

        // Act
        let act = { try Arguments.parse(input) }

        // Assert
        #expect(throws: ArgumentError.self) { try act() }
    }
}
