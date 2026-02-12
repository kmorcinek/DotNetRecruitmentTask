using Abstractions.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Commands;

internal sealed class CommandDispatcher(IServiceProvider serviceProvider, ILogger<CommandDispatcher> logger)
    : ICommandDispatcher
{
    public async Task<TCommandResult> Dispatch<TCommand, TCommandResult>(
        TCommand command,
        CancellationToken cancellation)
        where TCommand : ICommand<TCommandResult>
    {
        try
        {
            var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TCommandResult>>();
            return await handler.Handle(command, cancellation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, message: "{@Command}", command);
            throw;
        }
    }
}
