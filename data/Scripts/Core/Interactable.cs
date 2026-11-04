using System;
using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// Something the visitor can press with the physics tool or the E key: a button that toggles a lift, resets a
    /// station or drops a mannequin. Scripts subscribe to <see cref="pressed"/>.
    /// </summary>
    public class Interactable : ScriptComponent
    {
        [Tooltip("What the crosshair hint says, for example \"Reset the tower\".")]
        public string prompt = "Use";

        [Tooltip("Seconds before it can be pressed again.")]
        public float cooldown = 0.4f;

        public AudioClip pressSound;

        /// <summary>Raised when the visitor presses it, with the entity that pressed it.</summary>
        public event Action<Entity> pressed;

        private float readyTime;

        /// <summary>Presses the interactable on an entity, or on the nearest ancestor that has one.</summary>
        /// <returns>False when nothing there can be pressed.</returns>
        public static bool Notify(Entity target, Entity presser)
        {
            var interactable = Find(target);
            if (interactable == null)
            {
                return false;
            }
            interactable.Press(presser);
            return true;
        }

        /// <summary>The interactable on an entity or its nearest ancestor, or null.</summary>
        public static Interactable Find(Entity target)
        {
            for (Entity current = target; current; current = current.transform.parent)
            {
                var interactable = current.GetComponent<Interactable>();
                if (interactable != null)
                {
                    return interactable;
                }
            }
            return null;
        }

        /// <summary>Presses it, unless it was pressed a moment ago.</summary>
        public void Press(Entity presser)
        {
            if (Time.time < readyTime)
            {
                return;
            }
            readyTime = Time.time + cooldown;
            var source = owner.GetComponent<AudioSourceComponent>();
            if (source != null && pressSound != null)
            {
                source.clip = pressSound;
                source.Play();
            }
            pressed?.Invoke(presser);
        }
    }
}
