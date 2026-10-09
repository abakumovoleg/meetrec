import Foundation

public enum Events {
    // Bumped on any incompatible change to the lines below; meetrec refuses a helper with another protocol.
    public static let protocolVersion = 1

    public static func hello(version: String) -> String {
        line([("t", "hello"), ("protocol", protocolVersion), ("version", version)])
    }

    public static func level(mic: Float?, sys: Float) -> String {
        var fields: [(String, Any)] = [("t", "level")]
        if let mic {
            fields.append(("mic", clamped(mic)))
        }

        fields.append(("sys", clamped(sys)))
        return line(fields)
    }

    public static func started(offsetMs: Int) -> String {
        line([("t", "started"), ("offsetMs", offsetMs)])
    }

    public static func permissions(mic: Permission, system: Permission) -> String {
        line([("t", "permissions"), ("mic", mic.rawValue), ("system", system.rawValue)])
    }

    public static func device(uid: String, name: String, isDefault: Bool) -> String {
        line([("t", "device"), ("uid", uid), ("name", name), ("default", isDefault)])
    }

    // Fields are written in a fixed order so that the protocol lines look exactly like the spec examples.
    private static func line(_ fields: [(String, Any)]) -> String {
        let body = fields.map { key, value in "\(json(key)):\(json(value))" }.joined(separator: ",")
        return "{\(body)}"
    }

    private static func json(_ value: Any) -> String {
        switch value {
        case let string as String:
            let data = try! JSONSerialization.data(withJSONObject: string, options: [.fragmentsAllowed, .withoutEscapingSlashes])
            return String(decoding: data, as: UTF8.self)
        case let bool as Bool:
            return bool ? "true" : "false"
        case let double as Double:
            return String(format: "%.3f", double)
        default:
            return "\(value)"
        }
    }

    private static func clamped(_ value: Float) -> Double {
        Double(min(max(value, 0), 1))
    }
}
