using System;
using System.Collections.Generic;
using CoreLibrary.Utilities;
using dc;
using dc.h2d;
using dc.hxd;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views
{
    /// 测试页面
    internal class DebugView : UIProcess
    {
        private readonly LobbyView host;
        private Flow? contentFlow;
        private readonly List<Flow> items = new();
        private readonly Dictionary<Flow, Action> resizers = new();

        public DebugView(LobbyView host, Process? parent = null) : base(parent)
        {
            this.host = host;
            OnScreenResized += MyOnResize;
        }

        public void Build(Flow parent)
        {
            var content = new Flow(parent);
            content.set_isVertical(true);
            content.set_horizontalAlign(new FlowAlign.Middle());
            content.set_verticalAlign(new FlowAlign.Middle());
            content.set_verticalSpacing(host.Pixel(12));
            content.set_minWidth(host.CardW);
            content.set_maxWidth(host.CardW);

            contentFlow = content;

            AddTitle(content, "Debug Menu");

            AddDivider(content);

            AddDebugToggle(content, "显示网络统计", () => ToggleFlag("netstat"));
            AddDebugBtn(content, "本地2人测试", () => DebugLauncher.StartTest(1));
            AddDebugBtn(content, "本地3人测试", () => DebugLauncher.StartTest(2));
            AddDebugBtn(content, "本地4人测试", () => DebugLauncher.StartTest(3));

            AddDivider(content);

            var back = host.BuildBackButton(content);
            items.Add(back);

            content.reflow();
            parent.reflow();
        }


        private void AddTitle(Flow parent, string text)
        {
            var flow = new Flow(parent);
            var t = Assets.Class.makeMedievalText(text.AsHaxeString(), null, flow, null);
            t.scaleX = t.scaleY = host.get_pixelScale.Invoke() * 0.5;

            void Resize()
            {
                t.scaleX = t.scaleY = host.get_pixelScale.Invoke() * 0.5;
                t.posChanged = true;
            }
            Resize();
            resizers[flow] = Resize;
            items.Add(flow);
        }

        private void AddDivider(Flow parent)
        {
            var g = new Graphics(parent);
            g.beginFill(Ref<int>.In(0xFFFFFF), Ref<double>.In(0.15));
            g.drawRect(0, 0, host.CardW - host.Pixel(40), 1);
            g.endFill();
        }

        private void AddDebugBtn(Flow parent, string label, Action cb)
        {
            var btn = new Flow(parent);
            var text = Assets.Class.makeMedievalText(label.AsHaxeString(), null, btn, null);

            void Resize()
            {
                btn.set_padding(host.Pixel(8));
                text.scaleX = text.scaleY = host.get_pixelScale.Invoke() * 0.4;
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
                    cb();
                });
            }
            items.Add(btn);
        }

        private void AddDebugToggle(Flow parent, string label, Action cb)
        {
            AddDebugBtn(parent, $"[切换] {label}", cb);
        }


        private void ToggleFlag(string key)
        {
            DebugFlags.Toggle(key);
            ModEntry.Instance.Logger.Information($"[Debug] {key} = {DebugFlags.Get(key)}");
        }

        public void Refresh(LobbyContext ctx) { }

        private void MyOnResize()
        {
            if (contentFlow == null) return;

            contentFlow.set_minWidth(host.CardW);
            contentFlow.set_maxWidth(host.CardW);

            foreach (var r in resizers.Values)
            {
                try { r(); }
                catch (Exception ex) { Logger.Warning(ex, "Debug resize failed"); }
            }

            contentFlow.reflow();
        }

        public void Clear()
        {
            foreach (var item in items)
            {
                resizers.Remove(item);
                item.remove();
            }
            items.Clear();
            resizers.Clear();

            contentFlow?.removeChildren();
            contentFlow?.remove();
            contentFlow = null;
        }

        protected override void MyDispose()
        {
            OnScreenResized -= MyOnResize;
            Clear();
        }
    }
}