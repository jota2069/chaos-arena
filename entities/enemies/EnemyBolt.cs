using Godot;
using ChaosArena.entities;
using ChaosArena.entities.player;

namespace ChaosArena.entities.enemies
{
    /// <summary>Тёмный снаряд Мага-Призрака: летит по направлению, бьёт игрока при попадании.</summary>
    public partial class EnemyBolt : Area2D
    {
        private static readonly Color BoltColor = new(0.7f, 0.3f, 0.95f);
        private const float Speed = 220f;

        private Vector2 _dir = Vector2.Right;
        private float _damage = 15f;

        public void Init(Vector2 dir, float damage)
        {
            _dir = dir.Normalized();
            _damage = damage;
        }

        public override void _Ready()
        {
            CollisionLayer = 0;
            CollisionMask = 1; // игроки/стены
            Monitoring = true;

            var sprite = new Sprite2D();
            var icon = SpriteSheetSlicer.CroppedIcon("res://assets/projectiles/dark_orb.png");
            if (icon != null)
            {
                sprite.Texture = icon;
                float s = icon.GetHeight() > 0 ? 16f / icon.GetHeight() : 0.1f;
                sprite.Scale = new Vector2(s, s);
            }
            else
            {
                // dark_orb.png отсутствует в проекте — рисуем светящуюся точку.
                sprite.Texture = Fx.DotTexture();
                sprite.Scale = new Vector2(1.6f, 1.6f);
                sprite.Modulate = BoltColor;
            }
            AddChild(sprite);

            AddChild(new PointLight2D
            {
                Texture = Fx.LightTexture(),
                Color = BoltColor,
                Energy = 1f,
                TextureScale = 0.7f,
            });
            AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 6f } });

            BodyEntered += OnBody;
            var t = GetTree().CreateTimer(3.0);
            t.Timeout += QueueFree;
        }

        public override void _PhysicsProcess(double delta)
        {
            Position += _dir * Speed * (float)delta;
        }

        private void OnBody(Node2D body)
        {
            if (body is PlayerBase p && !p.IsDead)
            {
                p.TakeDamage(_damage);
                Fx.HitSpark(GetTree(), GlobalPosition, BoltColor);
                QueueFree();
            }
            else if (body is StaticBody2D)
            {
                QueueFree();
            }
        }
    }
}
