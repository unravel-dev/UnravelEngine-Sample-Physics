using System;
using System.Text;
using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A laser that sweeps across panes of glass to a steel plate and shows two ray queries side by side. The glass
    /// panes are sensors: <see cref="Physics.Raycast(Vector3, Vector3, float, int, bool)"/> without sensors passes
    /// them and stops on the first solid body, where the beam ends; <see cref="Physics.RaycastAll(Vector3, Vector3,
    /// float, int, bool)"/> with sensors reports every pane and body along the way, nearest first.
    /// </summary>
    public class LaserScanner : ScriptComponent
    {
        [Tooltip("The beam: a child cylinder 1 meter long along its Y axis, stretched to where the ray stops.")]
        public Entity beam;

        [Tooltip("A small glowing sphere put where the ray stops.")]
        public Entity hitMarker;

        [Tooltip("Text that lists what the queries found.")]
        public Entity readout;

        [Tooltip("How far the laser reaches, meters.")]
        public float range = 24.0f;

        [Tooltip("How far it sweeps either way, degrees.")]
        public float sweepAngle = 22.0f;

        [Tooltip("Seconds one sweep there and back takes.")]
        public float sweepSeconds = 9.0f;

        [Tooltip("Thickness of the beam, meters.")]
        public float beamWidth = 0.03f;

        /// <summary>How often the readout is rewritten, per second.</summary>
        private const float ReadoutRate = 8.0f;

        private float baseYaw;
        private float nextReadout;
        private readonly StringBuilder text = new StringBuilder();

        public override void OnStart()
        {
            baseYaw = transform.localEulerAngles.y;
        }

        public override void OnUpdate()
        {
            float phase = Time.time * 2.0f * Mathf.PI / Mathf.Max(sweepSeconds, 0.1f);
            transform.localRotation = Quaternion.Euler(0.0f, baseYaw + Mathf.Sin(phase) * sweepAngle, 0.0f);
            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;
            var solid = Physics.Raycast(origin, direction, range, LayerMask.Everything, false);
            float length = solid.HasValue ? solid.Value.distance : range;
            ShowBeam(length, solid);
            if (Time.time >= nextReadout)
            {
                nextReadout = Time.time + 1.0f / ReadoutRate;
                WriteReadout(origin, direction, solid);
            }
        }

        private void ShowBeam(float length, RaycastHit? solid)
        {
            if (beam)
            {
                beam.transform.localPosition = new Vector3(0.0f, 0.0f, length * 0.5f);
                beam.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                beam.transform.localScale = new Vector3(beamWidth, length, beamWidth);
            }
            if (hitMarker)
            {
                hitMarker.transform.position = solid.HasValue ? solid.Value.point : transform.position;
                hitMarker.transform.localScale = Vector3.one * (solid.HasValue ? 0.12f : 0.0f);
            }
        }

        private void WriteReadout(Vector3 origin, Vector3 direction, RaycastHit? solid)
        {
            var label = readout ? readout.GetComponent<TextComponent>() : null;
            if (label == null)
            {
                return;
            }
            text.Clear();
            text.Append(solid.HasValue
                ? string.Format("Raycast stops on the {0} at {1:0.0} m.", solid.Value.entity.name, solid.Value.distance)
                : "Raycast hits nothing in range.");
            var all = Physics.RaycastAll(origin, direction, range, LayerMask.Everything, true);
            Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
            text.Append("\nRaycastAll with sensors finds ").Append(all.Length).Append(all.Length == 1 ? " hit:" : " hits:");
            foreach (var hit in all)
            {
                text.AppendFormat("\n  {0}, {1:0.0} m", hit.entity.name, hit.distance);
            }
            label.text = text.ToString();
        }
    }
}
