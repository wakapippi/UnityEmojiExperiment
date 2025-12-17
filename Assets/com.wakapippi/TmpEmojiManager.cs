using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using com.wakapippi.Static;
using TMPro;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using com.wakapippi.Native;
#endif


namespace com.wakapippi
{
    /// <summary>
    /// 絵文字を管理するクラス
    /// </summary>
    public class TmpEmojiManager : MonoBehaviour
    {
        private const string AtlasDirectoryName = "EmojiAtlas";
        public static TmpEmojiManager Instance { get; private set; }

#if (UNITY_IOS && !UNITY_EDITOR)
        const string DllName = "__Internal";    // iOS
#elif (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX
        const string DllName = "EmojiAtlas"; // Mac
#endif

#if (UNITY_IOS && !UNITY_EDITOR) || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX
        [DllImport(DllName, EntryPoint = "create_emoji_atlas")]
        private static extern void CreateEmojiAtlas(string inputPath, string outputDirectory);
#elif (UNITY_ANDROID && !UNITY_EDITOR)
        private static void CreateEmojiAtlas(string inputPath, string outputDirectory)
        {
            using (AndroidJavaClass javaClass = new AndroidJavaClass("com.wakapippi.EmojiAtlas"))
            {
                javaClass.CallStatic("createAtlas", inputPath, outputDirectory);
            }
        }
#elif UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        [DllImport("EmojiAtlas", EntryPoint = "create_emoji_atlas", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CreateEmojiAtlas(string inputPath, string outputDir);
#endif

        [SerializeField] private TextAsset emojiListFile;
        [SerializeField] private TMP_Text sampleText;

        private string _jsonFilePath;
        private string _pngFilePath;
        private TMP_SpriteAsset _spriteAsset;
        private readonly List<Object> _dynamicGeneratedObjects = new List<Object>();
        private readonly CodePointTable _codePointTable = new CodePointTable();

        public void SetupTextComponent(TMP_Text text)
        {
            if (text.spriteAsset != null)
            {
                text.spriteAsset.fallbackSpriteAssets ??= new List<TMP_SpriteAsset>();
                text.spriteAsset.fallbackSpriteAssets.Add(text.spriteAsset);
                return;
            }

            text.spriteAsset = _spriteAsset;
        }

        public string GetEmojiReplacedText(string str)
        {
            // strをまとめてコードポイントに変換する
            var outputCodePoints = new List<int>();
            var codePoints = new List<string>();
            for (var i = 0; i < str.Length; i++)
            {
                var code = char.IsSurrogatePair(str, i)
                    ? char.ConvertToUtf32(str, i++)
                    : str[i];
                codePoints.Add(code.ToString("X4"));
            }

            // codePointsを先頭から順に見ていき、CodePointTableに登録されている絵文字があれば、Private Use Areaのコードポイントに変換する
            for (var i = 0; i < codePoints.Count;)
            {
                var found = false;
                // 最大で8つのコードポイントを組み合わせて絵文字を探す
                for (var len = Math.Min(8, codePoints.Count - i); len > 0; len--)
                {
                    var subList = codePoints.GetRange(i, len);
                    if (_codePointTable.TryGetPrivateCodePoint(subList, out var privateCodePoint))
                    {
                        outputCodePoints.Add(privateCodePoint);
                        i += len;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // 見つからなかった場合、そのまま追加する
                    outputCodePoints.Add(int.Parse(codePoints[i], System.Globalization.NumberStyles.HexNumber));
                    i++;
                }
            }

            // outputCodePointsを文字列に変換して返す
            var sb = new StringBuilder();
            foreach (var cp in outputCodePoints)
            {
                sb.Append(char.ConvertFromUtf32(cp));
            }

            return sb.ToString();
        }

        private void OnEnable()
        {
            if (Instance != null)
            {
                Debug.LogWarning("EmojiManagerがすでに初期化されています");
                return;
            }

            Instance = this;
            CreateAtlasIfNeeded();
            Create();
        }

        private void OnDestroy()
        {
            foreach (var obj in _dynamicGeneratedObjects)
            {
                Destroy(obj);
            }

            _dynamicGeneratedObjects.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private bool SetAtlasPath(string directoryPath)
        {
            // ファイルの有無をチェックする
            var jsonFiles = Directory.GetFiles(directoryPath, "*.json");
            var pngFiles = Directory.GetFiles(directoryPath, "*.png");
            if (jsonFiles.Length <= 0 || pngFiles.Length <= 0) return false;
            _jsonFilePath = jsonFiles[0];
            _pngFilePath = pngFiles[0];
            return true;
        }

        private void Create()
        {
            _spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            _spriteAsset.name = "EmojiSpriteAsset";
            var shader = Shader.Find("TextMeshPro/Sprite");
            var material = new Material(shader);
            _dynamicGeneratedObjects.Add(material);
            _spriteAsset.material = material;

            var texture = new Texture2D(2, 2);
            _dynamicGeneratedObjects.Add(texture);
            var imageBytes = File.ReadAllBytes(_pngFilePath);
            texture.LoadImage(imageBytes, true);
            _spriteAsset.spriteSheet = texture;
            material.mainTexture = texture;
            _spriteAsset.spriteInfoList = new List<TMP_Sprite>();

            var jsonString = File.ReadAllText(_jsonFilePath);
            var frames = JsonUtility.FromJson<Frames>(jsonString);
            var index = 0;

            foreach (var frame in frames.frames)
            {
                // UIKitの座標系では、左上が(0,0)だが、Unityでは、左下が(0,0）なので変換しておく必要がある
                var unityY = texture.height - frame.frame.y - frame.frame.h;
                var sprite = Sprite.Create(
                    texture,
                    new Rect(frame.frame.x, unityY, frame.frame.w, frame.frame.h),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                var spriteInfo = new TMP_Sprite
                {
                    name = frame.name,
                    unicode = 0,
                    pivot = new Vector2(0.5f, 0.5f),
                    xAdvance = 0,
                    scale = 1f,
                    sprite = sprite,
                };
                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)index,
                    sprite = sprite,
                    scale = 1f,
                    glyphRect = new GlyphRect(
                        frame.frame.x,
                        unityY,
                        frame.frame.w,
                        frame.frame.h
                    ),
                    metrics = new GlyphMetrics(
                        frame.frame.h,
                        frame.frame.w,
                        0,
                        frame.frame.h,
                        frame.frame.w
                    )
                };

                _spriteAsset.spriteGlyphTable.Add(glyph);

                // Unicodeは、U+F0000〜U+FFFFDのPrivate Use Areaを使う
                var privateCodePoint = 0xf0000 + index;
                _spriteAsset.spriteCharacterTable.Add(
                    new TMP_SpriteCharacter((uint)privateCodePoint, _spriteAsset, glyph)
                );
                _spriteAsset.spriteInfoList.Add(spriteInfo);

                // Code PointからPrivate Use Areaへの変換用のテーブルを作る
                _codePointTable.AddEntry(frame.codePoint, privateCodePoint);
                index++;
            }

            _spriteAsset.UpdateLookupTables();
        }

        private void CreateAtlasIfNeeded()
        {
            // emojiListFileをApplication.persistentDataPath / AtlasDirectoryName に保存する
            var directoryPath = Path.Combine(Application.persistentDataPath, AtlasDirectoryName);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            else
            {
                // ファイルの有無をチェックする
                if (SetAtlasPath(directoryPath))
                {
                    return;
                }
            }

            var filePath = Path.Combine(directoryPath, emojiListFile.name + ".txt");
            File.WriteAllText(filePath, emojiListFile.text);
            CreateEmojiAtlas(filePath, directoryPath);
            SetAtlasPath(directoryPath);
        }
    }

    [Serializable]
    public class FrameData
    {
        public int x;
        public int y;
        public int h;
        public int w;
    }

    [Serializable]
    public class Emoji
    {
        public FrameData frame;
        public string name;
        public string codePoint;
    }

    [Serializable]
    public class Frames
    {
        public List<Emoji> frames;
    }
}