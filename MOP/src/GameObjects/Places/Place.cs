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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOP
{
    class Place
    {
        readonly GameObject gameObject;
        readonly string name;
        readonly float toggleDistance;

        // Objects from that list will not be disabled
        // It is so to prevent from restock script and Teimo's bike routine not working
        internal List<string> GameObjectBlackList;

        /// <summary>
        /// List of childs that are allowed to be disabled
        /// </summary>
        internal List<Transform> DisableableChilds;
        internal Transform[] Doors;
        internal List<PlayMakerFSM> PlayMakers;

        /// <summary>
        /// Saves what value has been last used, to prevent unnescesary launch of loop.
        /// </summary>
        internal bool isActive = true;

        /// <summary>
        /// Initialize the Store class
        /// </summary>
        public Place(string placeName, float distance = 200)
        {
            gameObject = GameObject.Find(placeName);
            name = placeName;
            toggleDistance = distance;
            GameObjectBlackList = new List<string>();
            PlayMakers = new List<PlayMakerFSM>();

            IgnoreRuleAtPlace[] ignoreRulesAtThisPlace = Rules.instance.IgnoreRulesAtPlaces.Where(r => r.Place == placeName).ToArray();
            if (ignoreRulesAtThisPlace.Length > 0)
                foreach (IgnoreRuleAtPlace rule in ignoreRulesAtThisPlace)
                    GameObjectBlackList.Add(rule.ObjectName);
        }

        /// <summary>
        /// Enable or disable the place
        /// </summary>
        /// <param name="enabled"></param>
        public void ToggleActive(bool enabled)
        {
            // Don't execute the code, if the enabled value is the same as the activity status.
            if (isActive == enabled) 
                return;

            isActive = enabled;

            // In case of yard, reset doors pivots
            if (gameObject.name == "YARD" && !enabled && Doors.Length > 0)
            {
                for (int i = 0; i < Doors.Length; i++)
                {
                    Transform pivot = Doors[i].Find("Pivot");
                    pivot.localEulerAngles = Vector3.zero;
                }
            }

            // Load and unload only the objects that aren't on the whitelist.
            for (int i = 0; i < DisableableChilds.Count; i++)
            {
                // If the object is missing, skip and continue.
                if (DisableableChilds[i] == null)
                    continue;

                DisableableChilds[i].gameObject.SetActive(enabled);
            }

            if (PlayMakers.Count > 0)
            {
                for (int i = 0; i < PlayMakers.Count; i++)
                {
                    PlayMakers[i].enabled = enabled;
                }
            }
        }

        /// <summary>
        /// Returns all childs of the object.
        /// </summary>
        /// <returns></returns>
        internal List<Transform> GetDisableableChilds()
        {
            return gameObject.GetComponentsInChildren<Transform>(true)
                .Where(trans => !trans.gameObject.name.ContainsAny(GameObjectBlackList)).ToList();
        }

        /// <summary>
        /// Returns the transform of this object.
        /// </summary>
        /// <returns></returns>
        public Transform GetTransform()
        {
            return gameObject.transform;
        }

        /// <summary>
        /// Returns the gameo object name.
        /// </summary>
        /// <returns></returns>
        public string GetName()
        {
            return name;
        }

        /// <summary>
        /// Returns toggling distance of the place.
        /// </summary>
        /// <returns></returns>
        public float GetToggleDistance()
        {
            return toggleDistance;
        }
    }
}
