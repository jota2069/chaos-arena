using Godot;
using System.Collections.Generic;
using ChaosArena.entities.player;

namespace ChaosArena.autoload
{
    /// <summary>
    /// Сетевой менеджер: P2P через ENet (порт 7000), режимы хоста и клиента.
    /// Хост (PlayerId=0) — авторитет фаз/экономики/спавна; клиент (PlayerId=1)
    /// получает фазы и позиции по RPC. Синхронизирует фазы игры (надёжно) и
    /// позиции игроков с частотой 20 Гц (ненадёжно). Архитектура — см. CLAUDE.md.
    /// </summary>
    public partial class NetworkManager : Node
    {
        public const int DefaultPort = 7000;

        // Всего два игрока: хост + один клиент.
        public const int MaxClients = 1;

        // Частота отправки позиций — 20 Гц (каждые 0.05 с).
        private const float PositionSendInterval = 0.05f;

        public bool IsNetworked { get; private set; }
        public bool IsHost { get; private set; }

        // Id локального игрока: 0 у хоста, 1 у клиента.
        public int LocalPlayerId { get; private set; }

        private EventBus _eventBus;
        private GameManager _gameManager;
        private float _posTimer;

        // Последние полученные позиции удалённых игроков — для RemotePlayer (TODO).
        private readonly Dictionary<int, Vector2> _remotePositions = new();

        public override void _Ready()
        {
            _eventBus = GetNode<EventBus>("/root/EventBus");
            _gameManager = GetNode<GameManager>("/root/GameManager");

            // Хост рассылает смену фаз. Подписка C#-стилем (соглашение CLAUDE.md).
            _eventBus.PhaseChanged += OnPhaseChanged;

            Multiplayer.PeerConnected += OnPeerConnected;
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
            Multiplayer.ConnectionFailed += OnConnectionFailed;
            Multiplayer.ServerDisconnected += OnServerDisconnected;
        }

        public override void _ExitTree()
        {
            if (_eventBus != null && GodotObject.IsInstanceValid(_eventBus))
                _eventBus.PhaseChanged -= OnPhaseChanged;

            Multiplayer.PeerConnected -= OnPeerConnected;
            Multiplayer.PeerDisconnected -= OnPeerDisconnected;
            Multiplayer.ConnectionFailed -= OnConnectionFailed;
            Multiplayer.ServerDisconnected -= OnServerDisconnected;
        }

        // --- Запуск сети ---

        /// <summary>
        /// Поднимает сервер (хост) на указанном порту. Хост — игрок 0.
        /// </summary>
        public Error HostGame(int port = DefaultPort)
        {
            var peer = new ENetMultiplayerPeer();
            Error err = peer.CreateServer(port, MaxClients);
            if (err != Error.Ok)
            {
                GD.PrintErr($"[NetworkManager] Не удалось поднять сервер на порту {port}: {err}");
                return err;
            }

            Multiplayer.MultiplayerPeer = peer;
            IsNetworked = true;
            IsHost = true;
            LocalPlayerId = 0;
            AssignLocalPlayer();
            return Error.Ok;
        }

        /// <summary>
        /// Подключается к хосту по адресу. Клиент — игрок 1.
        /// </summary>
        public Error JoinGame(string address = "127.0.0.1", int port = DefaultPort)
        {
            var peer = new ENetMultiplayerPeer();
            Error err = peer.CreateClient(address, port);
            if (err != Error.Ok)
            {
                GD.PrintErr($"[NetworkManager] Не удалось подключиться к {address}:{port}: {err}");
                return err;
            }

            Multiplayer.MultiplayerPeer = peer;
            IsNetworked = true;
            IsHost = false;
            LocalPlayerId = 1;
            AssignLocalPlayer();

            // Клиент не ведёт фазы сам — ждёт авторитетный хост.
            _gameManager.SetNetworkClient(true);
            return Error.Ok;
        }

        /// <summary>
        /// Разрывает соединение и возвращает игру в оффлайн-режим.
        /// </summary>
        public void Disconnect()
        {
            if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet)
                enet.Close();

            Multiplayer.MultiplayerPeer = null;
            IsNetworked = false;
            IsHost = false;
            _remotePositions.Clear();
            _gameManager.SetNetworkClient(false);
        }

        // --- Синхронизация позиций (20 Гц) ---

        public override void _Process(double delta)
        {
            if (!IsNetworked) return;

            _posTimer += (float)delta;
            if (_posTimer < PositionSendInterval) return;
            _posTimer = 0f;

            var local = FindPlayer(LocalPlayerId);
            if (local == null) return;

            Rpc(MethodName.ReceivePosition, LocalPlayerId, local.GlobalPosition, local.Velocity);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
             TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
        private void ReceivePosition(int playerId, Vector2 position, Vector2 velocity)
        {
            if (playerId == LocalPlayerId) return; // собственный эхо-апдейт игнорируем

            _remotePositions[playerId] = position;

            // Если узел удалённого игрока уже есть в сцене — двигаем его.
            var remote = FindPlayer(playerId);
            if (remote != null && GodotObject.IsInstanceValid(remote))
            {
                remote.GlobalPosition = position;
                remote.Velocity = velocity;
            }
        }

        /// <summary>
        /// Последняя известная позиция удалённого игрока (для интерполяции RemotePlayer).
        /// </summary>
        public bool TryGetRemotePosition(int playerId, out Vector2 position)
            => _remotePositions.TryGetValue(playerId, out position);

        // --- Синхронизация фаз ---

        // Хост ловит смену фазы из EventBus и рассылает её клиентам.
        private void OnPhaseChanged(int newPhase)
        {
            if (!IsNetworked || !IsHost) return;
            Rpc(MethodName.ReceivePhase, newPhase, _gameManager.GetPhaseTimeLeft());
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
             TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ReceivePhase(int phase, float timeLeft)
        {
            _gameManager.ApplyNetworkPhase(phase, timeLeft);
        }

        // --- Сигналы соединения ---

        // Хост вводит только что подключившегося клиента в текущую фазу.
        private void OnPeerConnected(long id)
        {
            if (IsHost)
                RpcId(id, MethodName.ReceivePhase, (int)_gameManager.CurrentPhase, _gameManager.GetPhaseTimeLeft());
        }

        private void OnPeerDisconnected(long id)
        {
            _remotePositions.Clear();
        }

        private void OnConnectionFailed()
        {
            GD.PrintErr("[NetworkManager] Соединение с хостом не удалось.");
            Disconnect();
        }

        private void OnServerDisconnected()
        {
            GD.PrintErr("[NetworkManager] Хост отключился.");
            Disconnect();
        }

        // --- Вспомогательное ---

        // Назначает локальному игроку его сетевой id и право управления узлом.
        private void AssignLocalPlayer()
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
            {
                if (node is LocalPlayer p)
                {
                    p.PlayerId = LocalPlayerId;
                    p.SetMultiplayerAuthority(Multiplayer.GetUniqueId());
                    break;
                }
            }
        }

        private PlayerBase FindPlayer(int playerId)
        {
            foreach (var node in GetTree().GetNodesInGroup("players"))
            {
                if (node is PlayerBase p && p.PlayerId == playerId)
                    return p;
            }
            return null;
        }
    }
}
