using System;
using System.Collections.Generic;
using dc;
using dc.h2d;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views
{
    internal class ErrorView : UIProcess
    {
        private readonly LobbyView host;
        private dc.ui.Text? errorText;
        private readonly List<Flow> cards = new();

        public ErrorView(LobbyView host, Process? parent = null) : base(parent)
        {
            this.host = host;
        }

        public void Build(Flow parent, string? message)
        {
            parent.set_isVertical(true);
            parent.set_horizontalAlign(new FlowAlign.Middle());
            parent.set_verticalAlign(new FlowAlign.Middle());

            errorText = Assets.Class.makeText(
                (message ?? "未知错误").AsHaxeString(), 0xFF4444, null, parent);
            errorText.scaleX = errorText.scaleY = host.get_pixelScale.Invoke() * 0.4;

            var back = host.BuildBackButton(parent);
            cards.Add(back);

            parent.reflow();
        }

        public void Refresh(LobbyContext ctx)
        {
            if (errorText != null && !string.IsNullOrEmpty(ctx.LastError))
                errorText.set_text(ctx.LastError.AsHaxeString());
        }

        public void Clear()
        {
            errorText?.remove();
            errorText = null;
            foreach (var c in cards) c.remove();
            cards.Clear();
        }

        protected override void MyDispose() => Clear();
    }
}