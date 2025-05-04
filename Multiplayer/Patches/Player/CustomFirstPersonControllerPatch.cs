using HarmonyLib;
using Multiplayer.Components.Networking;
using Multiplayer.Utils;
using System;
using UnityEngine;

namespace Multiplayer.Patches.Player;

[HarmonyPatch(typeof(CustomFirstPersonController))]
public static class CustomFirstPersonControllerPatch
{
    private const float ROTATION_THRESHOLD = 0.001f;

    private static CustomFirstPersonController fps;

    private static Vector3 lastPosition;
    private static float lastRotationY;
    private static bool sentFinalPosition;
    private static bool wasCrouching;
    private static bool wasSitting;
    private static bool wasSwimming;
    

    private static bool isJumping;
    private static bool isOnCar;
    private static bool isCrouching;
    private static bool isSitting;
    private static float sitHeight;
    private static TrainCar car;

    [HarmonyPatch(nameof(CustomFirstPersonController.Awake))]
    [HarmonyPostfix]
    private static void CharacterMovement(CustomFirstPersonController __instance)
    {
        fps = __instance;
        isOnCar = PlayerManager.Car != null;
        NetworkLifecycle.Instance.OnTick += OnTick;
        PlayerManager.CarChanged += OnCarChanged;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CustomFirstPersonController.OnDestroy))]
    private static void OnDestroy()
    {
        if (UnloadWatcher.isQuitting)
            return;

        NetworkLifecycle.Instance.OnTick -= OnTick;
        PlayerManager.CarChanged -= OnCarChanged;
    }

    private static void OnCarChanged(TrainCar trainCar)
    {
        isOnCar = trainCar != null;
        car = trainCar;
    }

    private static void OnTick(uint tick)
    {
        if(UnloadWatcher.isUnloading)
            return;

        Vector3 position = isOnCar ? PlayerManager.PlayerTransform.localPosition : PlayerManager.PlayerTransform.GetWorldAbsolutePosition();
        float rotationY = (isOnCar ? PlayerManager.PlayerTransform.localEulerAngles : PlayerManager.PlayerTransform.eulerAngles).y;

        //bool positionOrRotationChanged = lastPosition != position || !Mathf.Approximately(lastRotationY, rotationY);

        bool positionOrRotationChanged = Vector3.Distance(lastPosition, position) > 0 || Math.Abs(lastRotationY - rotationY) > ROTATION_THRESHOLD;

        //Multiplayer.LogDebug(() =>$"Player OnTick() Position: {position}, Last position:{lastPosition}, Rotation: {rotationY}, Last rotation: {lastRotationY}, Changed: {positionOrRotationChanged}");

        if (!positionOrRotationChanged && sentFinalPosition)
            return;

        lastPosition = position;
        lastRotationY = rotationY;
        sentFinalPosition = !positionOrRotationChanged;

        if (fps.IsCrouching)
        {
            wasCrouching = true;
            Multiplayer.LogDebug(() =>$"Crouch");
        }
        else if(wasCrouching)
        {
            wasCrouching = false;
            Multiplayer.LogDebug(() => $"Stand from crouch");
        }

        if (fps.provider.IsSitting)
        {
            wasSitting = true;
            Multiplayer.LogDebug(() => $"Sit");
        }
        else if (wasSitting)
        {
            wasSitting = false;
            Multiplayer.LogDebug(() => $"Stand from sit");
        }

        if (fps.underwater)
        {
            wasSwimming = true;
            Multiplayer.LogDebug(() => $"Swim");
        }
        else if (wasSwimming)
        {
            wasSwimming = false;
            Multiplayer.LogDebug(() => $"Stand from swim");
        }

        ushort carNetID = isOnCar ? car.GetNetId() : (ushort)0;

        NetworkLifecycle.Instance.Client.SendPlayerPosition(lastPosition, PlayerManager.PlayerTransform.InverseTransformDirection(fps.m_MoveDir), lastRotationY, carNetID, isJumping, isOnCar, isJumping || sentFinalPosition);
        isJumping = false;
    }


    [HarmonyPostfix]
    [HarmonyPatch(nameof(CustomFirstPersonController.SetJumpParameters))]
    private static void SetJumpParameters()
    {
        isJumping = true;
    }
}
