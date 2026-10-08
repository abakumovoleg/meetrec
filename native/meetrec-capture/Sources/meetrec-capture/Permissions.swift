import AVFoundation
import CaptureKit
import Foundation

enum Permissions {
    static var microphone: Permission {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized:
            return .granted
        case .notDetermined:
            return .unknown
        default:
            return .denied
        }
    }

    // There is no public API to query the system-audio-capture permission, and without it the tap silently
    // records zeros. TCCAccessPreflight from the private TCC framework (the approach AudioCap uses) answers
    // without showing a prompt; if the symbol ever disappears the status is reported as unknown.
    static var systemAudio: Permission {
        guard let symbol = tccSymbol("TCCAccessPreflight") else {
            return .unknown
        }

        typealias Preflight = @convention(c) (CFString, CFDictionary?) -> Int
        let preflight = unsafeBitCast(symbol, to: Preflight.self)
        switch preflight(serviceAudioCapture as CFString, nil) {
        case 0:
            return .granted
        case 1:
            return .denied
        default:
            return .unknown
        }
    }

    // Starting the tap does not reliably raise the prompt for every terminal app: the tap may just record zeros.
    // The explicit request (TCCAccessRequest, as AudioCap does) shows it and waits for the answer.
    static func requestSystemAudio() -> Permission {
        guard let symbol = tccSymbol("TCCAccessRequest") else {
            return .unknown
        }

        typealias Request = @convention(c) (CFString, CFDictionary?, @escaping @convention(block) (Bool) -> Void) -> Void
        let request = unsafeBitCast(symbol, to: Request.self)
        let done = DispatchSemaphore(value: 0)
        var granted = false
        request(serviceAudioCapture as CFString, nil) { result in
            granted = result
            done.signal()
        }
        done.wait()
        return granted ? .granted : .denied
    }

    private static let serviceAudioCapture = "kTCCServiceAudioCapture"

    private static func tccSymbol(_ name: String) -> UnsafeMutableRawPointer? {
        guard let handle = dlopen("/System/Library/PrivateFrameworks/TCC.framework/Versions/A/TCC", RTLD_NOW) else {
            return nil
        }

        return dlsym(handle, name)
    }
}
