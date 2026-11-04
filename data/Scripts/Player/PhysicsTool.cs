using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// The visitor's physics tool, on the camera entity.
    /// <list type="bullet">
    /// <item>Left click pushes what the crosshair is on: an impulse at the hit point, so an off-center push also
    /// spins a body. A ragdoll's bone takes the push through <see cref="RagdollCharacter.TakeHit"/>.</item>
    /// <item>Hold right click to grab a dynamic body and carry it. The body hangs on a soft fixed joint from a
    /// kinematic hand in front of the camera: the joint's spring makes the carry smooth. Bodies over the carry mass
    /// stay where they are, and a body caught behind something is let go.</item>
    /// <item>While carrying, left click throws; the mouse wheel moves the body nearer or further.</item>
    /// <item>A click or E on a button, a barrel or the buggy presses its <see cref="Interactable"/> instead.</item>
    /// </list>
    /// The input actions "Push" and "Grab" (a gamepad's B and Y, and the on-screen buttons of a touch screen) do the
    /// same: Push pushes or throws, Grab picks a body up and, pressed again, puts it down.
    /// </summary>
    public class PhysicsTool : ScriptComponent
    {
        [Header("Push")]
        [Tooltip("How far the tool reaches, in meters.")]
        public float range = 30.0f;

        [Tooltip("Impulse of a push, in newton seconds.")]
        public float pushImpulse = 60.0f;

        [Tooltip("Layers the tool hits.")]
        public LayerMask hitLayers = LayerMask.Everything;

        [Header("Carry")]
        [Tooltip("How far from the camera a grabbed body is held, in meters.")]
        public float holdDistance = 3.0f;

        [Tooltip("Nearest and furthest hold distance the mouse wheel reaches, in meters.")]
        public Vector2 holdRange = new Vector2(1.5f, 8.0f);

        [Tooltip("Spring of the carrying joint, oscillations per second. Lower values let the body lag and sway.")]
        public float carryFrequency = 5.0f;

        [Tooltip("Heaviest body the tool picks up, in kilograms.")]
        public float maxCarryMass = 250.0f;

        [Tooltip("A carried body that falls this many meters behind the hand (caught on something) is let go.")]
        public float dropDistance = 1.5f;

        [Tooltip("Speed a thrown body leaves with, in meters per second.")]
        public float throwSpeed = 18.0f;

        [Tooltip("Fastest the hand moves to where the body should be held, in meters per second. The carrying joint " +
                 "only ever has a short way to pull, so a sudden turn or a wheel step cannot fling the body.")]
        public float handSpeed = 10.0f;

        [Tooltip("How far away E presses a button, in meters.")]
        public float useRange = 4.0f;

        [Header("Sounds")]
        public AudioClip pushSound;
        public AudioClip grabSound;
        public AudioClip releaseSound;

        private Entity hand;
        private Entity carryJoint;
        private Entity carried;
        private float carryGap;
        private AudioSourceComponent audioSource;
        private FirstPersonController walker;
        /// <summary>The right mouse button holds the body: letting go of it drops it. Grab carries until pressed again.</summary>
        private bool isHeldByMouse;

        /// <summary>What the crosshair is on, if anything within range.</summary>
        public RaycastHit? aim { get; private set; }

        /// <summary>The body being carried, or an invalid entity.</summary>
        public Entity carriedBody => carried;

        public override void OnStart()
        {
            hand = Scene.CreateEntity("Physics Tool Hand");
            hand.transform.position = HoldPoint();
            var handBody = hand.AddComponent<PhysicsComponent>();
            handBody.bodyType = RigidbodyType.Kinematic;
            audioSource = owner.AddComponent<AudioSourceComponent>();
            audioSource.spatial = false;
            var parent = owner.transform.parent;
            walker = parent ? parent.GetComponent<FirstPersonController>() : null;
        }

        public override void OnUpdate()
        {
            aim = FindAim();
            MoveHand();
            if (carried && Vector3.Distance(carried.transform.position, hand.transform.position) > carryGap + dropDistance)
            {
                // Caught behind something: let go rather than drag it through.
                Drop(true);
            }
            if (walker != null && walker.driving)
            {
                if (carried)
                {
                    Drop(false);
                }
                return;
            }
            if (walker != null ? !walker.hasControl : Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }
            if ((Input.IsPressed(KeyCode.E) || Input.IsPressed("Use")) && aim.HasValue && aim.Value.distance <= useRange &&
                Interactable.Notify(aim.Value.entity, owner))
            {
                Play(pushSound, 0.4f);
                return;
            }
            // The mouse works the tool only while it looks around: on a touch screen a press is a finger, not a click.
            bool usesMouse = Cursor.lockState == CursorLockMode.Locked;
            bool pushes = (usesMouse && Input.IsPressed(MouseButton.Left)) || Input.IsPressed("Push");
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            holdDistance = Mathf.Clamp(holdDistance + wheel * 0.5f, holdRange.x, holdRange.y);
            if (carried)
            {
                if (pushes)
                {
                    Throw();
                }
                else if (isHeldByMouse ? Input.IsReleased(MouseButton.Right) : Input.IsPressed("Grab"))
                {
                    Drop(true);
                }
                return;
            }
            if (pushes)
            {
                Push();
            }
            else if (usesMouse && Input.IsPressed(MouseButton.Right))
            {
                Grab();
                isHeldByMouse = true;
            }
            else if (Input.IsPressed("Grab"))
            {
                Grab();
                isHeldByMouse = false;
            }
        }

        public override void OnDestroy()
        {
            if (carryJoint)
            {
                Scene.DestroyEntity(carryJoint);
            }
        }

        /// <summary>
        /// The nearest hit along the view that is not the visitor: the ray starts inside the visitor's own capsule, which
        /// a plain ray cast would report first.
        /// </summary>
        private RaycastHit? FindAim()
        {
            RaycastHit? nearest = null;
            Entity self = walker != null ? walker.owner : owner;
            foreach (var hit in Physics.RaycastAll(transform.position, transform.forward, range, hitLayers))
            {
                if (hit.entity == self || hit.entity == hand || hit.entity == carried)
                {
                    continue;
                }
                if (!nearest.HasValue || hit.distance < nearest.Value.distance)
                {
                    nearest = hit;
                }
            }
            return nearest;
        }

        private void MoveHand()
        {
            Vector3 target = HoldPoint();
            hand.transform.position = carried
                ? Vector3.MoveTowards(hand.transform.position, target, handSpeed * Time.deltaTime)
                : target;
            hand.transform.rotation = transform.rotation;
        }

        private Vector3 HoldPoint()
        {
            return transform.position + transform.forward * holdDistance;
        }

        private void Push()
        {
            if (!aim.HasValue)
            {
                return;
            }
            var hit = aim.Value;
            Vector3 impulse = transform.forward * pushImpulse;
            Play(pushSound, 0.8f);
            if (Interactable.Notify(hit.entity, owner))
            {
                return;
            }
            var character = RagdollCharacter.Find(hit.entity);
            if (character != null)
            {
                character.TakeHit(hit.entity, hit.point, impulse);
                return;
            }
            var ragdoll = FindRagdoll(hit.entity);
            if (ragdoll != null)
            {
                // A ragdoll without a character script (the powered sparring dummy) takes the push on its bone.
                ragdoll.AddImpulse(ragdoll.FindBody(hit.entity), impulse, hit.point);
                return;
            }
            var body = hit.entity.GetComponent<PhysicsComponent>();
            if (body != null && body.bodyType == RigidbodyType.Dynamic)
            {
                body.ApplyForceAtPosition(impulse, hit.point, ForceMode.Impulse);
            }
        }

        private static RagdollComponent FindRagdoll(Entity bone)
        {
            for (Entity current = bone; current; current = current.transform.parent)
            {
                var ragdoll = current.GetComponent<RagdollComponent>();
                if (ragdoll != null)
                {
                    return ragdoll;
                }
            }
            return null;
        }

        private void Grab()
        {
            if (!aim.HasValue)
            {
                return;
            }
            var hit = aim.Value;
            var body = hit.entity.GetComponent<PhysicsComponent>();
            if (body == null || body.bodyType != RigidbodyType.Dynamic || body.mass > maxCarryMass)
            {
                return;
            }
            holdDistance = Mathf.Clamp(hit.distance, holdRange.x, holdRange.y);
            hand.transform.position = HoldPoint();
            // The joint frame sits at the grabbed point and is fixed to both bodies where they are now.
            carryJoint = Scene.CreateEntity("Carry Joint");
            carryJoint.transform.position = hit.point;
            carryJoint.transform.rotation = transform.rotation;
            var joint = carryJoint.AddComponent<JointComponent>();
            joint.type = JointType.Fixed;
            joint.bodyA = hand;
            joint.bodyB = hit.entity;
            joint.spring = new JointSpring { enabled = true, frequency = carryFrequency, dampingRatio = 1.0f };
            carried = hit.entity;
            carryGap = Vector3.Distance(carried.transform.position, hand.transform.position);
            body.WakeUp();
            Play(grabSound, 0.6f);
        }

        private void Throw()
        {
            var body = carried.GetComponent<PhysicsComponent>();
            Drop(false);
            if (body != null)
            {
                body.ApplyForce(transform.forward * throwSpeed, ForceMode.VelocityChange);
            }
            Play(pushSound, 0.9f);
        }

        private void Drop(bool playSound)
        {
            if (carryJoint)
            {
                Scene.DestroyEntity(carryJoint);
            }
            carryJoint = Entity.Invalid;
            carried = Entity.Invalid;
            if (playSound)
            {
                Play(releaseSound, 0.5f);
            }
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.Play();
        }
    }
}
