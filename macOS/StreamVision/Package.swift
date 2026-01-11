// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "StreamVision",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(name: "StreamVision", targets: ["StreamVision"])
    ],
    dependencies: [
        .package(url: "https://github.com/nicklockwood/SwiftFormat", from: "0.52.0"),
    ],
    targets: [
        .executableTarget(
            name: "StreamVision",
            dependencies: [],
            path: "Sources"
        )
    ]
)
