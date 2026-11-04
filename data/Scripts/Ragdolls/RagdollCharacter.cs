using System.Collections;
using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A mannequin that walks a loop and reacts to hits with its <see cref="RagdollComponent"/>:
    /// <list type="bullet">
    /// <item>A light hit staggers it: the bodies from the hit bone down simulate, the joint drives pull them back to the
    /// animation, and after a moment they blend back. Only that limb (or the upper body) reacts.</item>
    /// <item>A hard hit, or enough light ones, knocks it down. The body keeps the momentum the walk gave it and falls
    /// with some muscle tone that fades, so it topples instead of folding where it stands.</item>
    /// <item>Once the body has lain still it rolls onto its back if it fell on its front. The mannequin is then moved
    /// under its hips, turned to how it lies, and blends into a get-up animation.</item>
    /// </list>
    /// </summary>
    public class RagdollCharacter : ScriptComponent
    {
        public enum Mood
        {
            Walking,
            Standing,
            Down,
            GettingUp,
        }

        [Header("Animation")]
        public AnimationClip walkClip;
        public AnimationClip idleClip;

        [Tooltip("Played to get up from lying on the back.")]
        public AnimationClip getUpClip;

        [Tooltip("Hips to head in the get-up animation's first pose, in the character's space.")]
        public Vector3 getUpHeadDirection = new Vector3(0.0f, 0.0f, -1.0f);

        [Tooltip("The hips in the get-up animation's first pose, in the character's space. The character is placed so " +
                 "that they land where the ragdoll's hips lie.")]
        public Vector3 getUpHipsOffset = new Vector3(0.0f, 0.0f, -0.24f);

        [Tooltip("Seconds the get-up animation crossfades into walking or standing at its end.")]
        public float getUpExitBlend = 0.3f;

        [Header("Walk")]
        [Tooltip("Whether the mannequin walks its loop or stands.")]
        public bool walks = true;

        [Tooltip("Center of the walked circle.")]
        public Vector3 loopCenter;

        [Tooltip("Radius of the walked circle, meters.")]
        public float loopRadius = 3.0f;

        [Tooltip("Walking speed, meters per second.")]
        public float walkSpeed = 1.1f;

        [Header("Hits")]
        [Tooltip("An impulse at least this strong knocks the mannequin down, newton seconds.")]
        public float knockdownImpulse = 90.0f;

        [Tooltip("Light hits add up; this much in a few seconds knocks it down too.")]
        public float knockdownDamage = 160.0f;

        [Tooltip("Seconds a staggered limb stays simulated before blending back.")]
        public float staggerSeconds = 0.45f;

        [Header("Falling")]
        [Tooltip("How much of the joint drives' strength the body keeps as it is knocked down, 0..1.")]
        [Range(0, 1)]
        public float fallStiffness = 0.6f;

        [Tooltip("Seconds that strength takes to fade to a limp body.")]
        public float fallLimpSeconds = 0.8f;

        [Tooltip("Seconds lying still before getting up.")]
        public float restBeforeGetUp = 1.2f;

        [Tooltip("Radians per second the body turns at to roll from its front onto its back.")]
        public float rollOverSpeed = 10.5f;

        [Header("Sounds")]
        public AudioClip[] impactSounds;

        /// <summary>Rays that find the floor under the hips start this far above them, meters.</summary>
        private const float GroundProbeHeight = 0.5f;

        /// <summary>How far below the hips the floor may be, meters.</summary>
        private const float GroundProbeDepth = 3.0f;

        /// <summary>Roll-over attempts before getting up the way the body lies.</summary>
        private const int RollOverAttempts = 3;

        /// <summary>How much faster each roll-over attempt turns than the first.</summary>
        private const float RollOverGrowth = 0.2f;

        /// <summary>Half the torso's width and depth, meters: where the edge it rolls over lies.</summary>
        private const float RollOverHalfWidth = 0.18f;
        private const float RollOverHalfDepth = 0.11f;

        /// <summary>Seconds a rolling body may take to settle on its back.</summary>
        private const float RollOverTimeout = 2.5f;

        private RagdollComponent ragdoll;
        private AnimationComponent animator;
        private AudioSourceComponent voice;
        private float angle;
        private float damage;
        private float staggerTimer;
        private string staggeredBone;
        private Mood mood;

        /// <summary>What the mannequin does now.</summary>
        public Mood currentMood => mood;

        /// <summary>The character a bone entity belongs to, or null: what a ray cast's hit entity maps to.</summary>
        public static RagdollCharacter Find(Entity bone)
        {
            for (Entity current = bone; current; current = current.transform.parent)
            {
                var character = current.GetComponent<RagdollCharacter>();
                if (character != null)
                {
                    return character;
                }
            }
            return null;
        }

        public override void OnStart()
        {
            ragdoll = owner.GetComponent<RagdollComponent>();
            animator = owner.GetComponent<AnimationComponent>();
            voice = owner.GetComponent<AudioSourceComponent>();
            Vector3 offset = transform.position - loopCenter;
            angle = Mathf.Atan2(offset.x, offset.z);
            StartMood(walks ? Mood.Walking : Mood.Standing);
        }

        public override void OnUpdate()
        {
            damage = Mathf.MoveTowards(damage, 0.0f, 25.0f * Time.deltaTime);
            if (mood == Mood.Walking)
            {
                WalkLoop();
            }
            UpdateStagger();
        }

        /// <summary>
        /// Takes a hit on a bone: a stagger or a knockdown by the impulse (newton seconds) and the damage so far.
        /// </summary>
        public void TakeHit(Entity bone, Vector3 point, Vector3 impulse)
        {
            HitBody(ragdoll.FindBody(bone), point, impulse);
        }

        /// <summary>Hits the body of a bone given by name, at its center: for tools and tests.</summary>
        public void HitBone(string bone, Vector3 impulse)
        {
            int body = ragdoll.FindBody(bone);
            if (body >= 0)
            {
                HitBody(body, ragdoll.GetBodyPosition(body), impulse);
            }
        }

        /// <summary>Knocks the mannequin down from outside (explosions, launch pads): every body simulates.</summary>
        public void KnockDown()
        {
            if (mood == Mood.Down)
            {
                return;
            }
            StopAllCoroutines();
            BeginFall();
        }

        private void BeginFall()
        {
            staggeredBone = null;
            damage = 0.0f;
            mood = Mood.Down;
            ragdoll.Power();
            ragdoll.driveStrength = fallStiffness;
            StartCoroutine(LieDown());
        }

        /// <summary>
        /// Stands the mannequin at a point, facing a yaw in degrees, and after <paramref name="windUp"/> seconds knocks
        /// it down with every body moving at <paramref name="velocity"/>: a push off a ledge, a catapult shot.
        /// </summary>
        public void Launch(Vector3 position, float yaw, Vector3 velocity, float windUp)
        {
            StopAllCoroutines();
            staggeredBone = null;
            StartCoroutine(LaunchFrom(position, yaw, velocity, windUp));
        }

        private IEnumerator LaunchFrom(Vector3 position, float yaw, Vector3 velocity, float windUp)
        {
            ragdoll.Recover(0.0f);
            transform.position = position;
            transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
            ragdoll.ResetToAnimation();
            StartMood(Mood.Standing, 0.0f);
            yield return new WaitForSeconds(windUp);
            BeginFall();
            ragdoll.AddVelocity(velocity);
        }

        private void HitBody(int body, Vector3 point, Vector3 impulse)
        {
            if (body < 0)
            {
                return;
            }
            PlayImpact(impulse.magnitude);
            if (mood == Mood.Down || mood == Mood.GettingUp && ragdoll.state != RagdollState.Animated)
            {
                ragdoll.AddImpulse(body, impulse, point);
                return;
            }
            damage += impulse.magnitude;
            if (impulse.magnitude >= knockdownImpulse || damage >= knockdownDamage)
            {
                KnockDown();
                ragdoll.AddImpulse(body, impulse, point);
                return;
            }
            Stagger(body, point, impulse);
        }

        private void Stagger(int body, Vector3 point, Vector3 impulse)
        {
            // A hit on the hips or the legs staggers the whole upper body; elsewhere the hit bone and what hangs on it.
            string bone = ragdoll.GetBodyBone(body);
            string simulated = IsLowerBody(bone) ? "spine_01" : bone;
            if (staggeredBone != null && staggeredBone != simulated)
            {
                ragdoll.SetBoneSimulated(staggeredBone, false, true, 0.3f);
            }
            ragdoll.SetBoneSimulated(simulated, true, true);
            ragdoll.AddImpulse(body, impulse, point);
            staggeredBone = simulated;
            staggerTimer = staggerSeconds;
        }

        private void UpdateStagger()
        {
            if (staggeredBone == null)
            {
                return;
            }
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0.0f)
            {
                ragdoll.SetBoneSimulated(staggeredBone, false, true, 0.35f);
                staggeredBone = null;
            }
        }

        private static bool IsLowerBody(string bone)
        {
            string name = bone.ToLowerInvariant();
            return name.Contains("pelvis") || name.Contains("thigh") || name.Contains("calf") || name.Contains("foot");
        }

        private IEnumerator LieDown()
        {
            yield return FadeToLimp();
            yield return new WaitUntil(() => ragdoll.restTime > restBeforeGetUp);
            for (int attempt = 0; attempt < RollOverAttempts && !ragdoll.isFaceUp; ++attempt)
            {
                yield return RollOntoBack(attempt);
            }
            yield return GetUp();
        }

        private IEnumerator FadeToLimp()
        {
            float rate = fallStiffness / Mathf.Max(fallLimpSeconds, 0.01f);
            while (ragdoll.driveStrength > 0.0f)
            {
                ragdoll.driveStrength = Mathf.MoveTowards(ragdoll.driveStrength, 0.0f, rate * Time.deltaTime);
                yield return null;
            }
            ragdoll.Simulate();
            ragdoll.driveStrength = 1.0f;
        }

        /// <summary>
        /// Turns the whole body about the edge it lies on, as someone rolling over does, so it tips onto its back; then
        /// waits for it to settle. Each attempt turns faster than the one before.
        /// </summary>
        private IEnumerator RollOntoBack(int attempt)
        {
            int chest = ragdoll.FindBody("spine_03");
            int hips = ragdoll.FindBody("pelvis");
            if (chest < 0 || hips < 0)
            {
                yield break;
            }
            Vector3 chestPosition = ragdoll.GetBodyPosition(chest);
            Vector3 hipsPosition = ragdoll.GetBodyPosition(hips);
            Vector3 length = chestPosition - hipsPosition;
            length.y = 0.0f;
            length.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, length);
            // The edge the body tips over: beside the torso on the floor, away from the side that lifts.
            Vector3 edge = (chestPosition + hipsPosition) * 0.5f - side * RollOverHalfWidth - Vector3.up * RollOverHalfDepth;
            ragdoll.AddAngularVelocity(length * (rollOverSpeed * (1.0f + RollOverGrowth * attempt)), edge);
            float waited = 0.0f;
            yield return new WaitForSeconds(0.3f);
            while (ragdoll.restTime < 0.5f && waited < RollOverTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator GetUp()
        {
            mood = Mood.GettingUp;
            ragdoll.GetRecoveryPose(getUpHeadDirection, out Vector3 hips, out Quaternion rotation);
            Vector3 offset = rotation * getUpHipsOffset;
            Vector3 position = FindGround(hips) - new Vector3(offset.x, 0.0f, offset.z);
            transform.position = position;
            transform.rotation = rotation;
            if (getUpClip != null)
            {
                animator.Blend(getUpClip, 0.0f, false, false);
            }
            ragdoll.Recover(0.45f);
            float seconds = getUpClip != null ? Mathf.Max(getUpClip.length - getUpExitBlend, 0.1f) : 0.5f;
            yield return new WaitForSeconds(seconds);
            Vector3 fromCenter = transform.position - loopCenter;
            angle = Mathf.Atan2(fromCenter.x, fromCenter.z);
            StartMood(walks ? Mood.Walking : Mood.Standing, getUpExitBlend);
        }

        /// <summary>The floor under a point: the nearest hit below it that is not one of this ragdoll's own bodies.</summary>
        private Vector3 FindGround(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * GroundProbeHeight;
            float nearest = float.MaxValue;
            Vector3 ground = new Vector3(point.x, point.y, point.z);
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, GroundProbeHeight + GroundProbeDepth))
            {
                if (hit.distance < nearest && !IsOwnBody(hit.entity))
                {
                    nearest = hit.distance;
                    ground = hit.point;
                }
            }
            return ground;
        }

        private bool IsOwnBody(Entity entity)
        {
            for (Entity current = entity; current; current = current.transform.parent)
            {
                if (current == owner)
                {
                    return true;
                }
            }
            return false;
        }

        private void StartMood(Mood next, float blend = 0.35f)
        {
            mood = next;
            AnimationClip clip = next == Mood.Walking ? walkClip : idleClip;
            if (clip != null)
            {
                animator.Blend(clip, blend, true, false);
            }
        }

        private void WalkLoop()
        {
            if (loopRadius <= 0.0f)
            {
                return;
            }
            angle += walkSpeed / loopRadius * Time.deltaTime;
            Vector3 position = loopCenter + new Vector3(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle)) * loopRadius;
            position.y = transform.position.y;
            Vector3 heading = new Vector3(Mathf.Cos(angle), 0.0f, -Mathf.Sin(angle));
            transform.position = position;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading, Vector3.up), 8.0f * Time.deltaTime);
        }

        private void PlayImpact(float strength)
        {
            if (voice == null || impactSounds == null || impactSounds.Length == 0)
            {
                return;
            }
            voice.clip = impactSounds[Random.Range(0, impactSounds.Length)];
            voice.volume = Mathf.Clamp01(strength / 120.0f) * 0.8f + 0.2f;
            voice.Play();
        }
    }
}
