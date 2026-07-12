using System;
using NUnit.Framework;

namespace Siegebox.App.Tests
{
    [TestFixture]
    public sealed class StaticTextAppTests
    {
        [Test]
        public void Lifecycle_runs_created_to_running_to_closed()
        {
            var app = new StaticTextApp("about", "Siegebox.");
            Assert.That(app.State, Is.EqualTo(AppState.Created));

            app.OnLaunched();
            Assert.That(app.State, Is.EqualTo(AppState.Running));

            app.OnFocusGained();
            app.OnFocusLost();
            app.OnClosed();
            Assert.That(app.State, Is.EqualTo(AppState.Closed));
        }

        [Test]
        public void Second_launch_throws()
        {
            var app = new StaticTextApp("about", "Siegebox.");
            app.OnLaunched();

            Assert.Throws<InvalidOperationException>(app.OnLaunched);
        }

        [Test]
        public void Title_and_text_stay_fixed_and_revision_never_moves()
        {
            var app = new StaticTextApp("about", "Siegebox.");
            var revisionBefore = app.Revision;

            app.OnLaunched();
            app.OnFocusGained();

            Assert.That(app.Title, Is.EqualTo("about"));
            Assert.That(app.Text, Is.EqualTo("Siegebox."));
            Assert.That(app.Revision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void Null_and_blank_arguments_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(() => new StaticTextApp(null, "text"));
            Assert.Throws<ArgumentNullException>(() => new StaticTextApp("title", null));
            Assert.Throws<ArgumentException>(() => new StaticTextApp(" ", "text"));
        }
    }
}
