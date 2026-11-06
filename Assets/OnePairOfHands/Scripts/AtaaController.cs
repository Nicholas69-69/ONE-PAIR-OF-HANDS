using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OnePairOfHands
{
    /// <summary>
    /// Point-and-click (mouse click or touch tap) movement on the NavMesh, using the new Input System.
    /// Clicking a task location walks Ataa there. When she is standing at a task, clicking/tapping
    /// on that task (or on Ataa) performs its action instead; holding counts as holding the action.
    /// Space bar also performs/holds the action.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AtaaController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private LayerMask clickMask = ~0;
        [SerializeField, Min(0.5f), Tooltip("Clicks this close to Ataa count as the task action when she is at a task.")]
        private float actionRadiusAroundAtaa = 3f;
        [SerializeField, Min(0.5f)] private float navSampleRadius = 4f;
        [SerializeField] private Transform clickMarker;

        private NavMeshAgent agent;
        private bool pointerActionHeld;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public NavMeshAgent Agent => agent;

        /// <summary>Set by the UI flow while an overlay (opening, pause, leave, end) is open.</summary>
        public static bool InputBlocked { get; set; }

        public bool IsMoving =>
            agent != null && agent.isOnNavMesh &&
            (agent.pathPending ||
             (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.05f) ||
             agent.velocity.sqrMagnitude > 0.05f);

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void Update()
        {
            var tm = TaskManager.Instance;
            if (tm != null && tm.DayOver)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                pointerActionHeld = false;
                return;
            }

            if (InputBlocked)
            {
                pointerActionHeld = false;
            }
            else
            {
                HandlePointer(tm);
                HandleKeyboard(tm);
            }

            if (clickMarker != null && clickMarker.gameObject.activeSelf && !IsMoving)
                clickMarker.gameObject.SetActive(false);
        }

        private void HandlePointer(TaskManager tm)
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                Vector2 screenPos = pointer.position.ReadValue();
                if (IsOverUI(screenPos)) return;

                if (!TryGetWorldPoint(screenPos, out Vector3 point, out bool hitAtaa)) return;

                TaskState here = tm != null ? tm.ActiveTask : null;
                TaskState clicked = tm != null ? tm.TaskNear(point) : null;
                bool nearAtaa = hitAtaa || FlatDistance(point, transform.position) <= actionRadiusAroundAtaa;

                if (here != null && (clicked == here || nearAtaa))
                {
                    pointerActionHeld = true;
                    tm.Tap();
                    tm.Hold(Time.deltaTime);
                    return;
                }

                pointerActionHeld = false;
                if (clicked != null) MoveTo(clicked.StandPoint);
                else MoveTo(point);
                return;
            }

            if (pointerActionHeld)
            {
                if (pointer.press.isPressed) tm?.Hold(Time.deltaTime);
                else pointerActionHeld = false;
            }
        }

        private void HandleKeyboard(TaskManager tm)
        {
            var kb = Keyboard.current;
            if (kb == null || tm == null) return;
            if (kb.spaceKey.wasPressedThisFrame) tm.Tap();
            if (kb.spaceKey.isPressed) tm.Hold(Time.deltaTime);
        }

        public void MoveTo(Vector3 worldPoint)
        {
            if (!agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                if (clickMarker != null)
                {
                    clickMarker.position = hit.position + Vector3.up * 0.05f;
                    clickMarker.gameObject.SetActive(true);
                }
            }
        }

        private bool TryGetWorldPoint(Vector2 screenPos, out Vector3 point, out bool hitAtaa)
        {
            point = default;
            hitAtaa = false;
            if (viewCamera == null) return false;
            Ray ray = viewCamera.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, 1000f, clickMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<AtaaController>() == this)
                {
                    hitAtaa = true;
                    continue;
                }
                point = h.point;
                return true;
            }
            if (hitAtaa)
            {
                point = transform.position;
                return true;
            }
            return false;
        }

        private bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
