using Orblit;
using OrblitTools.Build;
using OrblitTools.Internals;
using JetBrains.Annotations;

namespace OrblitTools
{
    public partial class HotReloadAssemblyWatcher : Entity
    {
#nullable disable
        private Timer _watchTimer;
#nullable enable

        public override void _Notification(int what)
        {
            if (what == Entity.NotificationWMWindowFocusIn)
            {
                RestartTimer();

                if (Internal.IsAssembliesReloadingNeeded())
                {
                    BuildManager.UpdateLastValidBuildDateTime();
                    Internal.ReloadAssemblies();
                }
            }
        }

        private void TimerTimeout()
        {
            if (Internal.IsAssembliesReloadingNeeded())
            {
                BuildManager.UpdateLastValidBuildDateTime();
                Internal.ReloadAssemblies();
            }
        }

        [UsedImplicitly]
        public void RestartTimer()
        {
            _watchTimer.Stop();
            _watchTimer.Start();
        }

        public override void OnStart()
        {
            base.OnStart();

            _watchTimer = new Timer
            {
                OneShot = false,
                WaitTime = 0.5f
            };
            _watchTimer.Timeout += TimerTimeout;
            AddChild(_watchTimer);
            _watchTimer.Start();
        }
    }
}
