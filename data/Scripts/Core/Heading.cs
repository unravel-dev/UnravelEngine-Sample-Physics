using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// The compass heading of a direction: its turn about the up axis in degrees, 0 along +Z. Euler angles do not give
    /// it reliably, since a heading past 90 degrees can come back as a flipped triple such as (180, 60, 180).
    /// </summary>
    public static class Heading
    {
        /// <summary>The heading of a direction, ignoring how far it points up or down.</summary>
        public static float Of(Vector3 forward)
        {
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>The heading an entity faces.</summary>
        public static float Of(Entity entity)
        {
            return Of(entity.transform.forward);
        }
    }
}
