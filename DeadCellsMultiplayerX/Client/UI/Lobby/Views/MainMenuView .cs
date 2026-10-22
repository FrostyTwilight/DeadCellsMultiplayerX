using System;
using System.Collections.Generic;
using dc.h2d;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using static DeadCellsMultiplayerX.Client.UI.Lobby.Views.LobbyView;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views
{
    internal class MainMenuView : UIProcess
    {
        
        private readonly LobbyView host;
        private readonly List<Flow> cards = new();
        private Flow? footerRow;

        public MainMenuView(LobbyView host, Process? parent = null) : base(parent)
        {
            this.host = host;
        }

        public void Build(Flow parent)
        {
            parent.set_isVertical(true);
            parent.set_horizontalAlign(new FlowAlign.Middle());
            parent.set_verticalAlign(new FlowAlign.Middle());
            parent.set_verticalSpacing(host.Pixel(30));

            // 卡片行
            var cardRow = new Flow(parent);
            cardRow.set_isVertical(false);
            cardRow.set_horizontalAlign(new FlowAlign.Middle());
            cardRow.set_verticalAlign(new FlowAlign.Middle());
            cardRow.set_horizontalSpacing(host.Pixel(30));

            var createCard = host.BuildCard(cardRow, "创建房间", "建立新大厅",
                () => host.OnGoCreateRoom());
            cards.Add(createCard);

            var joinCard = host.BuildCard(cardRow, "加入房间", "输入 IP 加入",
                () => host.OnGoJoinRoom());
            cards.Add(joinCard);

            cardRow.reflow();

            footerRow = host.BuildButtonRow(parent, MakeFooterButtons().ToArray());

            parent.reflow();
        }

        private IEnumerable<ButtonSpec> MakeFooterButtons()
        {
            yield return new ButtonSpec(
                Label: "返回主页",
                OnClick: host.OnRequestClose,
                onClickSound: "sfx/ui/menu_back.wav");

#if DEBUG
            yield return new ButtonSpec(
                Label: "[DEBUG]",
                OnClick: host.GotoDebug,
                Color: 0xFF00FF,
                Scale: 0.35);
#endif
        }

        public void Refresh(LobbyContext ctx) { }

        public void Clear()
        {
            foreach (var card in cards) card.remove();
            cards.Clear();
        }

        protected override void MyDispose() => Clear();
    }
}