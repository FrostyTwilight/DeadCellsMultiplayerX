using dc;
using dc.en;
using dc.libs.heaps.slib;
using DeadCellsMultiplayerX.Client;
using DeadCellsMultiplayerX.Common;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using ModCore.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DeadCellsMultiplayerX.Server.Connection
{
    internal partial class SGuestConnection : IServerRPC
    {
        private int transitionGate = 0;

        public Task<bool> CheckVersion(string version)
        {
            if (Version.Parse(version) != VersionUtils.ModVersion)
            {
                Logger.Error("Dismatch version: {ver}", guestInfo.Guid, version);
                Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                {
                    if (rpc == null)
                    {
                        return;
                    }
                    if (!rpc.IsDisposed)
                    {
                        Dispose();
                    }
                });
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        public Task SetGuestInfo(GuestInfo info)
        {
            GuestInfo = info;
            return Task.CompletedTask;
        }

        public async Task UploadSavedata(byte[] data)
        {
            await Task.Delay(1).ConfigureAwait(false);

            var savePath = ServerMain.Instance.savePath;

            await File.WriteAllBytesAsync(savePath, data);

            Logger.Information("Saving savedata to {path}", savePath);

            Logger.Information("Building game...");

            await Main.LaunchGame();

            await Task.Delay(1).ConfigureAwait(false);
        }

        public async Task<byte[]> DownloadSavedata()
        {
            while (string.IsNullOrEmpty(Main.savePath))
            {
                await Task.Yield();
            }
            return File.ReadAllBytes(Main.savePath);
        }

        public async Task<byte[]> RequestAreaInfo(IServerRPC.AreaInfoRequest request)
        {
            var rect = request.Rect;
            if (rect.X < 0)
            {
                rect.X = 0;
            }
            if (rect.Y < 0)
            {
                rect.Y = 0;
            }
            var areaInfo = new AreaInfo()
            {
                Rect = rect
            };

            lastRequest = request;

            var lvl = (dc.pr.Level)dc.pr.Game.Class.ME.subLevels.getDyn(request.SubLevelId);
            var map = lvl.map;

            areaInfo.Collision = new int[rect.Width * rect.Height];

            // 碰撞箱
            unsafe
            {
                var src = new Span<int>((void*)map.collisions.bytes, map.collisions.length);
                for (int y = 0; y < rect.Height; y++)
                {
                    var dst = new Span<int>(areaInfo.Collision, y * rect.Width, rect.Width);
                    src.Slice((rect.Y + y) * map.wid + rect.X, rect.Width).CopyTo(dst);
                }
            }

            // Entity
            {
                var rx = rect.X;
                var ry = rect.Y;
                var rxt = rect.X + rect.Width;
                var ryt = rect.Y + rect.Height;

                foreach (Entity v in lvl.entities)
                {
                    if (v is not Mob)
                    {
                        continue;
                    }

                    if (v.cx >= rx && v.cx <= rxt && v.cy >= ry && v.cy <= ryt && v.visible)
                    {
                        // EntityInfo inf = worldXDataDirector.GetEntityByPointer(v.HashlinkPointer)!;

                        // if (inf != null)
                        // {
                        //     v.isOnScreen = true;

                        //     FillEntityInfo(v, inf);

                        //     areaInfo.Entities.Add(inf);
                        // }
                    }
                }
            }

            // foreach (var item in await Session.GetGuestsHeroInfos())
            // {
            //     item.remoteTime = Session.CurrentTimeStamp;
            //     areaInfo.Entities.Add(item);
            // }

            return DCMXSerializers.MessagePack.Serialize(areaInfo);
        }


        public Task<long> GetTimeStamp() => Task.FromResult(Session.CurrentTimeStamp);


        public async Task GusetEnterNextLevel(string plyerid, string levelid)
        {
            if (Session.GuestsGameInfo[plyerid].LevelSelected)
                return;

            Session.GuestsGameInfo[plyerid].NextLevelID = levelid;
            Session.GuestsGameInfo[plyerid].LevelSelected = true;

            if (Interlocked.CompareExchange(ref transitionGate, 1, 0) != 0)
                return;

            while (!Session.GuestsGameInfo.All(e => e.Value.LevelSelected))
            {
                await Task.Delay(100);
            }

            var selectedLevelIds = Session.GuestsGameInfo
                .Select(e => e.Value.NextLevelID)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            string resultLevelId;

            if (selectedLevelIds.Count == 1)
            {
                resultLevelId = selectedLevelIds[0];
            }
            else
            {
                int index = System.Random.Shared.Next(selectedLevelIds.Count);
                resultLevelId = selectedLevelIds[index];
            }


            try
            {
                await Session.NoticeGuestsEnterNextLevel(resultLevelId);
            }
            finally
            {
                Interlocked.Exchange(ref transitionGate, 0);
            }
        }

        public Task SubmitLevelVote(LevelVote info)
        {
            Session.GuestsGameInfo[info.PlyerID] = info;
            return Task.CompletedTask;
        }

        public Task Ping() => Task.CompletedTask;

        public Task UploadClientWorldFrame(ClientWorldFrame frame)
        {
            Session.ClientFrames.OnFrame(GuestInfo.Guid, frame);
            return Task.CompletedTask;
        }

        public Task<WorldFrameBundle> RequestWorldFrames(WorldFrameRangeRequest request)
        {
            return Task.FromResult(Session.Broadcaster.GetFrames(request, Session.CurrentTimeStamp));
        }

        public Task<WorldResync> RequestWorldResync(int knownEpoch)
        {
            return Task.FromResult(Session.Broadcaster.BuildResync(Session.CurrentTimeStamp));
        }
    }
}
