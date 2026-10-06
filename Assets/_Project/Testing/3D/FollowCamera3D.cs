using UnityEngine;

namespace _Project.Testing.ThreeD
{
    /// <summary>
    /// Smoothly follows a target at a fixed world-space offset and looks at it.
    /// </summary>
    public class FollowCamera3D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 8f, -8f);
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (target == null)
                return;

            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref _velocity, smoothTime);
            transform.LookAt(target.position);
        }
    }
}
