using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A button that opens a gate: a kinematic body that slides out of the way over a moment and lets what it held
    /// go (crates at the top of the friction ramps, balls on the bounce shelf). Pressed again, it closes. The
    /// station's reset closes it.
    /// </summary>
    public class SlideGate : StationPart
    {
        [Tooltip("The gate: an entity with a kinematic Physics component.")]
        public Entity gate;

        [Tooltip("Where the open gate is, from where it starts, in the world.")]
        public Vector3 openOffset = new Vector3(0.0f, -1.0f, 0.0f);

        [Tooltip("Seconds the gate takes to open or close.")]
        public float travelSeconds = 0.35f;

        public AudioClip openSound;

        private Vector3 closedPosition;
        private float openness;
        private bool isOpen;

        public override void OnStart()
        {
            if (gate)
            {
                closedPosition = gate.transform.position;
            }
            var button = owner.GetComponent<Interactable>();
            if (button != null)
            {
                button.pressed += _ => Toggle();
            }
        }

        public override void OnUpdate()
        {
            if (!gate)
            {
                return;
            }
            float target = isOpen ? 1.0f : 0.0f;
            if (openness == target)
            {
                return;
            }
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime / Mathf.Max(travelSeconds, 0.01f));
            gate.transform.position = closedPosition + openOffset * Mathf.SmoothStep(0.0f, 1.0f, openness);
        }

        /// <summary>Opens the gate, or closes it when it is open.</summary>
        public void Toggle()
        {
            isOpen = !isOpen;
            var source = owner.GetComponent<AudioSourceComponent>();
            if (source != null && openSound != null)
            {
                source.clip = openSound;
                source.Play();
            }
        }

        public override void ResetPart()
        {
            isOpen = false;
            openness = 0.0f;
            if (gate)
            {
                gate.transform.position = closedPosition;
            }
        }
    }
}
