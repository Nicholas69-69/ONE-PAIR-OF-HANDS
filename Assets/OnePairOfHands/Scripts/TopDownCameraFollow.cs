using UnityEngine;
using UnityEngine.InputSystem;

namespace OnePairOfHands
{
    /// <summary>
    /// Angled top-down follow camera. Smooth follow, scroll-wheel (or two-finger pinch) zoom within limits.
    /// No rotation.
    /// </summary>
    public class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Range(20f, 89f)] private float pitch = 50f;
        [SerializeField, Tooltip("Fixed compass heading of the camera. Never changes at runtime.")]
        private float yaw = 0f;
        [SerializeField] private float distance = 12f;
        [SerializeField] private float minDistance = 12f;
        [SerializeField] private float maxDistance = 72f;
        [SerializeField, Tooltip("Fraction of the distance changed per scroll notch.")]
        private float scrollZoomStep = 0.1f;
        [SerializeField] private float pinchZoomSpeed = 0.05f;
        [SerializeField] private float followSmoothTime = 0.25f;
        [SerializeField] private float zoomSmoothTime = 0.15f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

        private float targetDistance;
        private float distanceVelocity;
        private Vector3 followVelocity;
        private Vector3 focus;
        private float lastPinchDistance = -1f;

        public Transform Target { get => target; set => target = value; }

        private void Start()
        {
            targetDistance = distance = Mathf.Clamp(distance, minDistance, maxDistance);
            if (target != null) focus = target.position + targetOffset;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            ReadZoomInput();
            distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity, zoomSmoothTime);

            if (target != null)
                focus = Vector3.SmoothDamp(focus, target.position + targetOffset, ref followVelocity, followSmoothTime);

            ApplyTransform();
        }

        private void ReadZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float notches = Mathf.Sign(scroll) * Mathf.Max(1f, Mathf.Abs(scroll) / 120f);
                    targetDistance *= 1f - notches * scrollZoomStep;
                }
            }

            var touch = Touchscreen.current;
            if (touch != null && touch.touches.Count >= 2 &&
                touch.touches[0].press.isPressed && touch.touches[1].press.isPressed)
            {
                float d = Vector2.Distance(touch.touches[0].position.ReadValue(), touch.touches[1].position.ReadValue());
                if (lastPinchDistance > 0f) targetDistance -= (d - lastPinchDistance) * pinchZoomSpeed;
                lastPinchDistance = d;
            }
            else
            {
                lastPinchDistance = -1f;
            }

            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        public void SnapToTarget()
        {
            if (target != null) focus = target.position + targetOffset;
            followVelocity = Vector3.zero;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }
    }
}
