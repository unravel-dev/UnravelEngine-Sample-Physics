using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A button that gives a body a push at a point: it tips the first domino, starts the cradle swinging.
    /// </summary>
    public class Nudge : ScriptComponent
    {
        [Tooltip("The body to push.")]
        public Entity target;

        [Tooltip("The push in the button's space, newton seconds: it turns with the station.")]
        public Vector3 impulse = new Vector3(0.0f, 0.0f, 4.0f);

        [Tooltip("Where on the body the push lands, in its space: above the center tips it over.")]
        public Vector3 point = new Vector3(0.0f, 0.5f, 0.0f);

        public override void OnStart()
        {
            var button = owner.GetComponent<Interactable>();
            if (button != null)
            {
                button.pressed += _ => Push();
            }
        }

        /// <summary>Pushes the target.</summary>
        public void Push()
        {
            var body = target ? target.GetComponent<PhysicsComponent>() : null;
            if (body == null)
            {
                return;
            }
            body.WakeUp();
            Vector3 at = target.transform.position + target.transform.rotation * point;
            body.ApplyForceAtPosition(transform.rotation * impulse, at, ForceMode.Impulse);
        }
    }
}
