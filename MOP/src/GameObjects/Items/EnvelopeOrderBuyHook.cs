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

using UnityEngine;
using MSCLoader;

namespace MOP
{
    class EnvelopeOrderBuyHook : MonoBehaviour
    {
        CashRegisterHook cashRegisterHook;

        public void Initialize(CashRegisterHook cashRegisterHook)
        {
            this.cashRegisterHook = cashRegisterHook;
        }

        void Start()
        {
            FsmHook.FsmInject(this.gameObject, "State 3", cashRegisterHook.Packages);
        }
    }
}
