using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    /// <summary>
    /// Pins the real/effective identity split on the context: the 5-arg ctor keeps the two
    /// distinct (setuid spawn), the 4-arg ctor delegates real == effective (ordinary spawn),
    /// and a null real identity is rejected.
    /// </summary>
    [TestFixture]
    public sealed class ExecutionContextRealCredentialsTests
    {
        private static FileDescriptorTable Descriptors()
            => new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());

        [Test]
        public void Five_arg_constructor_keeps_real_and_effective_distinct()
        {
            var effective = new Credentials(0);
            var real = new Credentials(1000);

            var context = new ExecutionContext("/", effective, new Dictionary<string, string>(), Descriptors(), real);

            Assert.That(context.Credentials, Is.SameAs(effective));
            Assert.That(context.RealCredentials, Is.SameAs(real));
        }

        [Test]
        public void Four_arg_constructor_delegates_real_to_effective()
        {
            var credentials = new Credentials(1000);

            var context = new ExecutionContext("/", credentials, new Dictionary<string, string>(), Descriptors());

            Assert.That(context.Credentials, Is.SameAs(credentials));
            Assert.That(context.RealCredentials, Is.SameAs(credentials));
        }

        [Test]
        public void Null_real_credentials_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ExecutionContext("/", new Credentials(0), new Dictionary<string, string>(), Descriptors(), null));
        }
    }
}
