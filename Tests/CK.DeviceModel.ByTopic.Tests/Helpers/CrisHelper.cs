using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DeviceModel.ByTopic.Tests.Helpers
{
    public class CrisHelper
    {
        public static async Task<TResult> SendCrisCommandWithResultAsync<TCommand, TResult>(
            TCommand command,
            IActivityMonitor monitor,
            CrisBackgroundExecutor cbe
        )
    where TCommand : class, ICommand<TResult>
        {
            ExecutingCommand<TCommand> ec = (ExecutingCommand<TCommand>)cbe.Submit(
                monitor,
                command
            );

            return await ec.WithResult<TResult>().Result;
        }

        public static async Task SendCrisCommandAsync<TCommand>(
            TCommand command,
            IActivityMonitor monitor,
            CrisBackgroundExecutor cbe
        )
            where TCommand : class, IAbstractCommand
        {
            ExecutingCommand<TCommand> ec = (ExecutingCommand<TCommand>)cbe.Submit(
                monitor,
                command
            );

            var executedCommand = await ec.ExecutedCommand;

            if( executedCommand.Result is ICrisResultError err )
            {
                throw new CKException( $"Command failed with {err.Errors.Count} messages: {string.Join( "; ", err.Errors.Select( um => um.Message ) )}" );
            }
        }
    }
}
