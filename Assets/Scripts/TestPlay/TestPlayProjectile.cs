using UnityEngine;

public class TestPlayProjectile : MonoBehaviour
{
    public TestPlayController owner;
    public TestPlayTargetDummy target;
    public float speed = 25f;
    public float damage = 50f;
    public float lifeSeconds = 3f;
    public float hitRadius = 0.5f;
    public float homingTurnRate = 0f;
    public int downValue;
    public float horizontalImpactForce;
    public float verticalImpactForce;
    public string sourceCommand = "Projectile";
    public TestPlayCombatValueSource valueSource = TestPlayCombatValueSource.UnityFallback;
    [Min(1f)]
    public float originalTickRate = 60f;
    [Min(1)]
    public int maximumCatchUpTicks = 8;

    float age;
    float simulationAccumulator;

    void Update()
    {
        float tickDeltaTime = 1f / Mathf.Max(1f, originalTickRate);
        simulationAccumulator += Mathf.Max(0f, Time.deltaTime);
        int catchUpTicks = 0;
        int catchUpLimit = Mathf.Max(1, maximumCatchUpTicks);
        while (simulationAccumulator + 0.0000001f >= tickDeltaTime && catchUpTicks < catchUpLimit)
        {
            simulationAccumulator -= tickDeltaTime;
            catchUpTicks++;
            if (SimulateOriginalTick(tickDeltaTime))
                return;
        }
    }

    public bool SimulateOriginalTick(float tickDeltaTime)
    {
        bool targetAlive = target != null && target.IsAlive;
        TestPlayProjectileTickResult result = TestPlayCombatCore.TickProjectile(
            new TestPlayProjectileTickInput
            {
                position = transform.position,
                rotation = transform.rotation,
                age = age,
                lifeSeconds = lifeSeconds,
                speed = speed,
                homingTurnRate = homingTurnRate,
                tickDeltaTime = tickDeltaTime,
                targetAlive = targetAlive,
                targetPosition = targetAlive ? target.transform.position : Vector3.zero,
                combinedHitRadius = hitRadius + (targetAlive ? target.hitRadius : 0f)
            });

        age = result.age;
        transform.SetPositionAndRotation(result.position, result.rotation);
        if (result.expired)
        {
            Destroy(gameObject);
            return true;
        }

        if (result.hit && targetAlive)
        {
            TestPlayProjectilePayload payload = new TestPlayProjectilePayload
            {
                source = sourceCommand,
                damage = damage,
                down = downValue,
                horizontalImpactForce = horizontalImpactForce,
                verticalImpactForce = verticalImpactForce,
                valueSource = valueSource
            };
            TestPlayCombatHitResult hit = TestPlayCombatCore.CreateHitResult(payload, transform.forward);
            target.ApplyImpact(hit.damage, hit.impactForce, hit.down, hit.source);
            owner?.NotifyProjectileHit(hit);
            Destroy(gameObject);
            return true;
        }

        return false;
    }
}
