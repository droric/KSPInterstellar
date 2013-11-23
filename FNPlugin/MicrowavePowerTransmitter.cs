﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FNPlugin {
    class MicrowavePowerTransmitter : FNResourceSuppliableModule {
        //Persistent True
        [KSPField(isPersistant = true)]
        bool IsEnabled;

        //Persistent False
        [KSPField(isPersistant = false)]
        public string animName;

        //GUI 
        [KSPField(isPersistant = false, guiActive = true, guiName = "Beamed Power")]
        public string beamedpower;

        //Internal
        protected Animation anim;
        protected double nuclear_power;
        protected double solar_power;
        protected bool relay;
        protected long activeCount = 0;
        protected List<FNGenerator> generators;
        protected List<MicrowavePowerReceiver> receivers;
        protected List<ModuleDeployableSolarPanel> panels;

        [KSPEvent(guiActive = true, guiName = "Activate Transmitter", active = true)]
        public void ActivateTransmitter() {
            if (relay) { return; }
            if (anim != null) {
                anim[animName].speed = 1f;
                anim[animName].normalizedTime = 0f;
                anim.Blend(animName, 2f);
            }
            IsEnabled = true;
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate Transmitter", active = false)]
        public void DeactivateTransmitter() {
            if (relay) { return; }
            if (anim != null) {
                anim[animName].speed = -1f;
                anim[animName].normalizedTime = 1f;
                anim.Blend(animName, 2f);
            }
            IsEnabled = false;
        }

        [KSPEvent(guiActive = true, guiName = "Activate Relay", active = true)]
        public void ActivateRelay() {
            if (IsEnabled) { return; }
            if (anim != null) {
                anim[animName].speed = 1f;
                anim[animName].normalizedTime = 0f;
                anim.Blend(animName, 2f);
            }
            IsEnabled = true;
            relay = true;
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate Relay", active = true)]
        public void DeactivateRelay() {
            if (!relay) { return; }
            if (anim != null) {
                anim[animName].speed = 1f;
                anim[animName].normalizedTime = 0f;
                anim.Blend(animName, 2f);
            }
            IsEnabled = false;
            relay = false;
        }

        [KSPAction("Activate Transmitter")]
        public void ActivateTransmitterAction(KSPActionParam param) {
            ActivateTransmitter();
        }

        [KSPAction("Deactivate Transmitter")]
        public void DeactivateTransmitterAction(KSPActionParam param) {
            DeactivateTransmitter();
        }

        [KSPAction("Activate Relay")]
        public void ActivateRelayAction(KSPActionParam param) {
            ActivateRelay();
        }

        [KSPAction("Deactivate Relay")]
        public void DeactivateRelayAction(KSPActionParam param) {
            DeactivateRelay();
        }

        public override void OnStart(PartModule.StartState state) {
            if (state == StartState.Editor) { return; }

            generators = vessel.FindPartModulesImplementing<FNGenerator>();
            receivers = vessel.FindPartModulesImplementing<MicrowavePowerReceiver>();
            panels = vessel.FindPartModulesImplementing<ModuleDeployableSolarPanel>();

            anim = part.FindModelAnimators(animName).FirstOrDefault();
            if (anim != null) {
                anim[animName].layer = 1;
                if (!IsEnabled) {
                    anim[animName].normalizedTime = 1f;
                    anim[animName].speed = -1f;

                } else {
                    anim[animName].normalizedTime = 0f;
                    anim[animName].speed = 1f;

                }
                anim.Play();
            }

            this.part.force_activate();
        }

        public override void OnUpdate() {
            Events["ActivateTransmitter"].active = !IsEnabled && !relay;
            Events["DeactivateTransmitter"].active = IsEnabled && !relay;
            Events["ActivateRelay"].active = !IsEnabled && !relay;
            Events["DeactivateRelay"].active = IsEnabled && relay;
            double inputPower = nuclear_power + solar_power;
            if (inputPower > 1000) {
                beamedpower = (inputPower / 1000).ToString("0.000") + "MW";
            } else {
                beamedpower = inputPower.ToString("0.000") + "KW";
            }
        }

        public override void OnFixedUpdate() {
            activeCount++;
            nuclear_power = 0;
            solar_power = 0;
            if (IsEnabled && !relay) {
                foreach (FNGenerator generator in generators) {
                    if (generator.IsEnabled) {
                        double output = generator.getMaxPowerOutput();
                        double gpower = consumeFNResource(output*TimeWarp.fixedDeltaTime, FNResourceManager.FNRESOURCE_MEGAJOULES);
                        nuclear_power = gpower * 1000/TimeWarp.fixedDeltaTime;
                    }
                }

                foreach (ModuleDeployableSolarPanel panel in panels) {
                    double output = panel.flowRate;
                    double spower = part.RequestResource("ElectricCharge", output * TimeWarp.fixedDeltaTime);
                    double inv_square_mult = Math.Pow(Vector3d.Distance(vessel.transform.position, FlightGlobals.Bodies[PluginHelper.REF_BODY_KERBOL].transform.position), 2) / Math.Pow(Vector3d.Distance(FlightGlobals.Bodies[PluginHelper.REF_BODY_KERBIN].transform.position, FlightGlobals.Bodies[PluginHelper.REF_BODY_KERBOL].transform.position), 2);
                    solar_power += spower / TimeWarp.fixedDeltaTime*inv_square_mult;
                }
            }

            if (double.IsInfinity(nuclear_power) || double.IsNaN(nuclear_power)) {
                nuclear_power = 0;
            }

            if (double.IsInfinity(solar_power) || double.IsNaN(solar_power)) {
                solar_power = 0;
            } 

            if (activeCount % 1000 == 9) {
                ConfigNode config = PluginHelper.getPluginSaveFile();
                string vesselID = vessel.id.ToString();
                if (config.HasNode("VESSEL_MICROWAVE_POWER_" + vesselID)) {
                    ConfigNode power_node = config.GetNode("VESSEL_MICROWAVE_POWER_" + vesselID);
                    if (power_node.HasValue("nuclear_power")) {
                        power_node.SetValue("nuclear_power", nuclear_power.ToString("E"));
                    } else {
                        power_node.AddValue("nuclear_power", nuclear_power.ToString("E"));
                    }
                    if (power_node.HasValue("solar_power")) {
                        power_node.SetValue("solar_power", solar_power.ToString("E"));
                    } else {
                        power_node.AddValue("solar_power", solar_power.ToString("E"));
                    }

                } else {
                    ConfigNode power_node = config.AddNode("VESSEL_MICROWAVE_POWER_" + vesselID);
                    power_node.AddValue("nuclear_power", nuclear_power.ToString("E"));
                    power_node.AddValue("solar_power", solar_power.ToString("E"));
                }

                if (config.HasNode("VESSEL_MICROWAVE_RELAY_" + vesselID)) {
                    ConfigNode relay_node = config.GetNode("VESSEL_MICROWAVE_RELAY_" + vesselID);
                    if (relay_node.HasValue("relay")) {
                        relay_node.SetValue("relay", relay.ToString());
                    } else {
                        relay_node.AddValue("relay", relay.ToString());
                    }
                } else {
                    ConfigNode relay_node = config.AddNode("VESSEL_MICROWAVE_RELAY_" + vesselID);
                    relay_node.AddValue("relay", relay.ToString());
                }

                config.Save(PluginHelper.getPluginSaveFilePath());
            }
            activeCount++;
        }

    }
}
