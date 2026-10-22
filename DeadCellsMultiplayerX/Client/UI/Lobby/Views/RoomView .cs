using System;
using System.Collections.Generic;
using CoreLibrary.Utilities;
using dc;
using dc.h2d;
using dc.hxd;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using static DeadCellsMultiplayerX.Client.UI.Lobby.Views.LobbyView;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views
{
    internal class RoomView : UIProcess
    {
        private readonly LobbyView host;
        private PlayerSlotPanel? panel;
        private Flow? btnRow;
        private Flow? contentFlow;

        public RoomView(LobbyView host, Process? parent = null) : base(parent)
        {
            this.host = host;
            OnScreenResized += MyonResize;
        }

        public void Build(Flow parent)
        {
            var content = new Flow(parent);
            content.set_isVertical(true);
            content.set_horizontalAlign(new FlowAlign.Middle());
            content.set_verticalAlign(new FlowAlign.Middle());
            content.set_verticalSpacing(host.Pixel(20));
            content.set_minWidth(host.CardW);
            content.set_maxWidth(host.CardW);

            contentFlow = content;

            panel = new PlayerSlotPanel(host, content, host);

            btnRow = new Flow(content);
            btnRow.set_isVertical(false);
            btnRow.set_horizontalAlign(new FlowAlign.Middle());
            btnRow.set_horizontalSpacing(host.Pixel(15));
            btnRow.set_minWidth(host.CardW);
            btnRow.set_maxWidth(host.CardW);

            btnRow = host.BuildButtonRow(content, MakeRoomButtons().ToArray());

            content.reflow();
            parent.reflow();
        }

        private IEnumerable<ButtonSpec> MakeRoomButtons()
        {
            var ctx = host.context;
            if (ctx.IsHost)
            {
                yield return new ButtonSpec("开始游戏", () => host.OnClickStartGame());
                yield return new ButtonSpec("销毁房间", () => host.OnClickLeaveRoom());
            }
            else
            {
                yield return new ButtonSpec("准备 / 取消", () => host.OnClickToggleReady());
                yield return new ButtonSpec("离开房间", () => host.OnClickLeaveRoom());
            }
        }


        public void MyonResize()
        {
            if (contentFlow == null) return;

            contentFlow.set_minWidth(host.CardW);
            contentFlow.set_maxWidth(host.CardW);


            if (btnRow != null)
            {
                btnRow.set_minWidth(host.CardW);
                btnRow.set_maxWidth(host.CardW);
                btnRow.reflow();
            }

            contentFlow.reflow();
        }

        public void Refresh(LobbyContext ctx)
        {
            panel?.Refresh(ctx.Lobby?.Guests.Values);

            if (panel != null && panel.TitleText != null)
            {
                var color = ctx.LatencyMs <= 50 ? "00FF00"
                          : ctx.LatencyMs <= 100 ? "FFFF00"
                          : "FF0000";
                var text = $"人数: {ctx.Lobby?.Guests.Count ?? 0}  延迟: <font color=\"#{color}\">{ctx.LatencyMs}ms</font>";
                panel.TitleText.set_text(text.AsHaxeString());
            }
        }

        public void Clear()
        {

            panel?.Dispose();
            panel = null;

            contentFlow?.removeChildren();
            contentFlow?.remove();
            contentFlow = null;
        }

        protected override void MyDispose()
        {
            OnScreenResized -= MyonResize;
            Clear();
        }
    }
}