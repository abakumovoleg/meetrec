import AVFoundation
import CaptureKit

final class Recorder {
    let systemMeter = LevelMeter()
    let micMeter: LevelMeter?

    private let lock = NSLock()
    private let systemWriter: WavWriter
    private let micWriter: WavWriter?
    private let systemResampler = MonoResampler()
    private let micResampler = MonoResampler()
    private let onStarted: (Int) -> Void

    private var systemStart: Double?
    private var micStart: Double?
    private var pendingMic: [Float] = []
    private var micSamplesToDrop = 0
    private var aligned = false
    private var failure: Error?

    init(systemWriter: WavWriter, micWriter: WavWriter?, onStarted: @escaping (Int) -> Void) {
        self.systemWriter = systemWriter
        self.micWriter = micWriter
        self.micMeter = micWriter == nil ? nil : LevelMeter()
        self.onStarted = onStarted
    }

    var error: Error? { lock.withLock { failure } }

    func handleSystem(_ buffer: AVAudioPCMBuffer, hostTime: UInt64) {
        guarded {
            let samples = try systemResampler.convert(buffer)
            if systemStart == nil {
                systemStart = AVAudioTime.seconds(forHostTime: hostTime)
                try alignIfReady()
            }

            try systemWriter.append(samples)
            systemMeter.observe(samples)
        }
    }

    func handleMic(_ buffer: AVAudioPCMBuffer, hostTime: UInt64) {
        guarded {
            let samples = try micResampler.convert(buffer)
            micMeter?.observe(samples)
            if micStart == nil {
                micStart = AVAudioTime.seconds(forHostTime: hostTime)
            }

            if aligned {
                try writeMic(samples)
            } else {
                // Mic audio that arrives before the system track has started is held back until the offset is
                // known, so that the part recorded ahead of system.wav can be trimmed.
                pendingMic.append(contentsOf: samples)
                try alignIfReady()
            }
        }
    }

    func flushHeaders() {
        guarded {
            try systemWriter.updateHeader()
            try micWriter?.updateHeader()
        }
    }

    func finish() throws {
        try lock.withLock {
            try systemWriter.append(systemResampler.finish())
            if let micWriter {
                if !aligned {
                    try micWriter.append(pendingMic)
                }

                try writeMic(micResampler.finish())
                try micWriter.close()
            }

            try systemWriter.close()
            if let failure {
                throw failure
            }
        }
    }

    private func alignIfReady() throws {
        guard !aligned, let systemStart else {
            return
        }

        guard micWriter != nil else {
            aligned = true
            onStarted(0)
            return
        }

        guard let micStart else {
            return
        }

        let alignment = StartAlignment.compute(systemStartSeconds: systemStart, micStartSeconds: micStart, sampleRate: WavWriter.sampleRate)
        aligned = true
        micSamplesToDrop = alignment.micSamplesToDrop
        let pending = pendingMic
        pendingMic = []
        try writeMic(pending)
        onStarted(alignment.offsetMs)
    }

    private func writeMic(_ samples: [Float]) throws {
        guard let micWriter else {
            return
        }

        let dropped = min(micSamplesToDrop, samples.count)
        micSamplesToDrop -= dropped
        try micWriter.append(Array(samples.dropFirst(dropped)))
    }

    private func guarded(_ action: () throws -> Void) {
        lock.withLock {
            guard failure == nil else {
                return
            }

            do {
                try action()
            } catch {
                failure = error
            }
        }
    }
}
