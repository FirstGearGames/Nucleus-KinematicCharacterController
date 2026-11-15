using Nucleus.Components;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// The replicated state of a <see cref="NetworkPrefabLauncher"/>: how many times it has fired.
    /// </summary>
    public partial class NetworkPrefabLauncherComponent : NetworkComponent
    {
        /// <summary>
        /// How many prefabs the launcher has fired since it spawned, written by the server.
        /// </summary>
        public readonly NetworkMember<uint> LaunchCount = new();
    }
}
