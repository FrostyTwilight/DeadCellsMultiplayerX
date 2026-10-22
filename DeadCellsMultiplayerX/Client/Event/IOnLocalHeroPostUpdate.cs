
using dc.en;
using ModCore.Events;

namespace DeadCellsMultiplayerX.Client.Event
{
    /// <summary>
    /// 用于收集本地客户端快照信息
    /// </summary>
    /// <remarks>每帧更新,挂载至:<see cref="ClientMain.Hook_Hero_postUpdate(Hook_Hero.orig_postUpdate, Hero)"/></remarks>
    [Event]
    public interface IOnLocalHeroPostUpdate
    {
        void OnPostUpdate(Hero hero);
    }
}