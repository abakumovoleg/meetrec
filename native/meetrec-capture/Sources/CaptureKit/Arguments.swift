public enum Command: Equatable {
    case record(outputDirectory: String, micUID: String?, noMic: Bool)
    case listDevices
    case help
}

public struct ArgumentError: Error, Equatable, CustomStringConvertible {
    public let description: String
}

public enum Arguments {
    public static let usage = """
        meetrec-capture --out <sessionDir> [--mic <uid>] [--no-mic]
        meetrec-capture --list-devices
        """

    public static func parse(_ args: [String]) throws -> Command {
        var output: String?
        var mic: String?
        var noMic = false
        var listDevices = false

        var iterator = args.makeIterator()
        while let arg = iterator.next() {
            switch arg {
            case "--out":
                output = try value(of: arg, &iterator)
            case "--mic":
                mic = try value(of: arg, &iterator)
            case "--no-mic":
                noMic = true
            case "--list-devices":
                listDevices = true
            case "--help", "-h":
                return .help
            default:
                throw ArgumentError(description: "Неизвестный аргумент: \(arg)")
            }
        }

        if listDevices {
            return .listDevices
        }

        guard let output else {
            throw ArgumentError(description: "Не указан --out")
        }

        if noMic && mic != nil {
            throw ArgumentError(description: "--mic и --no-mic несовместимы")
        }

        return .record(outputDirectory: output, micUID: mic, noMic: noMic)
    }

    private static func value(of name: String, _ iterator: inout IndexingIterator<[String]>) throws -> String {
        guard let value = iterator.next(), !value.hasPrefix("--") else {
            throw ArgumentError(description: "Для \(name) нужно значение")
        }

        return value
    }
}
