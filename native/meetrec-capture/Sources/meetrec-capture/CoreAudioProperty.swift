import CoreAudio

struct CoreAudioError: Error, CustomStringConvertible {
    let operation: String
    let status: OSStatus

    var description: String { "\(operation): OSStatus \(status)" }
}

func check(_ status: OSStatus, _ operation: String) throws {
    if status != noErr {
        throw CoreAudioError(operation: operation, status: status)
    }
}

func address(_ selector: AudioObjectPropertySelector, scope: AudioObjectPropertyScope = kAudioObjectPropertyScopeGlobal) -> AudioObjectPropertyAddress {
    AudioObjectPropertyAddress(mSelector: selector, mScope: scope, mElement: kAudioObjectPropertyElementMain)
}

func readValue<T>(_ object: AudioObjectID, _ selector: AudioObjectPropertySelector, initial: T) throws -> T {
    var property = address(selector)
    var value = initial
    var size = UInt32(MemoryLayout<T>.size)
    try withUnsafeMutableBytes(of: &value) { bytes in
        try check(AudioObjectGetPropertyData(object, &property, 0, nil, &size, bytes.baseAddress!), "AudioObjectGetPropertyData(\(selector))")
    }
    return value
}

func readString(_ object: AudioObjectID, _ selector: AudioObjectPropertySelector) throws -> String {
    var property = address(selector)
    var value: Unmanaged<CFString>?
    var size = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
    try check(AudioObjectGetPropertyData(object, &property, 0, nil, &size, &value), "AudioObjectGetPropertyData(\(selector))")
    return value?.takeRetainedValue() as String? ?? ""
}

func readArray<T>(_ object: AudioObjectID, _ selector: AudioObjectPropertySelector, scope: AudioObjectPropertyScope = kAudioObjectPropertyScopeGlobal, of _: T.Type) throws -> [T] {
    var property = address(selector, scope: scope)
    var size: UInt32 = 0
    try check(AudioObjectGetPropertyDataSize(object, &property, 0, nil, &size), "AudioObjectGetPropertyDataSize(\(selector))")
    let count = Int(size) / MemoryLayout<T>.stride
    guard count > 0 else {
        return []
    }

    let pointer = UnsafeMutableRawPointer.allocate(byteCount: Int(size), alignment: MemoryLayout<T>.alignment)
    defer { pointer.deallocate() }
    try check(AudioObjectGetPropertyData(object, &property, 0, nil, &size, pointer), "AudioObjectGetPropertyData(\(selector))")
    return Array(UnsafeBufferPointer(start: pointer.bindMemory(to: T.self, capacity: count), count: count))
}
