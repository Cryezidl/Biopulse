using UnityEngine;

namespace Biopulse.Neon
{
    // Loops a sprite sequence (menu hero idle, ambient props).
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class NeonFlipbook : MonoBehaviour
    {
        public Sprite[] Frames;
        public float Fps = 8;
        private SpriteRenderer sr;
        private float t;

        private void Awake() => sr = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (Frames == null || Frames.Length == 0) return;
            t += Time.unscaledDeltaTime * Fps;
            sr.sprite = Frames[(int)Mathf.Repeat(t, Frames.Length)];
        }
    }
}
