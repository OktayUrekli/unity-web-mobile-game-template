using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Testing.ThreeD
{
    /// <summary>
    /// Rigidbody player for the 3D test scene. Keyboard/gamepad use the project-wide "Player/Move"
    /// action; touch (or mouse) held on the screen moves the player toward that point on the ground.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TestPlayer3D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float pointerStopDistance = 0.3f;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float respawnBelowY = -10f;

        private Rigidbody _rigidbody;
        private InputAction _moveAction;
        private Vector3 _moveDirection;
        private Vector3 _spawnPosition;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _spawnPosition = transform.position;
            if (viewCamera == null)
                viewCamera = Camera.main;

            InputActionAsset actions = InputSystem.actions;
            _moveAction = actions != null ? actions.FindAction("Player/Move") : null;
            if (_moveAction == null)
                Debug.LogWarning("TestPlayer3D: project-wide action 'Player/Move' not found; only pointer input works.");
        }

        private void OnEnable() => _moveAction?.Enable();

        private void Update()
        {
            Vector2 stick = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _moveDirection = stick.sqrMagnitude > 0.01f
                ? CameraRelative(stick)
                : PointerDirection();
        }

        private void FixedUpdate()
        {
            if (_rigidbody.position.y < respawnBelowY)
            {
                _rigidbody.position = _spawnPosition;
                _rigidbody.linearVelocity = Vector3.zero;
            }

            Vector3 velocity = _moveDirection * moveSpeed;
            velocity.y = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = velocity;
        }

        private Vector3 CameraRelative(Vector2 input)
        {
            if (viewCamera == null)
                return Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        private Vector3 PointerDirection()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || viewCamera == null || !pointer.press.isPressed)
                return Vector3.zero;

            // Intersect with a horizontal plane at the player's height; no physics raycast needed.
            Ray ray = viewCamera.ScreenPointToRay(pointer.position.ReadValue());
            var ground = new Plane(Vector3.up, transform.position);
            if (!ground.Raycast(ray, out float distance))
                return Vector3.zero;

            Vector3 toTarget = ray.GetPoint(distance) - transform.position;
            toTarget.y = 0f;
            return toTarget.magnitude > pointerStopDistance ? toTarget.normalized : Vector3.zero;
        }
    }
}
