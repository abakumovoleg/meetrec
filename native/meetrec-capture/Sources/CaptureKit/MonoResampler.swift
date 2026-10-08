import AVFoundation

public final class MonoResampler {
    private let target = AVAudioFormat(
        commonFormat: .pcmFormatFloat32, sampleRate: Double(WavWriter.sampleRate), channels: 1, interleaved: false)!

    private var converter: AVAudioConverter?

    public init() {}

    public func convert(_ buffer: AVAudioPCMBuffer) throws -> [Float] {
        guard buffer.frameLength > 0 else {
            return []
        }

        // Devices may switch sample rate mid-recording (e.g. a Bluetooth headset changing profile), so the
        // converter follows the incoming format instead of being fixed at start.
        if converter?.inputFormat != buffer.format {
            guard let created = AVAudioConverter(from: buffer.format, to: target) else {
                throw ResamplerError(description: "Нет конвертера из \(buffer.format)")
            }

            created.downmix = true
            converter = created
        }

        var consumed = false
        let capacity = AVAudioFrameCount((Double(buffer.frameLength) * target.sampleRate / buffer.format.sampleRate).rounded(.up)) + 256
        return try drain(capacity: capacity) { _, status in
            if consumed {
                status.pointee = .noDataNow
                return nil
            }

            consumed = true
            status.pointee = .haveData
            return buffer
        }
    }

    public func finish() throws -> [Float] {
        guard converter != nil else {
            return []
        }

        defer { converter = nil }
        return try drain(capacity: 4096) { _, status in
            status.pointee = .endOfStream
            return nil
        }
    }

    private func drain(capacity: AVAudioFrameCount, _ input: @escaping AVAudioConverterInputBlock) throws -> [Float] {
        guard let converter else {
            return []
        }

        var samples: [Float] = []
        while true {
            guard let output = AVAudioPCMBuffer(pcmFormat: target, frameCapacity: capacity) else {
                throw ResamplerError(description: "Не удалось выделить буфер")
            }

            var error: NSError?
            let status = converter.convert(to: output, error: &error, withInputFrom: input)
            if let error {
                throw error
            }

            samples.append(contentsOf: UnsafeBufferPointer(start: output.floatChannelData![0], count: Int(output.frameLength)))
            if status != .haveData || output.frameLength < capacity {
                return samples
            }
        }
    }
}

public struct ResamplerError: Error, CustomStringConvertible {
    public let description: String
}
