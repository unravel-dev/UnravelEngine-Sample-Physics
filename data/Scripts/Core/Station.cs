using System.Collections.Generic;
using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// One exhibit of the playground: what the visitor reads on the screen near it, where the station menu puts them,
    /// and what <see cref="ResetStation"/> puts back. Every dynamic body under the station entity returns to where it
    /// started.
    /// </summary>
    public class Station : ScriptComponent
    {
        [Tooltip("Place in the station menu.")]
        public int order;

        public string title = "Station";

        [Tooltip("What the station shows, one or two sentences.")]
        public string description = "";

        [Tooltip("How to try it, shown under the description.")]
        public string controls = "";

        [Tooltip("How to try it on a touch screen, when that differs from the controls; empty shows the controls.")]
        public string touchControls = "";

        [Tooltip("Where the station menu puts the visitor; their facing is this entity's yaw.")]
        public Entity viewpoint;

        [Tooltip("The station's text shows while the visitor is within this many meters of it.")]
        public float radius = 9.0f;

        private readonly List<Entity> bodies = new List<Entity>();
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Quaternion> rotations = new List<Quaternion>();

        /// <summary>Every station of the scene, in menu order.</summary>
        public static List<Station> All()
        {
            var stations = new List<Station>();
            foreach (var entity in Scene.FindEntitiesWithComponent<Station>())
            {
                stations.Add(entity.GetComponent<Station>());
            }
            stations.Sort((a, b) => a.order.CompareTo(b.order));
            return stations;
        }

        public override void OnStart()
        {
            Remember(owner);
        }

        /// <summary>
        /// Puts every part of the station back as play started (<see cref="StationPart"/>), then every dynamic body
        /// where it started, at rest, and repairs the broken joints.
        /// </summary>
        public void ResetStation()
        {
            foreach (var part in owner.GetComponentsInChildren<StationPart>())
            {
                part.ResetPart();
            }
            for (int i = 0; i < bodies.Count; ++i)
            {
                if (!bodies[i])
                {
                    continue;
                }
                var body = bodies[i].GetComponent<PhysicsComponent>();
                bodies[i].transform.position = positions[i];
                bodies[i].transform.rotation = rotations[i];
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            foreach (var joint in owner.GetComponentsInChildren<JointComponent>())
            {
                if (joint.isBroken)
                {
                    joint.Repair();
                }
            }
        }

        private void Remember(Entity entity)
        {
            var body = entity.GetComponent<PhysicsComponent>();
            if (body != null && body.bodyType == RigidbodyType.Dynamic)
            {
                bodies.Add(entity);
                positions.Add(entity.transform.position);
                rotations.Add(entity.transform.rotation);
            }
            foreach (var child in entity.transform.children)
            {
                Remember(child);
            }
        }
    }
}
