using System.Diagnostics.CodeAnalysis;

namespace Abstractions.Commands;

public interface ICommandDispatcher
{
    Task<TCommandResult> Dispatch<TCommand, TCommandResult>(TCommand command, CancellationToken cancellation)
        where TCommand : ICommand<TCommandResult>;
}
