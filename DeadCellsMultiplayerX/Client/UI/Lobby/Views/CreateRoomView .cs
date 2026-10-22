using System;
using System.Collections.Generic;
using dc.h2d;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views
{
    internal class CreateRoomView : UIProcess
    {
        private readonly LobbyView host;
        private readonly List<Flow> cards = new();

        public CreateRoomView(LobbyView host, Process? parent = null) : base(parent)
        {
            this.host = host;
        }

        public void Build(Flow parent)
        {
            parent.set_isVertical(true);
            parent.set_horizontalAlign(new FlowAlign.Middle());
            parent.set_verticalAlign(new FlowAlign.Middle());

            // 卡片行
            var cardRow = new Flow(parent);
            cardRow.set_isVertical(false);
            cardRow.set_horizontalAlign(new FlowAlign.Middle());
            cardRow.set_horizontalSpacing(host.Pixel(30));

            // IP联机
            var ipCard = host.BuildCard(cardRow, "IP 直连", "通过 IP 连接", ShowIPInput);
            cards.Add(ipCard);

            // Steam联机
            var steamCard = host.BuildCard(cardRow, "Steam P2P", "暂不可用", OnSelectSteam);
            cards.Add(steamCard);

            cardRow.reflow();

            var back = host.BuildBackButton(parent);
            cards.Add(back);

            parent.reflow();
        }

        private void OnSelectSteam()
        {
            ModEntry.Instance.Logger.Information("Steam 模式暂未支持");
        }

        private void ShowIPInput()
        {
            var input = new dc.ui.TextInput(
                host,
                "输入 IP 和端口".AsHaxeString(),
                "127.0.0.1:12345".AsHaxeString(),
                "127.0.0.1:12345".AsHaxeString(),
                str =>
                {
                    if (!TryParseIpPort(str.ToString(), out var ip, out var port)) return;
                    host.OnSubmitCreateRoom(ip, port);
                },
                "回车确认".AsHaxeString(),
                null,
                null);
        }

        private static bool TryParseIpPort(string input, out string ip, out int port)
        {
            ip = string.Empty;
            port = 0;
            var parts = input.Split([':', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            if (!int.TryParse(parts[^1], out port)) return false;
            ip = parts[0];
            return true;
        }

        public void Refresh(LobbyContext ctx) { }

        public void Clear()
        {
            foreach (var c in cards) c.remove();
            cards.Clear();
        }

        protected override void MyDispose() => Clear();
    }
}