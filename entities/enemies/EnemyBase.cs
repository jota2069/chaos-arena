using System.Collections.Generic;
using Godot;
using ChaosArena.autoload;
using ChaosArena.entities;

namespace ChaosArena.entities.enemies
{
    /// <summary>
    /// Базовый класс врага: HP, анимированное тело (AnimatedSprite2D из спрайтшита),
    /// мигание и анимация при уроне, смерть с анимацией/частицами и награда владельцу.
    ///
    /// Враги создаются из кода (см. EnemySpawner): EnemyBase сам строит коллизию,
    /// хитбокс и тело, если их нет в сцене. Наследники задают лист, статы и поведение.
    /// </summary>
    public abstract partial class EnemyBase : CharacterBody2D
    {
        [Export] public float MaxHealth = 30f;
        [Export] public float MoveSpeed = 80f;
        [Export] public int MinReward = 5;
        [Export] public int MaxReward = 15;

        // Id игрока-владельца арены: награда за убийство идёт ему.
        [Export] public int OwnerPlayerId { get; set; } = 0;

        public float CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        // --- Конфигурация наследника ---
        /// <summary>Путь к спрайтшиту тела. null => используется узел Sprite2D из сцены.</summary>
        protected virtual string SheetPath => null;
        /// <summary>Сколько последних кадров листа — анимация смерти.</summary>
        protected virtual int DeathFrames => 2;
        /// <summary>Высота тела на экране (пикс.).</summary>
        protected virtual float BodyHeight => 30f;
        /// <summary>FPS анимаций (покой, бег).</summary>
        protected virtual (float idle, float run) AnimFps => (6f, 10f);
        /// <summary>Размер тела для коллизии CharacterBody2D.</summary>
        protected virtual Vector2 CollisionSize => new(14, 14);
        /// <summary>Размер хитбокса Area2D (приём пуль).</summary>
        protected virtual Vector2 HitboxSize => new(18, 18);

        // Тело: либо анимированное (из листа), либо легаси-Sprite2D из сцены.
        protected AnimatedSprite2D Body;
        protected Sprite2D LegacySprite;

        // Пока > 0 — держим анимацию hurt, не перебивая её ходьбой.
        protected float HurtTime;

        private EventBus _eventBus;

        public override void _Ready()
        {
            MotionMode = MotionModeEnum.Floating; // top-down (см. CLAUDE.md)
            IsDead = false;
            _eventBus = GetNode<EventBus>("/root/EventBus");

            EnsurePhysicsNodes();
            BuildBody();

            OnReady();                 // наследник задаёт статы
            CurrentHealth = MaxHealth; // после OnReady — учитываем заданное MaxHealth
        }

        public override void _Process(double delta)
        {
            if (HurtTime > 0f) HurtTime -= (float)delta;
        }

        protected virtual void OnReady() { }

        /// <summary>Эффекты смерти у наследника (частицы, тряска). Вызывается до удаления.</summary>
        protected virtual void OnDeath(Vector2 position) { }

        // Создаёт коллизию тела и хитбокс, если их нет (враг из кода, не из сцены).
        private void EnsurePhysicsNodes()
        {
            CollisionLayer = 1;
            CollisionMask = 1;

            if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null)
                AddChild(new CollisionShape2D
                {
                    Name = "CollisionShape2D",
                    Shape = new RectangleShape2D { Size = CollisionSize },
                });

            if (GetNodeOrNull<Area2D>("Hitbox") == null)
            {
                var hb = new Area2D { Name = "Hitbox", CollisionLayer = 4, CollisionMask = 0 };
                hb.AddToGroup("enemy_hitboxes");
                hb.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = HitboxSize } });
                AddChild(hb);
            }
        }

        private void BuildBody()
        {
            var path = SheetPath;
            if (path != null)
            {
                Body = SpriteSheetSlicer.BuildBody(path, BodyHeight, EnemyPlan, "idle");
                AddChild(Body);
            }
            else
            {
                LegacySprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            }
        }

        // План анимаций тела по числу найденных кадров N (последние DeathFrames — смерть).
        private IEnumerable<AnimSpec> EnemyPlan(int n)
        {
            int d = Mathf.Clamp(n - 1, 1, DeathFrames);
            int walkEnd = Mathf.Max(1, n - d);
            var (idleFps, runFps) = AnimFps;

            yield return new AnimSpec("idle", idleFps, true, SpriteSheetSlicer.Range(0, Mathf.Min(2, walkEnd)));
            yield return new AnimSpec("run", runFps, true, SpriteSheetSlicer.Range(0, walkEnd));
            yield return new AnimSpec("attack", runFps, false, new[] { Mathf.Max(0, walkEnd - 1) });
            yield return new AnimSpec("hurt", 8f, false, SpriteSheetSlicer.Range(0, Mathf.Min(2, walkEnd)));
            yield return new AnimSpec("death", 6f, false, SpriteSheetSlicer.Range(walkEnd, d));
        }

        // --- Помощники анимации (работают и с Body, и с легаси-спрайтом) ---

        protected void FlipBody(float dirX)
        {
            if (Mathf.Abs(dirX) < 0.01f) return;
            bool left = dirX < 0f;
            if (Body != null) Body.FlipH = left;
            else if (LegacySprite != null)
            {
                float a = Mathf.Abs(LegacySprite.Scale.Y);
                LegacySprite.Scale = new Vector2(left ? -a : a, a);
            }
        }

        protected void PlayLoco(bool moving)
        {
            if (Body?.SpriteFrames == null || HurtTime > 0f) return;
            string want = moving ? "run" : "idle";
            if (Body.Animation != want && Body.SpriteFrames.HasAnimation(want))
                Body.Play(want);
        }

        protected void PlayAnim(string name)
        {
            if (Body?.SpriteFrames != null && Body.SpriteFrames.HasAnimation(name))
                Body.Play(name);
        }

        // Добавлено virtual, чтобы наследники могли переопределить (напр. щит Скелета).
        public virtual void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0) return;

            CurrentHealth -= amount;
            FlashHurt();
            PlayAnim("hurt");
            HurtTime = 0.15f;

            if (CurrentHealth <= 0f)
                Die();
        }

        private void FlashHurt()
        {
            CanvasItem vis = Body != null ? Body : LegacySprite;
            if (vis == null) return;
            var t = CreateTween();
            t.TweenProperty(vis, "modulate", new Color(1f, 0.3f, 0.3f), 0.08f);
            t.TweenProperty(vis, "modulate", Colors.White, 0.12f);
        }

        /// <summary>
        /// Усиление врага (саботаж «Проклятие Великана»): множит HP и размер.
        /// </summary>
        public void Empower(float hpMultiplier, float scaleMultiplier)
        {
            MaxHealth *= hpMultiplier;
            CurrentHealth *= hpMultiplier;
            Scale *= scaleMultiplier;
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;

            int reward = GD.RandRange(MinReward, MaxReward);
            _eventBus.EmitSignal(EventBus.SignalName.EnemyDied, GlobalPosition, reward, OwnerPlayerId);

            OnDeath(GlobalPosition);

            // Стоп ИИ/коллизий, проигрываем смерть, затем удаляемся.
            SetPhysicsProcess(false);
            Velocity = Vector2.Zero;
            DisableCollision();
            PlayAnim("death");

            var t = GetTree().CreateTimer(0.45);
            t.Timeout += QueueFree;
        }

        private void DisableCollision()
        {
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D")
                ?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
            GetNodeOrNull<Area2D>("Hitbox")
                ?.SetDeferred(Area2D.PropertyName.Monitorable, false);
        }
    }
}
