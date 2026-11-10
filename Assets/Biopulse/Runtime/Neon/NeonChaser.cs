using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    // Continuous pursuit. Proximity commits an attack; hanging and terminal interactions never grant immunity.
    public sealed class NeonChaser : MonoBehaviour
    {
        public RunnerMotor2D Runner;
        public Transform Visual, Mop;
        public SpriteRenderer Eye;
        public float FollowDistance = 4.6f, HoverHeight = 1.3f;   // initial gap; pursuit then moves independently
        public float GraceSeconds = .4f, LungeSpeed = 11f;
        [Header("Pursuit")]
        public float ChaseSpeedRatio = .97f;
        public float AccelerationPerSecond = .003f, MaxSpeedBonus = .6f;
        public float CatchUpDistance = 8f, CatchUpGain = .5f, MaxCatchUpBonus = 3f;
        [Header("Proximity attack")]
        public float AttackDistance = 2.8f, EscapeDistance = 5f, ContactDistance = .15f;
        [Range(.05f, 1f)] public float TerminalSpeedMultiplier = .2f;
        public float PursuitSeconds { get; private set; }
        public float CurrentSpeed => Runner == null ? 0 : Runner.RunSpeed * ChaseSpeedRatio +
            Mathf.Min(MaxSpeedBonus, PursuitSeconds * AccelerationPerSecond);
        public float MovementSpeed { get; private set; }
        public bool IsAttacking => attackTime >= 0;
        private float attackTime = -1, bobPhase, talkCooldown = 4, angry;
        private Vector2 velocity;

        private static readonly string[] Idle =
        {
            "ПОЛ ДОЛЖЕН БЫТЬ ЧИСТЫМ!", "ОБНАРУЖЕН БЕГУЩИЙ МУСОР", "СТОЯТЬ! ПОЛ ВЛАЖНЫЙ!",
            "ВЫ НЕ ПРОТЁРЛИ НОГИ!", "ЭТО МОЯ ЛЮБИМАЯ ПЛИТКА!", "ВОЗВРАЩАЙСЯ, Я ТЕБЯ ОТПОЛИРУЮ"
        };
        private static readonly string[] Closing = { "ДОЛГО НЕ ВИСИ!", "Я УЖЕ РЯДОМ!", "ВАШЕ ВРЕМЯ ИСТЕКАЕТ!" };

        public bool Lunging => IsAttacking && attackTime >= GraceSeconds;

        public void ResetBehind()
        {
            if (Runner == null) return;
            attackTime = -1; PursuitSeconds = 0; MovementSpeed = 0; velocity = Vector2.zero; angry = 0;
            transform.position = new Vector3(Runner.Body.position.x - FollowDistance - 2, Runner.Body.position.y + HoverHeight, 0);
        }

        private bool CanPursue(NeonLevelSession session)
        {
            if (Runner == null || session == null || session.State != NeonSessionState.Playing || Runner.IsSuspended) return false;
            var pipeline = session.Input != null ? session.Input.Pipeline : null;
            return session.Input == null || session.Input.isActiveAndEnabled && session.Input.Source != null &&
                session.Input.Source.Status == Biopulse.Emg.EmgSourceStatus.Streaming && pipeline != null &&
                pipeline.Frame.IsValid && !pipeline.ResumeRequired && !pipeline.WaitingForRest;
        }

        private void StepChase(NeonLevelSession session, float dt)
        {
            MovementSpeed = 0; velocity = Vector2.zero;
            if (!CanPursue(session) || dt <= 0) return;
            PursuitSeconds += dt;
            Vector2 runnerPos = Runner.Body.position;
            Vector2 pos = transform.position;
            float distance = Vector2.Distance(pos, runnerPos);
            if (IsAttacking && distance > Mathf.Max(EscapeDistance, AttackDistance + .5f)) attackTime = -1;
            if (!IsAttacking && distance <= AttackDistance)
            {
                attackTime = 0;
                session.Hud?.Toast("MOP-3000 АТАКУЕТ!", NeonPalette.Danger);
                NeonAudio.Play(Sfx.Robot, 1.3f, .8f);
            }
            else if (IsAttacking) attackTime += dt;

            // Slow only while a closed terminal is being worked. Once opened, full pressure resumes
            // even if the player still holds the opening combination instead of releasing the channels.
            bool workingTerminal = Runner.IsAtLock && (session.Locks == null || session.Locks.Current == null || !session.Locks.Current.IsOpen);
            float multiplier = workingTerminal ? Mathf.Clamp(TerminalSpeedMultiplier, .05f, 1) : 1;
            float catchUp = Mathf.Min(MaxCatchUpBonus, Mathf.Max(0, distance - CatchUpDistance) * CatchUpGain);
            MovementSpeed = (Lunging ? LungeSpeed : CurrentSpeed + catchUp) * multiplier;
            Vector2 goal = runnerPos + (IsAttacking ? new Vector2(-.65f, .2f) : Vector2.up * HoverHeight);
            Vector2 next = Vector2.MoveTowards(pos, goal, MovementSpeed * dt);
            velocity = (next - pos) / dt;
            transform.position = next;
            if (Lunging && Vector2.Distance(next, goal) <= ContactDistance) session.Kill(NeonDeath.Mopped);
        }

        private void Update()
        {
            var session = NeonLevelSession.Current;
            if (Runner == null || session == null) return;
            float dt = Time.deltaTime;
            bool playing = CanPursue(session);
            StepChase(session, dt);
            if (playing)
            {
                bobPhase += dt * 3;
                angry = Mathf.MoveTowards(angry, IsAttacking ? 1 : 0, dt * 3);
            }

            if (Visual != null)
            {
                Visual.localPosition = new Vector3(0, Mathf.Sin(bobPhase) * .12f, 0);
                Visual.localRotation = Quaternion.Euler(0, 0, Lunging ? -14 : Mathf.Sin(bobPhase * .7f) * 4 - velocity.x * .8f);
            }
            if (Mop != null) Mop.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(bobPhase * (Lunging ? 10 : 3)) * (Lunging ? 35 : 18));
            if (Eye != null) Eye.color = Color.Lerp(NeonPalette.Gold, NeonPalette.Danger, angry);

            if (!playing) return;
            talkCooldown -= dt;
            if (talkCooldown <= 0 && !IsAttacking && session.Hud != null && Camera.main != null && transform.position.x > Camera.main.ViewportToWorldPoint(new Vector3(.02f, 0)).x)
            {
                bool waiting = Runner.IsAtLock || Runner.IsGrabbed;
                var lines = waiting ? Closing : Idle;
                session.Hud.Say(transform, lines[Random.Range(0, lines.Length)], NeonPalette.Gold, new Vector2(0, 1.6f), 2.2f);
                talkCooldown = Random.Range(7f, 12f);
                NeonAudio.Play(Sfx.Robot, Random.Range(.9f, 1.15f), .6f);
            }
        }
    }
}
