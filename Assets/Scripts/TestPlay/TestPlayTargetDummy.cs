using UnityEngine;

public class TestPlayTargetDummy : MonoBehaviour
{
    public float hp = 1000f;
    public float hitRadius = 1.5f;
    public int stateId = 1;
    public bool logHits = true;
    public Vector3 lastImpactForce;
    public int lastDownValue;

    public bool IsAlive => hp > 0f;

    public void ApplyDamage(float damage, Vector3 hitPoint, string source)
    {
        if (damage <= 0f)
            return;

        hp = Mathf.Max(0f, hp - damage);
        stateId = hp > 0f ? 3 : 6;

        if (logHits)
            Debug.Log($"[TestPlayTargetDummy] Hit {damage} from {source}. HP={hp}");
    }

    public void ApplyImpact(float damage, Vector3 impactForce, int downValue, string source)
    {
        lastImpactForce = impactForce;
        lastDownValue = downValue;
        ApplyDamage(damage, transform.position, source);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
