import Foundation

public final class WavWriter {
    public static let sampleRate = 16_000

    private static let headerSize = 44

    private let handle: FileHandle
    public private(set) var sampleCount = 0

    public init(path: String) throws {
        guard FileManager.default.createFile(atPath: path, contents: WavWriter.header(dataBytes: 0)) else {
            throw CocoaError(.fileWriteUnknown, userInfo: [NSFilePathErrorKey: path])
        }

        handle = try FileHandle(forWritingTo: URL(fileURLWithPath: path))
        try handle.seekToEnd()
    }

    public func append(_ samples: [Float]) throws {
        guard !samples.isEmpty else {
            return
        }

        var data = Data(capacity: samples.count * 2)
        for sample in samples {
            let value = Int16(max(-32768, min(32767, (sample * 32767).rounded())))
            withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) }
        }

        try handle.write(contentsOf: data)
        sampleCount += samples.count
    }

    // Called periodically as well as on close, so that a killed process still leaves a readable file.
    public func updateHeader() throws {
        let end = try handle.offset()
        try handle.seek(toOffset: 0)
        try handle.write(contentsOf: WavWriter.header(dataBytes: sampleCount * 2))
        try handle.seek(toOffset: end)
    }

    public func close() throws {
        try updateHeader()
        try handle.close()
    }

    static func header(dataBytes: Int) -> Data {
        var data = Data(capacity: headerSize)
        func append<T: FixedWidthInteger>(_ value: T) {
            withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) }
        }

        data.append(contentsOf: Array("RIFF".utf8))
        append(UInt32(36 + dataBytes))
        data.append(contentsOf: Array("WAVEfmt ".utf8))
        append(UInt32(16))
        append(UInt16(1))
        append(UInt16(1))
        append(UInt32(sampleRate))
        append(UInt32(sampleRate * 2))
        append(UInt16(2))
        append(UInt16(16))
        data.append(contentsOf: Array("data".utf8))
        append(UInt32(dataBytes))
        return data
    }
}
