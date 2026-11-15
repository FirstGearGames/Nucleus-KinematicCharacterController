using Nucleus.Integrations.Unity;
using UnityEngine;

namespace Nucleus.Integrations.KinematicCharacterController.Demo
{
    /// <summary>
    /// A zone that sends a character walking into it to another teleporter, decided inside the character's tick by
    /// <see cref="NetworkDemoCharacter"/>, so the teleport is predicted and replayed with the rest of the move.
    /// </summary>
    /// <remarks>
    /// A trigger is no use here: <c>OnTriggerEnter</c> is raised by the physics step once per contact it sees begin, and a replay restores
    /// the character to an earlier tick behind the physics engine's back, so a trigger would be entered on a different tick than the server
    /// entered it, or not at all. The character instead tests its own capsule against <see cref="Collider"/> every tick and records the
    /// zone it stands in, so a character arriving inside the destination is not sent straight back.
    /// </remarks>
    [NucleusHelpUrl(typeof(NetworkTeleporter))]
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
        /// Resolves the zone's collider, on this object or a child.
        /// </summary>
        private void Awake() => Collider = GetComponentInChildren<Collider>();
    }
}
