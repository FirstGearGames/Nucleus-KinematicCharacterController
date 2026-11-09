using Nucleus.Managers.Rpc;
using Nucleus.Serializers;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// A client's request that a <see cref="NetworkPrefabLauncher"/> fire.
    /// </summary>
    [NetworkType]
    public struct LaunchRequestedRpc : IRpc
    {
    }
}
