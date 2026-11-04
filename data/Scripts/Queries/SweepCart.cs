using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A kinematic cart that shuttles along a track and looks ahead with <see cref="Physics.BoxCastAll"/>: it sweeps
    /// its own box forward and waits while anything is in the way, so a crate the visitor drops on the track stops it
    /// short of a collision. A shape cast answers what a moving box would hit, where a ray would miss thin or offset
    /// obstacles.
    /// </summary>
    public class SweepCart : ScriptComponent
    {
        [Tooltip("From where the cart starts to the far end of its track, in the world, meters.")]
        public Vector3 travel = new Vector3(8.0f, 0.0f, 0.0f);

        [Tooltip("Speed along the track, meters per second.")]
        public float speed = 1.6f;

        [Tooltip("Half the size of the cart's box, in its space, meters.")]
        public Vector3 halfExtents = new Vector3(0.55f, 0.35f, 0.45f);

        [Tooltip("How far ahead the cart sweeps its box, meters.")]
        public float lookAhead = 1.4f;

        [Tooltip("Text that says what the cast found.")]
        public Entity readout;

        [Tooltip("A light that turns red while the cart waits.")]
        public Entity warningLamp;

        /// <summary>Pause at either end of the track, seconds.</summary>
        private const float EndPause = 0.8f;

        private Vector3 start;
        private float progress;
        private float direction = 1.0f;
        private float pauseLeft;
        private string lastText = "";

        public override void OnStart()
        {
            start = transform.position;
        }

        public override void OnUpdate()
        {
            if (pauseLeft > 0.0f)
            {
                pauseLeft -= Time.deltaTime;
                return;
            }
            Vector3 heading = travel.normalized * direction;
            var blocker = FindBlocker(heading);
            SetLamp(blocker.HasValue);
            if (blocker.HasValue)
            {
                Show(string.Format("BoxCast ahead hits the {0} {1:0.0} m away, so the cart waits.",
                                   blocker.Value.entity.name, blocker.Value.distance));
                return;
            }
            Show("BoxCast ahead finds the track clear.");
            float length = travel.magnitude;
            progress = Mathf.Clamp01(progress + direction * speed * Time.deltaTime / Mathf.Max(length, 0.01f));
            transform.position = start + travel * progress;
            if (progress <= 0.0f || progress >= 1.0f)
            {
                direction = -direction;
                pauseLeft = EndPause;
            }
        }

        private RaycastHit? FindBlocker(Vector3 heading)
        {
            RaycastHit? nearest = null;
            foreach (var hit in Physics.BoxCastAll(transform.position, halfExtents, heading, transform.rotation, lookAhead))
            {
                if (IsPartOfCart(hit.entity) || IsFloor(hit))
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

        private bool IsPartOfCart(Entity entity)
        {
            for (Entity current = entity; current; current = current.transform.parent)
            {
                if (current == owner)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsFloor(RaycastHit hit)
        {
            // The track and the floor face up; the sweep slides over them.
            return hit.normal.y > 0.7f;
        }

        private void Show(string message)
        {
            if (message == lastText)
            {
                return;
            }
            lastText = message;
            var text = readout ? readout.GetComponent<TextComponent>() : null;
            if (text != null)
            {
                text.text = message;
            }
        }

        private void SetLamp(bool waiting)
        {
            var light = warningLamp ? warningLamp.GetComponent<LightComponent>() : null;
            if (light != null)
            {
                light.color = waiting ? new Color(1.0f, 0.15f, 0.1f, 1.0f) : new Color(0.25f, 0.8f, 1.0f, 1.0f);
            }
        }
    }
}
