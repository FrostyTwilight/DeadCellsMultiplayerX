using dc;
using dc.pr;
using DeadCellsMultiplayerX.Client;
using DeadCellsMultiplayerX.Utils;
using ModCore.Events.Interfaces;
using ModCore.Mods;
using ModCore.Modules;
using ModCore.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;

namespace DeadCellsMultiplayerX
{
    internal class ModEntry(ModInfo info) : ModBase(info), IOnAfterLoadingAssets
    {
        public static ModEntry Instance { get; private set; } = null!;
        /// <summary>
        /// 初始化
        /// </summary>
        public override void Initialize()
        {
            Instance = this;

#if DEBUG
            DebugLauncher.Init();
#endif

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DCCM_MULTIPLAYER_NO_CLIENT")))
            {
                return;
            }

            Environment.SetEnvironmentVariable("DCCM_MULTIPLAYER_NO_CLIENT", "1");

            new ClientMain().Init();

            GetText.Instance.RegisterMod("DeadCellsMultiplayerX");

            //可以捕获到奇怪的报错
#if DEBUG
            Hook_Boot.mainLoop += Hook_Boot_mainLoop;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
#endif
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.Fatal(e.Exception, "TaskScheduler_UnobservedTaskException.");
        }

        private void Hook_Boot_mainLoop(Hook_Boot.orig_mainLoop orig, Boot self)
        {
            try
            {
                orig(self);
            }
            catch (Exception ex)
            {
                Logger.Error("{ex}", ex);
                //System.Diagnostics.Debugger.Break();
                ExceptionDispatchInfo.Capture(ex).Throw();
                throw;
            }
        }

        void IOnAfterLoadingAssets.OnAfterLoadingAssets()
        {
            var res = Info.ModRoot!.GetFilePath("res.pak");
            FsPak.Instance.FileSystem.loadPak(res.AsHaxeString());
        }
    }
}
