using UnityEngine;
namespace cowsins
{
    public class PlayerCrouchState : PlayerBaseState
    {
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs

        public PlayerCrouchState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            statsProvider = _ctx.PlayerStatsProvider;
            playerControlProvider = _ctx.PlayerControlProvider;
        }

        public sealed override void EnterState()
        {
            playerMovement.crouchSlideBehaviour?.Enter();
            statsProvider.AddOnDieListener(SwitchToDie);
        }

        public sealed override void UpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.crouchSlideBehaviour?.HandleCrouch();
            playerMovement.cameraLookBehaviour?.Tick();

            CheckSwitchState();
        }
        public sealed override void FixedUpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.basicMovementBehaviour?.Movement();
            playerMovement.crouchSlideBehaviour?.FixedTick();
            playerMovement.footstepsBehaviour?.FootSteps();
            playerMovement.speedLinesBehaviour?.Tick();
        }

        public sealed override void ExitState() 
        { 
            playerMovement.crouchSlideBehaviour?.Exit(); // Invoke your own method on the moment you are standing up NOT WHILE YOU ARE NOT CROUCHING
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        public sealed override void CheckSwitchState()
        {
            if (playerMovement.jumpBehaviour.CanExecute() && inputManager.Jumping && playerMovement.playerSettings.canJumpWhileCrouching)
                SwitchState(_factory.Jump());

            if (inputManager.Dashing && playerMovement.dashBehaviour.CanExecute()) SwitchState(_factory.Dash());

            // If the player is airborne & no longer sliding fall through to the jump state so the state machine stays coherent
            if (!playerMovement.Grounded && !playerMovement.IsSliding)
            {
                SwitchState(_factory.Jump());
                return;
            }

            if(playerMovement.crouchSlideBehaviour.CheckUnCrouch()) SwitchState(_factory.Default());
        }
    }
}