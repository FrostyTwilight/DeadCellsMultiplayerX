using DeadCellsMultiplayerX.Client;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using PolyType;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeadCellsMultiplayerX.Server
{
    [JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    internal partial interface IServerRPC
    {
        public class AreaInfoRequest
        {
            public RectInt Rect { get; set; } = new();
            public int SubLevelId { get; set; }
        }
        /// <summary>
        /// 设置玩家信息
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public Task SetGuestInfo(GuestInfo info);

        /// <summary>
        /// Host 上传存档文件
        /// </summary>
        /// <param name="savedata"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public Task UploadSavedata(byte[] data);

        /// <summary>
        /// Guest 下载存档文件
        /// </summary>
        /// <returns></returns>
        public Task<byte[]> DownloadSavedata();

        /// <summary>
        /// 请求一个区域信息
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <returns></returns>
        public Task<byte[]> RequestAreaInfo(AreaInfoRequest request);

        /// <summary>提交下一关的选择（投票）</summary>
        /// <param name="vote">玩家的关卡选择</param>
        public Task SubmitLevelVote(LevelVote vote);

        /// <summary>
        /// 客户端告诉服务端,选择的下一个关卡
        /// </summary>
        /// <param name="plyerid"></param>
        /// <param name="levelid"></param>
        /// <returns></returns>
        public Task GusetEnterNextLevel(string plyerid, string levelid);

        /// <summary>
        /// 获取服务器时间
        /// </summary>
        /// <returns></returns>
        public Task<long> GetTimeStamp();

        public Task<bool> CheckVersion(string version);

        public Task Ping();

        /// <summary>
        /// 客户端每帧上传本地世界帧
        /// </summary>
        public Task UploadClientWorldFrame(ClientWorldFrame frame);

        /// <summary>
        /// 请求补发缺失的服务端世界帧;历史已淘汰时返回全量基线
        /// </summary>
        public Task<WorldFrameBundle> RequestWorldFrames(WorldFrameRangeRequest request);

        /// <summary>
        /// 请求全量世界基线
        /// </summary>
        public Task<WorldResync> RequestWorldResync(int knownEpoch);
    }
}
