using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A buggy on four wheel joints. Each wheel spins on its joint's motor, rides a spring suspension and the front
    /// two steer; nothing else holds the chassis up. The visitor gets in with E (or a click on it) and drives:
    /// <list type="bullet">
    /// <item>W and S (the left stick) drive the motors forward and back, Space (the action "Jump") brakes.</item>
    /// <item>A and D steer the front wheels.</item>
    /// <item>F (the action "Flip") puts the buggy back on its wheels, E ("Use") gets out.</item>
    /// </list>
    /// While it drives, the visitor's camera follows behind the chassis.
    /// </summary>
    public class Buggy : StationPart
    {
        [Header("Wheels")]
        [Tooltip("Wheel joints whose motors drive the buggy.")]
        public Entity[] driveWheels;

        [Tooltip("Wheel joints that steer.")]
        public Entity[] steerWheels;

        [Tooltip("Tire radius, meters: turns the top speed into the motors' spin.")]
        public float wheelRadius = 0.42f;

        [Tooltip("Top speed, meters per second.")]
        public float topSpeed = 15.0f;

        [Tooltip("Torque of each driving motor, newton meters.")]
        public float driveTorque = 420.0f;

        [Tooltip("Torque each motor holds a braked wheel with, newton meters.")]
        public float brakeTorque = 1400.0f;

        [Tooltip("Torque that slows the wheels while coasting, newton meters.")]
        public float coastTorque = 25.0f;

        [Tooltip("Steering lock, degrees.")]
        public float steerLimit = 30.0f;

        [Tooltip("How fast the wheels turn to the steering, degrees per second.")]
        public float steerRate = 110.0f;

        [Header("Driver")]
        [Tooltip("The visitor, with the FirstPersonController.")]
        public Entity player;

        [Tooltip("The entity with the Hud.")]
        public Entity hud;

        [Tooltip("Where the camera follows from, in the chassis' space with only its heading, meters.")]
        public Vector3 cameraOffset = new Vector3(0.0f, 2.6f, -6.5f);

        [Tooltip("How quickly the camera catches up, per second.")]
        public float cameraFollow = 5.0f;

        [Tooltip("Where the visitor is let out, in the chassis' space, meters.")]
        public Vector3 exitOffset = new Vector3(-2.2f, 0.2f, 0.0f);

        /// <summary>Where the visitor waits while driving: out of the buggy's way.</summary>
        private static readonly Vector3 ParkedPlayer = new Vector3(0.0f, -60.0f, 0.0f);

        /// <summary>Degrees per radian, to turn the motors' spin from radians into degrees per second.</summary>
        private const float DegreesPerRadian = 57.29578f;

        private FirstPersonController walker;
        private Hud display;
        private Entity eye;
        private Vector3 eyeLocalPosition;
        private Quaternion eyeLocalRotation;
        private Vector3 cameraPosition;
        private float steering;
        private float enteredTime;
        private bool isDriving;

        /// <summary>Whether the visitor is driving.</summary>
        public bool driving => isDriving;

        public override void OnStart()
        {
            walker = player ? player.GetComponent<FirstPersonController>() : null;
            display = hud ? hud.GetComponent<Hud>() : null;
            var button = owner.GetComponent<Interactable>();
            if (button != null)
            {
                button.pressed += _ => Enter();
            }
            SetWheels(0.0f, false);
        }

        public override void OnUpdate()
        {
            if (!isDriving)
            {
                return;
            }
            if ((Input.IsPressed(KeyCode.E) || Input.IsPressed("Use")) && Time.time > enteredTime + 0.3f)
            {
                Exit();
                return;
            }
            if (Input.IsPressed(KeyCode.F) || Input.IsPressed("Flip"))
            {
                Upright();
            }
            float throttle = Input.GetAxis("Vertical");
            bool braking = Input.IsDown(KeyCode.Space) || Input.IsDown("Jump");
            float wanted = Input.GetAxis("Horizontal") * steerLimit;
            steering = Mathf.MoveTowards(steering, wanted, steerRate * Time.deltaTime);
            SetWheels(braking ? 0.0f : throttle, braking);
        }

        public override void OnLateUpdate()
        {
            if (isDriving && eye)
            {
                FollowWithCamera();
            }
        }

        /// <summary>Puts the visitor in the driver's seat.</summary>
        public void Enter()
        {
            if (isDriving || walker == null || !walker.cameraEntity)
            {
                return;
            }
            isDriving = true;
            enteredTime = Time.time;
            eye = walker.cameraEntity;
            eyeLocalPosition = eye.transform.localPosition;
            eyeLocalRotation = eye.transform.localRotation;
            cameraPosition = eye.transform.position;
            walker.SetDriving(true);
            walker.TeleportTo(ParkedPlayer, 0.0f);
            display?.SetDriving(true);
        }

        /// <summary>Lets the visitor out beside the buggy.</summary>
        public void Exit()
        {
            if (!isDriving)
            {
                return;
            }
            isDriving = false;
            SetWheels(0.0f, true);
            eye.transform.localPosition = eyeLocalPosition;
            eye.transform.localRotation = eyeLocalRotation;
            Vector3 exit = transform.position + HeadingRotation() * exitOffset;
            walker.TeleportTo(exit, Heading.Of(owner));
            walker.SetDriving(false);
            display?.SetDriving(false);
        }

        public override void ResetPart()
        {
            Exit();
            steering = 0.0f;
            SetWheels(0.0f, false);
        }

        private void SetWheels(float throttle, bool braking)
        {
            float spin = throttle * topSpeed / Mathf.Max(wheelRadius, 0.05f) * DegreesPerRadian;
            float torque = braking ? brakeTorque : Mathf.Abs(throttle) > 0.05f ? driveTorque : coastTorque;
            foreach (var wheel in driveWheels)
            {
                var joint = wheel ? wheel.GetComponent<JointComponent>() : null;
                if (joint != null)
                {
                    joint.motor = new JointMotor { enabled = true, speed = spin, maxForce = torque };
                }
            }
            foreach (var wheel in steerWheels)
            {
                var joint = wheel ? wheel.GetComponent<JointComponent>() : null;
                if (joint != null)
                {
                    var steer = joint.steering;
                    steer.enabled = true;
                    steer.angle = steering;
                    steer.limit = steerLimit;
                    joint.steering = steer;
                }
            }
            owner.GetComponent<PhysicsComponent>()?.WakeUp();
        }

        private void Upright()
        {
            transform.position = transform.position + Vector3.up * 1.2f;
            transform.rotation = Quaternion.Euler(0.0f, Heading.Of(owner), 0.0f);
            var body = owner.GetComponent<PhysicsComponent>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>The chassis' heading only, so the camera does not roll or pitch with the bumps.</summary>
        private Quaternion HeadingRotation()
        {
            Vector3 forward = transform.forward;
            forward.y = 0.0f;
            return forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
        }

        private void FollowWithCamera()
        {
            Vector3 wanted = transform.position + HeadingRotation() * cameraOffset;
            cameraPosition = Vector3.Lerp(cameraPosition, wanted, 1.0f - Mathf.Exp(-cameraFollow * Time.deltaTime));
            eye.transform.position = cameraPosition;
            Vector3 target = transform.position + Vector3.up * 1.0f;
            eye.transform.rotation = Quaternion.LookRotation((target - cameraPosition).normalized, Vector3.up);
        }
    }
}
