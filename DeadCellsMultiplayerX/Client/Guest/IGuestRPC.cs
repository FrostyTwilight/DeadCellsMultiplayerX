using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using PolyType;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeadCellsMultiplayerX.Client.Guest
{
    [JsonRpcContract, GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    internal partial interface IGuestRPC
    {
        /// <summary>
        /// 告诉客户端进入新level
        /// </summary>
        /// <param name="saveData"></param>
        /// <returns></returns>
        public Task EnterNewLevel(byte[] saveData);

        /// <summary>
        /// 告诉客户端切换关卡
        /// </summary>
        /// <param name="levelid"></param>
        /// <returns></returns>
        public Task EnterNextLevel(string levelid);


        /// <summary>服务端每 tick 推送完整世界帧</summary>
        public Task SyncWorldFrame(WorldFrame frame);

        /// <summary>服务端请求补发缺失的客户端上传帧</summary>
        public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request);
    }
}
