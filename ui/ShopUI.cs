using Godot;
using System.Collections.Generic;
using ChaosArena.autoload;
using ChaosArena.systems;

namespace ChaosArena.ui
{
    /// <summary>
    /// Интерфейс магазина. Показывает набор товаров на раунд (из ShopSystem)
    /// с тремя вкладками ОРУЖИЕ / РАСХОДНИКИ / САБОТАЖ, кнопками КУПИТЬ/ПРОДАТЬ
    /// и балансом золота. Сетка товаров (3 колонки) строится динамически.
    /// Кнопка ГОТОВ шлёт сигнал ReadyPressed наружу (обрабатывает ShopArena).
    /// Шрифт Press Start 2P в проект не добавлен — используем размер/цвет, как в HUD.
    /// </summary>
    public partial class ShopUI : CanvasLayer
    {
        /// <summary>Игрок нажал ГОТОВ.</summary>
        [Signal] public delegate void ReadyPressedEventHandler();

        // Вкладки. РАСХОДНИКИ объединяет расходники и улучшения (вкладок всего 3).
        private enum Tab { Weapons, Items, Sabotage }

        private static readonly Color Gold = new(1f, 0.843f, 0f);

        private static readonly Texture2D SlotTex =
            GD.Load<Texture2D>("res://assets/ui/shop/ui/shop_item_slot.png");
        private static readonly Texture2D BuyTex =
            GD.Load<Texture2D>("res://assets/ui/shop/ui/shop_button_buy.png");
        private static readonly Texture2D SellTex =
            GD.Load<Texture2D>("res://assets/ui/shop/ui/shop_button_sell.png");

        private Control _root;
        private Label _goldLabel;
        private GridContainer _itemsGrid;
        private Button _tabWeapons, _tabItems, _tabSabotage;

        private ShopSystem _shop;
        private SabotageSystem _sabotage;
        private EconomyManager _economy;
        private EventBus _eventBus;
        private GameManager _gameManager;

        private int _playerId;
        private Tab _activeTab = Tab.Weapons;

        public bool IsOpen => _root != null && _root.Visible;

        public override void _Ready()
        {
            _root = GetNode<Control>("Root");
            _goldLabel = GetNode<Label>("Root/Panel/GoldLabel");
            _itemsGrid = GetNode<GridContainer>("Root/Panel/ItemsGrid");
            _tabWeapons = GetNode<Button>("Root/Panel/Tabs/TabWeapons");
            _tabItems = GetNode<Button>("Root/Panel/Tabs/TabItems");
            _tabSabotage = GetNode<Button>("Root/Panel/Tabs/TabSabotage");

            _shop = GetNode<ShopSystem>("/root/ShopSystem");
            _sabotage = GetNode<SabotageSystem>("/root/SabotageSystem");
            _economy = GetNode<EconomyManager>("/root/EconomyManager");
            _gameManager = GetNode<GameManager>("/root/GameManager");
            _eventBus = GetNode<EventBus>("/root/EventBus");

            _tabWeapons.Pressed += () => SetTab(Tab.Weapons);
            _tabItems.Pressed += () => SetTab(Tab.Items);
            _tabSabotage.Pressed += () => SetTab(Tab.Sabotage);
            GetNode<BaseButton>("Root/Panel/ReadyButton").Pressed += OnReadyPressed;

            // Баланс обновляется в реальном времени.
            _eventBus.CurrencyChanged += OnCurrencyChanged;

            _root.Visible = false;
        }

        public override void _ExitTree()
        {
            if (_eventBus != null && GodotObject.IsInstanceValid(_eventBus))
                _eventBus.CurrencyChanged -= OnCurrencyChanged;
        }

        /// <summary>Открывает магазин для игрока playerId.</summary>
        public void Open(int playerId)
        {
            _playerId = playerId;
            _activeTab = Tab.Weapons;
            _root.Visible = true;
            RefreshGold();
            RefreshItems();
        }

        public void Close() => _root.Visible = false;

        // --- Вкладки ---

        private void SetTab(Tab tab)
        {
            _activeTab = tab;
            HighlightTabs();
            RefreshItems();
        }

        private void HighlightTabs()
        {
            _tabWeapons.Modulate = _activeTab == Tab.Weapons ? Gold : Colors.White;
            _tabItems.Modulate = _activeTab == Tab.Items ? Gold : Colors.White;
            _tabSabotage.Modulate = _activeTab == Tab.Sabotage ? Gold : Colors.White;
        }

        private bool InActiveTab(ShopItem item) => _activeTab switch
        {
            Tab.Weapons => item.Type == ShopItemType.Weapon,
            Tab.Items => item.Type is ShopItemType.Consumable or ShopItemType.Upgrade,
            Tab.Sabotage => item.Type == ShopItemType.Sabotage,
            _ => false,
        };

        // --- Наполнение сетки ---

        private void RefreshItems()
        {
            HighlightTabs();

            foreach (var child in _itemsGrid.GetChildren())
            {
                _itemsGrid.RemoveChild(child);
                child.QueueFree();
            }

            // Вкладка САБОТАЖ показывает все 12 саботажей из SabotageSystem (не из набора).
            if (_activeTab == Tab.Sabotage)
            {
                foreach (var sab in _sabotage.All)
                    _itemsGrid.AddChild(BuildSabotageCell(sab));
                return;
            }

            var offer = _shop.GetOffer(_playerId, _gameManager.CurrentRound);
            var shown = offer.FindAll(InActiveTab);

            if (shown.Count == 0)
            {
                var empty = new Label
                {
                    Text = "Нет товаров",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                empty.AddThemeColorOverride("font_color", Gold);
                _itemsGrid.AddChild(empty);
                return;
            }

            foreach (var item in shown)
                _itemsGrid.AddChild(BuildCell(item));
        }

        // Ячейка саботажа: иконка/название/цена/КУПИТЬ. Покупка через SabotageSystem
        // (1 саботаж за раунд — после покупки кнопки во вкладке блокируются).
        private Control BuildSabotageCell(SabotageData sab)
        {
            var cell = new Control { CustomMinimumSize = new Vector2(210, 150) };

            cell.AddChild(new TextureRect
            {
                Texture = SlotTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Position = Vector2.Zero,
                Size = new Vector2(210, 150),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            cell.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>(sab.IconPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new Vector2(81, 8),
                Size = new Vector2(48, 48),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            var name = new Label
            {
                Text = sab.Name,
                Position = new Vector2(6, 58),
                Size = new Vector2(198, 32),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                TooltipText = sab.Description,
            };
            name.AddThemeFontSizeOverride("font_size", 12);
            name.AddThemeColorOverride("font_color", Gold);
            cell.AddChild(name);

            var price = new Label
            {
                Text = $"{sab.Price}g",
                Position = new Vector2(6, 92),
                Size = new Vector2(198, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            price.AddThemeFontSizeOverride("font_size", 12);
            cell.AddChild(price);

            bool affordable = _economy.GetBalance(_playerId) >= sab.Price && _sabotage.CanBuy(_playerId);
            var buy = MakeImageButton(BuyTex, new Vector2(61, 114), affordable);
            buy.Pressed += () => OnBuySabotage(sab.Id);
            cell.AddChild(buy);

            return cell;
        }

        private void OnBuySabotage(string sabotageId)
        {
            if (_sabotage.Buy(_playerId, sabotageId))
                RefreshItems(); // баланс обновится через CurrencyChanged
        }

        // Строит ячейку товара: фон-слот, иконка, название, цена, КУПИТЬ/ПРОДАТЬ.
        private Control BuildCell(ShopItem item)
        {
            var cell = new Control { CustomMinimumSize = new Vector2(210, 150) };

            var bg = new TextureRect
            {
                Texture = SlotTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Position = Vector2.Zero,
                Size = new Vector2(210, 150),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            cell.AddChild(bg);

            var icon = new TextureRect
            {
                Texture = GD.Load<Texture2D>(item.IconPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new Vector2(81, 8),
                Size = new Vector2(48, 48),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            cell.AddChild(icon);

            var name = new Label
            {
                Text = item.Name,
                Position = new Vector2(6, 58),
                Size = new Vector2(198, 32),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                TooltipText = item.Description,
            };
            name.AddThemeFontSizeOverride("font_size", 12);
            name.AddThemeColorOverride("font_color", Gold);
            cell.AddChild(name);

            var price = new Label
            {
                Text = $"{item.Price}g",
                Position = new Vector2(6, 92),
                Size = new Vector2(198, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            price.AddThemeFontSizeOverride("font_size", 12);
            cell.AddChild(price);

            bool affordable = _economy.GetBalance(_playerId) >= item.Price
                              && _shop.CanBuyMore(_playerId, item.Id);
            bool owned = _shop.Owns(_playerId, item.Id);

            var buy = MakeImageButton(BuyTex, new Vector2(12, 114), affordable);
            buy.Pressed += () => OnBuy(item.Id);
            cell.AddChild(buy);

            var sell = MakeImageButton(SellTex, new Vector2(110, 114), owned);
            sell.Pressed += () => OnSell(item.Id);
            cell.AddChild(sell);

            return cell;
        }

        private static TextureButton MakeImageButton(Texture2D tex, Vector2 pos, bool enabled)
        {
            return new TextureButton
            {
                TextureNormal = tex,
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                Position = pos,
                Size = new Vector2(88, 30),
                CustomMinimumSize = new Vector2(88, 30),
                Disabled = !enabled,
                Modulate = enabled ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 0.6f),
            };
        }

        // --- Действия ---

        private void OnBuy(string itemId)
        {
            if (_shop.Buy(_playerId, itemId))
                RefreshItems(); // баланс обновится через CurrencyChanged
        }

        private void OnSell(string itemId)
        {
            if (_shop.Sell(_playerId, itemId))
                RefreshItems();
        }

        private void OnReadyPressed() => EmitSignal(SignalName.ReadyPressed);

        private void OnCurrencyChanged(int playerId, int newAmount)
        {
            if (playerId != _playerId) return;
            // Только обновляем баланс. Сетку перестраивают OnBuy/OnSell — иначе
            // SpendCurrency, эмитящий CurrencyChanged внутри Buy, вызвал бы
            // повторную перестройку прямо во время обработки клика.
            _goldLabel.Text = $"Золото: {newAmount}";
        }

        private void RefreshGold()
        {
            _goldLabel.Text = $"Золото: {_economy.GetBalance(_playerId)}";
        }
    }
}
