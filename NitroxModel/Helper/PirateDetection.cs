using System;
using System.IO;
using NitroxModel.Platforms.OS.Shared;

namespace NitroxModel.Helper
{
    public static class PirateDetection
    {
        public static bool HasTriggered { get; private set; }

        /// <summary>
        ///     Event that calls subscribers if the pirate detection triggered successfully.
        ///     New subscribers are immediately invoked if the pirate flag has been set at the time of subscription.
        /// </summary>

        public static bool TriggerOnDirectory(string subnauticaRoot)
        {

            return false;
        }

    }
}
