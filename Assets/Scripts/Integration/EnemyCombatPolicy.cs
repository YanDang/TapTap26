using UnityEngine;

public enum EnemyControlKind { Knockback, Slow, Paralysis, Pacify, Defect, Taunt, Wet, Stagger }

/// <summary>Optional runtime policy. Null retains the ordinary enemy rules. Never owns HP.
/// Execution eligibility is queried both before animation and at settlement (including Executing).
/// Override ExecutionDamage for nonlethal executions; Parried owns custom posture response.</summary>
public class EnemyCombatPolicy
{
    public virtual bool ReceiveDamage(EnemyController enemy, ref float damage, ref float guardDamage) => true;
    public virtual bool InterruptOnHit => true;
    public virtual bool VulnerabilityBonus => true;
    public virtual bool Control(EnemyController enemy, EnemyControlKind kind, ref float strength, ref float duration) => true;
    public virtual bool CanExecute(EnemyController enemy) => true;
    public virtual float ExecutionDamage(EnemyController enemy) => enemy.currentHp;
    public virtual void Parried(EnemyController enemy) { enemy.aiState=EnemyController.EnemyAIState.Pending; }
}

public enum PlayerDamageResult { Ignored, Dodged, Blocked, Damaged }
