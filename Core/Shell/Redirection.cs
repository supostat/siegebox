using System;

namespace Siegebox.Shell
{
    public sealed class Redirection : IShellAstNode
    {
        public Redirection(RedirectionKind kind, string targetWord)
        {
            if (targetWord is null)
            {
                throw new ArgumentNullException(nameof(targetWord));
            }

            Kind = kind;
            TargetWord = targetWord;
        }

        public RedirectionKind Kind { get; }

        public string TargetWord { get; }

        public int Descriptor => Kind == RedirectionKind.In ? 0 : 1;
    }
}
