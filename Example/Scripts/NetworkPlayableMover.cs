using KinematicCharacterController;
using UnityEngine;
using UnityEngine.Playables;

namespace Nucleus.Integrations.KinematicCharacterController.Example
{
    /// <summary>
    /// A mover that follows a timeline, as KCC's <c>PlayableMover</c> does, evaluated at the mover's
    /// <see cref="ValidatedPhysicsMover.SimulationTime"/> instead of Unity's time, so every peer and every replay puts it in the same place
    /// on the same tick.
    /// </summary>
    /// <remarks>
    /// The timeline animates the transform, so each evaluation reads the pose it produces and puts the transform back, leaving the move
    /// itself to the mover, exactly as KCC's version does.
    /// </remarks>
    [RequireComponent(typeof(ValidatedPhysicsMover))]
    public class NetworkPlayableMover : MonoBehaviour, IMoverController
    {
        /// <summary>
        /// How fast the timeline plays, as a multiple of its authored speed.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the timeline plays, as a multiple of its authored speed.")]
        private float _speed = 1f;
        /// <summary>
        /// The timeline that animates the mover.
        /// </summary>
        [SerializeField]
        [Tooltip("The timeline that animates the mover.")]
        private PlayableDirector _playableDirector;
        /// <summary>
        /// The mover's clock.
        /// </summary>
        private ValidatedPhysicsMover _validatedPhysicsMover;

        /// <inheritdoc/>
        public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
        {
            Transform moverTransform = transform;
            moverTransform.GetPositionAndRotation(out Vector3 positionBeforeAnimation, out Quaternion rotationBeforeAnimation);

            _playableDirector.time = (_validatedPhysicsMover.SimulationTime * _speed) % _playableDirector.duration;
            _playableDirector.Evaluate();

            moverTransform.GetPositionAndRotation(out goalPosition, out goalRotation);
            moverTransform.SetPositionAndRotation(positionBeforeAnimation, rotationBeforeAnimation);
        }

        /// <summary>
        /// Takes the timeline off Unity's clock and becomes the mover's controller.
        /// </summary>
        private void Awake()
        {
            _validatedPhysicsMover = GetComponent<ValidatedPhysicsMover>();
            _playableDirector.timeUpdateMode = DirectorUpdateMode.Manual;
            GetComponent<PhysicsMover>().MoverController = this;
        }
    }
}
