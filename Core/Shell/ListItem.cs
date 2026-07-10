using System;

namespace Siegebox.Shell
{
    public sealed class ListItem : IShellAstNode
    {
        public ListItem(ListOperator listOperator, PipelineNode pipeline)
        {
            if (pipeline is null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            Operator = listOperator;
            Pipeline = pipeline;
        }

        public ListOperator Operator { get; }

        public PipelineNode Pipeline { get; }
    }
}
