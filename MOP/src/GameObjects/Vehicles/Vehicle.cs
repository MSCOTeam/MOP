﻿// Modern Optimization Plugin
// Copyright(C) 2019-2020 Athlon

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.If not, see<http://www.gnu.org/licenses/>.

using HutongGames.PlayMaker;
using MSCLoader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOP
{
    class Vehicle
    {
        // Vehicle class - made by Konrad "Athlon" Figura
        // 
        // It fixes known issue with missing vehicles engine sound by (admittedly hacky way) creating new GameObject,
        // that stores GameObjects responsible for playing sounds in vehicle (named "audio", or "SoundSrc").
        // Whenever the vehicle is disabled, all audio objects are changing parent to that temporary object.
        //
        // It also fixes the issue of vehicles going back to the original spawn position, instead of staying in the same place - as they should,
        // by simply saving the Transform.position and Transform.rotation parameters just before disabling the object, and then loading these values,
        // just after loading them.

        public GameObject gameObject { get; private set; }

        // Values that are being saved or loaded
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        public bool IsActive = true;

        // All objects that cannot be unloaded (because it causes problems) land under that object
        internal Transform temporaryParent;

        // List of non unloadable objects
        internal List<PreventToggleOnObject> preventToggleOnObjects;

        // Overwrites the "Component.transform", to prevent eventual mod crashes caused by missuse of Vehicle.transform.
        // Technically, you should use Vehicle.Object.transform (ex. GIFU.Object.Transform), this here just lets you use Vehicle.transform
        // (ex. GIFU.transform).
        public Transform transform => gameObject.transform;

        // Loaded only for Satsuma
        internal Satsuma SatsumaScript;

        // Unity car systems and rigidbody
        internal CarDynamics carDynamics;
        internal Axles axles;
        internal Rigidbody rb;
        readonly Drivetrain drivetrain;

        // Applies extra fixes, if is set to true.
        readonly bool isHayosiko;

        // Used to send FINISHED event, if the trailer is supposed to be attached.
        readonly bool isKekmet;

        // Prevents MOP from disabling car's physics when the car has rope hooked
        readonly PlayMakerFSM fsmHookFront;
        readonly PlayMakerFSM fsmHookRear;

        // Reference to one of the wheels that checks if the vehicle is on ground
        readonly Wheel wheel;

        // Currently used only by Shitsuma.
        internal Quaternion lastGoodRotation;
        internal Vector3 lastGoodPosition;
        bool lastGoodRotationSaved;
        readonly EventSounds eventSounds;

        /// <summary>
        /// Initialize class
        /// </summary>
        /// <param name="gameObjectName"></param>
        public Vehicle(string gameObjectName)
        {
            // gameObject the object by name
            gameObject = GameObject.Find(gameObjectName);

            // Use Resources.FindObjectsOfTypeAll method, if the vehicle was not found.
            if (gameObject == null)
                gameObject = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == gameObjectName);

            if (gameObject == null)
            {
                ModConsole.Error($"[MOP] Could not find {gameObjectName} vehicle.");
                return;
            }

            // Get the object position and rotation
            Position = gameObject.transform.localPosition;
            Rotation = gameObject.transform.localRotation;

            // Creates a new gameobject that is names after the original file + '_TEMP' (ex. "SATSUMA(557kg, 248)_TEMP")
            temporaryParent = new GameObject($"{gameObject.name}_TEMP").transform;

            preventToggleOnObjects = new List<PreventToggleOnObject>();

            // This should fix bug that leads to items inside of vehicles to fall through it.
            PlayMakerFSM lodFSM = gameObject.GetPlayMakerByName("LOD");
            if (lodFSM != null)
            {
                lodFSM.Fsm.RestartOnEnable = false;
                FsmState resetState = lodFSM.FindFsmState("Fix Collider");
                if (resetState != null)
                {
                    resetState.Actions = new FsmStateAction[] { new CustomStopAction() };
                    resetState.SaveActions();
                }

                lodFSM.FindFsmState("Load game").Actions = new FsmStateAction[] { new CustomNullState() };
            }

            if (gameObject.name == "BOAT")
                return;

            // Get the object's child which are responsible for audio
            foreach (Transform audioObject in FindAudioObjects())
            {
                preventToggleOnObjects.Add(new PreventToggleOnObject(audioObject));
            }

            // Fix for fuel level resetting after respawn
            Transform fuelTank = gameObject.transform.Find("FuelTank");
            if (fuelTank != null)
            {
                PlayMakerFSM fuelTankFSM = fuelTank.GetComponent<PlayMakerFSM>();
                if (fuelTankFSM)
                    fuelTankFSM.Fsm.RestartOnEnable = false;
            }

            // If the vehicle is Gifu, find knobs and add them to list of unloadable objects
            if (gameObject.name == "GIFU(750/450psi)")
            {
                Transform knobs = gameObject.transform.Find("Dashboard/Knobs");
                foreach (PlayMakerFSM knobsFSMs in knobs.GetComponentsInChildren<PlayMakerFSM>())
                    knobsFSMs.Fsm.RestartOnEnable = false;

                PlayMakerFSM shitFsm = gameObject.transform.Find("ShitTank").gameObject.GetComponent<PlayMakerFSM>();
                FsmState loadGame = shitFsm.FindFsmState("Load game");
                List<FsmStateAction> loadArrayActions = new List<FsmStateAction> { new CustomNullState() };
                loadArrayActions.Add(new CustomNullState());
                loadGame.Actions = loadArrayActions.ToArray();
                loadGame.SaveActions();
            }

            // Fixed kickstand resetting to the default value.
            if (gameObject.name == "JONNEZ ES(Clone)")
            {
                PlayMakerFSM kickstandFsm = gameObject.transform.Find("Kickstand").gameObject.GetComponent<PlayMakerFSM>();
                FsmState loadGame = kickstandFsm.FindFsmState("Load game");
                List<FsmStateAction> loadArrayActions = new List<FsmStateAction> { new CustomNullState() };
                loadArrayActions.Add(new CustomNullState());
                loadGame.Actions = loadArrayActions.ToArray();
                loadGame.SaveActions();

                // Disable on restart for wheels script.
                Transform wheelsParent = transform.Find("Wheels");
                foreach (Transform wheel in wheelsParent.GetComponentsInChildren<Transform>())
                {
                    if (!wheel.gameObject.name.StartsWith("Moped_wheel")) continue;
                    wheel.gameObject.GetComponent<PlayMakerFSM>().Fsm.RestartOnEnable = false;
                }
            }

            carDynamics = gameObject.GetComponent<CarDynamics>();
            axles = gameObject.GetComponent<Axles>();
            rb = gameObject.GetComponent<Rigidbody>();

            // Hook HookFront and HookRear
            // Get hooks first
            Transform hookFront = transform.Find("HookFront");
            Transform hookRear = transform.Find("HookRear");

            // If hooks exists, attach the RopeHookUp and RopeUnhook to appropriate states
            if (hookFront != null)
            {
                fsmHookFront = hookFront.GetComponent<PlayMakerFSM>();
            }

            if (hookRear != null)
            {
                fsmHookRear = hookRear.GetComponent<PlayMakerFSM>();
            }

            // If vehicle is flatbed, hook SwitchToggleMethod to Add scale script
            if (gameObject.name == "FLATBED")
            {
                PlayMakerFSM logTriggerFsm = transform.Find("Bed/LogTrigger").gameObject.GetComponent<PlayMakerFSM>();
                FsmState loadGame = logTriggerFsm.FindFsmState("Load game");
                List<FsmStateAction> loadArrayActions = new List<FsmStateAction> { new CustomNullState() };
                loadGame.Actions = loadArrayActions.ToArray();
                loadGame.SaveActions();

                GameObject trailerLogUnderFloorCheck = new GameObject("MOP_TrailerLogUnderFloorFix");
                trailerLogUnderFloorCheck.transform.parent = gameObject.transform;
                trailerLogUnderFloorCheck.AddComponent<TrailerLogUnderFloor>();
            }

            // Set default toggling method - that is entire vehicle
            Toggle = ToggleActive;

            isHayosiko = gameObject.name == "HAYOSIKO(1500kg, 250)";
            isKekmet = gameObject.name == "KEKMET(350-400psi)";

            // If the user selected to toggle vehicle's physics only, it overrided any previous set for Toggle method
            if (Rules.instance.SpecialRules.ToggleAllVehiclesPhysicsOnly)
            {
                Toggle = IgnoreToggle;
            }

            // Get all HingeJoints and add HingeManager to them
            // Ignore for Satsuma or cars that use ToggleUnityCar method (and force for Hayosiko - no matter what)
            if (SatsumaScript == null && Toggle != ToggleUnityCar || isHayosiko)
            {
                HingeJoint[] joints = gameObject.transform.GetComponentsInChildren<HingeJoint>();
                foreach (HingeJoint joint in joints)
                    joint.gameObject.AddComponent<HingeManager>();
            }

            // Get one of the wheels.
            wheel = axles.allWheels[0];
            drivetrain = gameObject.GetComponent<Drivetrain>();

            // Ignore Rules.
            IgnoreRule vehicleRule = Rules.instance.IgnoreRules.Find(v => v.ObjectName == this.gameObject.name);
            if (vehicleRule != null)
            {
                Toggle = IgnoreToggle;

                if (vehicleRule.TotalIgnore)
                    IsActive = false;
            }

            // Prevent Toggle On Object Rule.
            IgnoreRuleAtPlace[] preventToggleOnObjectRule = Rules.instance.IgnoreRulesAtPlaces
                .Where(v => v.Place == this.gameObject.name).ToArray();
            if (preventToggleOnObjectRule.Length > 0)
            {
                foreach (var p in preventToggleOnObjectRule)
                {
                    Transform t = transform.FindRecursive(p.ObjectName);
                    if (t == null)
                    {
                        ModConsole.Error($"[MOP] Couldn't find {p.ObjectName} in {p.Place}.");
                        continue;
                    }
                    
                    preventToggleOnObjects.Add(new PreventToggleOnObject(t));
                }
            }

            eventSounds = gameObject.GetComponent<EventSounds>();

            // Odometers fix.
            switch (gameObject.name)
            {
                case "GIFU(750/450psi)":
                    transform.Find("Dashboard/Odometer").gameObject.GetComponent<PlayMakerFSM>().Fsm.RestartOnEnable = false;
                    break;
                case "HAYOSIKO(1500kg, 250)":
                    transform.Find("Odometer").gameObject.GetComponent<PlayMakerFSM>().Fsm.RestartOnEnable = false;
                    break;
                case "KEKMET(350-400psi)":
                    transform.Find("Dashboard/HourMeter").gameObject.GetComponent<PlayMakerFSM>().Fsm.RestartOnEnable = false;
                    break;
                case "SATSUMA(557kg, 248)":
                    GameObject.Find("dashboard meters(Clone)").transform.Find("Gauges/Odometer").gameObject.GetComponent<PlayMakerFSM>().Fsm.RestartOnEnable = false;
                    break;
            }
        }

        public delegate void ToggleHandler(bool enabled);
        public ToggleHandler Toggle;

        /// <summary>
        /// Enable or disable car
        /// </summary>
        internal void ToggleActive(bool enabled)
        {
            if (gameObject == null || gameObject.activeSelf == enabled || !IsActive) return;

            // Fix for when the player doesn't have keys for Hayosiko.
            // Van will NOT be toggled
            if (isHayosiko && MopFsmManager.PlayerHasHayosikoKey() == false)
            {
                ToggleUnityCar(enabled);
                return;
            }

            // If we're disabling a car, set the audio child parent to TemporaryAudioParent, and save the position and rotation.
            // We're doing that BEFORE we disable the object.
            if (!enabled)
            {
                for (int i = 0; i < preventToggleOnObjects.Count; i++)
                    preventToggleOnObjects[i].ObjectTransform.parent = temporaryParent;

                Position = gameObject.transform.localPosition;
                Rotation = gameObject.transform.localRotation;
            }

            gameObject.SetActive(enabled);

            // Uppon enabling the object, set the localPosition and localRotation to the object's transform, and change audio source parents to Object
            // We're doing that AFTER we enable the object.
            if (enabled)
            {
                gameObject.transform.localPosition = Position;
                gameObject.transform.localRotation = Rotation;

                for (int i = 0; i < preventToggleOnObjects.Count; i++)
                    preventToggleOnObjects[i].ObjectTransform.parent = preventToggleOnObjects[i].OriginalParent;

                if (isKekmet && MopFsmManager.IsTrailerAttached())
                {
                    if (Vector3.Distance(transform.Find("Trailer/Hook").position, WorldManager.instance.GetFlatbed().transform.Find("HookTarget").position) >= 0.4f)
                    {
                        GameFixes.Instance.KekmetTrailerDetach();
                    }
                    else
                    {
                        GameFixes.Instance.KekmetTrailerAttach();
                    }
                }
            }
        }

        /// <summary>
        /// Toggle car physics only.
        /// </summary>
        /// <param name="enabled"></param>
        public void ToggleUnityCar(bool enabled)
        {
            if ((gameObject == null) || gameObject.name == "BOAT" || !IsActive)
                return;

            if (rb.isKinematic == !enabled && carDynamics.enabled == enabled)
                return;

            // Don't toggle physics, unless car's on ground
            if ((IsMoving() || !IsOnGround()) && !enabled)
                return;

            // If satsumaScript in this is not null, and Satsuma is in inspection area and is enabled, 
            // don't toggle unitycar
            if (!enabled && SatsumaScript != null && SatsumaScript.IsSatsumaInInspectionArea)
                enabled = true;

            // Prevent disabling car physics if the rope is hooked
            if (!enabled && gameObject.activeSelf == true && IsRopeHooked())
                enabled = true;

            carDynamics.enabled = enabled;
            axles.enabled = enabled;
            rb.isKinematic = !enabled;
            rb.useGravity = enabled;

            // We're completly freezing Satsuma, so it won't flip (hopefully...).
            if (gameObject.name == "SATSUMA(557kg, 248)")
            {
                if (!enabled && !lastGoodRotationSaved)
                {
                    lastGoodRotationSaved = true;
                    lastGoodRotation = transform.localRotation;
                    lastGoodPosition = transform.localPosition;
                }
                
                if (enabled)
                {
                    lastGoodRotationSaved = false;
                }

                rb.constraints = enabled ? RigidbodyConstraints.None : RigidbodyConstraints.FreezePosition;
            }
        }

        public void ForceToggleUnityCar(bool enabled)
        {
            if ((gameObject == null) || gameObject.name == "BOAT" || (carDynamics.enabled == enabled) || !IsActive)
                return;

            carDynamics.enabled = enabled;
            axles.enabled = enabled;
            rb.isKinematic = !enabled;
            rb.useGravity = enabled;

            // We're completly freezing Satsuma, so it won't flip (hopefully...).
            if (gameObject.name == "SATSUMA(557kg, 248)")
            {
                if (!enabled && !lastGoodRotationSaved)
                {
                    lastGoodRotationSaved = true;
                    lastGoodRotation = transform.localRotation;
                    lastGoodPosition = transform.localPosition;
                }

                if (enabled)
                {
                    lastGoodRotationSaved = false;
                }

                rb.constraints = enabled ? RigidbodyConstraints.None : RigidbodyConstraints.FreezePosition;
            }
        }

        /// <summary>
        /// This is an empty void for when the toggling is meant to be ignored.
        /// </summary>
        /// <param name="enabled"></param>
        internal void IgnoreToggle(bool enabled) 
        {
            return;
        }

        /// <summary>
        /// Retrieves the child audio objects from parent object.
        /// Basically looks for files with "audio" and "SoundSrc" name in it
        /// </summary>
        /// <returns></returns>
        Transform[] FindAudioObjects()
        {
            Transform[] childs = gameObject.transform.GetComponentsInChildren<Transform>();
            return childs.Where(obj => obj.gameObject.name.Contains("audio") || obj.gameObject.name.Contains("SoundSrc")).ToArray();
        }

        /// <summary>
        /// Checks PlayMaker of front and rear hooks and returns "true", if in any of the hooks value "Attached" is true.
        /// </summary>
        /// <returns></returns>
        bool IsRopeHooked()
        {
            bool isFrontHookAttached = fsmHookFront ? fsmHookFront.FsmVariables.GetFsmBool("Attached").Value : false;
            bool isRearHookAttached = fsmHookRear ? fsmHookRear.FsmVariables.GetFsmBool("Attached").Value : false;
            return (isFrontHookAttached || isRearHookAttached) ? true : false;
        }

        /// <summary>
        /// Checks one of the wheels' onGroundDown value
        /// 
        /// WORKAROUND FOR JONNEZ:
        /// Because onGroundDown for Jonnez doesn't work the same way as for others, it will check if the Jonnnez's engine torque.
        /// </summary>
        /// <returns></returns>
        internal bool IsOnGround()
        {
            switch (this.gameObject.name)
            {
                case "JONNEZ ES(Clone)":
                    return drivetrain.torque == 0;
                case "SATSUMA(557kg, 248)":
                    if (!wheel.enabled)
                        return drivetrain.torque == 0;
                    break;
            }

            return wheel.onGroundDown;
        }

        /// <summary>
        /// Returns true, if the vehicle is moving.
        /// </summary>
        /// <returns></returns>
        internal bool IsMoving()
        {
            return rb.velocity.magnitude > 0.1f;
        }

        /// <summary>
        /// Disable the EventSounds component.
        /// </summary>
        /// <param name="enabled"></param>
        public void ToggleEventSounds(bool enabled)
        {
            eventSounds.disableSounds = !enabled;
        }

        /// <summary>
        /// Freezes the car completely by adding ItemFreezer class.
        /// </summary>
        public void Freeze()
        {
            gameObject.AddComponent<ItemFreezer>();
        }
    }
}
