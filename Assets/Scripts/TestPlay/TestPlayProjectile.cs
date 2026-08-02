using UnityEngine;

public class TestPlayProjectile : MonoBehaviour
{
    public TestPlayTargetDummy target;
    public float speed = 25f;
    public float damage = 50f;
    public float lifeSeconds = 3f;
    public float hitRadius = 0.5f;
    public float homingTurnRate = 0f;
    public string sourceCommand = "Projectile";

    float age;

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (target != null && target.IsAlive && homingTurnRate > 0f)
        {
            Vector3 dir = target.transform.position - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, homingTurnRate * dt);
            }
        }

        transform.position += transform.forward * speed * dt;

        if (target != null && target.IsAlive)
        {
            float radius = hitRadius + target.hitRadius;
            if ((target.transform.position - transform.position).sqrMagnitude <= radius * radius)
            {
                target.ApplyDamage(damage, transform.position, sourceCommand);
                Destroy(gameObject);
            }
        }
    }
}
