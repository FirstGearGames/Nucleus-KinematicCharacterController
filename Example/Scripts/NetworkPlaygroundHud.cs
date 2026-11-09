using Nucleus.Integrations.Unity;
using Nucleus.Integrations.Unity.Managers.Transports;
using Nucleus.Managers.Core;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// Starts the playground as a host, a server or a client from three buttons, then shows the controls.
    /// </summary>
    /// <remarks>
    /// For two editors, start a host in the main editor and a client in a ParrelSync clone. Nothing starts on its own, so a test can load
    /// the scene and start whichever role it needs.
    /// </remarks>
    public class NetworkPlaygroundHud : MonoBehaviour
    {
        /// <summary>
        /// The address a client connects to.
        /// </summary>
        [SerializeField]
        [Tooltip("The address a client connects to.")]
        private string _address = "127.0.0.1";
        /// <summary>
        /// The scene's transport manager, which starts every role.
        /// </summary>
        private UnityTransportManager _unityTransportManager;
        /// <summary>
        /// The CoreManager the scene binds, whose transport reports whether a role came up.
        /// </summary>
        private CoreManager _coreManager;
        /// <summary>
        /// What the HUD shows once a role was asked for, or null while nothing has been.
        /// </summary>
        private string _status;
        /// <summary>
        /// The controls, shown under the status once a role is up.
        /// </summary>
        private const string ControlsText = "WASD moves, Space jumps, C crouches, the mouse looks and the right button zooms.";

        /// <summary>
        /// Resolves the managers the buttons start.
        /// </summary>
        private void Awake()
        {
            _unityTransportManager = FindAnyObjectByType<UnityTransportManager>();
            _coreManager = NucleusUnity.BoundCoreManager;

            if (_unityTransportManager == null || _coreManager is null)
            {
                Debug.LogError($"[{nameof(NetworkPlaygroundHud)}] needs a UnityCoreManager and a [{nameof(UnityTransportManager)}] in the scene.");
                enabled = false;
            }
        }

        /// <summary>
        /// Draws the buttons until a role is asked for, then the status and the controls.
        /// </summary>
        private void OnGUI()
        {
            GUILayout.BeginArea(new(10f, 10f, 320f, 160f));

            if (_status is null)
            {
                if (GUILayout.Button("Start Host"))
                    StartHost();

                if (GUILayout.Button("Start Server"))
                    StartServer();

                _address = GUILayout.TextField(_address);

                if (GUILayout.Button("Start Client"))
                    StartClient();
            }
            else
            {
                GUILayout.Label(_status);
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Starts a host: a server whose own client plays too.
        /// </summary>
        private async void StartHost()
        {
            _status = "Starting a host.";
            await _unityTransportManager.StartHostAsync();
            _status = _coreManager.TransportManager.IsServerStarted ? $"Host started. {ControlsText}" : "The host did not start; the console says why.";
        }

        /// <summary>
        /// Starts a server with no player of its own.
        /// </summary>
        private async void StartServer()
        {
            _status = "Starting a server.";
            await _unityTransportManager.StartServerAsync();
            _status = _coreManager.TransportManager.IsServerStarted ? "Server started. Connect a client to play." : "The server did not start; the console says why.";
        }

        /// <summary>
        /// Starts a client that connects to <see cref="_address"/>.
        /// </summary>
        private async void StartClient()
        {
            _status = $"Connecting to {_address}. {ControlsText}";
            await _unityTransportManager.StartClientAsync(_address, port: null);
        }
    }
}
