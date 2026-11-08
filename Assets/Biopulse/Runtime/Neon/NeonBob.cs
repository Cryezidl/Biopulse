using UnityEngine;

namespace Biopulse.Neon
{
    // Gentle hover for drones and floating props. Purely visual; colliders live on the parent.
    public sealed class NeonBob : MonoBehaviour
    {
        public float Amplitude = .06f, Speed = 2.2f, Tilt = 3f;
        private Vector3 origin;
        private float phase;

        private void Awake() { origin = transform.localPosition; phase = transform.position.x * .37f; }

        private void Update()
        {
            float t = Time.time * Speed + phase;
            transform.localPosition = origin + new Vector3(0, Mathf.Sin(t) * Amplitude, 0);
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * .5f) * Tilt);
        }
    }
}
