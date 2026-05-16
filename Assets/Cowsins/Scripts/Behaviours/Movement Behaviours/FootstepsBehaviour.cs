using cowsins;
using UnityEngine;
using UnityEngine.Rendering;

public class FootstepsBehaviour
{
    private MovementContext movementContext;
    private Rigidbody rb => movementContext.Rigidbody;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private PlayerOrientation orientation => playerMovement?.Orientation;

    private float stepTimer;
    private PlayerMovementSettings playerSettings;
    private AudioSource audioSource;

    public FootstepsBehaviour(MovementContext context)
    {
        playerMovement = context.Dependencies.PlayerMovementState;
        playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerSettings = context.Settings;
        
        // Ensure footsteps have their own dedicated AudioSource to prevent interference with weapons
        var sources = context.Transform.GetComponents<AudioSource>();
        AudioSource mainSource = context.Transform.GetComponent<AudioSource>();

        if (sources.Length > 1) this.audioSource = sources[1]; 
        else 
        {
            this.audioSource = context.Transform.gameObject.AddComponent<AudioSource>();
            // Copy basic settings from the main source so it sounds consistent
            if (mainSource != null)
            {
                this.audioSource.spatialBlend = mainSource.spatialBlend;
                this.audioSource.outputAudioMixerGroup = mainSource.outputAudioMixerGroup;
            }
        }

        movementContext = context;
        playerEvents.Events.OnWallRunStart.AddListener(ResetFootsteps);

        // Cache layer indices for better performance
        CacheLayerIndices();
    }

    public void Dispose()
    {
        playerEvents.Events.OnWallRunStart.RemoveListener(ResetFootsteps);
    }

    private void CacheLayerIndices()
    {
        foreach (var entry in playerSettings.footstepSounds.surfaceSounds)
        {
            if (entry.cachedLayerIndex == -1)
                entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);
        }
    }

    public bool CanExecute()
    {
        if (!playerMovement.Grounded && !playerMovement.IsWallRunning || playerMovement.IsIdle || playerMovement.IsSliding)
        {
            stepTimer = 1 - playerSettings.footstepSpeed;
            return false;
        }
        return true;
    }

    public void FootSteps()
    {
        if (!CanExecute()) return;

        stepTimer -= Time.deltaTime * playerMovement.CurrentSpeed / 15;

        if (stepTimer <= 0)
        {
            stepTimer = 1 - playerSettings.footstepSpeed;
            audioSource.pitch = UnityEngine.Random.Range(.7f, 1.3f);

            Vector3 footstepCheckDirection = !playerMovement.IsWallRunning ? Vector3.down :
                (movementContext.WallLeft ? -orientation.Right : orientation.Right) * 2;

            if (Physics.Raycast(movementContext.Transform.position, footstepCheckDirection, out RaycastHit hit, 2.5f, movementContext.WhatIsGround))
            {
                PlayFootstepSound(hit.transform.gameObject.layer);
            }
        }
    }

    private void PlayFootstepSound(int layer)
    {
        AudioClip[] sounds = playerSettings.footstepSounds.GetSoundsForLayer(layer);

        if (sounds.Length == 0) return;
        {
            int randomIndex = UnityEngine.Random.Range(0, sounds.Length);
            AudioClip clip = sounds[randomIndex];

            // 원본: audioSource.PlayOneShot(clip) → audio.clip 이 null 이라 BossHearing 감지 불가
            // 수정: clip 을 직접 할당하고 Play() 로 재생 → audio.clip.name 으로 클립 이름 읽기 가능


            // 달리기와 걷기를 볼륨으로 구분
            // BossHearing 에서 볼륨 기준으로 달리기(중간 소리) / 걷기(작은 소리)를 분류함
            // 현재 속도가 runSpeed 의 80% 이상이면 달리기로 판단
            bool isRunning = playerMovement.CurrentSpeed >= playerSettings.runSpeed * 0.8f;
            float footVolume = isRunning ? 1.0f : 0.6f;

            // PlayOneShot 유지 - 소리가 겹쳐도 끊기지 않음
            // BossHearing 은 audioSource.volume 으로 달리기(1.0) / 걷기(0.6) 를 구분함
            audioSource.volume = footVolume;
            audioSource.PlayOneShot(clip, footVolume);
        }
    }

    public void ResetFootsteps()
    {
        stepTimer = 0;
    }
}