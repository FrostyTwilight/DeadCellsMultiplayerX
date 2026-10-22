using System;
using CoreLibrary.Utilities;
using dc;
using dc.h2d;
using dc.hxd;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Client.UI.Lobby.Views;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views
{
    internal class LobbyView : UIProcess
    {
        private readonly TitleScreen titleScreen;
        private readonly LobbyUI owner;
        public readonly LobbyContext context;
        private readonly Dictionary<Flow, Action> resizers = new();

        private Flow? mainFlow;      // 全屏透明
        private Graphics? dimMask;   // 半透明遮罩
        private Flow? cardFlow;      //卡片容器

        // 子页面
        private MainMenuView? mainMenuView;
        private CreateRoomView? createRoomView;
        private JoinRoomView? joinRoomView;
        private RoomView? roomView;
        private ErrorView? errorView;
        private DebugView? debugView;

        public LobbyView(LobbyUI owner, TitleScreen titleScreen, Process? parent = null) : base(parent)
        {
            this.owner = owner;
            this.titleScreen = titleScreen;
            context = owner.Context;

            OnScreenResized += OnLayoutResize;
        }
        public int CardW => ScreenW * 2 / 3;
        public int CardH => ScreenH * 2 / 3;

        public void Show()
        {
            BuildSkeleton();
        }

        public void Hide()
        {
            ClearAllPages();
            mainFlow?.removeChildren();
            mainFlow?.remove();
            mainFlow = null;
            dimMask = null;
            cardFlow = null;
        }

        //事件转发
        public void OnGoCreateRoom() => owner.OnGoCreateRoom();
        public void OnGoJoinRoom() => owner.OnGoJoinRoom();
        public void OnBackToMain() => owner.OnBackToMain();

        public void OnSubmitCreateRoom(string ip, int port) => _ = owner.OnCreateRoom(ip, port);
        public Task OnSubmitCreateRoomAsync(string ip, int port) => owner.OnCreateRoom(ip, port);
        public void OnSubmitJoinRoom(string ip, int port) => _ = owner.OnJoinRoom(ip, port);

        public void OnClickStartGame() => _ = owner.OnStartGame();
        public void OnClickToggleReady() => owner.OnToggleReady();
        public void OnClickLeaveRoom() => owner.OnLeaveRoom();
        public void OnRequestClose() => owner.OnRequestClose(); //关闭整个 Lobby
        public void GotoDebug() => owner.OnGotoDebug();

        private void OnLayoutResize()
        {
            foreach (var r in resizers.Values)
                r();
        }


        //状态切换
        public void OnEnterState(LobbyState state, LobbyContext ctx)
        {
            if (IsDisposed) return;
            if (cardFlow == null) return;

            // 清空旧页面
            ClearAllPages();
            cardFlow.removeChildren();

            // 重置布局
            cardFlow.set_isVertical(false);
            cardFlow.set_horizontalAlign(new FlowAlign.Middle());
            cardFlow.set_verticalAlign(new FlowAlign.Middle());
            cardFlow.set_horizontalSpacing(Pixel(20));

            // 挂载新页面
            switch (state)
            {
                case LobbyState.MainMenu:
                    mainMenuView ??= new MainMenuView(this, this);
                    mainMenuView.Build(cardFlow);
                    break;

                case LobbyState.CreateRoom:
                    createRoomView ??= new CreateRoomView(this, this);
                    createRoomView.Build(cardFlow);
                    break;

                case LobbyState.JoinRoom:
                    joinRoomView ??= new JoinRoomView(this, this);
                    joinRoomView.Build(cardFlow);
                    break;

                case LobbyState.Connecting:
                    BuildConnectingPage(cardFlow);
                    break;

                case LobbyState.InRoom:
                case LobbyState.StartingGame:
                    roomView ??= new RoomView(this, this);
                    roomView.Build(cardFlow);
                    break;

                case LobbyState.Error:
                    errorView ??= new ErrorView(this, this);
                    errorView.Build(cardFlow, ctx.LastError);
                    break;
                case LobbyState.Debug:
                    debugView ??= new DebugView(this, this);
                    debugView.Build(cardFlow!);
                    break;
            }

            cardFlow.reflow();
            mainFlow?.reflow();
        }

        public void Refresh(LobbyContext ctx, LobbyState state)
        {
            if (IsDisposed) return;

            switch (state)
            {
                case LobbyState.MainMenu: mainMenuView?.Refresh(ctx); break;
                case LobbyState.CreateRoom: createRoomView?.Refresh(ctx); break;
                case LobbyState.JoinRoom: joinRoomView?.Refresh(ctx); break;
                case LobbyState.InRoom:
                case LobbyState.StartingGame: roomView?.Refresh(ctx); break;
                case LobbyState.Error: errorView?.Refresh(ctx); break;
                case LobbyState.Debug: debugView?.Refresh(ctx); break;
            }
        }


        private void ApplyFullscreenLayout()
        {
            if (mainFlow == null) return;

            mainFlow.set_minWidth(ScreenW);
            mainFlow.set_minHeight(ScreenH);
            mainFlow.set_maxWidth(ScreenW);
            mainFlow.set_maxHeight(ScreenH);

            if (dimMask != null)
            {
                dimMask.clear();
                dimMask.beginFill(Ref<int>.In(0x000000), Ref<double>.In(0.4));
                dimMask.drawRect(0, 0, ScreenW, ScreenH);
                dimMask.endFill();
            }
        }

        private void ApplyCardFlowLayout()
        {
            if (cardFlow == null) return;

            int cardW = ScreenW * 2 / 3;
            int cardH = ScreenH * 2 / 3;

            cardFlow.set_minWidth(cardW);
            cardFlow.set_minHeight(cardH);
            cardFlow.set_maxWidth(cardW);
            cardFlow.set_maxHeight(cardH);

            cardFlow.x = (ScreenW - cardW) / 2;
            cardFlow.y = (ScreenH - cardH) / 2;
            cardFlow.posChanged = true;
        }

        //骨架
        private void BuildSkeleton()
        {
            if (mainFlow != null) return;

            var layers = Main.Class.ME.root;

            mainFlow = new Flow(null);
            mainFlow.set_isVertical(false);
            mainFlow.posChanged = true;

            layers.addChildAt(mainFlow, dc.Const.Class.ROOT_DP_MENU);

            dimMask = new Graphics(mainFlow);
            mainFlow.getProperties(dimMask).set_isAbsolute(true);

            cardFlow = new Flow(mainFlow);
            cardFlow.set_isVertical(false);
            cardFlow.set_horizontalAlign(new FlowAlign.Middle());
            cardFlow.set_verticalAlign(new FlowAlign.Middle());
            cardFlow.set_horizontalSpacing(Pixel(20));

            mainFlow.getProperties(cardFlow).set_isAbsolute(true);

            ApplyFullscreenLayout();
            ApplyCardFlowLayout();

            mainFlow.reflow();
            cardFlow.reflow();
        }


        public override void onResize()
        {
            base.onResize();

            if (mainFlow == null) return;

            ApplyFullscreenLayout();
            ApplyCardFlowLayout();

            mainFlow.reflow();
            cardFlow?.reflow();
        }

        public int Pixel(double n) => (int)(get_pixelScale.Invoke() * n);

        //构建一张可点击卡片
        public Flow BuildCard(Flow parent, string title, string desc, Action onClick)
        {
            var card = FlowBox.Class.createBoxValidation(parent, default, default, Ref<bool>.In(true), null);
            card.set_isVertical(true);
            card.set_horizontalAlign(new FlowAlign.Middle());
            card.set_verticalAlign(new FlowAlign.Middle());

            var titleText = Assets.Class.makeMedievalText(title.AsHaxeString(), null, card, null);

            dc.ui.Text? descText = null;
            if (!string.IsNullOrEmpty(desc))
            {
                descText = Assets.Class.makeText(desc.AsHaxeString(), 0xAAAAAA, null, card);
            }

            void Resize()
            {
                card.set_minWidth(ScreenW / 5);
                card.set_minHeight(ScreenH / 2);
                card.set_padding(Pixel(20));

                titleText.scaleX = titleText.scaleY = get_pixelScale.Invoke() * 0.6;
                titleText.posChanged = true;

                if (descText != null)
                {
                    descText.scaleX = descText.scaleY = get_pixelScale.Invoke() * 0.3;
                    descText.posChanged = true;
                }

                card.reflow();

                if (card.interactive != null)
                {
                    card.interactive.width = card.calculatedWidth;
                    card.interactive.height = card.calculatedHeight;
                }
            }

            Resize();
            resizers[card] = Resize;

            // 交互
            card.set_enableInteractive(true);
            if (card.interactive != null)
            {
                card.interactive.cursor = new Cursor.Button();
                card.interactive.onClick = new HlAction<dc.hxd.Event>(_ =>
                {
                    AudioHelper.LoadAudioFormString("sfx/ui/menu_select.wav");
                    onClick();
                });
                card.interactive.onOver = new(_ =>
                {
                    AudioHelper.LoadAudioFormString("sfx/ui/menu_click1.wav");
                });
            }

            return card;
        }


        //构建一个文本按钮。
        public Flow BuildTextButton(
            Flow parent,
            string label,
            Action onClick,
            int? color = null,
            double scale = 0.4,
            string sound = "sfx/ui/menu_select.wav")
        {
            var btn = new Flow(parent);
            var text = Assets.Class.makeMedievalText(
                label.AsHaxeString(),
                null,
                btn,
                null);

            void Resize()
            {
                btn.set_padding(Pixel(6));
                text.scaleX = text.scaleY = get_pixelScale.Invoke() * scale;
                text.posChanged = true;
                btn.reflow();

                if (btn.interactive != null)
                {
                    btn.interactive.width = btn.calculatedWidth;
                    btn.interactive.height = btn.calculatedHeight;
                }
            }

            Resize();
            resizers[btn] = Resize;

            btn.set_enableInteractive(true);
            if (btn.interactive != null)
            {
                btn.interactive.cursor = new Cursor.Button();
                btn.interactive.onClick = new HlAction<dc.hxd.Event>(_ =>
                {
                    AudioHelper.LoadAudioFormString("sfx/ui/menu_select.wav");
                    onClick();
                });
                btn.interactive.onOver = new(_ =>
                {
                    AudioHelper.LoadAudioFormString("sfx/ui/menu_click1.wav");
                });
            }

            return btn;
        }

        public Flow BuildBackButton(Flow parent)
            => BuildTextButton(parent, "返回", OnBackToMain, sound: "sfx/ui/menu_back.wav");


        //按钮规格
        public readonly record struct ButtonSpec(
            string Label,
            Action OnClick,
            int? Color = null,
            double Scale = 0.4,
            string onClickSound = "sfx/ui/menu_select.wav",
            string onOverSound = "sfx/ui/menu_click1.wav");


        /// 构建一排横向按钮。
        public Flow BuildButtonRow(Flow parent, params ButtonSpec[] specs)
        {
            var row = new Flow(parent);
            row.set_isVertical(false);
            row.set_horizontalAlign(new FlowAlign.Middle());
            row.set_verticalAlign(new FlowAlign.Middle());
            row.set_minWidth(CardW);
            row.set_maxWidth(CardW);

            var buttons = new List<(Flow btn, dc.ui.Text text, double scale)>();

            foreach (var spec in specs)
            {
                var btn = new Flow(row);
                var text = Assets.Class.makeMedievalText(
                    spec.Label.AsHaxeString(),
                    null,
                    btn,
                    null);

                btn.set_enableInteractive(true);
                if (btn.interactive != null)
                {
                    btn.interactive.cursor = new Cursor.Button();
                    var click = spec.OnClick;
                    var onClickSound = spec.onClickSound;
                    var onOverSound = spec.onOverSound;
                    btn.interactive.onClick = new HlAction<dc.hxd.Event>(_ =>
                    {
                        AudioHelper.LoadAudioFormString(onClickSound);
                        click();
                    });
                    btn.interactive.onOver = new(_ =>
                    {
                        AudioHelper.LoadAudioFormString(onOverSound);
                    });

                }

                buttons.Add((btn, text, spec.Scale));
            }

            void Resize()
            {
                row.set_horizontalSpacing(Pixel(15));
                row.set_minWidth(CardW);
                row.set_maxWidth(CardW);

                foreach (var (btn, text, scale) in buttons)
                {
                    btn.set_padding(Pixel(8));
                    text.scaleX = text.scaleY = get_pixelScale.Invoke() * scale;
                    text.posChanged = true;
                    btn.reflow();

                    if (btn.interactive != null)
                    {
                        btn.interactive.width = btn.calculatedWidth;
                        btn.interactive.height = btn.calculatedHeight;
                    }
                }

                row.reflow();
            }

            Resize();
            resizers[row] = Resize;

            return row;
        }


        //连接中页面
        private void BuildConnectingPage(Flow parent)
        {
            var text = Assets.Class.makeMedievalText("连接中...".AsHaxeString(), null, parent, null);
            text.scaleX = text.scaleY = get_pixelScale.Invoke() * 0.6;
            parent.reflow();
        }


        //屏幕中下方显示提示
        public void ShowToast(string message)
        {
            var toast = Assets.Class.makeMedievalText(message.AsHaxeString(), null, mainFlow, null);
            toast.scaleX = toast.scaleY = get_pixelScale.Invoke() * 0.4;

            //底部
            mainFlow?.getProperties(toast).set_isAbsolute(true);
            toast.x = ScreenW / 2 - toast.get_textWidth() / 2;
            toast.y = ScreenH * 0.85;
            toast.posChanged = true;

            // 淡出
            delayer.addF(null, () =>
            {
                toast.remove();
            }, 90);
        }

        //清理
        private void ClearAllPages()
        {
            mainMenuView?.Clear();
            createRoomView?.Clear();
            joinRoomView?.Clear();
            roomView?.Clear();
            errorView?.Clear();
            resizers.Clear();
            debugView?.Clear();
        }

        protected override void MyDispose()
        {
            mainMenuView?.Dispose();
            createRoomView?.Dispose();
            joinRoomView?.Dispose();
            roomView?.Dispose();
            errorView?.Dispose();
            debugView?.Dispose();
            Hide();
        }
    }
}