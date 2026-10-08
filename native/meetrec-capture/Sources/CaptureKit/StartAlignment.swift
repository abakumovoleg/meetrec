public struct StartAlignment: Equatable {
    public let offsetMs: Int
    public let micSamplesToDrop: Int

    // offsetMs follows the session.json contract: how much later mic.wav starts than system.wav. A mic that
    // started earlier is trimmed instead, so the offset is never negative.
    public static func compute(systemStartSeconds: Double, micStartSeconds: Double, sampleRate: Int) -> StartAlignment {
        let delay = micStartSeconds - systemStartSeconds
        if delay >= 0 {
            return StartAlignment(offsetMs: Int((delay * 1000).rounded()), micSamplesToDrop: 0)
        }

        return StartAlignment(offsetMs: 0, micSamplesToDrop: Int((-delay * Double(sampleRate)).rounded()))
    }
}
