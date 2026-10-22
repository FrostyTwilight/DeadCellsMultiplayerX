using System;
using System.Threading;
using dc;
using dc.ui;
using Microsoft;
using ModCore.Events;
using Serilog;

namespace DeadCellsMultiplayerX.Client.UI.Common
{
    /// <summary>
    /// UI的基类
    /// </summary>
    public abstract class UIProcess : Process, IEventReceiver, IDisposableObservable
    {
        private readonly CancellationTokenSource disposedSource = new();
        public CancellationToken DisposeToken => disposedSource.Token;
        public bool IsDisposed => disposedSource.IsCancellationRequested;
        public virtual ILogger Logger { get; }

        public event Action? OnScreenResized;

        private int screenW = -1;
        private int screenH = -1;

        public int ScreenW
        {
            get
            {
                int w = dc.libs.Process.Class.CUSTOM_STAGE_WIDTH > 0
                ? dc.libs.Process.Class.CUSTOM_STAGE_WIDTH
                : dc.hxd.Window.Class.getInstance().get_width();
                ScreenW = w;
                return screenW;
            }
            private set => screenW = value;
        }

        public int ScreenH
        {
            get
            {
                int h = dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT > 0
                ? dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT
                : dc.hxd.Window.Class.getInstance().get_height();
                ScreenH = h;
                return screenH;
            }
            private set => screenH = value;
        }

        protected UIProcess(Process? parent, int? dpLayer = null) : base(parent!)
        {
            Logger = Log.ForContext(GetType());
            EventSystem.AddReceiver(this);

            var layers = Main.Class.ME?.root;
            if (layers != null)
            {
                int layer = dpLayer ?? dc.Const.Class.ROOT_DP_MENU;
                createRootInLayers(layers, layer);
            }

            CalcScreenSize();
        }

        public override void onResize()
        {
            base.onResize();
            CalcScreenSize();
            OnScreenResized?.Invoke();
        }

        protected void CalcScreenSize()
        {
            int w = dc.libs.Process.Class.CUSTOM_STAGE_WIDTH > 0
                ? dc.libs.Process.Class.CUSTOM_STAGE_WIDTH
                : dc.hxd.Window.Class.getInstance().get_width();

            int h = dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT > 0
                ? dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT
                : dc.hxd.Window.Class.getInstance().get_height();

            if (w == ScreenW && h == ScreenH) return;

            ScreenW = w;
            ScreenH = h;
        }

        protected abstract void MyDispose();

        public void Dispose()
        {
            if (IsDisposed) return;
            GC.SuppressFinalize(this);
            onDispose();
        }

        public override void onDispose()
        {
            if (!IsDisposed)
            {
                disposedSource.Cancel();
            }

            EventSystem.RemoveReceiver(this);

            MyDispose();

            destroy();
        }
    }
}