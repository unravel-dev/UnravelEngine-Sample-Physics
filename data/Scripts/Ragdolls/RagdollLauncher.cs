using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// Pressed (with the physics tool or E), it puts a mannequin on its start point and sends it off as a ragdoll:
    /// a nudge off the top of the stairs, or a catapult shot across the lab.
    /// </summary>
    public class RagdollLauncher : ScriptComponent
    {
        [Tooltip("The mannequin with the RagdollCharacter.")]
        public Entity character;

        [Tooltip("Where the mannequin stands before it is launched; it faces this entity's forward.")]
        public Entity startPoint;

        [Tooltip("Velocity the whole body leaves with, in the start point's space, meters per second.")]
        public Vector3 launchVelocity = new Vector3(0.0f, 0.0f, 2.0f);

        [Tooltip("Seconds the mannequin stands on the start point before it goes.")]
        public float windUp = 0.35f;

        public AudioClip launchSound;

        private Interactable button;

        public override void OnStart()
        {
            button = owner.GetComponent<Interactable>();
            if (button != null)
            {
                button.pressed += _ => Launch();
            }
        }

        /// <summary>Puts the mannequin on the start point and launches it.</summary>
        public void Launch()
        {
            var target = character ? character.GetComponent<RagdollCharacter>() : null;
            if (target == null || !startPoint)
            {
                return;
            }
            Vector3 velocity = startPoint.transform.rotation * launchVelocity;
            target.Launch(startPoint.transform.position, Heading.Of(startPoint), velocity, windUp);
            var source = owner.GetComponent<AudioSourceComponent>();
            if (source != null && launchSound != null)
            {
                source.clip = launchSound;
                source.Play();
            }
        }
    }
}
