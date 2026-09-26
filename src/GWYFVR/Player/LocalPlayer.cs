using Extensions;
using UnityEngine;

namespace GWYFVR.Player
{
    /// <summary>Quick access to the local player's objects.</summary>
    internal static class LocalPlayer
    {
        /// <summary>Set when the local player's head starts on this client (see PlayerPatches).</summary>
        public static PlayerHead Head { get; set; }

        public static PlayerController Controller => Head != null ? Head._pc : null;

        public static Camera Camera
        {
            get
            {
                var local = MonoSingleton<LocalManager>.Instance;
                return local != null ? local.mainCamera : null;
            }
        }
    }
}
