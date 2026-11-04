using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A barrel that blows up when the visitor sets it off, when it is hit hard, or when another barrel blows up
    /// near it. The blast is <see cref="Physics.Explode"/>: every body in the radius is pushed away by how much of it
    /// faces the blast; mannequins in range are knocked down and thrown with it. The station's reset brings the barrel
    /// back.
    /// </summary>
    public class ExplosiveBarrel : StationPart
    {
        [Tooltip("Bodies within this many meters are pushed.")]
        public float radius = 7.0f;

        [Tooltip("Push per square meter a body shows the blast, newton seconds.")]
        public float impulsePerArea = 45.0f;

        [Tooltip("Push a mannequin's bodies share, newton seconds.")]
        public float ragdollImpulse = 260.0f;

        [Tooltip("A hit that changes the barrel's velocity by this much sets it off, meters per second.")]
        public float triggerSpeedChange = 9.0f;

        [Tooltip("Seconds between being set off and blowing up.")]
        public float fuseSeconds = 0.7f;

        [Tooltip("Seconds a barrel caught in another's blast waits before it blows too.")]
        public float chainDelay = 0.18f;

        [Tooltip("The blast: an entity with an Audio Source and particle emitters under it, moved to the barrel and played.")]
        public Entity effect;

        public AudioClip explosionSound;
        public AudioClip fuseSound;

        private PhysicsComponent body;
        private Vector3 lastVelocity;
        private bool isLit;
        private bool hasExploded;

        public override void OnStart()
        {
            body = owner.GetComponent<PhysicsComponent>();
            var button = owner.GetComponent<Interactable>();
            if (button != null)
            {
                button.pressed += _ => Light(fuseSeconds);
            }
        }

        public override void OnFixedUpdate()
        {
            lastVelocity = body.velocity;
        }

        public override void OnCollisionEnter(Collision collision)
        {
            if ((lastVelocity - body.velocity).magnitude >= triggerSpeedChange)
            {
                Light(0.05f);
            }
        }

        /// <summary>Sets the barrel off: it blows up after <paramref name="delay"/> seconds.</summary>
        public void Light(float delay)
        {
            if (isLit || hasExploded)
            {
                return;
            }
            isLit = true;
            PlayAt(owner, fuseSound, 0.6f);
            StartCoroutine(Countdown(delay));
        }

        public override void ResetPart()
        {
            StopAllCoroutines();
            isLit = false;
            hasExploded = false;
            body.bodyType = RigidbodyType.Dynamic;
        }

        private System.Collections.IEnumerator Countdown(float delay)
        {
            yield return new WaitForSeconds(delay);
            Explode();
        }

        private void Explode()
        {
            hasExploded = true;
            Vector3 center = transform.position;
            // Out of the way before the blast, so it pushes nothing but what is around it.
            body.bodyType = RigidbodyType.Kinematic;
            transform.position = center + Vector3.down * 200.0f;
            Physics.Explode(center, radius, impulsePerArea);
            foreach (var entity in Scene.FindEntitiesWithComponent<RagdollCharacter>())
            {
                var character = entity.GetComponent<RagdollCharacter>();
                var ragdoll = entity.GetComponent<RagdollComponent>();
                if (Vector3.Distance(ragdoll.rootPosition, center) < radius)
                {
                    character.KnockDown();
                    ragdoll.AddExplosion(center, radius, ragdollImpulse);
                }
            }
            foreach (var entity in Scene.FindEntitiesWithComponent<ExplosiveBarrel>())
            {
                var other = entity.GetComponent<ExplosiveBarrel>();
                if (other != this && Vector3.Distance(entity.transform.position, center) < radius * 0.6f)
                {
                    other.Light(chainDelay);
                }
            }
            if (effect)
            {
                effect.transform.position = center;
                foreach (var emitter in effect.GetComponentsInChildren<ParticleEmitterComponent>())
                {
                    emitter.Stop();
                    emitter.Play();
                }
                PlayAt(effect, explosionSound, 1.0f);
            }
        }

        private static void PlayAt(Entity entity, AudioClip clip, float volume)
        {
            var source = entity.GetComponent<AudioSourceComponent>();
            if (source == null || clip == null)
            {
                return;
            }
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }
    }
}
