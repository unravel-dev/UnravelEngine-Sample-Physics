using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// Plays a sound when the body hits something, louder the harder it hits. How hard is the change in the body's
    /// velocity the contact made, so a box dropped on the floor knocks and a box resting on it stays quiet.
    /// </summary>
    public class ImpactSound : ScriptComponent
    {
        [Tooltip("One of these plays per impact.")]
        public AudioClip[] clips;

        [Tooltip("Impacts that change the velocity by less than this make no sound, meters per second.")]
        public float minSpeedChange = 1.2f;

        [Tooltip("An impact that changes the velocity by this much plays at full volume, meters per second.")]
        public float fullSpeedChange = 9.0f;

        [Tooltip("Shortest time between two sounds, seconds.")]
        public float minInterval = 0.08f;

        [Tooltip("Pitch varies by up to this much either way, so repeated knocks do not sound the same.")]
        [Range(0, 0.5f)]
        public float pitchVariation = 0.12f;

        private PhysicsComponent body;
        private AudioSourceComponent source;
        private Vector3 lastVelocity;
        private float readyTime;

        public override void OnStart()
        {
            body = owner.GetComponent<PhysicsComponent>();
            source = owner.GetComponent<AudioSourceComponent>() ?? owner.AddComponent<AudioSourceComponent>();
            source.spatial = true;
            source.minDistance = 2.0f;
            source.maxDistance = 35.0f;
        }

        public override void OnFixedUpdate()
        {
            if (body != null)
            {
                lastVelocity = body.velocity;
            }
        }

        public override void OnCollisionEnter(Collision collision)
        {
            if (body == null || clips == null || clips.Length == 0 || Time.time < readyTime)
            {
                return;
            }
            float change = (lastVelocity - body.velocity).magnitude;
            if (change < minSpeedChange)
            {
                return;
            }
            readyTime = Time.time + minInterval;
            float loudness = Mathf.InverseLerp(minSpeedChange, fullSpeedChange, change);
            source.clip = clips[Random.Range(0, clips.Length)];
            source.volume = 0.15f + 0.85f * loudness;
            source.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
            source.Play();
        }
    }
}
