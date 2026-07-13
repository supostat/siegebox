using NUnit.Framework;

namespace Siegebox.App.Tests
{
    /// <summary>
    /// Pins the opt-in persistence contract: an app that implements <see cref="IPersistentApp"/>
    /// hands the host an opaque state string on capture and receives the same string verbatim on
    /// restore. The host treats the payload as a black box.
    /// </summary>
    [TestFixture]
    public sealed class AppPersistenceTests
    {
        [Test]
        public void Persistent_app_round_trips_its_opaque_state_string()
        {
            var source = new FakePersistentApp();
            source.RestoreState("cwd=/var/log;selection=3");

            var captured = source.CaptureState();

            var restored = new FakePersistentApp();
            restored.RestoreState(captured);

            Assert.That(restored.CaptureState(), Is.EqualTo("cwd=/var/log;selection=3"));
        }

        [Test]
        public void A_persistent_app_is_also_an_app()
        {
            IApp app = new FakePersistentApp();

            Assert.That(app, Is.InstanceOf<IPersistentApp>());
        }
    }
}
