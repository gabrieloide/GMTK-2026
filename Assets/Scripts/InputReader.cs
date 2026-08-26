using Code.Scripts.Core;
using UnityEngine;

namespace Game
{
    public class InputReader : BaseSingleton<InputReader>
    {
        public InputSystem_Actions Actions { get; private set; }

        public Vector2 MoveInput => Actions == null ? Vector2.zero : Actions.Player.Move.ReadValue<Vector2>();

        public bool HandbrakeHeld => Actions != null && Actions.Player.Handbrake.IsPressed();

        public bool AcceleratePressed => Actions != null && Actions.Player.Accelerate.IsPressed();

        public bool NextTrackPressed => Actions != null && Actions.Player.Next.WasPressedThisFrame();

        public bool PreviousTrackPressed => Actions != null && Actions.Player.Previous.WasPressedThisFrame();

        protected override void Awake()
        {
            base.Awake();
            Actions ??= new InputSystem_Actions();
        }

        private void OnEnable()
        {
            Actions ??= new InputSystem_Actions();
            Actions.Enable();
        }

        private void OnDisable()
        {
            Actions?.Disable();
        }

        protected override void OnDestroy()
        {
            Actions?.Dispose();
            Actions = null;
            base.OnDestroy();
        }
    }
}
