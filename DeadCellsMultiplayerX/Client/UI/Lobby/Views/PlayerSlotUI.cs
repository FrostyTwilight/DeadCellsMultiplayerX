using dc;
using dc.h2d;
using dc.libs.heaps.slib;
using dc.tool;
using dc.ui;
using DeadCellsMultiplayerX.Client.Event;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Common;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views
{
    internal class PlayerSlotUI
    {
        public Flow Container { get; }
        public HSprite? HeroSprite { get; private set; }
        public dc.ui.Text? NameLabel { get; private set; }
        public dc.ui.Text? StatusLabel { get; private set; }

        public GuestInfo? Guest { get; set; }
        public bool IsOccupied => Guest != null;

        private readonly LobbyView host;
        private readonly ILogger logger;
        private Flow? emptyBox;
        private GuestInfo? prevSnapshot;

        public event Action<PlayerSlotUI>? OnChanged;

        public PlayerSlotUI(Flow parent, LobbyView host)
        {
            this.host = host;
            logger = Log.ForContext<PlayerSlotUI>();

            Container = new Flow(parent);
            Container.set_isVertical(true);
            Container.set_verticalAlign(new FlowAlign.Middle());
            Container.set_horizontalAlign(new FlowAlign.Middle());

            ShowEmpty();
        }

        public void Bind(GuestInfo guest, bool force = false)
        {
            if (string.IsNullOrEmpty(guest.SkinMould)) return;

            //已绑定同一玩家
            if (!force && Guest?.Guid == guest.Guid)
            {
                Guest = guest;
                if (NameLabel != null)
                    NameLabel.set_text(guest.Name.AsHaxeString());
                RefreshStatus();
                return;
            }

            //新玩家
            Guest = guest;
            ClearContent();
            HideEmpty();

            string skin = guest.IsHost
                ? Save.Class.tryLoad().heroSkin.ToString()
                : guest.SkinMould;

            HeroSprite = UIRenderer.CreateHeroSpr(skin, Container);
            NameLabel = Assets.Class.makeText(guest.Name.AsHaxeString(), 0xDDDDDD, null, Container);
            StatusLabel = Assets.Class.makeText("".AsHaxeString(), null, null, Container);

            RefreshStatus();
            ApplyScale();

            OnChanged?.Invoke(this);
        }

        public void Clear()
        {
            if (Guest == null) return;
            Guest = null;
            prevSnapshot = null;

            ClearContent();
            ShowEmpty();

            Container.reflow();
            OnChanged?.Invoke(this);
        }

        private void ClearContent()
        {
            HeroSprite?.remove(); HeroSprite = null;
            NameLabel?.remove(); NameLabel = null;
            StatusLabel?.remove(); StatusLabel = null;
        }

        //空位
        private void ShowEmpty()
        {
            if (emptyBox != null) return;
            emptyBox = new Flow(Container);
            var text = Assets.Class.makeText("[Empty]".AsHaxeString(), 0x666666, null, emptyBox);
            ApplyEmptyScale(text);
        }

        private void HideEmpty()
        {
            if (emptyBox == null) return;
            emptyBox.removeChildren();
            emptyBox.remove();
            emptyBox = null;
        }

        //状态刷新
        public void RefreshStatus()
        {
            if (Guest == null || StatusLabel == null) return;

            string status = Guest.IsHost ? "[Host]"
                          : Guest.IsReady ? "[Ready]"
                          : "[...]";

            int color = Guest.IsHost ? 0xFFDD44
                      : Guest.IsReady ? 0x44FF44
                      : 0x888888;

            StatusLabel.set_text(status.AsHaxeString());
            StatusLabel.set_textColor(color);
            StatusLabel.posChanged = true;

            //名称皮肤变化重绑
            if (prevSnapshot == null)
            {
                prevSnapshot = Guest.Clone();
                return;
            }

            if (prevSnapshot.SkinMould != Guest.SkinMould
                || prevSnapshot.Name != Guest.Name
                || prevSnapshot.IsHost != Guest.IsHost)
            {
                prevSnapshot = Guest.Clone();
                Bind(Guest, force: true);
            }
        }

        //缩放
        public void ApplyScale()
        {
            var scale = host.get_pixelScale.Invoke();

            if (emptyBox != null)
            {
                foreach (var child in emptyBox.children)
                {
                    if (child is dc.ui.Text t) ApplyEmptyScale(t, scale);
                }
            }

            if (HeroSprite != null)
            {
                HeroSprite.scaleX = HeroSprite.scaleY = scale * 0.5;
                HeroSprite.posChanged = true;
            }
            if (NameLabel != null)
            {
                NameLabel.scaleX = NameLabel.scaleY = scale * 0.30;
                NameLabel.posChanged = true;
            }
            if (StatusLabel != null)
            {
                StatusLabel.scaleX = StatusLabel.scaleY = scale * 0.25;
                StatusLabel.posChanged = true;
            }
        }

        private void ApplyEmptyScale(dc.ui.Text text, double? scale = null)
        {
            scale ??= host.get_pixelScale.Invoke();
            text.scaleX = text.scaleY = scale.Value * 0.25;
            text.posChanged = true;
        }
    }


    internal class PlayerSlotPanel : UIProcess
    {
        private const int MaxSlots = 4;

        public Flow Root { get; private set; }
        public Flow Container { get; private set; }
        public Flow Title { get; private set; }
        public HtmlText TitleText { get; }
        public PlayerSlotUI[] Slots { get; }

        private readonly LobbyView host;
        private readonly Queue<string> nameQueue = new();
        private bool isShowingName;

        public PlayerSlotPanel(LobbyView host, Flow parent, Process? processParent = null)
            : base(processParent)
        {
            this.host = host;

            Root = new Flow(parent);
            Root.set_isVertical(true);
            Root.set_horizontalAlign(new FlowAlign.Middle());
            Root.set_verticalAlign(new FlowAlign.Middle());
            Root.set_verticalSpacing(host.Pixel(10));
            Root.set_minWidth(host.CardW);
            Root.set_maxWidth(host.CardW);

            //标题
            Title = new Flow(Root);
            Title.set_isVertical(false);
            Title.set_horizontalAlign(new FlowAlign.Middle());
            Title.set_verticalAlign(new FlowAlign.Top());

            TitleText = new HtmlText(Assets.Class.font12, Title);
            ApplyTitleFont();

            //卡槽容器
            Container = new Flow(Root);
            Container.set_isVertical(false);
            Container.set_horizontalAlign(new FlowAlign.Middle());
            Container.set_verticalAlign(new FlowAlign.Middle());
            Container.set_horizontalSpacing(host.Pixel(20));

            //创建4个卡槽
            Slots = new PlayerSlotUI[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
            {
                Slots[i] = new PlayerSlotUI(Container, host);
                Slots[i].OnChanged += OnSlotChanged;
            }

            ApplyLayout();

            //订阅屏幕变化
            host.OnScreenResized += ApplyLayout;
        }


        public void Refresh(IEnumerable<GuestInfo>? guests)
        {
            var list = guests?.Take(MaxSlots).ToList() ?? new List<GuestInfo>();

            //移除已离开的
            for (int i = 0; i < MaxSlots; i++)
            {
                var g = Slots[i].Guest;
                if (g != null && !list.Any(x => x.Guid == g.Guid))
                    Slots[i].Clear();
            }

            //绑定和刷新
            for (int i = 0; i < list.Count; i++)
            {
                var guest = list[i];
                if (Slots[i].Guest?.Guid == guest.Guid)
                {
                    Slots[i].Guest = guest;
                    Slots[i].RefreshStatus();
                }
                else
                {
                    Slots[i].Clear();
                    Slots[i].Bind(guest);
                }
            }

            //多余槽清空
            for (int i = list.Count; i < MaxSlots; i++)
                Slots[i].Clear();
        }

        public void RefreshStatuses()
        {
            foreach (var s in Slots) s.RefreshStatus();
        }

        private void ApplyLayout()
        {
            Root.set_minWidth(host.CardW);
            Root.set_maxWidth(host.CardW);

            Title.set_minWidth(host.CardW);
            Title.set_minHeight(host.CardH / 8);
            Title.set_maxHeight(host.CardH / 8);

            Container.set_minWidth(host.CardW);

            int spacing = host.Pixel(20);
            Container.set_horizontalSpacing(spacing);
            Container.set_minWidth(host.CardW);
            Container.set_maxWidth(host.CardW);

            int totalSpacing = spacing * (MaxSlots - 1);
            int slotW = (host.CardW - totalSpacing) / MaxSlots;

            foreach (var s in Slots)
            {
                s.Container.set_minWidth(slotW);
                s.Container.set_maxWidth(slotW);
                s.ApplyScale();
                s.Container.reflow();
            }

            ApplyTitleFont();

            Title.reflow();
            Container.reflow();
            Root.reflow();
        }

        public void ClearAll()
        {
            foreach (var s in Slots) s.Clear();

            Title.removeChildren();
            Title.remove();
            Container.removeChildren();
            Container.remove();
            Root.removeChildren();
            Root.remove();
        }

        private void ApplyTitleFont()
        {
            var pixelScale = host.get_pixelScale.Invoke();
            var font = pixelScale <= 3.0 ? Assets.Class.fontConf.font12
                     : pixelScale <= 4.0 ? Assets.Class.fontConf.font18
                     : pixelScale <= 6.0 ? Assets.Class.fontConf.font12
                     : Assets.Class.fontConf.font18;

            TitleText.set_font(font.toFont());
            TitleText.scaleX = TitleText.scaleY = pixelScale * 0.25;
            TitleText.posChanged = true;
        }

        //新玩家提示
        private void OnSlotChanged(PlayerSlotUI slot)
        {
            var name = slot.Guest?.Name;
            if (string.IsNullOrEmpty(name)) return;

            nameQueue.Enqueue(name);
            if (!isShowingName)
                ShowNextName();
        }

        private void ShowNextName()
        {
            if (nameQueue.Count == 0)
            {
                isShowingName = false;
                return;
            }

            isShowingName = true;
            string next = nameQueue.Dequeue();
            host.ShowToast($"玩家 {next} 加入");
            ShowNextName();
        }

        protected override void MyDispose()
        {
            host.OnScreenResized -= ApplyLayout;

            foreach (var s in Slots)
                s.OnChanged -= OnSlotChanged;

            ClearAll();
        }
    }
}
