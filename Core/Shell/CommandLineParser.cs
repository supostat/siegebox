using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    /// <summary>
    /// Recursive-descent parser: list := pipeline ((';' | '&amp;&amp;' | '||' | '&amp;') pipeline?)*,
    /// pipeline := command ('|' command)*, command := (word | redirect)+.
    /// '&amp;' marks the preceding pipeline as background.
    /// </summary>
    public sealed class CommandLineParser
    {
        public ListNode Parse(IReadOnlyList<Token> tokens)
        {
            if (tokens is null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            if (tokens.Count == 0)
            {
                throw new ArgumentException("There is nothing to parse.", nameof(tokens));
            }

            return new ParseRun(tokens).ParseList();
        }

        private sealed class ParseRun
        {
            private readonly IReadOnlyList<Token> tokens;
            private int index;

            public ParseRun(IReadOnlyList<Token> tokens)
            {
                this.tokens = tokens;
            }

            private bool AtEnd => index == tokens.Count;

            private Token Current => tokens[index];

            public ListNode ParseList()
            {
                var items = new List<ListItem>();
                var chainOperator = ListOperator.Always;
                while (true)
                {
                    var commands = ParsePipelineCommands();
                    var background = TryConsume(TokenType.Background);
                    items.Add(new ListItem(chainOperator, new PipelineNode(commands, background)));
                    if (AtEnd)
                    {
                        break;
                    }

                    chainOperator = ConsumeSeparator(background);
                    if (AtEnd)
                    {
                        break;
                    }
                }

                return new ListNode(items);
            }

            private List<CommandNode> ParsePipelineCommands()
            {
                var commands = new List<CommandNode> { ParseCommand() };
                while (!AtEnd && Current.Type == TokenType.Pipe)
                {
                    index++;
                    commands.Add(ParseCommand());
                }

                return commands;
            }

            private CommandNode ParseCommand()
            {
                var words = new List<string>();
                var redirections = new List<Redirection>();
                while (!AtEnd)
                {
                    var token = Current;
                    if (token.Type == TokenType.Word)
                    {
                        words.Add(token.Text);
                        index++;
                        continue;
                    }

                    if (!IsRedirect(token.Type))
                    {
                        break;
                    }

                    index++;
                    redirections.Add(ParseRedirectTarget(token));
                }

                if (words.Count == 0)
                {
                    throw MissingCommandError();
                }

                return new CommandNode(words, redirections);
            }

            private Redirection ParseRedirectTarget(Token redirectToken)
            {
                if (AtEnd || Current.Type != TokenType.Word)
                {
                    throw new ShellParseException($"redirect '{redirectToken.Text}' without target", redirectToken.Position);
                }

                var target = Current.Text;
                index++;
                return new Redirection(KindOf(redirectToken.Type), target);
            }

            private ListOperator ConsumeSeparator(bool background)
            {
                var separator = Current;
                switch (separator.Type)
                {
                    case TokenType.Semicolon:
                        index++;
                        return ListOperator.Always;
                    case TokenType.AndIf:
                        index++;
                        RequireMoreInput(separator);
                        return ListOperator.AndIf;
                    case TokenType.OrIf:
                        index++;
                        RequireMoreInput(separator);
                        return ListOperator.OrIf;
                    default:
                        if (background && IsCommandStart(separator.Type))
                        {
                            return ListOperator.Always;
                        }

                        throw new ShellParseException($"unexpected token '{separator.Text}'", separator.Position);
                }
            }

            private void RequireMoreInput(Token separator)
            {
                if (AtEnd)
                {
                    throw new ShellParseException($"unexpected end of input after '{separator.Text}'", separator.Position);
                }
            }

            private bool TryConsume(TokenType type)
            {
                if (AtEnd || Current.Type != type)
                {
                    return false;
                }

                index++;
                return true;
            }

            private ShellParseException MissingCommandError()
            {
                if (AtEnd)
                {
                    var last = tokens[tokens.Count - 1];
                    return new ShellParseException("unexpected end of input", last.Position + last.Text.Length);
                }

                return new ShellParseException($"unexpected token '{Current.Text}'", Current.Position);
            }

            private static bool IsRedirect(TokenType type)
                => type == TokenType.RedirectIn || type == TokenType.RedirectOut || type == TokenType.RedirectAppend;

            private static bool IsCommandStart(TokenType type) => type == TokenType.Word || IsRedirect(type);

            private static RedirectionKind KindOf(TokenType type) => type switch
            {
                TokenType.RedirectIn => RedirectionKind.In,
                TokenType.RedirectOut => RedirectionKind.Out,
                _ => RedirectionKind.Append
            };
        }
    }
}
