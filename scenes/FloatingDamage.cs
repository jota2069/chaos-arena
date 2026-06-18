using Godot;

namespace ChaosArena.scenes
{
    /// <summary>
    /// Всплывающая цифра урона. Белая — обычный урон, красная — крит.
    /// Поднимается вверх на 40px за 1 сек, плавно исчезает и самоуничтожается.
    /// Использование: инстанцировать, AddChild, задать Position, вызвать Setup().
    /// </summary>
    public partial class FloatingDamage : Label
    {
        // Цвета берём из палитры проекта.
        private static readonly Color NormalColor = new(1f, 1f, 1f, 1f);       // белый
        private static readonly Color CritColor = new(1f, 0.27f, 0.27f, 1f);   // красный

        /// <summary>Задаёт значение/цвет и запускает анимацию всплытия.</summary>
        public void Setup(float amount, bool crit = false)
        {
            Text = crit ? $"{Mathf.RoundToInt(amount)}!" : $"{Mathf.RoundToInt(amount)}";
            Modulate = crit ? CritColor : NormalColor;

            // Узел уже в дереве (вызывать после AddChild) — можно создавать твин.
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "position:y", Position.Y - 40f, 1f);
            tween.TweenProperty(this, "modulate:a", 0f, 1f);
            tween.Chain().TweenCallback(Callable.From(QueueFree));
        }
    }
}
