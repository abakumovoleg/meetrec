import AVFoundation
import AudioToolbox

final class Microphone {
    private let engine = AVAudioEngine()

    static func ensurePermission() -> Bool {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized:
            return true
        case .notDetermined:
            let done = DispatchSemaphore(value: 0)
            var granted = false
            AVCaptureDevice.requestAccess(for: .audio) {
                granted = $0
                done.signal()
            }
            done.wait()
            return granted
        default:
            return false
        }
    }

    func start(device: InputDevice, onBuffer: @escaping (AVAudioPCMBuffer, UInt64) -> Void) throws {
        let input = engine.inputNode
        if !device.isDefault, let unit = input.audioUnit {
            var id = device.id
            try check(
                AudioUnitSetProperty(unit, kAudioOutputUnitProperty_CurrentDevice, kAudioUnitScope_Global, 0, &id, UInt32(MemoryLayout<AudioDeviceID>.size)),
                "AudioUnitSetProperty(CurrentDevice)")
        }

        let format = input.outputFormat(forBus: 0)
        guard format.sampleRate > 0, format.channelCount > 0 else {
            throw CoreAudioError(operation: "inputNode.outputFormat", status: kAudioHardwareBadDeviceError)
        }

        input.installTap(onBus: 0, bufferSize: 4096, format: format) { buffer, time in
            onBuffer(buffer, time.hostTime)
        }
        try engine.start()
    }

    func stop() {
        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
    }
}
