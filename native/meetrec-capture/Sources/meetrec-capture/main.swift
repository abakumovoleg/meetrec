import CaptureKit
import Foundation

setvbuf(stdout, nil, _IOLBF, 0)

func emit(_ line: String) {
    print(line)
}

// The embedded Info.plist is the helper's bundle, so its version is readable without a .app around the binary.
let helperVersion = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "dev"

func fail(_ message: String, _ code: ExitCode) -> Never {
    FileHandle.standardError.write(Data((message + "\n").utf8))
    exit(code.rawValue)
}

func listDevices() -> Never {
    do {
        for device in try InputDevices.all() {
            emit(Events.device(uid: device.uid, name: device.name, isDefault: device.isDefault))
        }
    } catch {
        fail("Не удалось получить список устройств: \(error)", .failure)
    }

    exit(ExitCode.ok.rawValue)
}

func checkPermissions() -> Never {
    emit(Events.permissions(mic: Permissions.microphone, system: Permissions.systemAudio))
    exit(ExitCode.ok.rawValue)
}

func record(outputDirectory: String, micUID: String?, noMic: Bool) -> Never {
    var systemAudio = Permissions.systemAudio
    if systemAudio == .unknown {
        systemAudio = Permissions.requestSystemAudio()
    }

    if systemAudio == .denied {
        fail("Нет разрешения на запись системного звука. Выдайте его терминалу: Системные настройки → Конфиденциальность и безопасность → Запись звука и экрана → «Только системный звук».", .noPermission)
    }

    var micDevice: InputDevice?
    if !noMic {
        guard Microphone.ensurePermission() else {
            fail("Нет разрешения на микрофон. Выдайте его терминалу: Системные настройки → Конфиденциальность → Микрофон.", .noPermission)
        }

        do {
            guard let device = try InputDevices.find(uid: micUID) else {
                fail(micUID.map { "Микрофон не найден: \($0)" } ?? "Нет микрофона по умолчанию", .noDevice)
            }

            micDevice = device
        } catch {
            fail("Не удалось получить список устройств: \(error)", .failure)
        }
    }

    let recorder: Recorder
    do {
        try FileManager.default.createDirectory(atPath: outputDirectory, withIntermediateDirectories: true)
        let directory = URL(fileURLWithPath: outputDirectory)
        recorder = Recorder(
            systemWriter: try WavWriter(path: directory.appendingPathComponent("system.wav").path),
            micWriter: try micDevice.map { _ in try WavWriter(path: directory.appendingPathComponent("mic.wav").path) },
            onStarted: { emit(Events.started(offsetMs: $0)) })
    } catch {
        fail("Не удалось создать файлы записи в \(outputDirectory): \(error)", .failure)
    }

    let tap = SystemTap()
    let microphone = micDevice.map { _ in Microphone() }

    func stopAll() {
        microphone?.stop()
        tap.stop()
    }

    do {
        try tap.start(onBuffer: recorder.handleSystem)
    } catch {
        tap.stop()
        fail("Не удалось начать запись системного звука: \(error)", .failure)
    }

    if let microphone, let micDevice {
        do {
            try microphone.start(device: micDevice, onBuffer: recorder.handleMic)
        } catch {
            stopAll()
            fail("Не удалось начать запись микрофона «\(micDevice.name)»: \(error)", .noDevice)
        }
    }

    func finish(_ code: ExitCode, _ message: String? = nil) -> Never {
        stopAll()
        do {
            try recorder.finish()
        } catch {
            fail("Ошибка записи: \(error)", .failure)
        }

        if let message {
            fail(message, code)
        }

        exit(code.rawValue)
    }

    let timer = DispatchSource.makeTimerSource(queue: .main)
    timer.schedule(deadline: .now() + 1, repeating: 1)
    timer.setEventHandler {
        recorder.flushHeaders()
        if let error = recorder.error {
            finish(.failure, "Ошибка записи: \(error)")
        }

        emit(Events.level(mic: recorder.micMeter?.takePeak(), sys: recorder.systemMeter.takePeak()))
    }
    timer.resume()

    let signals = [SIGINT, SIGTERM].map { number in
        signal(number, SIG_IGN)
        let source = DispatchSource.makeSignalSource(signal: number, queue: .main)
        source.setEventHandler { finish(.ok) }
        source.resume()
        return source
    }

    withExtendedLifetime((timer, signals)) {
        dispatchMain()
    }
}

let command: Command
do {
    command = try Arguments.parse(Array(CommandLine.arguments.dropFirst()))
} catch {
    fail("\(error)\n\(Arguments.usage)", .failure)
}

if command != .help {
    emit(Events.hello(version: helperVersion))
}

switch command {
case .help:
    print(Arguments.usage)
case .listDevices:
    listDevices()
case .checkPermissions:
    checkPermissions()
case let .record(outputDirectory, micUID, noMic):
    record(outputDirectory: outputDirectory, micUID: micUID, noMic: noMic)
}
