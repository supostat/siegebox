using System;
using System.Collections.Generic;

namespace Siegebox.Process.Tests
{
    internal sealed class StubCommand : ICommand
    {
        private readonly Func<ExecutionContext, IReadOnlyList<string>, IProcess> factory;

        public StubCommand(Func<ExecutionContext, IReadOnlyList<string>, IProcess> factory)
        {
            this.factory = factory;
        }

        public string Name => "stub";

        public ExecutionContext LastContext { get; private set; }

        public IReadOnlyList<string> LastArguments { get; private set; }

        public IProcess LastCreated { get; private set; }

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
        {
            LastContext = context;
            LastArguments = arguments;
            LastCreated = factory(context, arguments);
            return LastCreated;
        }
    }
}
