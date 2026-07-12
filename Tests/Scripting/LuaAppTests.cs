using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using NUnit.Framework;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell;
using Siegebox.Vfs;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class LuaAppTests
    {
        private const string HelloAppChunk =
            "close_count = 0" +
            " local focus_count = 0" +
            " siegebox.register_app{" +
            "   id = 'hello-app'," +
            "   name = 'hello'," +
            "   on_launch = function(app) app.set_text('launched') end," +
            "   on_focus = function(app)" +
            "     focus_count = focus_count + 1" +
            "     app.set_text('focused ' .. focus_count)" +
            "   end," +
            "   on_focus_lost = function(app) app.set_text('blurred') end," +
            "   on_close = function(app) close_count = close_count + 1 end" +
            " }";

        private static (AppRegistry Apps, LuaHost Host, List<string> Log) CreateAppEnvironment()
        {
            var apps = new AppRegistry();
            var log = new List<string>();
            var api = new ScriptApi(
                new CommandRegistry(),
                apps,
                new FileTypeRegistry(),
                new VirtualFileSystem(),
                new EventBus(),
                log.Add);
            var host = new LuaHost();
            api.InstallInto(host, api.CreateScope());
            return (apps, host, log);
        }

        [Test]
        public void Register_app_creates_a_descriptor()
        {
            var (apps, host, _) = CreateAppEnvironment();

            host.RunChunk(HelloAppChunk, "hello-app");

            Assert.That(apps.TryGet("hello-app", out var descriptor), Is.True);
            Assert.That(descriptor.DisplayName, Is.EqualTo("hello"));
        }

        [Test]
        public void Register_app_without_name_uses_the_id_as_display_name_and_title()
        {
            var (apps, host, _) = CreateAppEnvironment();

            host.RunChunk("siegebox.register_app{ id = 'plain', on_launch = function(app) end }", "plain");

            Assert.That(apps.TryGet("plain", out var descriptor), Is.True);
            Assert.That(descriptor.DisplayName, Is.EqualTo("plain"));
            Assert.That(((ITextContentApp)descriptor.CreateInstance()).Title, Is.EqualTo("plain"));
        }

        [Test]
        public void Launch_runs_on_launch_and_updates_text_and_revision()
        {
            var (apps, host, _) = CreateAppEnvironment();
            host.RunChunk(HelloAppChunk, "hello-app");
            apps.TryGet("hello-app", out var descriptor);

            var app = descriptor.CreateInstance();
            Assert.That(app.State, Is.EqualTo(AppState.Created));
            var surface = (ITextContentApp)app;
            var revisionBefore = surface.Revision;

            app.OnLaunched();

            Assert.That(app.State, Is.EqualTo(AppState.Running));
            Assert.That(surface.Title, Is.EqualTo("hello"));
            Assert.That(surface.Text, Is.EqualTo("launched"));
            Assert.That(surface.Revision, Is.GreaterThan(revisionBefore));
        }

        [Test]
        public void Focus_hooks_run_against_the_app_table()
        {
            var (apps, host, _) = CreateAppEnvironment();
            host.RunChunk(HelloAppChunk, "hello-app");
            apps.TryGet("hello-app", out var descriptor);
            var app = descriptor.CreateInstance();
            var surface = (ITextContentApp)app;
            app.OnLaunched();

            app.OnFocusGained();
            Assert.That(surface.Text, Is.EqualTo("focused 1"));

            app.OnFocusLost();
            Assert.That(surface.Text, Is.EqualTo("blurred"));

            app.OnFocusGained();
            Assert.That(surface.Text, Is.EqualTo("focused 2"));
        }

        [Test]
        public void Second_launch_throws()
        {
            var (apps, host, _) = CreateAppEnvironment();
            host.RunChunk(HelloAppChunk, "hello-app");
            apps.TryGet("hello-app", out var descriptor);
            var app = descriptor.CreateInstance();
            app.OnLaunched();

            Assert.Throws<InvalidOperationException>(app.OnLaunched);
        }

        [Test]
        public void On_closed_runs_the_close_hook_at_most_once()
        {
            var (apps, host, _) = CreateAppEnvironment();
            host.RunChunk(HelloAppChunk, "hello-app");
            apps.TryGet("hello-app", out var descriptor);
            var app = descriptor.CreateInstance();
            app.OnLaunched();

            app.OnClosed();
            app.OnClosed();

            Assert.That(app.State, Is.EqualTo(AppState.Closed));
            Assert.That(host.RunChunk("return close_count", "probe").Number, Is.EqualTo(1));
        }

        [Test]
        public void Two_instances_keep_independent_text()
        {
            var (apps, host, _) = CreateAppEnvironment();
            host.RunChunk(HelloAppChunk, "hello-app");
            apps.TryGet("hello-app", out var descriptor);
            var first = descriptor.CreateInstance();
            var second = descriptor.CreateInstance();
            first.OnLaunched();
            second.OnLaunched();

            first.OnFocusGained();

            Assert.That(((ITextContentApp)first).Text, Is.EqualTo("focused 1"));
            Assert.That(((ITextContentApp)second).Text, Is.EqualTo("launched"));
        }

        [Test]
        public void On_launch_error_propagates()
        {
            var (apps, host, _) = CreateAppEnvironment();
            host.RunChunk(
                "siegebox.register_app{ id = 'bad-launch', on_launch = function(app) error('nope') end }",
                "bad-launch");
            apps.TryGet("bad-launch", out var descriptor);
            var app = descriptor.CreateInstance();

            Assert.That(app.OnLaunched, Throws.InstanceOf<ScriptRuntimeException>());
        }

        [Test]
        public void Focus_hook_error_is_contained_and_reaches_the_error_sink()
        {
            var (apps, host, log) = CreateAppEnvironment();
            host.RunChunk(
                "siegebox.register_app{" +
                " id = 'bad-focus'," +
                " on_launch = function(app) end," +
                " on_focus = function(app) error('focus boom') end }",
                "bad-focus");
            apps.TryGet("bad-focus", out var descriptor);
            var app = descriptor.CreateInstance();
            app.OnLaunched();

            Assert.That(app.OnFocusGained, Throws.Nothing);

            Assert.That(log, Has.Count.EqualTo(1));
            Assert.That(log[0], Does.Contain("on_focus"));
            Assert.That(log[0], Does.Contain("focus boom"));
        }

        [Test]
        public void Infinite_loop_focus_hook_is_contained_and_reports_budget_exhaustion()
        {
            var (apps, host, log) = CreateAppEnvironment();
            host.RunChunk(
                "siegebox.register_app{" +
                " id = 'spin-focus'," +
                " on_launch = function(app) end," +
                " on_focus = function(app) while true do end end }",
                "spin-focus");
            apps.TryGet("spin-focus", out var descriptor);
            var app = descriptor.CreateInstance();
            app.OnLaunched();

            Assert.That(app.OnFocusGained, Throws.Nothing);

            Assert.That(log, Has.Count.EqualTo(1));
            Assert.That(log[0], Does.Contain("on_focus"));
            Assert.That(log[0], Does.Contain("budget"));
        }

        [Test]
        public void Missing_optional_hooks_are_skipped()
        {
            var (apps, host, log) = CreateAppEnvironment();
            host.RunChunk(
                "siegebox.register_app{ id = 'minimal', on_launch = function(app) app.set_text('up') end }",
                "minimal");
            apps.TryGet("minimal", out var descriptor);
            var app = descriptor.CreateInstance();
            app.OnLaunched();

            Assert.That(() =>
            {
                app.OnFocusGained();
                app.OnFocusLost();
                app.OnClosed();
            }, Throws.Nothing);
            Assert.That(log, Is.Empty);
        }
    }
}
