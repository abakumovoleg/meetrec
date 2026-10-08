import AVFoundation
import Testing
@testable import CaptureKit

struct MonoResamplerTests {
    @Test func convert_48kStereoInChunks_gives16kMonoOfSameDuration() throws {
        // Arrange
        let format = AVAudioFormat(standardFormatWithSampleRate: 48_000, channels: 2)!
        let resampler = MonoResampler()
        let chunks = (0..<10).map { sine(format: format, frames: 4_800, startFrame: $0 * 4_800) }

        // Act
        var samples = try chunks.flatMap { try resampler.convert($0) }
        samples += try resampler.finish()

        // Assert
        #expect(abs(samples.count - 16_000) <= 32)
        let middle = samples[4_000..<12_000]
        let rms = sqrt(middle.reduce(0) { $0 + $1 * $1 } / Float(middle.count))
        #expect(abs(rms - 0.5 / sqrt(2)) < 0.02)
    }

    @Test func convert_formatChangesMidStream_followsNewRate() throws {
        // Arrange
        let resampler = MonoResampler()
        let first = sine(format: AVAudioFormat(standardFormatWithSampleRate: 48_000, channels: 1)!, frames: 48_000, startFrame: 0)
        let second = sine(format: AVAudioFormat(standardFormatWithSampleRate: 24_000, channels: 1)!, frames: 24_000, startFrame: 0)

        // Act
        var samples = try resampler.convert(first)
        samples += try resampler.convert(second)
        samples += try resampler.finish()

        // Assert
        #expect(abs(samples.count - 32_000) <= 64)
    }

    private func sine(format: AVAudioFormat, frames: Int, startFrame: Int) -> AVAudioPCMBuffer {
        let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: AVAudioFrameCount(frames))!
        buffer.frameLength = AVAudioFrameCount(frames)
        for channel in 0..<Int(format.channelCount) {
            for i in 0..<frames {
                buffer.floatChannelData![channel][i] = 0.5 * sin(2 * .pi * 440 * Float(startFrame + i) / Float(format.sampleRate))
            }
        }
        return buffer
    }
}
