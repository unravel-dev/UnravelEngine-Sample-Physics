using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// How dense a body is against water, for a <see cref="ForceField"/> of kind Water: below 1 it floats, riding as
    /// deep as its density (0.5 floats half under), above 1 it sinks, slowed by the water. Bodies without it count as
    /// 0.8.
    /// </summary>
    public class Buoyant : ScriptComponent
    {
        [Tooltip("Density against water: 0.5 floats half under, 1 hovers, 3 sinks.")]
        public float density = 0.5f;
    }
}
