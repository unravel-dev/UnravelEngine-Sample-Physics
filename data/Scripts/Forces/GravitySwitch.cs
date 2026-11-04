using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A lever that changes the world's gravity for every body at once (<see cref="Physics.gravity"/>): Earth, the
    /// Moon, Mars and near weightlessness. Sleeping bodies are woken so they feel the change. The station's reset, and
    /// the end of play, bring Earth back.
    /// </summary>
    public class GravitySwitch : StationPart
    {
        [Tooltip("Text that shows the gravity now, optional.")]
        public Entity label;

        public AudioClip switchSound;

        private static readonly string[] Names = { "Earth", "the Moon", "Mars", "near weightlessness" };
        private static readonly float[] Strengths = { 9.8f, 1.62f, 3.71f, 0.25f };

        private int index;

        /// <summary>What the gravity is now, as the HUD shows it.</summary>
        public static string description { get; private set; } = "";

        public override void OnStart()
        {
            var button = owner.GetComponent<Interactable>();
            if (button != null)
            {
                button.pressed += _ => Select(index + 1);
            }
            Select(0);
        }

        public override void ResetPart()
        {
            Select(0);
        }

        private void Select(int next)
        {
            bool changed = next != index;
            index = next % Names.Length;
            Physics.gravity = new Vector3(0.0f, -Strengths[index], 0.0f);
            description = string.Format("Gravity of {0}: {1:0.##} meters per second squared.", Names[index], Strengths[index]);
            var text = label ? label.GetComponent<TextComponent>() : null;
            if (text != null)
            {
                text.text = description;
            }
            if (!changed)
            {
                return;
            }
            foreach (var entity in Scene.FindEntitiesWithComponent<PhysicsComponent>())
            {
                var body = entity.GetComponent<PhysicsComponent>();
                if (body.bodyType == RigidbodyType.Dynamic)
                {
                    body.WakeUp();
                }
            }
            var source = owner.GetComponent<AudioSourceComponent>();
            if (source != null && switchSound != null)
            {
                source.clip = switchSound;
                source.Play();
            }
        }
    }
}
