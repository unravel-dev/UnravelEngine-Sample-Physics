using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A part of a station with state of its own beyond where its bodies are: a barrel that blew up, a gate that
    /// opened, a lift that moved. <see cref="Station.ResetStation"/> calls <see cref="ResetPart"/> on every part under
    /// the station before it puts the bodies back.
    /// </summary>
    public abstract class StationPart : ScriptComponent
    {
        /// <summary>Puts the part back as play started.</summary>
        public abstract void ResetPart();
    }
}
