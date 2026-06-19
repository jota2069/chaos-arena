using Godot;
using ChaosArena.autoload;

namespace ChaosArena
{
    /// <summary>
    /// Загрузочный узел. На старте открывает Главное меню (обычный режим) либо сразу
    /// запускает матч (DEBUG_SKIP_MENU — для удобства разработки). Автостарт матча в
    /// GameManager выключен — единственная точка входа в игру управляется отсюда.
    /// </summary>
    public partial class Boot : Node2D
    {
        // DEBUG_SKIP_MENU: true — пропустить меню и сразу начать матч (для разработки).
        private const bool DebugSkipMenu = false;

        private const string MainMenuScene = "res://scenes/MainMenu.tscn";

        public override void _Ready()
        {
            // Откладываем до конца кадра: смена сцены/старт матча из _Ready загрузочной
            // сцены безопаснее в deferred-вызове.
            CallDeferred(DebugSkipMenu ? nameof(StartMatchNow) : nameof(GoToMenu));
        }

        private void GoToMenu() => GetTree().ChangeSceneToFile(MainMenuScene);

        private void StartMatchNow() => GetNode<GameManager>("/root/GameManager").StartMatch();
    }
}
