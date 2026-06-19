using Godot;
using System.Collections.Generic;
using ChaosArena.entities;
using ChaosArena.entities.enemies;
using ChaosArena.entities.player;
using ChaosArena.scenes;

namespace ChaosArena.entities.weapons
{
    /// <summary>
    /// Снаряд: летит по заданному направлению, наносит урон врагу при попадании
    /// (по телу или хитбоксу) и самоуничтожается по таймеру. CollisionLayer=2, Mask=1|4.
    ///
    /// PvP-режим включается, когда OwnerPlayerId >= 0: пуля бьёт ЧУЖОГО игрока,
    /// гасится колоннами/стенами и применяет боевые эффекты Оракула (Инферно/Вампир).
    /// При OwnerPlayerId == -1 поведение полностью совпадает с прежним PvE.
    ///
    /// Визуал снаряда (спрайт + шлейф частиц + свет) задаётся через <see cref="SetVisual"/>
    /// по типу оружия. При попадании — вспышка (Fx.HitSpark).
    /// </summary>
    public partial class Bullet : Area2D
    {
        [Export] public float Speed = 300f;
        [Export] public float Damage = 10f;
        [Export] public float Lifetime = 2f;

        // --- PvP. По умолчанию -1 => обычная PvE-пуля (бьёт только врагов). ---
        public int OwnerPlayerId = -1;     // id стрелявшего; пуля не бьёт своего владельца
        public bool Incendiary = false;    // поджигает цель (эффект «Инферно»)
        public float Vampirism = 0f;       // % нанесённого урона лечит владельца («Вампир»)

        /// <summary>Тип визуала снаряда (по оружию).</summary>
        public enum Visual { Default, Fire, Ice, Lightning, Dark, Bullet, Grenade, Portal }

        // Описание визуала: иконка снаряда, цвет (свет/частицы/вспышка), радиус света (0 — нет).
        private readonly struct VisualDef
        {
            public readonly string Icon;
            public readonly Color Color;
            public readonly float LightRadius;
            public VisualDef(string icon, Color color, float lightRadius)
            { Icon = icon; Color = color; LightRadius = lightRadius; }
        }

        private const string ProjDir = "res://assets/projectiles/";
        private static readonly Dictionary<Visual, VisualDef> Defs = new()
        {
            [Visual.Default]   = new(ProjDir + "bullet.png",     new Color(0.78f, 0.78f, 0.82f), 0f),
            [Visual.Fire]      = new(ProjDir + "fireball.png",   new Color(1f, 0.55f, 0.15f),    80f),
            [Visual.Ice]       = new(ProjDir + "ice_arrow.png",  new Color(0.55f, 0.8f, 1f),     60f),
            [Visual.Lightning] = new(ProjDir + "lightning.png",  new Color(1f, 1f, 0.45f),       100f),
            [Visual.Dark]      = new(ProjDir + "dark_orb.png",   new Color(0.65f, 0.35f, 0.95f), 70f),
            [Visual.Bullet]    = new(ProjDir + "bullet.png",     new Color(0.78f, 0.78f, 0.82f), 0f),
            [Visual.Grenade]   = new(ProjDir + "grenade.png",    new Color(0.4f, 0.4f, 0.4f),    0f),
            [Visual.Portal]    = new(ProjDir + "portal_orb.png", new Color(0.25f, 0.9f, 1f),     90f),
        };

        private static readonly PackedScene FloatingDamageScene =
            GD.Load<PackedScene>("res://scenes/FloatingDamage.tscn");

        private Vector2 _direction;
        private Visual _visual = Visual.Default;

        // Защита от двойного попадания: тело врага (слой 1) и его хитбокс (слой 4)
        // могут сработать в одном кадре до отложенного QueueFree.
        private bool _hasHit;

        // Владелец пули — для вампиризма (хранится только в PvP).
        private PlayerBase _owner;

        public void Init(Vector2 direction)
        {
            _direction = direction.Normalized();
        }

        /// <summary>Назначает владельца пули (для вампиризма). Используется в PvP.</summary>
        public void SetOwner(PlayerBase owner) => _owner = owner;

        /// <summary>Задаёт тип визуала снаряда. Можно вызывать до добавления в дерево.</summary>
        public void SetVisual(Visual visual)
        {
            _visual = visual;
            if (IsInsideTree()) ApplyVisual();
        }

        public override void _Ready()
        {
            CollisionLayer = 2;
            CollisionMask = 1 | 4;

            BodyEntered += OnBodyEntered;
            AreaEntered += OnAreaEntered;

            ApplyVisual();

            // Автоудаление по таймеру
            var timer = GetTree().CreateTimer(Lifetime);
            timer.Timeout += QueueFree;
        }

        public override void _PhysicsProcess(double delta)
        {
            Position += _direction * Speed * (float)delta;
        }

        // Настраивает спрайт/шлейф/свет под выбранный тип снаряда.
        private void ApplyVisual()
        {
            var def = Defs[_visual];

            var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null)
            {
                var icon = SpriteSheetSlicer.CroppedIcon(def.Icon);
                if (icon != null)
                {
                    sprite.Texture = icon;
                    float s = icon.GetHeight() > 0 ? 20f / icon.GetHeight() : 0.1f;
                    sprite.Scale = new Vector2(s, s);
                    sprite.Rotation = _direction.Angle();
                    sprite.SelfModulate = Colors.White;
                }
                else
                {
                    // Файла иконки нет (напр. dark_orb отсутствует) — тинтуем дефолт.
                    sprite.SelfModulate = def.Color;
                }
            }

            // Шлейф частиц позади снаряда (в мировых координатах — остаётся как хвост).
            AddChild(new CpuParticles2D
            {
                Texture = Fx.DotTexture(),
                Emitting = true,
                OneShot = false,
                LocalCoords = false,
                Amount = 18,
                Lifetime = 0.35f,
                Direction = -_direction,
                Spread = 12f,
                InitialVelocityMin = 10f,
                InitialVelocityMax = 40f,
                Gravity = Vector2.Zero,
                ScaleAmountMin = 1f,
                ScaleAmountMax = 2.2f,
                Color = def.Color,
            });

            // Источник света снаряда.
            if (def.LightRadius > 0f)
                AddChild(new PointLight2D
                {
                    Texture = Fx.LightTexture(),
                    Color = def.Color,
                    Energy = 1.1f,
                    TextureScale = def.LightRadius / 64f,
                });
        }

        private void OnBodyEntered(Node2D body)
        {
            if (_hasHit) return;

            // PvP: пуля бьёт чужого игрока и гасится колоннами/стенами.
            if (OwnerPlayerId >= 0)
            {
                if (body is PlayerBase p && p.PlayerId != OwnerPlayerId && !p.IsDead)
                {
                    _hasHit = true;
                    HitPlayer(p);
                    SpawnHit();
                    QueueFree();
                }
                else if (body is StaticBody2D)
                {
                    _hasHit = true;
                    SpawnHit();
                    QueueFree();
                }
                return;
            }

            // PvE: бьёт только врагов.
            if (body is EnemyBase enemy)
            {
                _hasHit = true;
                HitEnemy(enemy);
                QueueFree();
            }
        }

        private void OnAreaEntered(Area2D area)
        {
            if (_hasHit) return;
            if (OwnerPlayerId >= 0) return; // в PvP по Area не бьём (хитбоксов врагов нет)

            // Хитбокс врага — его родитель EnemyBase
            if (area.IsInGroup("enemy_hitboxes") && area.GetParent() is EnemyBase enemy)
            {
                _hasHit = true;
                HitEnemy(enemy);
                QueueFree();
            }
        }

        // Урон врагу + всплывающая цифра урона + вспышка попадания.
        private void HitEnemy(EnemyBase enemy)
        {
            float before = enemy.CurrentHealth;
            enemy.TakeDamage(Damage);
            float dealt = before - enemy.CurrentHealth;
            SpawnFloatingDamage(enemy, dealt > 0f ? dealt : Damage);
            SpawnHit();
        }

        // Наносит урон игроку, применяет вампиризм/поджог и показывает всплывающую цифру.
        private void HitPlayer(PlayerBase target)
        {
            if (target.IsDead) return;

            float before = target.CurrentHealth;
            target.TakeDamage(Damage);
            float dealt = before - target.CurrentHealth; // фактический урон (с учётом множителей/щита)

            // Вампиризм: владелец лечится на % фактически нанесённого урона.
            if (Vampirism > 0f && dealt > 0f && _owner != null
                && GodotObject.IsInstanceValid(_owner) && !_owner.IsDead)
                _owner.Heal(dealt * Vampirism / 100f);

            // Поджог: +5 урона/сек на 4 сек.
            if (Incendiary)
                target.Ignite(5f, 4f);

            SpawnFloatingDamage(target, dealt > 0f ? dealt : Damage);

            // Эффектное убийство в PvP: замедление времени + белая вспышка.
            if (target.IsDead)
                Fx.PvpKill(GetTree());
        }

        // Вспышка попадания в точке снаряда (цвет — под тип снаряда).
        private void SpawnHit()
        {
            Fx.HitSpark(GetTree(), GlobalPosition, Defs[_visual].Color);
        }

        private static void SpawnFloatingDamage(Node2D target, float amount)
        {
            if (FloatingDamageScene == null) return;
            var label = FloatingDamageScene.Instantiate<FloatingDamage>();
            target.AddChild(label);
            label.Position = new Vector2(0f, -24f);
            label.Setup(amount);
        }
    }
}
