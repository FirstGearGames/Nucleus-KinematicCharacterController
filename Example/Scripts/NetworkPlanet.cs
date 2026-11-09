using KinematicCharacterController;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// The playground's planet: turns about its orbit axis as KCC's <c>PlanetManager</c> turns it, and tells a character on it which way is
    /// down.
    /// </summary>
    /// <remarks>
    /// KCC's planet remembers its last rotation and sets the gravity of each character it has been told about. Both are state a replay
    /// cannot rewind, so here the turn starts from the mover's own rotation, which <see cref="ValidatedPhysicsMover"/> restores, and each
    /// character works out its own gravity from <see cref="Center"/> and records whether it is on the planet in its validated state.
    /// </remarks>
    [RequireComponent(typeof(ValidatedPhysicsMover))]
    public class NetworkPlanet : MonoBehaviour, IMoverController
    {
        /// <summary>
        /// The planet's centre, which a character on it falls towards.
        /// </summary>
        public Vector3 Center => _physicsMover.TransientPosition;
        /// <summary>
        /// The strength a character on the planet falls towards its centre with.
        /// </summary>
        public float GravityStrength => _gravityStrength;
        /// <summary>
        /// The strength a character on the planet falls towards its centre with.
        /// </summary>
        [SerializeField]
        [Tooltip("The strength a character on the planet falls towards its centre with.")]
        private float _gravityStrength = 10f;
        /// <summary>
        /// The axis the planet turns about.
        /// </summary>
        [SerializeField]
        [Tooltip("The axis the planet turns about.")]
        private Vector3 _orbitAxis = Vector3.forward;
        /// <summary>
        /// How fast the planet turns, in degrees per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the planet turns, in degrees per second.")]
        private float _orbitSpeed = 10f;
        /// <summary>
        /// The mover that carries the planet.
        /// </summary>
        private PhysicsMover _physicsMover;

        /// <inheritdoc/>
        /// <remarks>Turns from the rotation the mover's last tick ended at, which a restore puts back.</remarks>
        public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
        {
            goalPosition = _physicsMover.InitialSimulationPosition;
            goalRotation = Quaternion.Euler(_orbitAxis * _orbitSpeed * deltaTime) * _physicsMover.InitialSimulationRotation;
        }

        /// <summary>
        /// Becomes the mover's controller.
        /// </summary>
        private void Awake()
        {
            _physicsMover = GetComponent<PhysicsMover>();
            _physicsMover.MoverController = this;
        }
    }
}
