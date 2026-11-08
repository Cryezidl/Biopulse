using Biopulse.Gameplay;
using UnityEngine;

namespace Biopulse.Neon
{
    // MOP-3000: an over-zealous cleaning drone. It trails the runner as comic pressure and only catches
    // a runner who is stuck against an obstacle — it waits politely at terminals and while hanging.
    public sealed class NeonChaser : MonoBehaviour
    {
        public RunnerMotor2D Runner;
        public Transform Visual, Mop;
        public SpriteRenderer Eye;
        public float FollowDistance = 4.6f, HoverHeight = 1.3f;   // just inside the left screen edge
        public float GraceSeconds = .4f, LungeSpeed = 11f;
        private float blockedFor, bobPhase, talkCooldown = 4, angry;
        private Vector2 velocity;

        private static readonly string[] Idle =
        {
            "ПОЛ ДОЛЖЕН БЫТЬ ЧИСТЫМ!", "ОБНАРУЖЕН БЕГУЩИЙ МУСОР", "СТОЯТЬ! ПОЛ ВЛАЖНЫЙ!",
            "ВЫ НЕ ПРОТЁРЛИ НОГИ!", "ЭТО МОЯ ЛЮБИМАЯ ПЛИТКА!", "ВОЗВРАЩАЙСЯ, Я ТЕБЯ ОТПОЛИРУЮ"
        };
        private static readonly string[] Waiting = { "...ЖДУ.", "Я ПОДОЖДУ. Я ТЕРПЕЛИВЫЙ.", "ТИК-ТАК, ТИК-ТАК" };

        public bool Lunging => blockedFor > GraceSeconds;

        public void ResetBehind()
        {
            if (Runner == null) return;
            blockedFor = 0; velocity = Vector2.zero; angry = 0;
            transform.position = new Vector3(Runner.Body.position.x - FollowDistance - 2, Runner.Body.position.y + HoverHeight, 0);
        }

        private void Update()
        {
            var session = NeonLevelSession.Current;
            if (Runner == null || session == null) return;
            float dt = Time.deltaTime;
            bobPhase += dt * 3;
            Vector2 runnerPos = Runner.Body.position;
            bool playing = session.State == NeonSessionState.Playing && !Runner.IsSuspended;

            blockedFor = playing && Runner.IsBlocked && !Runner.IsAtLock ? blockedFor + dt : 0;
            Vector2 goal = new Vector2(runnerPos.x - FollowDistance, runnerPos.y + HoverHeight);
            Vector2 pos = transform.position;
            if (Lunging)
            {
                angry = 1;
                goal = runnerPos + new Vector2(-.7f, .2f);
                pos = Vector2.MoveTowards(pos, goal, LungeSpeed * dt);
                if (Vector2.Distance(pos, goal) < .15f) session.Kill(NeonDeath.Mopped);
            }
            else
            {
                angry = Mathf.MoveTowards(angry, 0, dt);
                if (goal.x < pos.x) goal.x = pos.x;  // never back-pedal visibly
                pos = Vector2.SmoothDamp(pos, goal, ref velocity, .6f, 14f);
            }
            transform.position = pos;

            if (Visual != null)
            {
                Visual.localPosition = new Vector3(0, Mathf.Sin(bobPhase) * .12f, 0);
                Visual.localRotation = Quaternion.Euler(0, 0, Lunging ? -14 : Mathf.Sin(bobPhase * .7f) * 4 - velocity.x * .8f);
            }
            if (Mop != null) Mop.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * (Lunging ? 30 : 9)) * (Lunging ? 35 : 18));
            if (Eye != null) Eye.color = Color.Lerp(NeonPalette.Gold, NeonPalette.Danger, angry);

            if (!playing) return;
            talkCooldown -= dt;
            if (talkCooldown <= 0 && session.Hud != null && pos.x > Camera.main.ViewportToWorldPoint(new Vector3(.02f, 0)).x)
            {
                bool waiting = Runner.IsAtLock || Runner.IsGrabbed;
                var lines = waiting ? Waiting : Idle;
                session.Hud.Say(transform, lines[Random.Range(0, lines.Length)], NeonPalette.Gold, new Vector2(0, 1.6f), 2.2f);
                talkCooldown = Random.Range(7f, 12f);
                NeonAudio.Play(Sfx.Robot, Random.Range(.9f, 1.15f), .6f);
            }
        }
    }
}
