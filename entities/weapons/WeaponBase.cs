using Godot;
using ChaosArena.autoload;

namespace ChaosArena.entities.weapons
{
    /// <summary>
    /// Базовый класс оружия. Заглушка — будет расширена позже.
    /// </summary>
    public abstract partial class WeaponBase : Node2D
    {
        [Export] public string WeaponName = "Без названия";
        [Export] public float Damage = 10f;

        /// <summary>
        /// Возвращает урон с учётом текущей фазы игры.
        /// PvE-оружие наносит больше урона мобам, PvP-оружие — игрокам.
        /// </summary>
        public virtual float GetDamage(GameManager.GamePhase phase)
        {
            return Damage;
        }

        /// <summary>
        /// Выстрел / удар. Реализуется в наследниках.
        /// </summary>
        public virtual void Fire(Vector2 direction) { }
    }
}
