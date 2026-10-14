using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(EnemyController))]
public sealed class TriggerfishEnemyAnimation : MonoBehaviour
{
    private static readonly int MovingParameter = Animator.StringToHash("IsMoving");
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int AttackParameter = Animator.StringToHash("Attack");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private EnemyController enemy;
    private Animator animator;
    private EnemyController.EnemyAIState previousState;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        animator = enemy.enemyAnimator;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        previousState = enemy.aiState;
    }

    private void Update()
    {
        if (enemy == null || animator == null) return;
        bool paused = PlayerSessionData.IsCombatInputBlocked || BiomechanicalAuraBase.IsCombatPaused || GameJam.TimeManager.Hitstop;
        bool controlled = enemy.IsStaggered || enemy.IsParalyzed || enemy.aiState == EnemyController.EnemyAIState.Pending || enemy.aiState == EnemyController.EnemyAIState.Executing;
        animator.speed = paused || controlled || !enemy.IsAlive ? 0f : 1f;
        animator.SetBool(MovingParameter, !paused && !controlled && enemy.IsAlive && animator.GetFloat(SpeedParameter) > 0.01f);

        if (previousState == EnemyController.EnemyAIState.Attack && enemy.aiState != EnemyController.EnemyAIState.Attack && enemy.aiState != EnemyController.EnemyAIState.Recover)
        {
            animator.ResetTrigger(AttackParameter);
            animator.Play(IdleState, 0, 0f);
        }
        previousState = enemy.aiState;
    }

    private void LateUpdate()
    {
        if (enemy == null) return;
        if (enemy.aiState != EnemyController.EnemyAIState.Attack && enemy.aiState != EnemyController.EnemyAIState.GrabWindup && enemy.aiState != EnemyController.EnemyAIState.Grabbing && enemy.aiState != EnemyController.EnemyAIState.Pending && enemy.aiState != EnemyController.EnemyAIState.Pacified)
            enemy.ApplyFlash(0f, Color.white);
    }
}
