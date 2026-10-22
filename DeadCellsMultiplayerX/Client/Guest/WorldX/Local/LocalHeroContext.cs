namespace DeadCellsMultiplayerX.Client.Guest.WorldX.LocalHero
{
    /// <summary>
    /// 本地 Hero 的状态快照
    /// </summary>
    /// <remarks>
    /// 唯一实例位于 <see cref="LocalHeroState"/> 中
    /// </remarks>
    internal class LocalHeroContext
    {
        /// <summary>
        /// 是否正在子关卡中
        /// </summary>
        public bool InSubLevel;

        /// <summary>
        /// 当前 level id
        /// </summary>
        public string CurrentLevelId = string.Empty;

        /// <summary>
        /// 是否在过场动画
        /// </summary>
        public bool InCinematic;

        /// <summary>
        /// 当前 sublevel 索引
        /// </summary>
        public int SubLevelIndex = -1;

        /// <summary>
        /// 本地 Hero 是否存在
        /// </summary>
        public bool HasHero;

        /// <summary>
        /// 本地 Hero 是否活着
        /// </summary>
        public bool IsAlive;
    }
}