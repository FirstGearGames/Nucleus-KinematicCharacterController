using KinematicCharacterController.Examples;
using Nucleus.Integrations.Unity.Systems;
using Nucleus.Managers.NetworkLoop;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// Walks a character back and forth on the server, as KCC's <c>ExampleAIController</c> walks its characters, timed by the server's tick
    /// so every AI character keeps step with the others.
    /// </summary>
    /// <remarks>
    /// Adopts the system the character's <see cref="ValidatedKinematicCharacterMotor"/> declares. Nobody controls the character, so only
    /// the server simulates it, and every client draws it from the server's state.
    /// </remarks>
    [RequireComponent(typeof(NetworkExampleCharacterController))]
    [RequireComponent(typeof(ValidatedKinematicCharacterMotor))]
    public class NetworkExampleAICharacter : NucleusBehaviour
    {
        /// <summary>
        /// How fast the character walks its back-and-forth cycle, in radians per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the character walks its back-and-forth cycle, in radians per second.")]
        private float _movementPeriod = 1f;
        /// <summary>
        /// The controller driven.
        /// </summary>
        private NetworkExampleCharacterController _networkExampleCharacterController;

        /// <inheritdoc/>
        protected override void Awake()
        {
            _networkExampleCharacterController = GetComponent<NetworkExampleCharacterController>();

            base.Awake();
        }

        /// <inheritdoc/>
        /// <remarks>Hands the controller this tick's walk, worked out from the tick so the server's characters move in step.</remarks>
        protected override void OnEarlyFixedUpdate(StepDelta stepDelta, SimulationContext simulationContext)
        {
            if (simulationContext.Type is SimulationType.None)
                return;

            float simulationTime = stepDelta.Tick / (float)CoreManager.NetworkLoopManager.TickRate;
            Vector3 moveVector = Mathf.Sin(simulationTime * _movementPeriod) * Vector3.forward;
            AICharacterInputs aiCharacterInputs = new()
            {
                MoveVector = moveVector,
                LookVector = Vector3.Slerp(-Vector3.forward, Vector3.forward, moveVector.z).normalized
            };

            _networkExampleCharacterController.SetInputs(in aiCharacterInputs);
        }
    }
}
