import Foundation
import Testing
@testable import CaptureKit

struct WavWriterTests {
    @Test func close_writesPcm16HeaderWithDataSize() throws {
        // Arrange
        let path = FileManager.default.temporaryDirectory.appendingPathComponent("meetrec-\(UUID()).wav").path
        defer { try? FileManager.default.removeItem(atPath: path) }
        let writer = try WavWriter(path: path)

        // Act
        try writer.append([0, 1, -1, 0.5])
        try writer.append([2])
        try writer.close()

        // Assert
        let data = try Data(contentsOf: URL(fileURLWithPath: path))
        #expect(data.count == 44 + 10)
        #expect(String(decoding: data[0..<4], as: UTF8.self) == "RIFF")
        #expect(uint32(data, at: 4) == 36 + 10)
        #expect(uint32(data, at: 24) == 16_000)
        #expect(uint32(data, at: 40) == 10)
        #expect(int16(data, at: 44) == 0)
        #expect(int16(data, at: 46) == 32767)
        #expect(int16(data, at: 48) == -32767)
        #expect(int16(data, at: 50) == 16384)
        #expect(int16(data, at: 52) == 32767)
    }

    @Test func updateHeader_midRecording_keepsAppending() throws {
        // Arrange
        let path = FileManager.default.temporaryDirectory.appendingPathComponent("meetrec-\(UUID()).wav").path
        defer { try? FileManager.default.removeItem(atPath: path) }
        let writer = try WavWriter(path: path)
        try writer.append([0.1, 0.2])

        // Act
        try writer.updateHeader()
        let midSize = uint32(try Data(contentsOf: URL(fileURLWithPath: path)), at: 40)
        try writer.append([0.3])
        try writer.close()

        // Assert
        let data = try Data(contentsOf: URL(fileURLWithPath: path))
        #expect(midSize == 4)
        #expect(uint32(data, at: 40) == 6)
        #expect(data.count == 44 + 6)
    }

    private func uint32(_ data: Data, at offset: Int) -> UInt32 {
        data[offset..<offset + 4].enumerated().reduce(0) { $0 | UInt32($1.element) << (8 * $1.offset) }
    }

    private func int16(_ data: Data, at offset: Int) -> Int16 {
        Int16(bitPattern: UInt16(data[offset]) | UInt16(data[offset + 1]) << 8)
    }
}
