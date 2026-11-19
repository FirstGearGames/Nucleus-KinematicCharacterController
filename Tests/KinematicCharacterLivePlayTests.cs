using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using KinematicCharacterController;
using Nucleus.Connections;
using Nucleus.Integrations.KinematicCharacterController.Demo;
using Nucleus.Integrations.Unity;
using Nucleus.Integrations.Unity.Managers.Transports;
using Nucleus.Integrations.Unity.Physics;
using Nucleus.Managers.Core;
using Nucleus.Managers.NetworkLoop;
using Nucleus.Managers.Rpc;
using Nucleus.Systems;
using Nucleus.Transports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Nucleus.Integrations.KinematicCharacterController.Tests
{
    /// <summary>
    /// Plays the demo over real sockets between two editors: one runs a <c>Server_</c> test and the other the
    /// <c>Client_</c> test of the same scenario, connected over UDP on the loopback, so the client's prediction is checked against a server
    /// in another process exactly as a player's would be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two processes meet through files in the folder named by <c>KCC_TEST_DIRECTORY</c>: the server writes <c>server-ready</c> once
    /// it listens, the client connects only after that, and the client writes <c>client-done</c> with its final pose once its script has
    /// run and the character has settled, which the server compares against its own before both shut down. A test run alone, with no
    /// partner, times out rather than passing.
    /// </para>
    /// <para>
    /// The client's script writes the character's client state from a loop callback on each live tick, ahead of the systems' early fixed
    /// updates, in place of the scene's player, which the tests remove so no device input is read.
    /// </para>
    /// </remarks>
    public class KinematicCharacterLivePlayTests
    {
        /// <summary>
        /// The CoreManager the demo scene binds, set once the scene has loaded.
        /// </summary>
        private CoreManager _coreManager;
        /// <summary>
        /// The scene's transport manager, which starts the role under test.
        /// </summary>
        private UnityTransportManager _unityTransportManager;
        /// <summary>
        /// The folder the two processes meet in.
        /// </summary>
        private string _directory;
        /// <summary>
        /// The UDP port the server listens on.
        /// </summary>
        private ushort _port;
        /// <summary>
        /// The file the server writes once it listens.
        /// </summary>
        private const string ServerReadyFile = "server-ready";
        /// <summary>
        /// The file the client writes once its character has settled, from which the server counts mismatches too.
        /// </summary>
        private const string ClientSettledFile = "client-settled";
        /// <summary>
        /// The file the client writes with its final pose once its script has run.
        /// </summary>
        private const string ClientDoneFile = "client-done";
        /// <summary>
        /// The file the server writes once it has read the client's report.
        /// </summary>
        private const string ServerDoneFile = "server-done";
        /// <summary>
        /// The name the launchers' cube prefab and so its spawned copies start with.
        /// </summary>
        private const string LaunchedCubeName = "NetworkDemoCube";
        /// <summary>
        /// The zone of the teleporter on top of the mesa, where the teleport scenario arrives.
        /// </summary>
        private const byte TeleportDestinationZoneId = 2;
        /// <summary>
        /// The name of the demo scene.
        /// </summary>
        private const string SceneName = "NetworkKccDemo";
        /// <summary>
        /// The port used when <c>KCC_TEST_PORT</c> names none, chosen away from the 7777 every demo listens on.
        /// </summary>
        private const ushort DefaultPort = 47781;
        /// <summary>
        /// The most seconds either side waits for the other before failing.
        /// </summary>
        private const float PartnerTimeoutSeconds = 240f;
        /// <summary>
        /// The live ticks a client's script runs for in every scenario.
        /// </summary>
        private const int ScriptTicks = 330;
        /// <summary>
        /// The live ticks at the start of a script that let a freshly spawned character land before anything is counted.
        /// </summary>
        private const int SettleTicks = 45;
        /// <summary>
        /// The live ticks the correction scenario walks for before standing still, short enough to stay clear of the demo's walls.
        /// </summary>
        private const int CorrectionWalkTicks = 15;
        /// <summary>
        /// The least the correction scenario's walk covers, in metres, before the server's move aside is added.
        /// </summary>
        private const float CorrectionWalkMinimumDistance = 1f;
        /// <summary>
        /// The most reconciles one move by the server may cost the client: those in flight when it lands, never a run of them.
        /// </summary>
        private const uint MaximumCorrectionReconcileCount = 10;
        /// <summary>
        /// The latency the lossy scenario's client simulates, in milliseconds.
        /// </summary>
        private const uint LossyLatencyMilliseconds = 60;
        /// <summary>
        /// The most metres the server's final position may sit from the client's for the two to count as agreeing.
        /// </summary>
        private const float AgreementDistance = 0.02f;

        /// <summary>
        /// Loads the demo and removes its local player, so the tests write the client state themselves.
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory = Environment.GetEnvironmentVariable("KCC_TEST_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "kcc-live-tests");
            _port = ushort.TryParse(Environment.GetEnvironmentVariable("KCC_TEST_PORT"), out ushort port) ? port : DefaultPort;
            Directory.CreateDirectory(_directory);

            yield return SceneManager.LoadSceneAsync(SceneName);

            Object.Destroy(Object.FindAnyObjectByType<DemoPlayer>().gameObject);
            _unityTransportManager = Object.FindAnyObjectByType<UnityTransportManager>();
            _coreManager = NucleusUnity.BoundCoreManager;

            yield return Await(_unityTransportManager.EnsureAddedAsync(), "adding the transports");

            foreach (Transport transport in _coreManager.TransportManager.Transports)
                transport.SetPort(_port);
        }

        /// <summary>
        /// Shuts every transport down.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_unityTransportManager != null)
                _unityTransportManager.ShutdownAll();

            yield return null;
        }

        /// <summary>
        /// The server half of walking, jumping and crouching on flat ground over a clean link.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Server_Walk() => RunServer(Scenario.Walk);

        /// <summary>
        /// The client half of walking, jumping and crouching on flat ground over a clean link: the server's results match the client's
        /// on every tick, so nothing is reconciled.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Client_Walk() => RunClient(Scenario.Walk);

        /// <summary>
        /// The server half of the same moves over a link with latency, jitter and loss.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Server_Lossy() => RunServer(Scenario.Lossy);

        /// <summary>
        /// The client half of the same moves over a link with latency, jitter and loss: whatever the loss costs, the client ends where
        /// the server does.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Client_Lossy() => RunClient(Scenario.Lossy);

        /// <summary>
        /// The server half of a correction: partway through the client's walk the server moves the character aside, which the client
        /// could not have predicted.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Server_Correction() => RunServer(Scenario.Correction);

        /// <summary>
        /// The client half of a correction: the client is reconciled, replays from the server's state and ends where the server does.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Client_Correction() => RunClient(Scenario.Correction);

        /// <summary>
        /// The server half of riding a moving platform: the character spawns on the platform's deck.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Server_Platform() => RunServer(Scenario.Platform);

        /// <summary>
        /// The client half of riding a moving platform: the client predicts the platform with its character, so the ride needs no
        /// reconcile and the character is still on the deck at the end.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Client_Platform() => RunClient(Scenario.Platform);

        /// <summary>
        /// The server half of the teleporter: the character spawns inside the teleporter on the ground.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Server_Teleport() => RunServer(Scenario.Teleport);

        /// <summary>
        /// The client half of the teleporter: the client predicts the teleport to the top of the mesa and stays there.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Client_Teleport() => RunClient(Scenario.Teleport);

        /// <summary>
        /// Runs the server half of a scenario: listens, waits for the client's character, applies the scenario's server side, and once the
        /// client reports done, checks the client's results against its own.
        /// </summary>
        /// <param name="scenario">The scenario.</param>
        private IEnumerator RunServer(Scenario scenario)
        {
            // A previous run's files are never read.
            File.Delete(Path.Combine(_directory, ServerReadyFile));
            File.Delete(Path.Combine(_directory, ClientSettledFile));
            File.Delete(Path.Combine(_directory, ClientDoneFile));
            File.Delete(Path.Combine(_directory, ServerDoneFile));

            PlaceSpawnPoints(scenario);

            yield return Await(_unityTransportManager.StartServerAsync(), "starting the server");

            Assert.IsTrue(_coreManager.TransportManager.IsServerStarted, $"The server did not start on port [{_port}].");
            File.WriteAllText(Path.Combine(_directory, ServerReadyFile), string.Empty);

            NetworkDemoCharacter networkDemoCharacter = null;

            yield return WaitUntil(() => TryFindCharacter(ControllerType.None, out networkDemoCharacter, isClientControlled: true), PartnerTimeoutSeconds, "the client's character to spawn on the server");

            ServerDisplacer serverDisplacer = null;

            if (scenario is Scenario.Correction)
            {
                serverDisplacer = new(networkDemoCharacter.Character.Motor);
                _coreManager.NetworkLoopManager.RegisterNetworkLoopStepCallbacks(serverDisplacer);
            }

            Connection clientConnection = networkDemoCharacter.NetworkSystem.ControllingClient;

            yield return WaitUntil(() => File.Exists(Path.Combine(_directory, ClientSettledFile)), PartnerTimeoutSeconds, "the client's character to settle");

            string clientDonePath = Path.Combine(_directory, ClientDoneFile);

            yield return WaitUntil(() => File.Exists(clientDonePath), PartnerTimeoutSeconds, "the client to finish its script");

            if (serverDisplacer is not null)
                _coreManager.NetworkLoopManager.UnregisterNetworkLoopStepCallbacks(serverDisplacer);

            ClientReport clientReport = ClientReport.Read(clientDonePath);
            KinematicCharacterMotor kinematicCharacterMotor = networkDemoCharacter.Character.Motor;
            uint roundTripTimeMilliseconds = clientConnection.RoundTripTimeMilliseconds;
            float serverClientDistance = Vector3.Distance(kinematicCharacterMotor.TransientPosition, clientReport.Position);
            bool isTeleported = networkDemoCharacter.Component.TeleporterZoneId.Value == TeleportDestinationZoneId;

            File.WriteAllText(Path.Combine(_directory, ServerDoneFile), string.Empty);
            File.WriteAllText(Path.Combine(_directory, "server-report.txt"), $"scenario={scenario} launchCount={ReadLaunchCount()} reconcileSentCount={clientConnection.ReconcileSentCount} clientStateDroppedCount={clientConnection.ClientStateDroppedCount} clientStateLateCount={clientConnection.ClientStateLateCount} roundTripTime={roundTripTimeMilliseconds} serverPosition={kinematicCharacterMotor.TransientPosition:F4} clientPosition={clientReport.Position:F4} distance={serverClientDistance:F4} isTeleported={isTeleported}");
            Debug.Log($"[{nameof(KinematicCharacterLivePlayTests)}] Server [{scenario}]: server at [{kinematicCharacterMotor.TransientPosition:F4}], client at [{clientReport.Position:F4}], [{serverClientDistance:F4}] m apart, teleported [{isTeleported}].");

            // A platform keeps moving, so the two poses there are of different ticks; elsewhere the character has come to rest.
            if (scenario is not Scenario.Platform)
                Assert.LessOrEqual(serverClientDistance, AgreementDistance, "The client did not end where the server did.");

            if (scenario is Scenario.Walk)
                Assert.Greater(ReadLaunchCount(), 0u, "The client's request never fired a launcher.");

            if (scenario is Scenario.Correction)
                Assert.IsTrue(serverDisplacer.IsDisplaced, "The server never moved the character aside.");

            // Proves the simulated link was in force rather than the loopback's own fraction of a millisecond.
            if (scenario is Scenario.Lossy)
                Assert.GreaterOrEqual(roundTripTimeMilliseconds, LossyLatencyMilliseconds, "The simulated latency never reached the round trip.");

            if (scenario is Scenario.Teleport)
                Assert.IsTrue(isTeleported, "The server's character did not arrive at the far teleporter.");
        }

        /// <summary>
        /// Runs the client half of a scenario: connects once the server listens, plays the scenario's script, lets the character settle,
        /// checks its own results and reports its final pose to the server.
        /// </summary>
        /// <param name="scenario">The scenario.</param>
        private IEnumerator RunClient(Scenario scenario)
        {
            if (scenario is Scenario.Lossy)
                _unityTransportManager.SetNetworkConditions(new() { LatencyMilliseconds = LossyLatencyMilliseconds, JitterMilliseconds = 10, PacketLossChance = 0.05f, Seed = 7 });

            yield return WaitUntil(() => File.Exists(Path.Combine(_directory, ServerReadyFile)), PartnerTimeoutSeconds, "the server to listen");
            yield return Await(_unityTransportManager.StartClientAsync("127.0.0.1", _port), "starting the client");

            NetworkDemoCharacter networkDemoCharacter = null;

            yield return WaitUntil(() => TryFindCharacter(ControllerType.Client, out networkDemoCharacter, isClientControlled: true), PartnerTimeoutSeconds, "the client's own character to spawn");

            ValidatedKinematicCharacterMotor validatedKinematicCharacterMotor = networkDemoCharacter.GetComponent<ValidatedKinematicCharacterMotor>();
            KinematicCharacterMotor kinematicCharacterMotor = validatedKinematicCharacterMotor.Motor;
            ClientScript clientScript = new(scenario, networkDemoCharacter.Component, kinematicCharacterMotor.TransientRotation.eulerAngles.y);
            _coreManager.NetworkLoopManager.RegisterNetworkLoopStepCallbacks(clientScript);

            yield return WaitUntil(() => clientScript.LiveTickCount >= SettleTicks, PartnerTimeoutSeconds, "the character to settle");

            Vector3 startPosition = kinematicCharacterMotor.TransientPosition;
            uint settledReconcileCount = ReadReconcileCount(networkDemoCharacter, validatedKinematicCharacterMotor);
            File.WriteAllText(Path.Combine(_directory, ClientSettledFile), string.Empty);
            float highestRise = 0f;

            while (clientScript.LiveTickCount < ScriptTicks)
            {
                highestRise = Mathf.Max(highestRise, Vector3.Dot(kinematicCharacterMotor.TransientPosition - startPosition, kinematicCharacterMotor.CharacterUp));

                yield return null;
            }

            _coreManager.NetworkLoopManager.UnregisterNetworkLoopStepCallbacks(clientScript);

            // Long enough for every correction the script's last ticks earn to arrive and be replayed.
            yield return new WaitForSecondsRealtime(2f);

            uint reconcileCount = ReadReconcileCount(networkDemoCharacter, validatedKinematicCharacterMotor) - settledReconcileCount;
            Vector3 finalPosition = kinematicCharacterMotor.TransientPosition;
            float travelledDistance = Vector3.Distance(startPosition, finalPosition);
            ValidatedPhysicsMover attachedValidatedPhysicsMover = kinematicCharacterMotor.AttachedRigidbody == null ? null : kinematicCharacterMotor.AttachedRigidbody.GetComponent<ValidatedPhysicsMover>();
            bool isTeleported = networkDemoCharacter.Component.TeleporterZoneId.Value == TeleportDestinationZoneId;

            if (scenario is Scenario.Walk)
                yield return Launch();

            new ClientReport { Position = finalPosition }.Write(Path.Combine(_directory, ClientDoneFile));
            File.WriteAllText(Path.Combine(_directory, "client-report.txt"), $"scenario={scenario} reconcileCount={reconcileCount} travelled={travelledDistance:F3} highestRise={highestRise:F3} attachedMover={(attachedValidatedPhysicsMover == null ? "none" : attachedValidatedPhysicsMover.name)} isTeleported={isTeleported} replayedTicks={Object.FindAnyObjectByType<NetworkKinematicCharacterSystem>().ReplayedTickCount}");
            Debug.Log($"[{nameof(KinematicCharacterLivePlayTests)}] Client [{scenario}]: [{reconcileCount}] reconciles after settling, travelled [{travelledDistance:F3}] m, rose [{highestRise:F3}] m, riding [{(attachedValidatedPhysicsMover == null ? "nothing" : attachedValidatedPhysicsMover.name)}], teleported [{isTeleported}].");

            yield return WaitUntil(() => File.Exists(Path.Combine(_directory, ServerDoneFile)), PartnerTimeoutSeconds, "the server to read the client's report");

            switch (scenario)
            {
                case Scenario.Walk:
                    Assert.AreEqual(0u, reconcileCount, "The client was reconciled on a clean link.");
                    Assert.Greater(travelledDistance, 2f, "The character did not walk.");
                    Assert.Greater(highestRise, 0.8f, "The character did not jump.");
                    break;
                case Scenario.Lossy:
                    Assert.Greater(travelledDistance, 2f, "The character did not walk.");
                    break;
                case Scenario.Correction:
                    Assert.Greater(reconcileCount, 0u, "The client was never told about the server's move.");
                    Assert.LessOrEqual(reconcileCount, MaximumCorrectionReconcileCount, "The client kept being reconciled after the server's move, so its replays do not start from the server's state.");
                    Assert.Greater(travelledDistance, CorrectionWalkMinimumDistance + 1f, "The client did not take the server's move aside.");
                    break;
                case Scenario.Platform:
                    Assert.AreEqual(0u, reconcileCount, "The client was reconciled while riding.");
                    Assert.IsNotNull(attachedValidatedPhysicsMover, "The character fell off the platform.");
                    break;
                case Scenario.Teleport:
                    Assert.IsTrue(isTeleported, "The client's character did not arrive at the far teleporter.");
                    break;
            }
        }

        /// <summary>
        /// Asks the server to fire the first launcher over the network, then waits for the fired count and a cube to reach this client.
        /// </summary>
        private static IEnumerator Launch()
        {
            NetworkPrefabLauncher networkPrefabLauncher = Object.FindAnyObjectByType<NetworkPrefabLauncher>();

            Assert.IsTrue(networkPrefabLauncher.NetworkSystem.TrySendRpc(RpcTarget.Server, Channel.Reliable, new LaunchRequestedRpc()), "The client could not ask the launcher to fire.");

            yield return WaitUntil(() => networkPrefabLauncher.Component.LaunchCount.Value > 0 && IsLaunchedCubeSpawned(), PartnerTimeoutSeconds, "the fired cube to reach the client");
        }

        /// <summary>
        /// Finds whether a fired cube has spawned on this peer: the scene's own cubes are named differently from the launched prefab.
        /// </summary>
        /// <returns>True when a launched cube exists.</returns>
        private static bool IsLaunchedCubeSpawned()
        {
            foreach (NetworkRigidbody networkRigidbody in Object.FindObjectsByType<NetworkRigidbody>(FindObjectsInactive.Exclude))
            {
                if (networkRigidbody.name.StartsWith(LaunchedCubeName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Adds up how many times the scene's launchers have fired.
        /// </summary>
        /// <returns>The launch count.</returns>
        private static uint ReadLaunchCount()
        {
            uint launchCount = 0;

            foreach (NetworkPrefabLauncher networkPrefabLauncher in Object.FindObjectsByType<NetworkPrefabLauncher>(FindObjectsInactive.Exclude))
                launchCount += networkPrefabLauncher.Component.LaunchCount.Value;

            return launchCount;
        }

        /// <summary>
        /// Moves the server's spawn points where a scenario needs the character to start.
        /// </summary>
        /// <param name="scenario">The scenario.</param>
        private static void PlaceSpawnPoints(Scenario scenario)
        {
            Transform spawnPoints = GameObject.Find("SpawnPoints").transform;

            switch (scenario)
            {
                case Scenario.Platform:
                {
                    // A platform sliding back and forth along one axis without turning, with the spawn riding on it.
                    Transform platform = GameObject.Find("Slider").transform;
                    Bounds bounds = platform.GetComponent<Collider>().bounds;
                    spawnPoints.SetParent(platform, worldPositionStays: false);
                    spawnPoints.SetPositionAndRotation(new(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z), Quaternion.identity);
                    CollapseSpawnPoints(spawnPoints);

                    break;
                }
                case Scenario.Teleport:
                {
                    // Inside the teleporter on the ground, so the first simulated tick sends the character to the top of the mesa.
                    Transform teleporter = GameObject.Find("Teleporter A").transform;
                    Bounds bounds = teleporter.GetComponentInChildren<Collider>().bounds;
                    spawnPoints.SetPositionAndRotation(new(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z), teleporter.rotation);
                    CollapseSpawnPoints(spawnPoints);

                    break;
                }
            }
        }

        /// <summary>
        /// Puts every spawn point at the parent's own position.
        /// </summary>
        /// <param name="spawnPoints">The parent of the spawn points.</param>
        private static void CollapseSpawnPoints(Transform spawnPoints)
        {
            foreach (Transform spawnPoint in spawnPoints)
                spawnPoint.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Finds a spawned player character by who controls it on this peer.
        /// </summary>
        /// <param name="controllerType">The control this peer must have of it, or <see cref="ControllerType.None"/> to ignore it.</param>
        /// <param name="networkDemoCharacter">The character found, or null.</param>
        /// <param name="isClientControlled">True to require that a client controls it.</param>
        /// <returns>True when one was found.</returns>
        private static bool TryFindCharacter(ControllerType controllerType, out NetworkDemoCharacter networkDemoCharacter, bool isClientControlled)
        {
            foreach (NetworkDemoCharacter candidate in Object.FindObjectsByType<NetworkDemoCharacter>(FindObjectsInactive.Exclude))
            {
                if (candidate.NetworkSystem is null || (controllerType is not ControllerType.None && !candidate.IsController(controllerType)))
                    continue;

                if (isClientControlled && candidate.NetworkSystem.ControllerConnectionId is NetworkSystem.UnsetId)
                    continue;

                networkDemoCharacter = candidate;

                return true;
            }

            networkDemoCharacter = null;

            return false;
        }

        /// <summary>
        /// Reads how many reconciles the character's systems have been through, the most of either.
        /// </summary>
        /// <param name="networkDemoCharacter">The character.</param>
        /// <param name="validatedKinematicCharacterMotor">The character's motor behaviour.</param>
        /// <returns>The reconcile count.</returns>
        private static uint ReadReconcileCount(NetworkDemoCharacter networkDemoCharacter, ValidatedKinematicCharacterMotor validatedKinematicCharacterMotor) => Math.Max(networkDemoCharacter.NetworkSystem.ReconcileCount, validatedKinematicCharacterMotor.NetworkSystem.ReconcileCount);

        /// <summary>
        /// Waits for a task, failing the test when it faults or runs out of time.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="description">What the task does, for the failure message.</param>
        private static IEnumerator Await(Task task, string description)
        {
            float deadline = Time.realtimeSinceStartup + PartnerTimeoutSeconds;

            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timed out {description}.");

                yield return null;
            }

            if (task.IsFaulted)
                Assert.Fail($"Failed {description}: {task.Exception}");
        }

        /// <summary>
        /// Waits until a condition holds, failing the test when it runs out of time.
        /// </summary>
        /// <param name="condition">The condition.</param>
        /// <param name="timeoutSeconds">The most seconds to wait.</param>
        /// <param name="description">What is waited for, for the failure message.</param>
        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string description)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timed out waiting for {description}.");

                yield return null;
            }
        }

        /// <summary>
        /// What the two processes play.
        /// </summary>
        private enum Scenario : byte
        {
            /// <summary>
            /// Walking, jumping and crouching on flat ground over a clean link.
            /// </summary>
            Walk,
            /// <summary>
            /// The same over a link with latency, jitter and loss.
            /// </summary>
            Lossy,
            /// <summary>
            /// The same, with the server moving the character aside partway through.
            /// </summary>
            Correction,
            /// <summary>
            /// Riding a moving platform.
            /// </summary>
            Platform,
            /// <summary>
            /// Teleporting from the ground to the top of the mesa.
            /// </summary>
            Teleport
        }

        /// <summary>
        /// Writes a scripted client state on each live tick, ahead of the systems' early fixed updates.
        /// </summary>
        private sealed class ClientScript : INetworkLoopStepCallback
        {
            /// <summary>
            /// How many live ticks the script has written.
            /// </summary>
            public int LiveTickCount { get; private set; }
            /// <summary>
            /// The scenario played.
            /// </summary>
            private readonly Scenario _scenario;
            /// <summary>
            /// The component of the character the client controls.
            /// </summary>
            private readonly NetworkDemoCharacterComponent _networkDemoCharacterComponent;
            /// <summary>
            /// The camera heading the script moves relative to, in degrees: the way the character faced when it spawned.
            /// </summary>
            private readonly float _cameraYaw;

            /// <summary>
            /// Creates a script.
            /// </summary>
            /// <param name="scenario">The scenario played.</param>
            /// <param name="networkDemoCharacterComponent">The component of the character the client controls.</param>
            /// <param name="cameraYaw">The camera heading the script moves relative to, in degrees.</param>
            public ClientScript(Scenario scenario, NetworkDemoCharacterComponent networkDemoCharacterComponent, float cameraYaw)
            {
                _scenario = scenario;
                _networkDemoCharacterComponent = networkDemoCharacterComponent;
                _cameraYaw = cameraYaw;
            }

            /// <inheritdoc/>
            public NetworkLoopSteps GetNetworkLoopSteps() => NetworkLoopSteps.EarlyFixedUpdate;

            /// <inheritdoc/>
            public void OnNetworkLoopStep(NetworkLoopSteps networkLoopStep, StepDelta stepDelta)
            {
                int tick = LiveTickCount++;
                int lastMovingTick = _scenario is Scenario.Correction ? SettleTicks + CorrectionWalkTicks : ScriptTicks - 90;
                bool isMoving = _scenario is not Scenario.Platform and not Scenario.Teleport && tick >= SettleTicks && tick < lastMovingTick;
                float moveAxisForward = 0f;
                float moveAxisRight = 0f;

                if (isMoving)
                {
                    int scriptTick = tick - SettleTicks;
                    moveAxisForward = scriptTick < 90 ? 1f : scriptTick < 130 ? 0f : -1f;
                    moveAxisRight = scriptTick is >= 90 and < 130 ? 1f : 0f;
                }

                _networkDemoCharacterComponent.MoveAxisForward.Value = moveAxisForward;
                _networkDemoCharacterComponent.MoveAxisRight.Value = moveAxisRight;
                _networkDemoCharacterComponent.CameraYaw.Value = _cameraYaw;
                _networkDemoCharacterComponent.IsJumpPressed.Value = isMoving && _scenario is not Scenario.Correction && (tick == SettleTicks + 20 || tick == SettleTicks + 60);
                _networkDemoCharacterComponent.IsCrouchHeld.Value = isMoving && tick >= SettleTicks + 90 && tick < SettleTicks + 130;
            }
        }

        /// <summary>
        /// Moves the server's copy of a character aside once, partway through the client's walk, on the server's own tick.
        /// </summary>
        private sealed class ServerDisplacer : INetworkLoopStepCallback
        {
            /// <summary>
            /// True once the character has been moved aside.
            /// </summary>
            public bool IsDisplaced { get; private set; }
            /// <summary>
            /// The server's copy of the character's motor.
            /// </summary>
            private readonly KinematicCharacterMotor _kinematicCharacterMotor;
            /// <summary>
            /// The live ticks seen since this was registered.
            /// </summary>
            private int _tickCount;
            /// <summary>
            /// The ticks after registering at which the character is moved aside, inside the client's walk.
            /// </summary>
            private const int DisplaceTick = 120;

            /// <summary>
            /// Creates a displacer.
            /// </summary>
            /// <param name="kinematicCharacterMotor">The server's copy of the character's motor.</param>
            public ServerDisplacer(KinematicCharacterMotor kinematicCharacterMotor) => _kinematicCharacterMotor = kinematicCharacterMotor;

            /// <inheritdoc/>
            public NetworkLoopSteps GetNetworkLoopSteps() => NetworkLoopSteps.EarlyFixedUpdate;

            /// <inheritdoc/>
            public void OnNetworkLoopStep(NetworkLoopSteps networkLoopStep, StepDelta stepDelta)
            {
                if (++_tickCount != DisplaceTick)
                    return;

                KinematicCharacterMotor kinematicCharacterMotor = _kinematicCharacterMotor;
                kinematicCharacterMotor.SetPositionAndRotation(kinematicCharacterMotor.TransientPosition + (kinematicCharacterMotor.CharacterRight * 1.5f), kinematicCharacterMotor.TransientRotation);
                IsDisplaced = true;
            }
        }

        /// <summary>
        /// What the client reports to the server once its script has run.
        /// </summary>
        private struct ClientReport
        {
            /// <summary>
            /// The client's final position for its character.
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Writes the report for the server to read.
            /// </summary>
            /// <param name="path">The report's file.</param>
            public readonly void Write(string path)
            {
                string temporaryPath = $"{path}.tmp";
                File.WriteAllText(temporaryPath, FormattableString.Invariant($"{Position.x:R},{Position.y:R},{Position.z:R}"));
                File.Move(temporaryPath, path);
            }

            /// <summary>
            /// Reads a report the client wrote.
            /// </summary>
            /// <param name="path">The report's file.</param>
            /// <returns>The report.</returns>
            public static ClientReport Read(string path)
            {
                string[] parts = File.ReadAllText(path).Split(',');

                return new() { Position = new(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture)) };
            }
        }
    }
}
