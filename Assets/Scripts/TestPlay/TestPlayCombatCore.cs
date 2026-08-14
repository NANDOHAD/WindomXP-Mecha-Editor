using System;
using UnityEngine;

public enum TestPlayCombatValueSource
{
    Unknown,
    OriginalScriptProfile,
    UnityFallback
}

public enum TestPlayCombatDecisionReason
{
    None,
    ComboQueued,
    SwordCancel,
    MeleeApproachFollowup,
    LocomotionCancel,
    AttackFinished
}

public struct TestPlayCombatSequenceInput
{
    public bool sequenceActive;
    public bool meleePressed;
    public bool meleeApproachActive;
    public bool comboInputPending;
    public bool currentActionIsMelee;
    public int currentActionId;
    public int meleeApproachActionId;
    public int swordCancelActionId;
    public bool swordCancelActionUsable;
    public int actionTick;
    public int meleeApproachMinimumTicks;
    public bool targetReached;
    public bool hasMovementEnergy;
    public int meleeApproachFollowupActionId;
    public bool meleeApproachFollowupUsable;
    public int heldLocomotionActionId;
    public bool heldActionIsLocomotionCancel;
}

public struct TestPlayCombatSequenceDecision
{
    public bool handled;
    public bool comboInputPending;
    public bool clearSequence;
    public bool clearMeleeApproach;
    public bool clearSwordCancel;
    public int transitionActionId;
    public TestPlayCombatDecisionReason reason;
}

public struct TestPlayCombatFinishDecision
{
    public bool clearSequence;
    public bool clearMeleeApproach;
    public bool clearComboInput;
    public bool clearSwordCancel;
    public int transitionActionId;
    public TestPlayCombatDecisionReason reason;
}

public struct TestPlayProjectilePayload
{
    public string source;
    public float damage;
    public int down;
    public float horizontalImpactForce;
    public float verticalImpactForce;
    public float speed;
    public bool homing;
    public TestPlayCombatValueSource valueSource;
}

public struct TestPlayCombatHitResult
{
    public string source;
    public float damage;
    public int down;
    public Vector3 impactForce;
    public TestPlayCombatValueSource valueSource;
}

public struct TestPlayProjectileTickInput
{
    public Vector3 position;
    public Quaternion rotation;
    public float age;
    public float lifeSeconds;
    public float speed;
    public float homingTurnRate;
    public float tickDeltaTime;
    public bool targetAlive;
    public Vector3 targetPosition;
    public float combinedHitRadius;
}

public struct TestPlayProjectileTickResult
{
    public Vector3 position;
    public Quaternion rotation;
    public float age;
    public bool expired;
    public bool hit;
}

/// <summary>
/// Scene-independent combat decisions. Confirmed ANI values remain separate
/// from explicit Unity fallback values so uncertain weapon types are traceable.
/// </summary>
public static class TestPlayCombatCore
{
    public const int AttackCooldownSlotCount = 5;

    public static void ConfigureAttackProfile(
        TestPlayAttackProfile profile,
        int power,
        int down,
        float force,
        float forceY)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        profile.power = power;
        profile.down = down;
        profile.force = force;
        profile.forceY = forceY;
    }

    public static void TickCooldowns(int[] cooldownTicks)
    {
        if (cooldownTicks == null)
            return;

        int count = Mathf.Min(AttackCooldownSlotCount, cooldownTicks.Length);
        for (int i = 0; i < count; i++)
        {
            if (cooldownTicks[i] > 0)
                cooldownTicks[i]--;
        }
    }

    public static bool TrySetCooldown(int[] cooldownTicks, int slot, int ticks)
    {
        if (cooldownTicks == null || slot < 0 ||
            slot >= AttackCooldownSlotCount || slot >= cooldownTicks.Length)
            return false;

        cooldownTicks[slot] = Mathf.Max(0, ticks);
        return true;
    }

    public static int GetCooldown(int[] cooldownTicks, int slot)
    {
        return cooldownTicks != null && slot >= 0 &&
               slot < AttackCooldownSlotCount && slot < cooldownTicks.Length
            ? cooldownTicks[slot]
            : 0;
    }

    public static int ResolveShotInputAction(
        bool currentActionIsBoost,
        int actionTick,
        bool boostShotUsable,
        bool swordEquipped,
        bool switchToGunUsable,
        int shotAction,
        int boostShotAction,
        int switchToGunAction)
    {
        if (currentActionIsBoost)
            return actionTick > 5 && boostShotUsable ? boostShotAction : -1;
        if (swordEquipped)
            return switchToGunUsable ? switchToGunAction : shotAction;
        return shotAction;
    }

    public static int ResolveMeleeInputAction(
        bool swordEquipped,
        bool switchToSwordUsable,
        int direction,
        int switchToSwordAction,
        int forwardMeleeAction,
        bool forwardUsable,
        int neutralMeleeAction,
        bool neutralUsable,
        int leftMeleeAction,
        bool leftUsable,
        int rightMeleeAction,
        bool rightUsable,
        int backMeleeAction,
        bool backUsable)
    {
        if (!swordEquipped && switchToSwordUsable)
            return switchToSwordAction;
        if ((direction == 7 || direction == 8 || direction == 9) && forwardUsable)
            return forwardMeleeAction;
        if (direction == 4 && leftUsable)
            return leftMeleeAction;
        if (direction == 6 && rightUsable)
            return rightMeleeAction;
        if (direction == 2 && backUsable)
            return backMeleeAction;
        return neutralUsable ? neutralMeleeAction : forwardMeleeAction;
    }

    public static bool CanAcceptNormalAttackInput(
        bool currentAnimationMissing,
        int currentLogicalActionId,
        int requestedActionId,
        bool boostMotionActive,
        bool boostShotWindowOpen,
        int boostActionId,
        int boostShotActionId,
        int idleActionId,
        int moveActionId,
        int riseActionId,
        int airMoveActionId,
        int airIdleActionId,
        bool currentActionIsStep)
    {
        if (currentAnimationMissing)
            return true;
        if (currentLogicalActionId == boostActionId)
            return boostMotionActive &&
                   (requestedActionId != boostShotActionId || boostShotWindowOpen);
        return currentLogicalActionId == idleActionId ||
               currentLogicalActionId == moveActionId ||
               currentLogicalActionId == riseActionId ||
               currentLogicalActionId == airMoveActionId ||
               currentLogicalActionId == airIdleActionId ||
               currentActionIsStep;
    }

    public static TestPlayCombatSequenceDecision EvaluateSequence(TestPlayCombatSequenceInput input)
    {
        TestPlayCombatSequenceDecision result = new TestPlayCombatSequenceDecision
        {
            transitionActionId = -1,
            comboInputPending = input.comboInputPending
        };
        if (!input.sequenceActive)
            return result;

        result.handled = true;
        if (input.meleePressed && !input.meleeApproachActive && input.currentActionIsMelee)
        {
            result.comboInputPending = true;
            result.reason = TestPlayCombatDecisionReason.ComboQueued;
        }

        if (result.comboInputPending && input.currentActionIsMelee &&
            input.swordCancelActionId > 0 && input.swordCancelActionUsable)
        {
            result.transitionActionId = input.swordCancelActionId;
            result.comboInputPending = false;
            result.clearMeleeApproach = true;
            result.clearSwordCancel = true;
            result.reason = TestPlayCombatDecisionReason.SwordCancel;
            return result;
        }

        if (input.meleeApproachActive && input.currentActionId == input.meleeApproachActionId)
        {
            int minimumTicks = Mathf.Max(1, input.meleeApproachMinimumTicks);
            if (input.actionTick >= minimumTicks &&
                (input.targetReached || !input.hasMovementEnergy) &&
                input.meleeApproachFollowupUsable)
            {
                result.transitionActionId = input.meleeApproachFollowupActionId;
                result.comboInputPending = false;
                result.clearMeleeApproach = true;
                result.clearSwordCancel = true;
                result.reason = TestPlayCombatDecisionReason.MeleeApproachFollowup;
            }
            return result;
        }

        if (input.currentActionIsMelee && input.actionTick > 15 && input.heldActionIsLocomotionCancel)
        {
            result.transitionActionId = input.heldLocomotionActionId;
            result.comboInputPending = false;
            result.clearSequence = true;
            result.clearMeleeApproach = true;
            result.clearSwordCancel = true;
            result.reason = TestPlayCombatDecisionReason.LocomotionCancel;
        }
        return result;
    }

    public static TestPlayCombatFinishDecision ResolveFinish(
        bool meleeApproachActive,
        int currentActionId,
        int meleeApproachActionId,
        int meleeApproachFollowupActionId,
        bool meleeApproachFollowupUsable,
        bool airborne,
        int airIdleActionId,
        int groundedRecoveryActionId)
    {
        if (meleeApproachActive && currentActionId == meleeApproachActionId &&
            meleeApproachFollowupUsable)
        {
            return new TestPlayCombatFinishDecision
            {
                clearMeleeApproach = true,
                clearComboInput = true,
                clearSwordCancel = true,
                transitionActionId = meleeApproachFollowupActionId,
                reason = TestPlayCombatDecisionReason.MeleeApproachFollowup
            };
        }

        return new TestPlayCombatFinishDecision
        {
            clearSequence = true,
            clearMeleeApproach = true,
            clearComboInput = true,
            clearSwordCancel = true,
            transitionActionId = airborne ? airIdleActionId : groundedRecoveryActionId,
            reason = TestPlayCombatDecisionReason.AttackFinished
        };
    }

    public static TestPlayProjectilePayload CreateProjectilePayload(
        TestPlayAttackProfile profile,
        float fallbackDamage,
        float speed,
        bool homing,
        string source)
    {
        bool usesScriptProfile = profile != null && profile.power > 0;
        return new TestPlayProjectilePayload
        {
            source = source ?? "",
            damage = usesScriptProfile ? profile.power : Mathf.RoundToInt(Mathf.Max(0f, fallbackDamage)),
            down = profile != null ? profile.down : 0,
            horizontalImpactForce = profile != null ? profile.force : 0f,
            verticalImpactForce = profile != null ? profile.forceY : 0f,
            speed = Mathf.Max(0f, speed),
            homing = homing,
            valueSource = usesScriptProfile
                ? TestPlayCombatValueSource.OriginalScriptProfile
                : TestPlayCombatValueSource.UnityFallback
        };
    }

    public static TestPlayCombatHitResult CreateHitResult(
        TestPlayAttackProfile profile,
        float fallbackDamage,
        Vector3 horizontalDirection,
        string source)
    {
        TestPlayProjectilePayload payload = CreateProjectilePayload(
            profile, fallbackDamage, 0f, false, source);
        horizontalDirection.y = 0f;
        horizontalDirection = horizontalDirection.sqrMagnitude > 0.000001f
            ? horizontalDirection.normalized
            : Vector3.forward;
        return new TestPlayCombatHitResult
        {
            source = payload.source,
            damage = payload.damage,
            down = payload.down,
            impactForce = horizontalDirection * payload.horizontalImpactForce +
                          Vector3.up * payload.verticalImpactForce,
            valueSource = payload.valueSource
        };
    }

    public static TestPlayCombatHitResult CreateHitResult(
        TestPlayProjectilePayload payload,
        Vector3 horizontalDirection)
    {
        horizontalDirection.y = 0f;
        horizontalDirection = horizontalDirection.sqrMagnitude > 0.000001f
            ? horizontalDirection.normalized
            : Vector3.forward;
        return new TestPlayCombatHitResult
        {
            source = payload.source,
            damage = payload.damage,
            down = payload.down,
            impactForce = horizontalDirection * payload.horizontalImpactForce +
                          Vector3.up * payload.verticalImpactForce,
            valueSource = payload.valueSource
        };
    }

    public static TestPlayProjectileTickResult TickProjectile(TestPlayProjectileTickInput input)
    {
        float dt = Mathf.Max(0f, input.tickDeltaTime);
        TestPlayProjectileTickResult result = new TestPlayProjectileTickResult
        {
            position = input.position,
            rotation = input.rotation,
            age = input.age + dt
        };
        if (result.age >= Mathf.Max(0f, input.lifeSeconds))
        {
            result.expired = true;
            return result;
        }

        if (input.targetAlive && input.homingTurnRate > 0f)
        {
            Vector3 targetDirection = input.targetPosition - result.position;
            if (targetDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
                result.rotation = Quaternion.RotateTowards(
                    result.rotation, desired, input.homingTurnRate * dt);
            }
        }

        result.position += result.rotation * Vector3.forward * Mathf.Max(0f, input.speed) * dt;
        if (input.targetAlive)
        {
            float radius = Mathf.Max(0f, input.combinedHitRadius);
            result.hit = (input.targetPosition - result.position).sqrMagnitude <= radius * radius;
        }
        return result;
    }
}
