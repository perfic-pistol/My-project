using cowsins;
using UnityEngine;

public class WallRunBehaviour
{
    private MovementContext context;
    private Rigidbody rb;
    private InputManager inputManager;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private IPlayerControlProvider playerControl;
    private IPlayerMultipliers playerMultipliers;

    public bool wallLeft => context.WallLeft;
    public bool wallRight;
    private Vector3 wallNormal;

    private RaycastHit wallLeftHit, wallRightHit;
    private Vector3 wallDirection;
    private float wallRunTimer = 0;

    private PlayerOrientation orientation => playerMovement.Orientation;

    private PlayerMovementSettings playerSettings;

    public WallRunBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerControl = context.Dependencies.PlayerControl;
        this.playerMultipliers = context.Dependencies.PlayerMultipliers;

        this.playerSettings = context.Settings;

        playerEvents.Events.OnWallJump.AddListener(WallRunJump);
        playerEvents.Events.CanWallBounce += CheckHeight;
    }

    public void Dispose()
    {
        playerEvents.Events.OnWallJump.RemoveListener(WallRunJump);
        playerEvents.Events.CanWallBounce -= CheckHeight;
    }
    public void Enter()
    {
        playerMovement.IsWallRunning = true;
        wallRunTimer = playerSettings.wallRunDuration;
        
        playerEvents.Events.OnWallRunStart?.Invoke();
        playerSettings.events.OnStartWallRun.Invoke();

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(wallDirection, ForceMode.Impulse);
    }

    public void FixedTick()
    {
        if (!CanExecute()) return;

        CheckWalls();

        float hVelMag = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        if (inputManager.Y > 0 && (wallLeft || wallRight) && !(wallLeft && wallRight) && CheckHeight() && 
            playerMovement.CurrentSpeed * playerMultipliers.WeightMultiplier > playerMovement.WalkSpeed && !playerMovement.Grounded && hVelMag > .1f) WallRun();
        else Exit();
    }

    public void Exit() 
    {
        if (playerMovement.IsWallRunning)
        {
            playerEvents.Events.OnWallRunStop?.Invoke();
            playerSettings.events.OnStopWallRun.Invoke();

            rb.AddForce(wallNormal * playerSettings.stopWallRunningImpulse, ForceMode.Impulse);
        }
        playerMovement.IsWallRunning = false;
        if (!playerMovement.IsClimbing && !context.IsPlayerOnSlope)
            rb.useGravity = true;
    }

    public bool CanExecute()
    {
        return playerControl.IsControllable && playerSettings.canWallRun;
    }
    private void WallRun()
    {
        wallNormal = wallRight ? wallRightHit.normal : wallLeftHit.normal;
        wallDirection = Vector3.Cross(wallNormal, context.Transform.up).normalized * 10;
        // Fixing wallrunning directions depending on the orientation 
        if ((orientation.Forward - wallDirection).magnitude > (orientation.Forward + wallDirection).magnitude) wallDirection = -wallDirection;

        // Handling WallRun Cancel
        if (OppositeVectors() < -.5f) Exit();

        if (playerSettings.cancelWallRunMethod == PlayerMovementSettings.CancelWallRunMethod.Timer)
        {
            wallRunTimer -= Time.deltaTime;
            if (wallRunTimer <= 0)
            {
                rb.AddForce(wallNormal * playerSettings.stopWallRunningImpulse, ForceMode.Impulse);
                Exit();
            }
        }

        // Start Wallrunning
        if (!playerMovement.IsWallRunning) Enter();

        rb.useGravity = playerSettings.useGravity;

        if (rb.linearVelocity.y < 0)
        {
            if (playerSettings.useGravity)
                rb.AddForce(context.Transform.up * playerSettings.wallrunGravityCounterForce, ForceMode.Force);
            else rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        }
        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, playerSettings.maxWallRunSpeed);

        if (!(wallRight && inputManager.X < 0) && !(wallLeft && inputManager.X > 0)) rb.AddForce(-wallNormal * 100, ForceMode.Force);

        rb.AddForce(wallDirection, ForceMode.Force);
    }
    private void WallRunJump()
    {
        // When we wallrun, we want to add extra side forces
        rb.AddForce(context.Transform.up * playerSettings.upwardsWallJumpForce);
        rb.AddForce(wallNormal * playerSettings.normalWallJumpForce, ForceMode.Impulse);
    }

    private void CheckWalls()
    {
        context.WallLeft = Physics.Raycast(context.Transform.position, -orientation.Right, out wallLeftHit, .8f, playerSettings.whatIsWallRunWall);
        wallRight = Physics.Raycast(context.Transform.position, orientation.Right, out wallRightHit, .8f, playerSettings.whatIsWallRunWall);
    }
    private bool CheckHeight() { return !Physics.Raycast(context.Transform.position, Vector3.down, playerSettings.wallMinimumHeight, playerSettings.whatIsWallRunWall); }

    private float OppositeVectors() { return Vector3.Dot(wallDirection, orientation.Forward); }
}
