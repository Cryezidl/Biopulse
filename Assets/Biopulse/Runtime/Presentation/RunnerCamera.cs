using UnityEngine;

namespace Biopulse.Presentation
{
    public sealed class RunnerCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(4, 1.5f, -10);
        [SerializeField, Min(.001f)] private float followSeconds = .15f;
        private Vector3 velocity;
        public Transform Target { get => target; set => target = value; }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset,
                ref velocity, followSeconds, Mathf.Infinity, Time.deltaTime);
        }
    }
}
