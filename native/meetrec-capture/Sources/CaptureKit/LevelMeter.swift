import Foundation

public final class LevelMeter {
    private let lock = NSLock()
    private var peak: Float = 0

    public init() {}

    public func observe(_ samples: [Float]) {
        let local = samples.reduce(Float(0)) { max($0, abs($1)) }
        lock.withLock { peak = max(peak, local) }
    }

    public func takePeak() -> Float {
        lock.withLock {
            defer { peak = 0 }
            return peak
        }
    }
}
