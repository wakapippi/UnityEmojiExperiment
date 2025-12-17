using System.Collections.Generic;

namespace com.wakapippi.Static
{
    public class CodePointTable
    {
        private readonly Entry _entryRoot = new Entry();

        public void AddEntry(string codePoint, int privateCodePoint)
        {
            // codePointは、スペース区切りになっているので、分割して処理する
            var codePoints = codePoint.Split(' ');
            var currentEntry = _entryRoot;
            foreach (var cpStr in codePoints)
            {
                if (!currentEntry.CodePointToEntry.ContainsKey(cpStr))
                {
                    currentEntry.CodePointToEntry[cpStr] = new Entry();
                }
                currentEntry = currentEntry.CodePointToEntry[cpStr];
                // 最後の場合、PrivateCodePointを設定する
                if (cpStr == codePoints[^1])
                {
                    currentEntry.PrivateCodePoint = privateCodePoint;
                }
            }
        }
        
        public bool TryGetPrivateCodePoint(List<string> codePoints, out int privateCodePoint)
        {
            var currentEntry = _entryRoot;
            foreach (var cpStr in codePoints)
            {
                if (!currentEntry.CodePointToEntry.TryGetValue(cpStr, out var value))
                {
                    privateCodePoint = 0;
                    return false;
                }
                currentEntry = value;
            }
            privateCodePoint = currentEntry.PrivateCodePoint;
            return privateCodePoint != 0;
        }   
    }

    class Entry
    {
        public int PrivateCodePoint { get; set; } = 0;
        public Dictionary<string, Entry> CodePointToEntry { get; set; } = new Dictionary<string, Entry>();
    }
}