using System;
using TMPro;
using UnityEngine;

namespace com.wakapippi
{
    public class TmpEmojiHelper : MonoBehaviour
    {
        private TMP_Text _textComponent;
        private string _originalText;
        private string _replacedText;

        public string Text;

        private void Start()
        {
            _textComponent = GetComponent<TMP_Text>();
            TmpEmojiManager.Instance.SetupTextComponent(_textComponent);
        }

        private void SetText()
        {
            _originalText = Text;
            _replacedText = TmpEmojiManager.Instance.GetEmojiReplacedText(_originalText);
            if (_textComponent != null)
            {
                _textComponent.text = _replacedText;
            }
        }

        private void Update()
        {
            if (_originalText != Text)
            {
                SetText();
            }
        }
    }
}