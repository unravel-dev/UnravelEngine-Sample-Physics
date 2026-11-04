using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// Walks the visitor through the playground: WASD (or the left stick), Shift to run, Space to jump, the mouse to
    /// look. Sits on the entity with the <see cref="CharacterControllerComponent"/>; the camera is a child entity
    /// that only pitches, the body only yaws.
    /// </summary>
    /// <remarks>
    /// The cursor locks while playing. Escape frees it (to click the interface), a click locks it again. A touch
    /// screen has no cursor to hold: the on-screen stick walks, pushed all the way it runs, and a finger dragged
    /// anywhere else looks around.
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

        [Header("Touch")]
        [Tooltip("Degrees a finger dragged across the whole height of the screen turns the view.")]
        public float touchLookSpeed = 220.0f;

        [Tooltip("How far the on-screen stick leans, as a share of its travel, before the visitor runs.")]
        [Range(0, 1)]
        public float touchRunTilt = 0.9f;

        private CharacterControllerComponent controller;
        private Vector3 velocity;
        private float yaw;
        private float pitch;
        private bool isDriving;
        private float walkingGravityScale = 1.0f;
        private Vector2 lastPointer;
        private bool wasPointerHeld;

        /// <summary>Whether the cursor is locked to the view, so mouse movement looks around.</summary>
        public bool isLooking => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// Whether the visitor's input looks around and works the tool: the cursor is locked to the view, or on a touch
        /// screen no menu is open.
        /// </summary>
        public bool hasControl => Input.touchSupported ? !Hud.isPointerOverPanel : isLooking;

        /// <summary>The camera's world position.</summary>
        public Vector3 eyePosition => cameraEntity ? cameraEntity.transform.position : transform.position;

        /// <summary>Whether something else (the buggy) has the controls.</summary>
        public bool driving => isDriving;

        /// <summary>The direction the camera looks.</summary>
        public Vector3 lookDirection => cameraEntity ? cameraEntity.transform.forward : transform.forward;

        public override void OnStart()
        {
            controller = owner.GetComponent<CharacterControllerComponent>();
            walkingGravityScale = controller.gravityScale;
            yaw = Heading.Of(owner);
            LockCursor(!Input.touchSupported);
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
        /// looks and its controller stays where it is: without gravity, since the camera hangs under the player, and a
        /// player falling away would carry it along and, kilometers down, cost its position the precision to move
        /// smoothly.
        /// </summary>
        public void SetDriving(bool driving)
        {
            isDriving = driving;
            velocity = Vector3.zero;
            controller.gravityScale = driving ? 0.0f : walkingGravityScale;
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
            if (Input.touchSupported)
            {
                // A touch screen holds no cursor, also one reported after the start (a touch laptop's first finger).
                if (isLooking)
                {
                    LockCursor(false);
                }
                return;
            }
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
            else if (Input.touchSupported && hasControl)
            {
                LookByDrag();
            }
            yaw += Input.GetAxis("Look Horizontal") * stickTurnSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Look Vertical") * stickTurnSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
            ApplyLook();
        }

        /// <summary>
        /// Turns the view by the fingers dragged across the screen; one on the on-screen controls belongs to them and
        /// is not among the touches. In the editor's phone and tablet previews the mouse, held down, stands in for a
        /// finger.
        /// </summary>
        private void LookByDrag()
        {
            Vector2 drag = Vector2.zero;
            for (int i = 0; i < Input.touchCount; ++i)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.Phase == TouchPhase.Moved)
                {
                    drag += touch.DeltaPosition;
                }
            }
            if (Input.touchCount == 0)
            {
                drag += GetMouseDrag();
            }
            // Screen coordinates grow down: a finger moving up looks up.
            float degreesPerPixel = touchLookSpeed / Mathf.Max(1.0f, Screen.height);
            yaw += drag.x * degreesPerPixel;
            pitch += drag.y * degreesPerPixel;
        }

        /// <summary>How far the held left mouse button moved since the last frame; a press on a control is not held.</summary>
        private Vector2 GetMouseDrag()
        {
            Vector2 pointer = Input.mousePosition;
            bool held = Input.IsDown(MouseButton.Left);
            Vector2 drag = held && wasPointerHeld ? pointer - lastPointer : Vector2.zero;
            wasPointerHeld = held;
            lastPointer = pointer;
            return drag;
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
            bool running = Input.IsDown(KeyCode.LShift) || Input.GetAxis("Run") > 0.5f ||
                           (Input.touchSupported && input.magnitude >= touchRunTilt);
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
