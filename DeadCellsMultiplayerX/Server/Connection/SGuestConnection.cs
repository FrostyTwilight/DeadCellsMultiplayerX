using dc;
using dc.en;
using dc.libs.heaps.slib;
using DeadCellsMultiplayerX.Client;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Host;
using DeadCellsMultiplayerX.Client.Networks;
using DeadCellsMultiplayerX.Common;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Server.Events;
using DeadCellsMultiplayerX.Utils;
using Hashlink.Virtuals;
using Microsoft.VisualStudio.Threading;
using ModCore.Events;
using ModCore.Events.Interfaces.Game;
using ModCore.Modules;
using ModCore.Utilities;
using Serilog;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace DeadCellsMultiplayerX.Server.Connection
{
    internal partial class SGuestConnection :
        DisposableEventReceiver,
        IOnServerEnterNewLevel,
        IOnEntitySetColorMap,
        IOnEntitySetGlowData,
        IOnEntityDisposed
    {


        private IServerRPC.AreaInfoRequest? lastRequest;

        private readonly JsonRpc rpc;
        public GuestInfo guestInfo = new();
        public override ILogger Logger { get; }
        public GuestInfo GuestInfo { get; set; } = new();
        public ServerSession Session { get; }
        public ServerMainThread Main => Session.Main;
        public IGuestRPC guest;
        private bool EnterNewLevelEnd { get; set; } = false;

        public SGuestConnection(ServerSession session, Stream connection)
        {
            Session = session;
            Logger = Log.ForContext("SourceContext", "Server-Guest-" + guestInfo.Guid);

            rpc = connection.CreateJsonRpc();

            rpc.AddLocalRpcTarget(this);
            

            guest = rpc.Attach<IGuestRPC>();

            rpc.SynchronizationContext = Game.SynchronizationContext;

            rpc.Disconnected += Rpc_Disconnected;

            rpc.StartListening();

            Logger.Information("Connected.");
        }

        private void Rpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
        {
            if (e.Reason == DisconnectedReason.LocallyDisposed)
            {
                return;
            }
            Logger.Error(e.Exception, "Abort connection: {reason}: {desc}", e.Reason, e.Description);
            Dispose();
        }

        protected override void MyDispose()
        {
            Session.RemoveGuest(this);

            base.MyDispose();

            rpc?.Dispose();
        }

        /// <summary>广播一帧世界快照(不阻塞游戏线程)。</summary>
        public void PushWorldFrame(WorldFrame frame)
        {
            if (IsDisposed || rpc.IsDisposed) return;

            try
            {
                var task = guest.SyncWorldFrame(frame);
                _ = task.ContinueWith(
                    t => Logger.Warning(t.Exception, "SyncWorldFrame failed"),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                // 同步序列化/连接异常不能冒泡到广播循环,否则会打断整个服务端 tick
                Logger.Warning(ex, "SyncWorldFrame failed (sync)");
            }
        }

        /// <summary>向该客户端请求补发上传帧。</summary>
        public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
            => guest.RequestClientFrames(request);



        void IOnServerEnterNewLevel.OnServerEnterNewLevel()
        {
            Debug.Assert(Main.savePath != null);

            guest.EnterNewLevel(File.ReadAllBytes(Main.savePath));

            EnterNewLevelEnd = true;

            EventSystem.BroadcastEvent<IOnHeroInitDone, Hero>(Game.Instance.HeroInstance!);
        }

        void IOnEntitySetColorMap.OnEntitySetColorMap(IOnEntitySetColorMap.Data data)
        {
           
        }

        void IOnEntitySetGlowData.OnEntitySetGlowData(IOnEntitySetGlowData.Data data)
        {
            // var info = GetEntityInfo(data.Entity);
            // info.GlowData[data.Index] = new byte[1024];
        }

        void IOnEntityDisposed.OnEntityDisposed(Entity e)
        {
            //entitiesInfo.Remove(e.HashlinkPointer);
        }


    }
}
