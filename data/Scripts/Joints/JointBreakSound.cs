using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// Plays a sound when a joint on this body breaks: <see cref="ScriptComponent.OnJointBreak"/> reaches the joint's
    /// entity and both of its bodies.
    /// </summary>
    public class JointBreakSound : ScriptComponent
    {
        public AudioClip[] clips;

        public override void OnJointBreak(JointBreak jointBreak)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }
            var source = owner.GetComponent<AudioSourceComponent>() ?? owner.AddComponent<AudioSourceComponent>();
            source.spatial = true;
            source.clip = clips[Random.Range(0, clips.Length)];
            source.pitch = Random.Range(0.9f, 1.1f);
            source.Play();
        }
    }
}
