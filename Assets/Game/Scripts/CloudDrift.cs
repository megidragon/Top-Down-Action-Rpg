using UnityEngine;

namespace TinyRpg
{
    /// Nubes decorativas que se desplazan lentamente y reaparecen por el otro lado.
    public class CloudDrift : MonoBehaviour
    {
        public float speed = 0.5f;
        public float wrapMinX = -15f;
        public float wrapMaxX = 111f;

        void Update()
        {
            var p = transform.position;
            p.x += speed * Time.deltaTime;
            if (p.x > wrapMaxX) p.x = wrapMinX;
            transform.position = p;
        }
    }
}
