using Godot;
using ChaosArena.entities;
using ChaosArena.entities.player;

namespace ChaosArena.entities.enemies
{
    /// <summary>Паутина Паука на полу: замедляет игрока на -70% на 2 сек. Живёт 5 сек.</summary>
    public partial class EnemyWeb : Area2D
    {
        public override void _Ready()
        {
            CollisionLayer = 0;
            CollisionMask = 1;
            Monitoring = true;

            AddChild(new Sprite2D
            {
                Texture = Fx.DotTexture(),
                Scale = new Vector2(4f, 4f),
                Modulate = new Color(0.85f, 0.85f, 0.9f, 0.32f),
                ZIndex = -1,
            });
            AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 28f } });

            BodyEntered += OnBody;
            var t = GetTree().CreateTimer(5.0);
            t.Timeout += QueueFree;
        }

        private void OnBody(Node2D body)
        {
            if (body is PlayerBase p && !p.IsDead)
                p.ApplySlow(0.3f, 2f); // -70% скорости на 2 сек
        }
    }
}
