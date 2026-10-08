import AVFoundation
import CoreAudio

// A plain IOProc on the input device: AVAudioEngine's inputNode silently delivered nothing when switched to a
// non-default device via kAudioOutputUnitProperty_CurrentDevice.
final class Microphone {
    private let queue = DispatchQueue(label: "meetrec.capture.mic", qos: .userInteractive)
    private var deviceID = AudioDeviceID(kAudioObjectUnknown)
    private var procID: AudioDeviceIOProcID?

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
        let sampleRate = try readValue(device.id, kAudioDevicePropertyNominalSampleRate, initial: Float64(0))
        guard sampleRate > 0 else {
            throw CoreAudioError(operation: "NominalSampleRate", status: kAudioHardwareBadDeviceError)
        }

        deviceID = device.id
        try check(AudioDeviceCreateIOProcIDWithBlock(&procID, device.id, queue) { _, inputData, inputTime, _, _ in
            // HAL IOProcs always see native Float32; only the first input stream is recorded.
            let buffers = UnsafeMutableAudioBufferListPointer(UnsafeMutablePointer(mutating: inputData))
            guard let first = buffers.first, first.mDataByteSize > 0, first.mNumberChannels > 0,
                  let format = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: sampleRate, channels: first.mNumberChannels, interleaved: true)
            else {
                return
            }

            var single = AudioBufferList(mNumberBuffers: 1, mBuffers: first)
            guard let buffer = AVAudioPCMBuffer(pcmFormat: format, bufferListNoCopy: &single, deallocator: nil) else {
                return
            }

            onBuffer(buffer, inputTime.pointee.mHostTime)
        }, "AudioDeviceCreateIOProcIDWithBlock(mic)")
        try check(AudioDeviceStart(device.id, procID), "AudioDeviceStart(mic)")
    }

    func stop() {
        if let procID, deviceID != kAudioObjectUnknown {
            AudioDeviceStop(deviceID, procID)
            AudioDeviceDestroyIOProcID(deviceID, procID)
        }

        procID = nil
        queue.sync {}
    }
}
