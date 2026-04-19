using cowsins;
using UnityEngine;
using System.Collections;

public class GrapplingHookBehaviour
{
    private MovementContext context;
    private Rigidbody rb;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerControlProvider playerControl;
    private IPlayerMovementEventsProvider playerEvents;
    private IWeaponReferenceProvider weaponReference;

    private PlayerOrientation orientation => playerMovement.Orientation;

    public bool grappleEnabled = true;
    public SpringJoint joint;
    public Vector3 grapplePoint;
    public LineRenderer grappleRenderer;

    private LayerMask whatIsGround;

    private PlayerMovementSettings playerSettings; 
    private Transform playerCam;

    private float elapsedTime;

    private bool grappleImpacted = false;
    private float currentWaveAmplitude = 0;

    private Coroutine cooldownCoroutine;
    private MonoBehaviour CoroutineRunner;

    private bool isGrappling;

    public GrapplingHookBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerControl = context.Dependencies.PlayerControl;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.weaponReference = context.Dependencies.WeaponReference;

        this.playerSettings = context.Settings;
        this.grappleRenderer = context.Transform.GetComponent<LineRenderer>();
        this.whatIsGround = context.WhatIsGround;
        this.CoroutineRunner = context.Transform.GetComponent<MonoBehaviour>();    
        this.playerCam = context.Camera;

        if (!playerSettings.allowGrapple) Object.Destroy(grappleRenderer);
    }
    public void Enter()
    {
        if (!grappleEnabled || isGrappling || !playerControl.IsControllable || weaponReference.MainCamera == null) return;
        RaycastHit hit;

        if (Physics.Raycast(weaponReference.MainCamera.transform.position, weaponReference.MainCamera.transform.forward, out hit, playerSettings.maxGrappleDistance, whatIsGround))
        {
            isGrappling = true;
            grapplePoint = hit.point;

            joint = context.Transform.gameObject.AddComponent<SpringJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = grapplePoint;

            float distanceFromAnchor = Vector3.Distance(context.Transform.position, grapplePoint);

            joint.maxDistance = playerSettings.grappleRopeLength;
            joint.minDistance = playerSettings.grappleRopeLength * 0.25f;

            joint.spring = playerSettings.grappleSpringForce;
            joint.damper = playerSettings.grappleDamper;
            joint.massScale = 4.5f;

            playerSettings.events.OnStartGrapple?.Invoke();
            if (playerSettings.sounds.startGrappleSFX)
                SoundManager.Instance.PlaySound(playerSettings.sounds.startGrappleSFX, 0, 0, false);

            playerEvents.Events.OnGrappleStart?.Invoke();
        }
    }

    public void Tick() 
    {
        UpdateGrappleRenderer();

        if (!isGrappling) return;

        Vector3 toGrapple = grapplePoint - context.Transform.position;
        float distance = toGrapple.magnitude;

        if (distance < playerSettings.distanceToBreakGrapple)
        {
            Exit();
            return;
        }

        Vector3 normalizedDirection = toGrapple.normalized;
        Vector3 force = normalizedDirection * playerSettings.grappleForce;

        if (playerSettings.grapplingHookMethod == PlayerMovementSettings.GrapplingHookMethod.Linear)
        {
            // Scale force based on distance
            float distanceMultiplier = Mathf.Clamp(distance, 1f, 10f);
            force *= distanceMultiplier;

            // Reset vertical velocity to avoid bouncing
            Vector3 currentVelocity = rb.linearVelocity;
            currentVelocity.y = 0;
            rb.linearVelocity = currentVelocity;

            rb.AddForce(force, ForceMode.Force);
        }
        else if (playerSettings.grapplingHookMethod == PlayerMovementSettings.GrapplingHookMethod.Combined)
        {
            Vector3 cameraDir = playerCam.forward;

            Vector3 flatCameraDir = new Vector3(cameraDir.x, 0f, cameraDir.z).normalized;
            float alignment = Vector3.Dot(normalizedDirection, cameraDir);
            alignment = Mathf.Clamp01(alignment);
            force += flatCameraDir * playerSettings.cameraInfluence * alignment;

            Vector3 velocity = rb.linearVelocity;

            float verticalDamping = 0.1f;
            velocity.y *= (1f - verticalDamping);

            rb.linearVelocity = velocity;

            rb.AddForce(force, ForceMode.Acceleration);
        }
    }

    public void Exit() 
    {
        if (!isGrappling) return;
        isGrappling = false;
        grappleEnabled = false;


        if (cooldownCoroutine != null)
        {
            CoroutineRunner.StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = null;
        }

        cooldownCoroutine = CoroutineRunner.StartCoroutine(GrappleCooldownRoutine());

        grappleRenderer.positionCount = 0; // Reset the quality/resolution of the rope
        UnityEngine.Object.Destroy(joint);

        playerSettings.events.OnStopGrapple?.Invoke();// Perform custom methods
    }

    public void UpdateGrappleRenderer()
    {
        if (isGrappling)
        {
            playerSettings.events.OnGrappling?.Invoke(); // Perform custom methods
            int ropeResolution = playerSettings.ropeResolution;

            if (grappleRenderer.positionCount == 0)
            {
                // Initial setup when starting to grapple
                grappleRenderer.positionCount = ropeResolution;
                for (int i = 0; i < ropeResolution; i++)
                {
                    grappleRenderer.SetPosition(i, context.Transform.position);
                    currentWaveAmplitude = playerSettings.waveAmplitude;
                }
                elapsedTime = 0f;
                grappleImpacted = false;
            }

            // Update the line renderer progressively
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= playerSettings.drawDuration && !grappleImpacted)
            {
                if (playerSettings.sounds.grappleImpactSFX)
                    SoundManager.Instance.PlaySound(playerSettings.sounds.grappleImpactSFX, 0, 0, false);
                playerSettings.events.OnGrappleImpacted?.Invoke();
                grappleImpacted = true;
            }
            float t = Mathf.Clamp01(elapsedTime / playerSettings.drawDuration);

            currentWaveAmplitude -= Time.deltaTime * playerSettings.waveAmplitudeMitigation;
            currentWaveAmplitude = Mathf.Clamp(currentWaveAmplitude, 0, 1.4f);
            Vector3 startPos = weaponReference.Id != null && weaponReference.Weapon != null && weaponReference.Weapon.grapplesFromTip ?
                weaponReference.Id.FirePoint[0].position : context.Transform.position;
            Vector3 endPos = Vector3.Lerp(context.Transform.position, grapplePoint, t);
            Vector3[] points = new Vector3[ropeResolution];
            points[0] = startPos;
            points[ropeResolution - 1] = endPos;

            // Apply wave effect to the rope
            for (int i = 1; i < ropeResolution - 1; i++)
            {

                float waveOffset = (float)i / (ropeResolution - 1);
                // Using sine function for oscillation
                float yOffset = Mathf.Sin(Time.time * 60 + waveOffset * Mathf.PI * 2f) * currentWaveAmplitude;
                Vector3 pointPos = Vector3.Lerp(startPos, endPos, waveOffset);
                pointPos.y += yOffset;
                points[i] = pointPos;
            }

            grappleRenderer.SetPositions(points);
        }
        else
        {
            // Stop grappling
            grappleRenderer.positionCount = 0;
        }
    }

    private void EnableGrapple()
    {
        grappleEnabled = true;
        playerSettings.events.OnGrappleEnabled?.Invoke();
    }
    private IEnumerator GrappleCooldownRoutine()
    {
        yield return new WaitForSeconds(playerSettings.grappleCooldown);
        EnableGrapple();
    }
}
