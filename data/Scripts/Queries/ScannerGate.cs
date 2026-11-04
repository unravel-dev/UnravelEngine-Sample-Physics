using System.Collections.Generic;
using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A gate over a conveyor that asks <see cref="Physics.BoxOverlap"/> what is inside its box every few frames: it
    /// counts the bodies and their mass, lights up while something passes, and shows what it found. An overlap query
    /// needs no sensor body and no events; it reads the world as it is now.
    /// </summary>
    public class ScannerGate : ScriptComponent
    {
        [Tooltip("Half the size of the scanned box, in the gate's space, meters.")]
        public Vector3 halfExtents = new Vector3(1.0f, 0.8f, 0.4f);

        [Tooltip("Text that shows what the gate found.")]
        public Entity readout;

        [Tooltip("A light that turns from the idle to the busy color while something is in the gate.")]
        public Entity lamp;

        public Color idleColor = new Color(0.2f, 0.9f, 0.45f, 1.0f);
        public Color busyColor = new Color(1.0f, 0.45f, 0.1f, 1.0f);

        public AudioClip beepSound;

        /// <summary>How often the gate scans, per second.</summary>
        private const float ScanRate = 10.0f;

        private readonly HashSet<Entity> counted = new HashSet<Entity>();
        private float nextScan;
        private int lastCount = -1;

        public override void OnUpdate()
        {
            if (Time.time < nextScan)
            {
                return;
            }
            nextScan = Time.time + 1.0f / ScanRate;
            Scan();
        }

        private void Scan()
        {
            counted.Clear();
            float mass = 0.0f;
            foreach (var entity in Physics.BoxOverlap(transform.position, halfExtents, transform.rotation))
            {
                var character = RagdollCharacter.Find(entity);
                if (character != null)
                {
                    if (counted.Add(character.owner))
                    {
                        mass += 70.0f;
                    }
                    continue;
                }
                var body = entity.GetComponent<PhysicsComponent>();
                if (body != null && body.bodyType == RigidbodyType.Dynamic && counted.Add(entity))
                {
                    mass += body.mass;
                }
            }
            if (counted.Count == lastCount)
            {
                return;
            }
            if (counted.Count > lastCount && lastCount >= 0)
            {
                Beep();
            }
            lastCount = counted.Count;
            Show(mass);
        }

        private void Show(float mass)
        {
            var text = readout ? readout.GetComponent<TextComponent>() : null;
            if (text != null)
            {
                text.text = counted.Count == 0
                    ? "BoxOverlap finds nothing in the gate."
                    : string.Format("BoxOverlap finds {0} {1} in the gate, {2:0.#} kg in all.", counted.Count,
                                    counted.Count == 1 ? "body" : "bodies", mass);
            }
            var light = lamp ? lamp.GetComponent<LightComponent>() : null;
            if (light != null)
            {
                light.color = counted.Count == 0 ? idleColor : busyColor;
            }
        }

        private void Beep()
        {
            var source = owner.GetComponent<AudioSourceComponent>();
            if (source != null && beepSound != null)
            {
                source.clip = beepSound;
                source.Play();
            }
        }
    }
}
