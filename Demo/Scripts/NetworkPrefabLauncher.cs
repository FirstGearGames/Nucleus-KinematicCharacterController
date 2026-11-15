using Nucleus.Connections;
using Nucleus.Integrations.Unity.Systems;
using Nucleus.Integrations.Unity;
using Nucleus.Managers.Rpc;
using Nucleus.Systems;
using Nucleus.Transports;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// Fires a networked rigidbody when any player presses Return: the player's client asks the server, the server spawns the prefab and
    /// gives it the launch velocity, and the prefab's own network component carries it to every peer.
    /// </summary>
    /// <remarks>
    /// Firing on whichever machine pressed the key would fire a different cube on every peer, or none on the server. Asking the server
    /// keeps one cube that everyone sees, and the server removes it after <see cref="_lifetime"/>.
    /// </remarks>
    [NucleusHelpUrl(typeof(NetworkPrefabLauncher))]
    public class NetworkPrefabLauncher : NucleusBehaviour<NetworkPrefabLauncherComponent>
    {
        /// <summary>
        /// The networked prefab fired, which needs a rigidbody and its own network component.
        /// </summary>
        [SerializeField]
        [Tooltip("The networked prefab fired. It needs a Rigidbody and a network component of its own, such as NetworkRigidbody.")]
        private Rigidbody _launchedPrefab;
        /// <summary>
        /// The speed the prefab leaves the launcher at, in metres per second.
        /// </summary>
        [SerializeField]
        [Tooltip("The speed the prefab leaves the launcher at, in metres per second.")]
        private float _launchSpeed = 10f;
        /// <summary>
        /// The seconds a fired prefab lasts before the server removes it.
        /// </summary>
        [SerializeField]
        [Tooltip("The seconds a fired prefab lasts before the server removes it.")]
        private float _lifetime = 8f;

        /// <inheritdoc/>
        /// <remarks>The system keeps the handler for as long as the launcher is spawned.</remarks>
        protected override void OnSystemLinked() => NetworkSystem.RegisterRpcHandler<LaunchRequestedRpc>(OnLaunchRequested);

        /// <inheritdoc/>
        /// <remarks>Nobody controls a launcher, so every client that sees it may ask it to fire.</remarks>
        protected override void OnServerStarted() => NetworkSystem.TrySetRpcAccess(RpcSendAccess.AnyClient);

        /// <summary>
        /// Asks the server to fire when this peer's player presses Return.
        /// </summary>
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Return) || !IsStarted(Invoker.Client) || NetworkSystem is not { IsStarted: true } networkSystem)
                return;

            networkSystem.TrySendRpc(RpcTarget.Server, Channel.Reliable, new LaunchRequestedRpc());
        }

        /// <summary>
        /// Fires the prefab on the server.
        /// </summary>
        /// <param name="rpcContext">Who asked.</param>
        /// <param name="launchRequestedRpc">The request.</param>
        /// <returns><see cref="RpcRelayAction.Relay"/>, since a request to the server is passed on to nobody else.</returns>
        private RpcRelayAction OnLaunchRequested(in RpcContext rpcContext, LaunchRequestedRpc launchRequestedRpc)
        {
            Transform launcherTransform = transform;
            launcherTransform.GetPositionAndRotation(out Vector3 launcherPosition, out Quaternion launcherRotation);

            Rigidbody launchedRigidbody = Instantiate(_launchedPrefab, launcherPosition, launcherRotation);
            launchedRigidbody.linearVelocity = launcherTransform.forward * _launchSpeed;
            Destroy(launchedRigidbody.gameObject, _lifetime);

            Component.LaunchCount.Value++;

            return RpcRelayAction.Relay;
        }
    }
}
