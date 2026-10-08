import CoreAudio

struct InputDevice {
    let id: AudioDeviceID
    let uid: String
    let name: String
    let isDefault: Bool
}

enum InputDevices {
    static func all() throws -> [InputDevice] {
        let defaultID = try? readValue(AudioObjectID(kAudioObjectSystemObject), kAudioHardwarePropertyDefaultInputDevice, initial: AudioDeviceID(0))
        return try readArray(AudioObjectID(kAudioObjectSystemObject), kAudioHardwarePropertyDevices, of: AudioDeviceID.self)
            .filter { hasInput($0) }
            .map { id in
                InputDevice(
                    id: id,
                    uid: try readString(id, kAudioDevicePropertyDeviceUID),
                    name: try readString(id, kAudioObjectPropertyName),
                    isDefault: id == defaultID)
            }
    }

    static func find(uid: String?) throws -> InputDevice? {
        let devices = try all()
        guard let uid else {
            return devices.first { $0.isDefault }
        }

        return devices.first { $0.uid == uid }
    }

    private static func hasInput(_ id: AudioDeviceID) -> Bool {
        let streams = try? readArray(id, kAudioDevicePropertyStreams, scope: kAudioObjectPropertyScopeInput, of: AudioStreamID.self)
        return !(streams ?? []).isEmpty
    }
}
