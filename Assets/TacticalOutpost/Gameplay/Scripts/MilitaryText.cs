using TMPro;
using UnityEngine;

namespace Outpost.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class MilitaryText : MonoBehaviour
    {
        TMP_Text label;
        float revealed;
        int total;
        string lastSpoken;
        public bool IsTyping => label != null && label.maxVisibleCharacters < total;
        void Awake() { label = GetComponent<TMP_Text>(); }
        public void Speak(string value)
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (lastSpoken == value && label.text == value) return;
            lastSpoken = value;
            label.text = value; label.ForceMeshUpdate(true);
            total = label.textInfo.characterCount; revealed = 0; label.maxVisibleCharacters = 0;
        }
        public void Instant(string value)
        {
            if (label == null) label = GetComponent<TMP_Text>();
            lastSpoken = null; label.text = value; label.maxVisibleCharacters = int.MaxValue; total = 0;
        }
        void Update()
        {
            if (!IsTyping || GameMenu.Instance != null && GameMenu.Instance.IsPaused) return;
            revealed += Time.unscaledDeltaTime * 75f;
            int count = Mathf.Min(total, Mathf.FloorToInt(revealed));
            if (count <= label.maxVisibleCharacters) return;
            label.maxVisibleCharacters = count;
            if (count > 0 && !char.IsWhiteSpace(label.textInfo.characterInfo[count - 1].character)) Outpost.Audio.OutpostAudio.RadioTick();
        }
    }
}
