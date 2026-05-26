using Godot;

namespace ChaosArena.entities.enemies
{
    public partial class BasicEnemy : EnemyBase
    {
        [Export] public float ContactDamage = 10f;
        [Export] public float AttackRange = 24f;

        private PlayerBase _target;
        private Vector2 _knockbackVelocity = Vector2.Zero;

        public override void _Ready()
        {
            base._Ready();
            
            // Обновляем цель раз в секунду (безопасно для памяти и производительности)
            var timer = new Timer();
            timer.WaitTime = 1f;
            timer.Autostart = true;
            timer.Timeout += UpdateTarget;
            AddChild(timer);
            
            UpdateTarget();
        }

        private void UpdateTarget()
        {
            var players = GetTree().GetNodesInGroup("players");
            if (players.Count > 0)
            {
                var player = players[0] as PlayerBase;
                if (player != null && !player.IsDead)
                {
                    _target = player;
                    return;
                }
            }
            _target = null;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsDead) return;

            float dt = (float)delta;
            Vector2 moveVelocity = Vector2.Zero;

            if (_target != null && IsInstanceValid(_target) && !_target.IsDead)
            {
                // Направление к игроку
                Vector2 direction = (_target.GlobalPosition - GlobalPosition).Normalized();
                moveVelocity = direction * MoveSpeed;

                // Поворот спрайта врага в сторону движения
                if (Sprite != null && Mathf.Abs(direction.X) > 0.05f)
                {
                    float baseScale = Mathf.Abs(Sprite.Scale.Y);
                    Sprite.Scale = new Vector2(direction.X > 0 ? baseScale : -baseScale, baseScale);
                }

                // Нанесение урона игроку вблизи
                float distance = GlobalPosition.DistanceTo(_target.GlobalPosition);
                if (distance < AttackRange)
                {
                    _target.TakeDamage(ContactDamage * dt);
                }
            }

            // Плавно гасим импульс отбрасывания (Knockback)
            _knockbackVelocity = _knockbackVelocity.Lerp(Vector2.Zero, 10f * dt);

            // Итоговая скорость: движение + отбрасывание от пуль
            Velocity = moveVelocity + _knockbackVelocity;
            MoveAndSlide();
        }

        // Переопределяем метод получения урона из базового класса, чтобы добавить отбрасывание
        public override void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0) return;

            base.TakeDamage(amount);

            // Если есть цель — отлетаем в противоположную от нее сторону
            if (_target != null && IsInstanceValid(_target))
            {
                Vector2 pushDirection = (GlobalPosition - _target.GlobalPosition).Normalized();
                _knockbackVelocity = pushDirection * 180f; // Сила отбрасывания
            }
        }
    }
}