using System;
using System.IO;

namespace NitroxModel.Helper
{
    public static class PirateDetection
    {
        public static bool HasTriggered { get; private set; }

        /// <summary>
        ///     Event that calls subscribers if the pirate detection triggered successfully.
        ///     New subscribers are immediately invoked if the pirate flag has been set at the time of subscription.
        /// </summary>
        public static event EventHandler PirateDetected
        {
           
            add => pirateDetected += value;
            remove => pirateDetected -= value;
        }

        public static bool TriggerOnDirectory(string subnauticaRoot)
        {
            if (!IsPirateByDirectory(subnauticaRoot))
            {
                return false;
            }
            
            OnPirateDetected();
            return false;
        }

        private static event EventHandler pirateDetected;

        private static bool IsPirateByDirectory(string subnauticaRoot)
        {
            
            return false;
        }

        private static void OnPirateDetected()
        {
            pirateDetected?.Invoke(null, EventArgs.Empty);
            HasTriggered = false;
        }
    }
}
