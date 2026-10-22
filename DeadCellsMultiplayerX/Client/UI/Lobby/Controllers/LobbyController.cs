using System;
using System.Threading.Tasks;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;

namespace DeadCellsMultiplayerX.Client.UI.Lobby.Controllers
{
    //Controller 基类
    internal abstract class LobbyController : UIProcess
    {
        protected readonly ClientMain Client;
        protected readonly LobbyContext Ctx;
        protected readonly LobbyStateMachine State;

        protected LobbyController(
            ClientMain client,
            LobbyContext ctx,
            LobbyStateMachine state,
            Process? parent = null) : base(parent)
        {
            Client = client;
            Ctx = ctx;
            State = state;
        }

        //错误处理
        protected void Fail(Exception ex, string msg)
        {
            Logger.Error(ex, msg);
            Ctx.LastError = $"{msg}: {ex.Message}";

            if (State.CanGo(LobbyState.Error))
                State.TryGo(LobbyState.Error);
        }

        //错误提示
        protected void Fail(string msg)
        {
            Logger.Error(msg);
            Ctx.LastError = msg;
        }

        protected async Task RunAsync(Func<CancellationToken, Task> action, string operationName)
        {
            try
            {
                await action(DisposeToken);
            }
            catch (OperationCanceledException) when (IsDisposed)
            {
                Logger.Information($"{operationName} canceled");
            }
            catch (Exception ex)
            {
                Fail(ex, operationName);
            }
        }
    }
}