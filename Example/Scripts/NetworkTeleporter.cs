using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// A zone that sends a character walking into it to another teleporter, as KCC's <c>Teleporter</c> does, but decided inside the
    /// character's tick by <see cref="NetworkExampleCharacter"/>, so the teleport is predicted and replayed with the rest of the move.
    /// </summary>
    /// <remarks>
    /// KCC's teleporter reacts to <c>OnTriggerEnter</c>, which the physics step raises once per contact it sees begin. A replay restores
    /// the character to an earlier tick behind the physics engine's back, so a trigger would be entered on a different tick than the server
    /// entered it, or not at all. The character instead tests its own capsule against <see cref="Collider"/> every tick and records the
    /// zone it stands in, which serves as the arrival guard KCC kept on the teleporter itself.
    /// </remarks>
    public class NetworkTeleporter : MonoBehaviour
    {
        /// <summary>
        /// The teleporter a character entering this one arrives at.
        /// </summary>
        public NetworkTeleporter Destination => _destination;
        /// <summary>
        /// The zone this teleporter covers, unique in the scene and never <see cref="UnsetZoneId"/>.
        /// </summary>
        public byte ZoneId => _zoneId;
        /// <summary>
        /// True when this teleporter stands on the planet, so a character arriving here falls towards the planet's centre.
        /// </summary>
        public bool IsOnPlanet => _isOnPlanet;
        /// <summary>
        /// The collider covering the zone, on this object or a child, or null for a teleporter that is only ever arrived at.
        /// </summary>
        public Collider Collider { get; private set; }
        /// <summary>
        /// The zone Id of no teleporter.
        /// </summary>
        public const byte UnsetZoneId = 0;
        /// <summary>
        /// The teleporter a character entering this one arrives at.
        /// </summary>
        [SerializeField]
        [Tooltip("The teleporter a character entering this one arrives at.")]
        private NetworkTeleporter _destination;
        /// <summary>
        /// The zone this teleporter covers, unique in the scene and never <see cref="UnsetZoneId"/>.
        /// </summary>
        [SerializeField]
        [Tooltip("A number unique to this teleporter in the scene, from 1 to 255.")]
        private byte _zoneId = 1;
        /// <summary>
        /// True when this teleporter stands on the planet, so a character arriving here falls towards the planet's centre.
        /// </summary>
        [SerializeField]
        [Tooltip("Whether this teleporter stands on the planet, so a character arriving here falls towards the planet's centre.")]
        private bool _isOnPlanet;

        /// <summary>
        /// Resolves the zone's collider, which KCC's teleporters keep on a child.
        /// </summary>
        private void Awake() => Collider = GetComponentInChildren<Collider>();
    }
}
