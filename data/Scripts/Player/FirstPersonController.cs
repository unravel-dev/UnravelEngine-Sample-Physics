using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// Walks the visitor through the playground: WASD (or the left stick), Shift to run, Space to jump, the mouse to
    /// look. Sits on the entity with the <see cref="CharacterControllerComponent"/>; the camera is a child entity
    /// that only pitches, the body only yaws.
    /// </summary>
    /// <remarks>
    /// The cursor locks while playing. Escape frees it (to click the interface), a click locks it again.
    /// </remarks>
    public class FirstPersonController : ScriptComponent
    {
        [Header("Movement")]
        [Tooltip("Walking speed in meters per second.")]
        public float walkSpeed = 3.5f;

        [Tooltip("Running speed in meters per second (Shift).")]
        public float runSpeed = 6.5f;

        [Tooltip("How fast the speed follows the input, meters per second squared.")]
        public float acceleration = 30.0f;

        [Tooltip("Upward speed of a jump in meters per second.")]
        public float jumpSpeed = 5.0f;

        [Header("Look")]
        [Tooltip("The child entity with the camera; it pitches up and down.")]
        public Entity cameraEntity;

        [Tooltip("Degrees turned per unit of mouse movement.")]
        public float mouseSensitivity = 0.12f;

        [Tooltip("Degrees per second the right stick turns at full tilt.")]
        public float stickTurnSpeed = 140.0f;

        [Tooltip("How far up or down the camera may look, in degrees.")]
        [Range(0, 89)]
        public float pitchLimit = 85.0f;

        private CharacterControllerComponent controller;
        private Vector3 velocity;
        private float yaw;
        private float pitch;
        private bool isDriving;

        /// <summary>Whether the cursor is locked to the view, so mouse movement looks around.</summary>
        public bool isLooking => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>The camera's world position.</summary>
        public Vector3 eyePosition => cameraEntity ? cameraEntity.transform.position : transform.position;

        /// <summary>Whether something else (the buggy) has the controls.</summary>
        public bool driving => isDriving;

        /// <summary>The direction the camera looks.</summary>
        public Vector3 lookDirection => cameraEntity ? cameraEntity.transform.forward : transform.forward;

        public override void OnStart()
        {
            controller = owner.GetComponent<CharacterControllerComponent>();
            yaw = Heading.Of(owner);
            LockCursor(true);
        }

        public override void OnUpdate()
        {
            UpdateCursor();
            if (isDriving)
            {
                return;
            }
            Look();
            Walk();
        }

        /// <summary>
        /// Hands control to something else (the buggy) or takes it back. While driving, the player neither walks nor
        /// looks and its controller stays where it is.
        /// </summary>
        public void SetDriving(bool driving)
        {
            isDriving = driving;
            velocity = Vector3.zero;
        }

        /// <summary>Puts the player at a point, facing a direction (station teleports, leaving the buggy).</summary>
        public void TeleportTo(Vector3 position, float facingYaw)
        {
            controller.Warp(position);
            yaw = facingYaw;
            pitch = 0.0f;
            velocity = Vector3.zero;
            ApplyLook();
        }

        private void UpdateCursor()
        {
            if (Input.IsPressed(KeyCode.Escape))
            {
                LockCursor(false);
            }
            else if (!isLooking && Input.IsPressed(MouseButton.Left) && !Hud.isPointerOverPanel)
            {
                LockCursor(true);
            }
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Look()
        {
            if (isLooking)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity * 10.0f;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * 10.0f;
            }
            yaw += Input.GetAxis("Look Horizontal") * stickTurnSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Look Vertical") * stickTurnSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
            ApplyLook();
        }

        private void ApplyLook()
        {
            transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
            if (cameraEntity)
            {
                cameraEntity.transform.localRotation = Quaternion.Euler(pitch, 0.0f, 0.0f);
            }
        }

        private void Walk()
        {
            var input = new Vector3(Input.GetAxis("Horizontal"), 0.0f, Input.GetAxis("Vertical"));
            if (input.sqrMagnitude > 1.0f)
            {
                input.Normalize();
            }
            bool running = Input.IsDown(KeyCode.LShift) || Input.GetAxis("Run") > 0.5f;
            Vector3 wanted = transform.rotation * input * (running ? runSpeed : walkSpeed);
            float control = controller.isGrounded ? 1.0f : 0.3f;
            velocity = Vector3.MoveTowards(velocity, wanted, acceleration * control * Time.deltaTime);
            controller.Move(velocity * Time.deltaTime);
            if (controller.isGrounded && (Input.IsPressed(KeyCode.Space) || Input.IsPressed("Jump")))
            {
                controller.Jump(jumpSpeed);
            }
        }
    }
}
