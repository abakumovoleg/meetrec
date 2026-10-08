// swift-tools-version:5.10
import PackageDescription

let package = Package(
    name: "meetrec-capture",
    platforms: [.macOS("14.2")],
    targets: [
        .target(name: "CaptureKit"),
        .executableTarget(
            name: "meetrec-capture",
            dependencies: ["CaptureKit"],
            linkerSettings: [
                // A command-line tool has no bundle, so the usage descriptions are embedded into the binary itself.
                .unsafeFlags([
                    "-Xlinker", "-sectcreate", "-Xlinker", "__TEXT", "-Xlinker", "__info_plist",
                    "-Xlinker", Context.packageDirectory + "/Info.plist",
                ]),
            ]
        ),
        .testTarget(name: "CaptureKitTests", dependencies: ["CaptureKit"]),
    ]
)
