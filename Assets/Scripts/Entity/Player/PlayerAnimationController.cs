using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PlayerAnimationController : MonoBehaviourPun {

    PlayerController controller;
    Animator animator;
    Rigidbody2D body;
    BoxCollider2D mainHitbox;

    [SerializeField] GameObject models, smallModel, largeModel, blueShell, propellerHelmet, propeller;
    [SerializeField] ParticleSystem dust, sparkles, drillParticle, giantParticle, fireParticle;
    [SerializeField] float blinkDuration = 0.1f, pipeDuration = 2f, heightSmallModel = 0.46f, heightLargeModel = 0.82f, deathUpTime = 0.6f, deathForce = 7f;
    [SerializeField] Avatar smallAvatar, largeAvatar;
    [SerializeField] [ColorUsage(true, false)] Color glowColor = Color.clear;
    [SerializeField] Color primaryColor = Color.clear, secondaryColor = Color.clear;

    AudioSource drillParticleAudio;
    [SerializeField] AudioClip normalDrill, propellerDrill;

    Enums.PlayerEyeState eyeState;
    float blinkTimer, pipeTimer, deathTimer, propellerVelocity;
    public bool deathUp, wasTurnaround;

    public void Start() {
        controller = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
        mainHitbox = GetComponent<BoxCollider2D>();
        drillParticleAudio = drillParticle.GetComponent<AudioSource>();

        DisableAllModels();

        if (photonView) {
            if (!photonView.IsMine)
                glowColor = Color.HSVToRGB(controller.playerId / ((float) PhotonNetwork.PlayerList.Length + 1), 1, 1);

            primaryColor = CustomColors.Primary[(int) photonView.Owner.CustomProperties[Enums.NetPlayerProperties.PrimaryColor]].color.linear;
            secondaryColor = CustomColors.Secondary[(int) photonView.Owner.CustomProperties[Enums.NetPlayerProperties.SecondaryColor]].color.linear;
        }
    }

    public void Update() {
        HandleAnimations();
    }

    void HandleAnimations() {
        Vector3 targetEuler = models.transform.eulerAngles;
        bool instant = false, changeFacing = false;
        if (!controller.knockback && !GameManager.Instance.gameover) {
            if (controller.dead) {
                if (animator.GetBool("firedeath") && deathTimer > deathUpTime) {
                    targetEuler = new Vector3(-15, controller.facingRight ? 110 : 250, 0);
                } else {
                    targetEuler = new Vector3(0, 180, 0);
                }
                instant = true;
            } else if (animator.GetBool("pipe")) {
                targetEuler = new Vector3(0, 180, 0);
                instant = true;
            } else if (animator.GetBool("inShell") && !controller.onSpinner) {
                targetEuler += Mathf.Abs(body.velocity.x) / controller.runningMaxSpeed * Time.deltaTime * new Vector3(0, 1800 * (controller.facingRight ? -1 : 1));
                instant = true;
            } else if (wasTurnaround || controller.skidding || controller.turnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
                if (controller.facingRight ^ (wasTurnaround = animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || controller.turnaround)) {
                    targetEuler = new Vector3(0, 250, 0);
                } else {
                    targetEuler = new Vector3(0, 110, 0);
                }
                instant = true;
            } else {
                if (controller.onSpinner && controller.onGround && Mathf.Abs(body.velocity.x) < 0.3f && !controller.holding) {
                    targetEuler += new Vector3(0, -1800, 0) * Time.deltaTime;
                    instant = true;
                    changeFacing = true;
                } else if (controller.flying || controller.propeller) {
                    targetEuler += new Vector3(0, -1200 - (controller.propellerTimer * 2000) - (controller.drill ? 800 : 0) + (controller.propeller && controller.propellerSpinTimer <= 0 && body.velocity.y < 0 ? 800 : 0), 0) * Time.deltaTime;
                    instant = true;
                } else {
                    targetEuler = new Vector3(0, controller.facingRight ? 110 : 250, 0);
                }
            }
            propellerVelocity = Mathf.Clamp(propellerVelocity + (1800 * ((controller.flying || controller.propeller || controller.usedPropellerThisJump) ? -1 : 1) * Time.deltaTime), -2500, -300);
            propeller.transform.Rotate(Vector3.forward, propellerVelocity * Time.deltaTime);

            if (instant) {
                models.transform.rotation = Quaternion.Euler(targetEuler);
            } else {
                float maxRotation = 2000f * Time.deltaTime;
                float x = models.transform.eulerAngles.x, y = models.transform.eulerAngles.y, z = models.transform.eulerAngles.z;
                x += Mathf.Clamp(targetEuler.x - x, -maxRotation, maxRotation);
                y += Mathf.Clamp(targetEuler.y - y, -maxRotation, maxRotation);
                z += Mathf.Clamp(targetEuler.z - z, -maxRotation, maxRotation);
                models.transform.rotation = Quaternion.Euler(x, y, z);
            }

            if (changeFacing)
                controller.facingRight = models.transform.eulerAngles.y < 180; 
        }

        //Particles
        SetParticleEmission(dust, (controller.wallSlideLeft || controller.wallSlideRight || (controller.onGround && ((controller.skidding && !controller.doIceSkidding) || (controller.crouching && Mathf.Abs(body.velocity.x) > 1))) || (controller.sliding && Mathf.Abs(body.velocity.x) > 0.2 && controller.onGround)) && !controller.pipeEntering);
        SetParticleEmission(drillParticle, controller.drill);
        if (controller.drill)
            drillParticleAudio.clip = (controller.State == Enums.PowerupState.PropellerMushroom ? propellerDrill : normalDrill);
        SetParticleEmission(sparkles, controller.invincible > 0);
        SetParticleEmission(giantParticle, controller.State == Enums.PowerupState.MegaMushroom && controller.giantStartTimer <= 0);
        SetParticleEmission(fireParticle, animator.GetBool("firedeath") && controller.dead && deathTimer > deathUpTime);

        //Blinking
        if (controller.dead) {
            eyeState = Enums.PlayerEyeState.Death;
        } else {
            if ((blinkTimer -= Time.fixedDeltaTime) < 0)
                blinkTimer = 3f + (Random.value * 2f);
            if (blinkTimer < blinkDuration) {
                eyeState = Enums.PlayerEyeState.HalfBlink;
            } else if (blinkTimer < blinkDuration * 2f) {
                eyeState = Enums.PlayerEyeState.FullBlink;
            } else if (blinkTimer < blinkDuration * 3f) {
                eyeState = Enums.PlayerEyeState.HalfBlink;
            } else {
                eyeState = Enums.PlayerEyeState.Normal;
            }
        }

        if (photonView.IsMine)
            HorizontalCamera.OFFSET_TARGET = (controller.flying || controller.propeller) ? 0.75f : 0f;

        if (controller.crouching || controller.sliding || controller.skidding) {
            dust.transform.localPosition = Vector2.zero;
        } else {
            dust.transform.localPosition = new Vector2(mainHitbox.size.x * (3f / 4f) * (controller.wallSlideLeft ? -1 : 1), mainHitbox.size.y * (3f / 4f));
        }
    }
    private void SetParticleEmission(ParticleSystem particle, bool value) {
        if (value) {
            if (particle.isStopped)
                particle.Play();
        } else {
            if (particle.isPlaying)
                particle.Stop();
        }
    }

    public void UpdateAnimatorStates() {

        if (photonView.IsMine) {
            //Animation
            animator.SetBool("skidding", !controller.doIceSkidding && controller.skidding);
            animator.SetBool("turnaround", controller.turnaround);
            animator.SetBool("onLeft", controller.wallSlideLeft);
            animator.SetBool("onRight", controller.wallSlideRight);
            animator.SetBool("onGround", controller.onGround);
            animator.SetBool("invincible", controller.invincible > 0);
            float animatedVelocity = Mathf.Abs(body.velocity.x) + Mathf.Abs(body.velocity.y * Mathf.Sin(controller.floorAngle * Mathf.Deg2Rad));
            if (controller.stuckInBlock) {
                animatedVelocity = 0;
            } else if (controller.propeller) {
                animatedVelocity = 2.5f;
            } else if (controller.doIceSkidding) {
                if (controller.skidding)
                    animatedVelocity = 3.5f;
                if (controller.iceSliding)
                    animatedVelocity = 0f;
            } else if (Mathf.Abs(body.velocity.x) > 0.1f && controller.State == Enums.PowerupState.MegaMushroom) {
                animatedVelocity = 4.5f;
            } 
            animator.SetFloat("velocityX", animatedVelocity);
            animator.SetFloat("velocityY", body.velocity.y);
            animator.SetBool("doublejump", controller.doublejump);
            animator.SetBool("triplejump", controller.triplejump);
            animator.SetBool("crouching", controller.crouching);
            animator.SetBool("groundpound", controller.groundpound);
            animator.SetBool("sliding", controller.sliding);
            animator.SetBool("holding", controller.holding != null);
            animator.SetBool("head carry", controller.holding != null && controller.holding.GetComponent<FrozenCube>() != null);
            animator.SetBool("knockback", controller.knockback);
            animator.SetBool("pipe", controller.pipeEntering != null);
            animator.SetBool("blueshell", controller.State == Enums.PowerupState.BlueShell);
            animator.SetBool("mini", controller.State == Enums.PowerupState.MiniMushroom);
            animator.SetBool("mega", controller.State == Enums.PowerupState.MegaMushroom);
            animator.SetBool("flying", controller.flying);
            animator.SetBool("drill", controller.drill);
            animator.SetBool("inShell", controller.inShell || (controller.State == Enums.PowerupState.BlueShell && (controller.crouching || (controller.groundpound && body.velocity.y < 0))));
            animator.SetBool("facingRight", controller.facingRight);
            animator.SetBool("propeller", controller.propeller);
            animator.SetBool("propellerSpin", controller.propellerSpinTimer > 0);
        } else {
            controller.wallSlideLeft = animator.GetBool("onLeft");
            controller.wallSlideRight = animator.GetBool("onRight");
            controller.onGround = animator.GetBool("onGround");
            controller.skidding = animator.GetBool("skidding");
            controller.turnaround = animator.GetBool("turnaround");
            controller.crouching = animator.GetBool("crouching");
            controller.invincible = animator.GetBool("invincible") ? 1f : 0f;
            controller.flying = animator.GetBool("flying");
            controller.drill = animator.GetBool("drill");
            controller.sliding = animator.GetBool("sliding");
            controller.facingRight = animator.GetBool("facingRight");
            controller.propellerSpinTimer = animator.GetBool("propellerSpin") ? 1f : 0f;
        }

        if (controller.giantEndTimer > 0) {
            transform.localScale = Vector3.one + (Vector3.one * (Mathf.Min(1, controller.giantEndTimer / (controller.giantStartTime / 2f)) * 2.6f));
        } else {
            transform.localScale = controller.State switch {
                Enums.PowerupState.MiniMushroom => Vector3.one / 2,
                Enums.PowerupState.MegaMushroom => Vector3.one + (Vector3.one * (Mathf.Min(1, 1 - (controller.giantStartTimer / controller.giantStartTime)) * 2.6f)),
                _ => Vector3.one,
            };
        }

        //Enable rainbow effect
        MaterialPropertyBlock block = new();
        block.SetFloat("RainbowEnabled", animator.GetBool("invincible") ? 1.1f : 0f);
        int ps = controller.State switch {
            Enums.PowerupState.FireFlower => 1,
            Enums.PowerupState.PropellerMushroom => 2,
            Enums.PowerupState.IceFlower => 3,
            _ => 0
        };
        block.SetFloat("PowerupState", ps);
        block.SetFloat("EyeState", (int) eyeState);
        block.SetFloat("ModelScale", transform.lossyScale.x);
        block.SetColor("GlowColor", glowColor);

        //Customizeable player color
        block.SetVector("OverallsColor", primaryColor);
        block.SetVector("ShirtColor", secondaryColor);

        Vector3 giantMultiply = Vector3.one;
        if (controller.giantTimer > 0 && controller.giantTimer < 4) {
            float v = ((Mathf.Sin(controller.giantTimer * 20f) + 1f) / 2f * 0.9f) + 0.1f;
            giantMultiply = new Vector3(v, 1, v);
        }
        block.SetVector("MultiplyColor", giantMultiply);
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            renderer.SetPropertyBlock(block);
        foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            renderer.SetPropertyBlock(block);

        //hit flash
        models.SetActive(controller.dead || !(controller.hitInvincibilityCounter > 0 && controller.hitInvincibilityCounter * (controller.hitInvincibilityCounter <= 0.75f ? 5 : 2) % (blinkDuration * 2f) < blinkDuration));

        //Hitbox changing
        UpdateHitbox();

        //Model changing
        bool large = controller.State >= Enums.PowerupState.Large;

        largeModel.SetActive(large);
        smallModel.SetActive(!large);
        blueShell.SetActive(controller.State == Enums.PowerupState.BlueShell);
        propellerHelmet.SetActive(controller.State == Enums.PowerupState.PropellerMushroom);
        animator.avatar = large ? largeAvatar : smallAvatar;

        HandleDeathAnimation();
        HandlePipeAnimation();

        if (animator.GetBool("pipe")) {
            gameObject.layer = PlayerController.HITS_NOTHING_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, 1);
        } else if (controller.dead || controller.stuckInBlock || controller.giantStartTimer > 0 || (controller.giantEndTimer > 0 &&  controller.stationaryGiantEnd)) {
            gameObject.layer = PlayerController.HITS_NOTHING_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, -4);
        } else {
            gameObject.layer = PlayerController.DEFAULT_LAYERID;
            transform.position = new Vector3(body.position.x, body.position.y, -4);
        }
    }
    void HandleDeathAnimation() {
        if (!controller.dead) {
            deathTimer = 0;
            return;
        }
        if (body.position.y < GameManager.Instance.GetLevelMinY() - transform.lossyScale.y)
            transform.position = body.position = new Vector2(body.position.x, GameManager.Instance.GetLevelMinY() - 20);

        deathTimer += Time.fixedDeltaTime;
        if (deathTimer < deathUpTime) {
            deathUp = false;
            body.gravityScale = 0;
            body.velocity = Vector2.zero;
            animator.Play("deadstart", controller.State >= Enums.PowerupState.Large ? 1 : 0);
        } else {
            if (!deathUp && body.position.y > GameManager.Instance.GetLevelMinY()) {
                body.velocity = new Vector2(0, deathForce);
                deathUp = true;
                if (animator.GetBool("firedeath"))
                    controller.PlaySound(Enums.Sounds.Player_Voice_LavaDeath);
            }
            body.gravityScale = 1.2f;
            body.velocity = new Vector2(0, Mathf.Max(-deathForce, body.velocity.y));
        }
        if (controller.photonView.IsMine && deathTimer + Time.fixedDeltaTime > (3 - 0.43f) && deathTimer < (3 - 0.43f))
            controller.fadeOut.FadeOutAndIn(0.33f, .1f);

        if (photonView.IsMine && deathTimer >= 3f)
            photonView.RPC("PreRespawn", RpcTarget.All);
    }

    void UpdateHitbox() {
        float width = mainHitbox.size.x;
        float height;

        if (controller.State <= Enums.PowerupState.Small || (controller.invincible > 0 && !controller.onGround && !controller.crouching && !controller.sliding && !controller.flying && !controller.propeller) || controller.groundpound) {
            height = heightSmallModel;
        } else {
            height = heightLargeModel;
        }

        if (controller.State != Enums.PowerupState.MiniMushroom && (controller.crouching || controller.inShell || controller.sliding || controller.triplejump))
            height *= controller.State <= Enums.PowerupState.Small ? 0.7f : 0.5f;

        mainHitbox.size = new Vector2(width, height);
        mainHitbox.offset = new Vector2(0, height / 2f);
    }
    void HandlePipeAnimation() {
        if (!photonView.IsMine)
            return;
        if (!controller.pipeEntering) {
            pipeTimer = 0;
            return;
        }

        PipeManager pe = controller.pipeEntering;

        body.isKinematic = true;
        body.velocity = controller.pipeDirection;

        if (pipeTimer < pipeDuration / 2f && pipeTimer + Time.fixedDeltaTime >= pipeDuration / 2f) {
            //tp to other pipe
            if (pe.otherPipe.bottom == pe.bottom)
                controller.pipeDirection *= -1;

            Vector2 offset = controller.pipeDirection * (pipeDuration / 2f);
            if (pe.otherPipe.bottom) {
                offset -= controller.pipeDirection;
                offset.y -= heightLargeModel - (mainHitbox.size.y * transform.localScale.y);
            }
            transform.position = body.position = new Vector3(pe.otherPipe.transform.position.x, pe.otherPipe.transform.position.y, 1) - (Vector3) offset;
            photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Player_Sound_Powerdown);
            controller.cameraController.Recenter();
        }
        if (pipeTimer >= pipeDuration) {
            controller.pipeEntering = null;
            body.isKinematic = false;
        }
        pipeTimer += Time.fixedDeltaTime;
    }

    public void DisableAllModels() {
        smallModel.SetActive(false);
        largeModel.SetActive(false);
        blueShell.SetActive(false);
        propellerHelmet.SetActive(false);
        animator.avatar = smallAvatar;
    }
}