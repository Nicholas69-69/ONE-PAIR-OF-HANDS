using UnityEngine;
using UnityEngine.AI;

namespace OnePairOfHands
{
    /// <summary>
    /// Visuals only for Ataa's Mixamo model. The NavMeshAgent on the parent drives all movement;
    /// root motion is off. This script:
    ///  - feeds a smoothed "Speed" (agent velocity) into the Animator's Locomotion blend tree,
    ///    and sets "AnimSpeed" so the Walking clip plays at the rate that matches the ground speed (no foot sliding);
    ///  - turns the model smoothly to face where she is going;
    ///  - sets "Working" and blends in a procedural working pose (forward lean + hands reaching
    ///    forward/down via Animator IK), since there is no Mixamo clip for it;
    ///  - shows the jerrycan in her right hand only while she is carrying water.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AtaaAnimatorDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AnimSpeedId = Animator.StringToHash("AnimSpeed");
        private static readonly int WorkingId = Animator.StringToHash("Working");

        [SerializeField] private NavMeshAgent agent;

        [Header("Locomotion")]
        [SerializeField, Tooltip("Ground speed (m/s) at which the Walking clip's feet match the ground at normal playback. Measured by the character builder.")]
        private float walkClipSpeed = 1.3f;
        [SerializeField] private float speedSmoothTime = 0.12f;
        [SerializeField, Tooltip("How quickly she turns to face her movement direction.")]
        private float turnSharpness = 9f;

        [Header("Working (procedural)")]
        [SerializeField] private float workBlendSeconds = 0.3f;
        [SerializeField] private float leanDegrees = 20f;
        [SerializeField, Tooltip("Hand target relative to her feet: x = half the gap between hands, y = height, z = forward.")]
        private Vector3 handReach = new Vector3(0.15f, 0.82f, 0.52f);
        [SerializeField] private float handBob = 0.035f;
        [SerializeField, Range(0f, 1f)] private float handIkWeight = 1f;

        [Header("Carrying")]
        [SerializeField] private GameObject jerrycan;
        [SerializeField, Tooltip("How far below the right hand the jerrycan's handle hangs (m).")]
        private float jerrycanDrop = 0.13f;

        private Animator animator;
        private Transform spine, chest, rightHand;
        private float speed, speedVelocity, work;
        private Quaternion facing;
        private TaskManager taskManager;
        private WaterTaskAction water;

        public float SmoothedSpeed => speed;
        public float WorkWeight => work;
        public bool JerrycanVisible => jerrycan != null && jerrycan.activeSelf;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            facing = transform.rotation;
            if (jerrycan != null) jerrycan.SetActive(false);
        }

        private void Start()
        {
            taskManager = TaskManager.Instance;
            if (taskManager != null) water = taskManager.Water;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 v = agent != null ? agent.velocity : Vector3.zero;
            v.y = 0f;
            float target = v.magnitude;
            if (target < 0.05f) target = 0f;
            speed = Mathf.SmoothDamp(speed, target, ref speedVelocity, speedSmoothTime);
            if (speed < 0.01f) speed = 0f;

            animator.SetFloat(SpeedId, speed);
            // Past the clip's natural speed, play it faster so the planted foot keeps pace with the ground.
            animator.SetFloat(AnimSpeedId, speed > walkClipSpeed ? speed / walkClipSpeed : 1f);

            // Smoothly face the direction of travel (independent of the agent's own rotation).
            if (v.sqrMagnitude > 0.04f) facing = Quaternion.LookRotation(v.normalized, Vector3.up);

            bool working = taskManager != null && taskManager.IsWorking && speed < 0.3f;
            if (working)
            {
                // Turn towards what she is working on (the fire, well, tent or screen under the task's label).
                var focus = taskManager.ActiveTask.transform.Find("Label");
                if (focus != null)
                {
                    Vector3 d = focus.position - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.16f) facing = Quaternion.LookRotation(d.normalized, Vector3.up);
                }
            }
            animator.SetBool(WorkingId, working);
            work = Mathf.MoveTowards(work, working ? 1f : 0f, dt / Mathf.Max(0.01f, workBlendSeconds));
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, 1f - Mathf.Exp(-turnSharpness * dt));

            if (jerrycan != null)
            {
                bool carry = water != null && water.IsCarrying;
                if (jerrycan.activeSelf != carry) jerrycan.SetActive(carry);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            float w = Mathf.SmoothStep(0f, 1f, work) * handIkWeight;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            if (w <= 0.001f) return;

            Vector3 root = transform.position;
            Vector3 fwd = transform.forward, right = transform.right;
            float t = Time.time * 7f;
            Vector3 centre = root + Vector3.up * handReach.y + fwd * handReach.z;
            animator.SetIKPosition(AvatarIKGoal.LeftHand, centre - right * handReach.x + Vector3.up * Mathf.Sin(t) * handBob);
            animator.SetIKPosition(AvatarIKGoal.RightHand, centre + right * handReach.x + Vector3.up * Mathf.Sin(t + Mathf.PI) * handBob);
        }

        private void LateUpdate()
        {
            float w = Mathf.SmoothStep(0f, 1f, work);
            if (w > 0.001f)
            {
                // Forward lean, split between spine and chest so it bends rather than tips.
                Vector3 axis = transform.right;
                if (spine != null) spine.rotation = Quaternion.AngleAxis(leanDegrees * 0.55f * w, axis) * spine.rotation;
                if (chest != null) chest.rotation = Quaternion.AngleAxis(leanDegrees * 0.45f * w, axis) * chest.rotation;
            }

            // The jerrycan is parented to the right hand bone but hangs upright under it.
            if (jerrycan != null && jerrycan.activeSelf && rightHand != null)
                jerrycan.transform.SetPositionAndRotation(
                    rightHand.position + Vector3.down * jerrycanDrop,
                    Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }
    }
}
