using Godot;
using ChaosArena.entities.player;

namespace ChaosArena.systems
{
    /// <summary>Общие хелперы визуала саботажа.</summary>
    public static class SabotageFx
    {
        /// <summary>Радиальная текстура света для затмения (центр непрозрачный → края прозрачные).</summary>
        public static Texture2D MakeLightTexture()
        {
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1f, 1f, 1f, 1f));
            gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
            return new GradientTexture2D
            {
                Gradient = gradient,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
                Width = 256,
                Height = 256,
            };
        }
    }

    /// <summary>
    /// Статичная зона саботажа: паутина (замедление при входе) или мина
    /// (невидима, при наступании — урон + оглушение, затем исчезает).
    /// Действует только на игрока с заданным id.
    /// </summary>
    public partial class SabotageZone : Area2D
    {
        public enum ZoneKind { Web, Mine }

        private ZoneKind _kind;
        private int _targetId;
        private bool _spent;

        public static SabotageZone Create(ZoneKind kind, Vector2 position, int targetId)
        {
            return new SabotageZone { _kind = kind, _targetId = targetId, Position = position };
        }

        public override void _Ready()
        {
            CollisionLayer = 0;
            CollisionMask = 1;
            Monitoring = true;

            float radius = _kind == ZoneKind.Web ? 46f : 24f;
            AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });

            if (_kind == ZoneKind.Web)
            {
                AddChild(new Sprite2D
                {
                    Texture = GD.Load<Texture2D>("res://assets/ui/sabotage/sabotage_04_spider_web.png"),
                    Scale = new Vector2(0.06f, 0.06f),
                    Modulate = new Color(1f, 1f, 1f, 0.8f),
                });
            }
            // Мина невидима — без спрайта.

            BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is not PlayerBase p || p.PlayerId != _targetId || p.IsDead) return;

            if (_kind == ZoneKind.Web)
            {
                // -70% скорость на 3 сек.
                p.SpeedMultiplier *= 0.3f;
                var timer = GetTree().CreateTimer(3f);
                timer.Timeout += () => { if (GodotObject.IsInstanceValid(p)) p.SpeedMultiplier /= 0.3f; };
            }
            else
            {
                if (_spent) return;
                _spent = true;
                p.TakeDamage(20f);
                p.Stun(1f);
                QueueFree();
            }
        }
    }

    /// <summary>
    /// Подвижная опасность саботажа: крыса (преследует игрока, 2 урона) или
    /// торнадо (бродит, 10 урона + отбрасывание). Самоуничтожается по таймеру.
    /// </summary>
    public partial class SabotageChaser : Area2D
    {
        public enum ChaserKind { Rat, Tornado }

        private ChaserKind _kind;
        private int _targetId;
        private float _speed;
        private float _damage;
        private bool _knockback;
        private float _lifetime;
        private float _contactCd;
        private Vector2 _wanderDir;
        private readonly RandomNumberGenerator _rng = new();

        public static SabotageChaser Create(ChaserKind kind, Vector2 position, int targetId)
        {
            var c = new SabotageChaser { _kind = kind, _targetId = targetId, Position = position };
            if (kind == ChaserKind.Rat)
            {
                c._speed = 200f; c._damage = 2f; c._knockback = false; c._lifetime = 20f;
            }
            else
            {
                c._speed = 130f; c._damage = 10f; c._knockback = true; c._lifetime = 12f;
            }
            return c;
        }

        public override void _Ready()
        {
            _rng.Randomize();
            CollisionLayer = 0;
            CollisionMask = 1;
            Monitoring = true;

            float radius = _kind == ChaserKind.Rat ? 9f : 32f;
            AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });

            if (_kind == ChaserKind.Rat)
            {
                AddChild(MakeCircle(8f, new Color(0.45f, 0.32f, 0.28f)));
            }
            else
            {
                var sprite = new Sprite2D
                {
                    Texture = GD.Load<Texture2D>("res://assets/ui/sabotage/sabotage_06_tornado.png"),
                    Scale = new Vector2(0.08f, 0.08f),
                };
                AddChild(sprite);
                var spin = sprite.CreateTween().SetLoops();
                spin.TweenProperty(sprite, "rotation", Mathf.Tau, 0.6).AsRelative();
            }

            _wanderDir = Vector2.Right.Rotated(_rng.RandfRange(0f, Mathf.Tau));

            var life = GetTree().CreateTimer(_lifetime);
            life.Timeout += QueueFree;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _contactCd = Mathf.Max(0f, _contactCd - dt);

            Vector2 dir;
            if (_kind == ChaserKind.Rat)
            {
                var target = FindTarget();
                dir = target != null ? (target.GlobalPosition - GlobalPosition).Normalized() : _wanderDir;
            }
            else
            {
                if (_rng.Randf() < 0.02f)
                    _wanderDir = Vector2.Right.Rotated(_rng.RandfRange(0f, Mathf.Tau));
                dir = _wanderDir;
            }
            Position += dir * _speed * dt;

            if (_contactCd > 0f) return;

            foreach (var body in GetOverlappingBodies())
            {
                if (body is not PlayerBase p || p.PlayerId != _targetId || p.IsDead) continue;

                p.TakeDamage(_damage);
                if (_knockback)
                    p.GlobalPosition += (p.GlobalPosition - GlobalPosition).Normalized() * 120f;
                _contactCd = 0.5f;
                break;
            }
        }

        private PlayerBase FindTarget()
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
                if (node is PlayerBase p && p.PlayerId == _targetId && !p.IsDead) return p;
            return null;
        }

        private static Polygon2D MakeCircle(float radius, Color color)
        {
            var pts = new Vector2[12];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = Vector2.Right.Rotated(i / 12f * Mathf.Tau) * radius;
            return new Polygon2D { Polygon = pts, Color = color };
        }
    }
}
