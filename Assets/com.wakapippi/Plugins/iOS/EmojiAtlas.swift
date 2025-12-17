import UIKit

struct FrameRect: Encodable {
    let x: CGFloat
    let y: CGFloat
    let w: CGFloat
    let h: CGFloat
}

struct EmojiFrame: Encodable {
    let name: String
    let codePoint: String
    let frame: FrameRect
}

struct AtlasMetadata: Encodable {
    let imageName: String
    let frames: [EmojiFrame]
}


@_cdecl("create_emoji_atlas")
public func createEmojiAtlasWrapper(inputPath: UnsafePointer<CChar>, outputDir: UnsafePointer<CChar>) {
    let inputPathString = String(cString: inputPath)
    let outputDirString = String(cString: outputDir)
    
    let emojiAtlas = EmojiAtlas()
    emojiAtlas.createAtlas(inputPath: inputPathString, outputDirectory: outputDirString)
}

class EmojiAtlas {
    private let tileSize: CGFloat = 34
    private let fontSize: CGFloat = 32
    private let outputImageName = "emoji_atlas.png"
    private let outputJsonName = "emoji_atlas.json"

    public func createAtlas(inputPath: String, outputDirectory: String) {
        guard let emojis = loadEmojis(from: inputPath) else {
            return
        }

        let count = CGFloat(emojis.count)
        let columns = ceil(sqrt(count))
        let rows = ceil(count / columns)
        
        let atlasWidth = columns * tileSize
        let atlasHeight = rows * tileSize
        let atlasSize = CGSize(width: atlasWidth, height: atlasHeight)
        
        var frames: [EmojiFrame] = []
        let renderer = UIGraphicsImageRenderer(size: atlasSize)
        
        let atlasImage = renderer.image { context in
            let font = UIFont.systemFont(ofSize: fontSize)
            let paragraphStyle = NSMutableParagraphStyle()
            paragraphStyle.alignment = .center
            
            let attributes: [NSAttributedString.Key: Any] = [
                .font: font,
                .paragraphStyle: paragraphStyle
            ]
            
            var validCount = 0
            for (index, item) in emojis.enumerated() {
                let emoji = item.char
                let col = CGFloat(index % Int(columns))
                let row = CGFloat(index / Int(columns))
                
                let x = col * tileSize
                let y = row * tileSize
                
                let stringSize = (emoji as NSString).size(withAttributes: attributes)
                if stringSize.width > tileSize {
                    print("未対応の絵文字: \(emoji)")
                    continue
                }
                
                let drawRect = CGRect(
                    x: x,
                    y: y + (tileSize - stringSize.height) / 2,
                    width: tileSize,
                    height: stringSize.height
                )
                
                (emoji as NSString).draw(in: drawRect, withAttributes: attributes)
                
                let frameData = EmojiFrame(
                    name: emoji,
                    codePoint: item.codePoint,
                    frame: FrameRect(x: x, y: y, w: tileSize, h: tileSize)
                )
                frames.append(frameData)
                validCount += 1
            }
        }
        
        save(image: atlasImage, frames: frames, to: outputDirectory)
    }

    private func loadEmojis(from path: String) -> [(char: String, codePoint: String)]? {
        let url = URL(fileURLWithPath: path)

        do {
            let content = try String(contentsOf: url, encoding: .utf8)
            let lines = content.components(separatedBy: .newlines)
            
            var validEmojis: [(char: String, codePoint: String)] = []
            
            for line in lines {
                let trimmedLine = line.trimmingCharacters(in: .whitespaces)
                if trimmedLine.isEmpty || trimmedLine.hasPrefix("#") {
                    continue
                }
                
                let parts = trimmedLine.components(separatedBy: ";")
                guard let codePointPart = parts.first else { continue }
                
                if let emoji = parseCodePoints(codePointPart) {
                    let cleanCodePoint = codePointPart.trimmingCharacters(in: .whitespaces)
                    validEmojis.append((char: emoji, codePoint: cleanCodePoint))
                }
            }
            return validEmojis.isEmpty ? nil : validEmojis
            
        } catch {
            print("ファイル読み込みエラー: \(error)")
            return nil
        }
    }

    private func parseCodePoints(_ rawString: String) -> String? {
        let hexParts = rawString.trimmingCharacters(in: .whitespaces).components(separatedBy: " ")
        var scalars: [UnicodeScalar] = []
        
        for hex in hexParts {
            if hex.isEmpty { continue }
            if let code = UInt32(hex, radix: 16), let scalar = UnicodeScalar(code) {
                scalars.append(scalar)
            }
        }
        
        if scalars.isEmpty { return nil }
        
        var emojiString = ""
        emojiString.unicodeScalars.append(contentsOf: scalars)
        return emojiString
    }

    private func save(image: UIImage, frames: [EmojiFrame], to directoryPath: String) {
        let metadata = AtlasMetadata(imageName: outputImageName, frames: frames)
        let encoder = JSONEncoder()
        encoder.outputFormatting = .prettyPrinted

        guard let jsonData = try? encoder.encode(metadata),
              let pngData = image.pngData() else {
            print("データ変換に失敗しました")
            return
        }

        // ディレクトリURLの構築
        let outputUrl = URL(fileURLWithPath: directoryPath, isDirectory: true)
        
        // ディレクトリが存在しない場合は作成を試みる（オプション）
        try? FileManager.default.createDirectory(at: outputUrl, withIntermediateDirectories: true)

        let imagePath = outputUrl.appendingPathComponent(outputImageName)
        let jsonPath = outputUrl.appendingPathComponent(outputJsonName)

        do {
            try pngData.write(to: imagePath)
            try jsonData.write(to: jsonPath)
        } catch {
            print("ファイル書き込みエラー: \(error)")
        }
    }
}
