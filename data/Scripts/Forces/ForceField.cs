using System.Collections.Generic;
using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A volume that pushes the bodies inside it, on an entity whose Physics component is a sensor. Every fixed step
    /// each dynamic body inside gets an acceleration, the same for light and heavy bodies, or with
    /// <see cref="byForce"/> a force, which moves light bodies more than heavy ones:
    /// <list type="bullet">
    /// <item>Wind blows along a direction.</item>
    /// <item>Lift cancels gravity and adds a little, so bodies float up and drift.</item>
    /// <item>Water holds bodies up by how deep they are below its surface and how dense they are
    /// (<see cref="Buoyant"/>), and slows them like a liquid.</item>
    /// <item>Attract pulls bodies to the field's center, harder when closer, like a magnet.</item>
    /// </list>
    /// A ragdoll inside is knocked down first, so a mannequin caught in the wind tumbles with it. The sensor's layers
    /// choose what it feels: the magnet's sensor includes only the Metal layer.
    /// </summary>
    public class ForceField : ScriptComponent
    {
        public enum Kind
        {
            Wind,
            Lift,
            Water,
            Attract,
        }

        public Kind kind = Kind.Wind;

        [Tooltip("Wind: the direction it blows, in the field's space.")]
        public Vector3 direction = new Vector3(0.0f, 0.0f, 1.0f);

        [Tooltip("Wind and attract: acceleration in meters per second squared, or newtons with byForce. Lift: how many " +
                 "times gravity holds bodies up. Water: how many times gravity holds up a fully sunk body of the " +
                 "water's own density.")]
        public float strength = 14.0f;

        [Tooltip("Wind: the air drags bodies towards the wind's speed (strength, meters per second) with a force of drag " +
                 "newton seconds per meter, so light bodies are soon blown along and heavy ones barely move. Attract: " +
                 "strength is a force in newtons.")]
        public bool byForce;

        [Tooltip("How fast the field slows what moves inside it, per second: thin air near 0, water 1.5 to 3.")]
        public float drag = 0.0f;

        [Tooltip("Water: depth over which a body goes from floating to fully under, meters.")]
        public float immersionDepth = 0.6f;

        [Tooltip("Water: height of the surface above the field's center, meters.")]
        public float surfaceHeight = 0.5f;

        [Tooltip("Attract: inside this distance the pull no longer grows, meters.")]
        public float coreRadius = 0.8f;

        // Overlapping shapes per entity: a ragdoll enters with each of its bodies and is inside until the last leaves.
        private readonly Dictionary<Entity, int> inside = new Dictionary<Entity, int>();
        private readonly List<Entity> gone = new List<Entity>();

        /// <summary>How many bodies are inside the field.</summary>
        public int count => inside.Count;

        public override void OnSensorEnter(Collision collision)
        {
            if (!collision.entity)
            {
                return;
            }
            inside.TryGetValue(collision.entity, out int shapes);
            inside[collision.entity] = shapes + 1;
            if (kind != Kind.Attract)
            {
                RagdollCharacter.Find(collision.entity)?.KnockDown();
            }
        }

        public override void OnSensorExit(Collision collision)
        {
            if (!inside.TryGetValue(collision.entity, out int shapes))
            {
                return;
            }
            if (shapes <= 1)
            {
                inside.Remove(collision.entity);
            }
            else
            {
                inside[collision.entity] = shapes - 1;
            }
        }

        public override void OnFixedUpdate()
        {
            gone.Clear();
            foreach (var entity in inside.Keys)
            {
                if (!entity)
                {
                    gone.Add(entity);
                    continue;
                }
                Push(entity);
            }
            foreach (var entity in gone)
            {
                inside.Remove(entity);
            }
        }

        private void Push(Entity entity)
        {
            var ragdoll = entity.GetComponent<RagdollComponent>();
            if (ragdoll != null)
            {
                if (ragdoll.state != RagdollState.Animated)
                {
                    ragdoll.AddVelocity(AccelerationAt(ragdoll.rootPosition, Vector3.zero) * Time.fixedDeltaTime);
                }
                return;
            }
            var body = entity.GetComponent<PhysicsComponent>();
            if (body == null || body.bodyType != RigidbodyType.Dynamic)
            {
                return;
            }
            Vector3 push = AccelerationAt(entity.transform.position, body.velocity);
            if (kind == Kind.Wind && byForce)
            {
                push = (transform.rotation * direction.normalized * strength - body.velocity) * drag;
            }
            else if (kind == Kind.Water)
            {
                var buoyant = entity.GetComponent<Buoyant>();
                push = WaterPush(entity.transform.position, body.velocity, buoyant != null ? buoyant.density : 0.8f);
            }
            bool asForce = byForce && (kind == Kind.Wind || kind == Kind.Attract);
            body.ApplyForce(push, asForce ? ForceMode.Force : ForceMode.Acceleration);
            body.WakeUp();
        }

        /// <summary>Buoyancy by how deep the body is and how dense, and the water's drag on it.</summary>
        private Vector3 WaterPush(Vector3 position, Vector3 velocity, float density)
        {
            float surface = transform.position.y + surfaceHeight;
            float depth = Mathf.Clamp01((surface - position.y) / Mathf.Max(immersionDepth, 0.01f));
            return -Physics.gravity * (strength * depth / Mathf.Max(density, 0.05f)) - velocity * (drag * depth);
        }

        private Vector3 AccelerationAt(Vector3 position, Vector3 velocity)
        {
            Vector3 gravity = Physics.gravity;
            Vector3 slowing = -velocity * drag;
            switch (kind)
            {
                case Kind.Wind:
                    return transform.rotation * direction.normalized * strength + slowing;
                case Kind.Lift:
                    return -gravity * strength + slowing;
                case Kind.Water:
                    return WaterPush(position, velocity, 0.8f);
                default:
                {
                    Vector3 toCenter = transform.position - position;
                    float distance = Mathf.Max(toCenter.magnitude, coreRadius);
                    return toCenter.normalized * (strength * coreRadius / distance) + slowing;
                }
            }
        }
    }
}
