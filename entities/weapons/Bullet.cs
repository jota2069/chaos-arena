using Godot;
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

        private static readonly PackedScene FloatingDamageScene =
            GD.Load<PackedScene>("res://scenes/FloatingDamage.tscn");

        private Vector2 _direction;

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

        public override void _Ready()
        {
            CollisionLayer = 2;
            CollisionMask = 1 | 4;

            BodyEntered += OnBodyEntered;
            AreaEntered += OnAreaEntered;

            // Автоудаление по таймеру
            var timer = GetTree().CreateTimer(Lifetime);
            timer.Timeout += QueueFree;
        }

        public override void _PhysicsProcess(double delta)
        {
            Position += _direction * Speed * (float)delta;
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
                    QueueFree();
                }
                else if (body is StaticBody2D)
                {
                    _hasHit = true;
                    QueueFree();
                }
                return;
            }

            // PvE (без изменений): бьёт только врагов.
            if (body is EnemyBase enemy)
            {
                _hasHit = true;
                enemy.TakeDamage(Damage);
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
                enemy.TakeDamage(Damage);
                QueueFree();
            }
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
