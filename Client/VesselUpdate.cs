﻿using System;
using UnityEngine;

namespace DarkMultiPlayer
{
    public class VesselUpdate
    {
        public string vesselID;
        public double planetTime;
        public string bodyName;
        public float[] rotation;
        public float[] angularVelocity;
        public FlightCtrlState flightState;
        public bool[] actiongroupControls;
        public bool isSurfaceUpdate;
        //Orbital parameters
        public double[] orbit;
        //Surface parameters
        //Position = lat,long,alt,ground height.
        public double[] position;
        public double[] velocity;
        public double[] acceleration;
        public float[] terrainNormal;
        public static LineRendererDebug ourNormalLine;
        public static LineRendererDebug theirNormalLine;
        public static LineRendererDebug crossNormalLine;
        public static QuaternionRendererDebug ourRotDebug;
        public static QuaternionRendererDebug theirRotDebug;
        public static QuaternionRendererDebug identityRotDebug;
        public static QuaternionRendererDebug fudgeRotDebug;
        public static ScreenMessage debugMessage;
        public static float lastUpdateTime = float.NegativeInfinity;

        public static VesselUpdate CopyFromVessel(Vessel updateVessel)
        {
            VesselUpdate returnUpdate = new VesselUpdate();
            try
            {
                returnUpdate.vesselID = updateVessel.id.ToString();
                returnUpdate.planetTime = Planetarium.GetUniversalTime();
                returnUpdate.bodyName = updateVessel.mainBody.bodyName;

                returnUpdate.rotation = new float[4];
                returnUpdate.rotation[0] = updateVessel.srfRelRotation.x;
                returnUpdate.rotation[1] = updateVessel.srfRelRotation.y;
                returnUpdate.rotation[2] = updateVessel.srfRelRotation.z;
                returnUpdate.rotation[3] = updateVessel.srfRelRotation.w;

                returnUpdate.angularVelocity = new float[3];
                returnUpdate.angularVelocity[0] = updateVessel.angularVelocity.x;
                returnUpdate.angularVelocity[1] = updateVessel.angularVelocity.y;
                returnUpdate.angularVelocity[2] = updateVessel.angularVelocity.z;
                //Flight state
                returnUpdate.flightState = new FlightCtrlState();
                returnUpdate.flightState.CopyFrom(updateVessel.ctrlState);
                returnUpdate.actiongroupControls = new bool[5];
                returnUpdate.actiongroupControls[0] = updateVessel.ActionGroups[KSPActionGroup.Gear];
                returnUpdate.actiongroupControls[1] = updateVessel.ActionGroups[KSPActionGroup.Light];
                returnUpdate.actiongroupControls[2] = updateVessel.ActionGroups[KSPActionGroup.Brakes];
                returnUpdate.actiongroupControls[3] = updateVessel.ActionGroups[KSPActionGroup.SAS];
                returnUpdate.actiongroupControls[4] = updateVessel.ActionGroups[KSPActionGroup.RCS];

                if (updateVessel.altitude < 10000)
                {
                    //Use surface position under 10k
                    returnUpdate.isSurfaceUpdate = true;
                    returnUpdate.position = new double[4];
                    returnUpdate.position[0] = updateVessel.latitude;
                    returnUpdate.position[1] = updateVessel.longitude;
                    returnUpdate.position[2] = updateVessel.altitude;
                    VesselUtil.DMPRaycastPair groundRaycast = VesselUtil.RaycastGround(updateVessel.latitude, updateVessel.longitude, updateVessel.mainBody);
                    returnUpdate.position[3] = groundRaycast.altitude;
                    returnUpdate.terrainNormal = new float[3];
                    returnUpdate.terrainNormal[0] = groundRaycast.terrainNormal.x;
                    returnUpdate.terrainNormal[1] = groundRaycast.terrainNormal.y;
                    returnUpdate.terrainNormal[2] = groundRaycast.terrainNormal.z;
                    returnUpdate.velocity = new double[3];
                    Vector3d srfVel = Quaternion.Inverse(updateVessel.mainBody.bodyTransform.rotation) * updateVessel.srf_velocity;
                    returnUpdate.velocity[0] = srfVel.x;
                    returnUpdate.velocity[1] = srfVel.y;
                    returnUpdate.velocity[2] = srfVel.z;
                    returnUpdate.acceleration = new double[3];
                    Vector3d srfAcceleration = Quaternion.Inverse(updateVessel.mainBody.bodyTransform.rotation) * updateVessel.acceleration;
                    returnUpdate.acceleration[0] = srfAcceleration.x;
                    returnUpdate.acceleration[1] = srfAcceleration.y;
                    returnUpdate.acceleration[2] = srfAcceleration.z;
                }
                else
                {
                    //Use orbital positioning over 10k
                    returnUpdate.isSurfaceUpdate = false;
                    returnUpdate.orbit = new double[7];
                    returnUpdate.orbit[0] = updateVessel.orbit.inclination;
                    returnUpdate.orbit[1] = updateVessel.orbit.eccentricity;
                    returnUpdate.orbit[2] = updateVessel.orbit.semiMajorAxis;
                    returnUpdate.orbit[3] = updateVessel.orbit.LAN;
                    returnUpdate.orbit[4] = updateVessel.orbit.argumentOfPeriapsis;
                    returnUpdate.orbit[5] = updateVessel.orbit.meanAnomalyAtEpoch;
                    returnUpdate.orbit[6] = updateVessel.orbit.epoch;
                }
            }
            catch (Exception e)
            {
                DarkLog.Debug("Failed to get vessel update, exception: " + e);
                returnUpdate = null;
            }
            return returnUpdate;
        }

        public void Apply()
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
            {
                return;
            }
            //Get updating player
            string updatePlayer = LockSystem.fetch.LockExists(vesselID) ? LockSystem.fetch.LockOwner(vesselID) : "Unknown";
            //Ignore updates to our own vessel if we are in flight and we aren't spectating
            if (!VesselWorker.fetch.isSpectating && (FlightGlobals.fetch.activeVessel != null ? FlightGlobals.fetch.activeVessel.id.ToString() == vesselID : false) && HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                DarkLog.Debug("ApplyVesselUpdate - Ignoring update for active vessel from " + updatePlayer);
                return;
            }
            Vessel updateVessel = FlightGlobals.fetch.vessels.FindLast(v => v.id.ToString() == vesselID);
            if (updateVessel == null)
            {
                //DarkLog.Debug("ApplyVesselUpdate - Got vessel update for " + vesselID + " but vessel does not exist");
                return;
            }
            CelestialBody updateBody = FlightGlobals.Bodies.Find(b => b.bodyName == bodyName);
            if (updateBody == null)
            {
                DarkLog.Debug("ApplyVesselUpdate - updateBody not found");
                return;
            }

            Quaternion normalRotate = Quaternion.identity;
            //Position/Velocity
            bool rotFudge = false;
            Vector3 upAxis = updateBody.GetSurfaceNVector(position[0], position[1]);
            Vector3 startPos = updateVessel.GetWorldPos3D() + Vector3.Scale(upAxis, new Vector3(5f, 5f, 5f));
            if (isSurfaceUpdate)
            {
                //Get the new position/velocity
                double altitudeFudge = 0;
                VesselUtil.DMPRaycastPair dmpRaycast = VesselUtil.RaycastGround(position[0], position[1], updateBody);
                if (dmpRaycast.altitude != -1d && position[3] != -1d)
                {

                    Vector3 theirNormal = new Vector3(terrainNormal[0], terrainNormal[1], terrainNormal[2]);
                    altitudeFudge = dmpRaycast.altitude - position[3];
                    if (Math.Abs(position[2] - position[3]) < 50f)
                    {
                        normalRotate = Quaternion.FromToRotation(theirNormal, dmpRaycast.terrainNormal);
                    }
                    //===DEBUG===
                    if (ourNormalLine == null)
                    {
                        ourNormalLine = new LineRendererDebug(Color.red);
                        theirNormalLine = new LineRendererDebug(Color.blue);
                        crossNormalLine = new LineRendererDebug(Color.black);
                    }
                    if (debugMessage == null)
                    {
                        debugMessage = ScreenMessages.PostScreenMessage("", float.PositiveInfinity, ScreenMessageStyle.UPPER_CENTER);
                    }
                    float outAngle;
                    Vector3 outAxis;
                    normalRotate.ToAngleAxis(out outAngle, out outAxis);
                    debugMessage.message = "Current difference: " + Math.Round(outAngle, 2) + "degrees, ourAlt: " + dmpRaycast.altitude + ", theirAlt: " + position[3];
                    Vector3 vec2 = new Vector3(2f, 2f, 2f);
                    Vector3 crossVector = Vector3.Cross(dmpRaycast.terrainNormal, theirNormal).normalized;
                    ourNormalLine.UpdatePosition(startPos, startPos + Vector3.Scale(updateBody.bodyTransform.rotation * dmpRaycast.terrainNormal, vec2));
                    theirNormalLine.UpdatePosition(startPos, startPos + Vector3.Scale(updateBody.bodyTransform.rotation * theirNormal, vec2));
                    crossNormalLine.UpdatePosition(startPos, startPos + Vector3.Scale(updateBody.bodyTransform.rotation * crossVector, vec2));
                    lastUpdateTime = Time.realtimeSinceStartup;
                    rotFudge = true;
                    //===END DEBUG===
                }

                Vector3d updatePostion = updateBody.GetWorldSurfacePosition(position[0], position[1], position[2] + altitudeFudge);
                Vector3d updateVelocity = updateBody.bodyTransform.rotation * new Vector3d(velocity[0], velocity[1], velocity[2]);
                Vector3d updateAcceleration = updateBody.bodyTransform.rotation * new Vector3d(acceleration[0], acceleration[1], acceleration[2]);
                if (updateVessel.packed)
                {
                    updateVessel.latitude = position[0];
                    updateVessel.longitude = position[1];
                    updateVessel.altitude = position[2] + altitudeFudge;
                    updateVessel.protoVessel.latitude = updateVessel.latitude;
                    updateVessel.protoVessel.longitude = updateVessel.longitude;
                    updateVessel.protoVessel.altitude = updateVessel.altitude;
                    if (!updateVessel.LandedOrSplashed)
                    {
                        //Not landed but under 10km.
                        Vector3d orbitalPos = updatePostion - updateBody.position;
                        Vector3d surfaceOrbitVelDiff = updateBody.getRFrmVel(updatePostion);
                        Vector3d orbitalVel = updateVelocity + surfaceOrbitVelDiff;
                        updateVessel.orbitDriver.orbit.UpdateFromStateVectors(orbitalPos.xzy, orbitalVel.xzy, updateBody, Planetarium.GetUniversalTime());
                        updateVessel.orbitDriver.pos = updateVessel.orbitDriver.orbit.pos.xzy;
                        updateVessel.orbitDriver.vel = updateVessel.orbitDriver.orbit.vel;
                    }
                }
                else
                {
                    double planetariumDifference = Planetarium.GetUniversalTime() - planetTime;
                    Vector3d positionFudge = Vector3d.zero;
                    Vector3d velocityFudge = Vector3d.zero;
                    if (Math.Abs(planetariumDifference) < 3f)
                    {
                        velocityFudge = updateAcceleration * planetariumDifference;
                        //Use the average velocity to determine the new position
                        positionFudge = (updateVelocity + (velocityFudge / 2)) * planetariumDifference;
                    }
                    Vector3d velocityOffset = (updateVelocity + velocityFudge) - updateVessel.srf_velocity;
                    updateVessel.SetPosition(updatePostion + positionFudge, true);
                    updateVessel.ChangeWorldVelocity(velocityOffset);
                }
            }
            else
            {
                Orbit updateOrbit = new Orbit(orbit[0], orbit[1], orbit[2], orbit[3], orbit[4], orbit[5], orbit[6], updateBody);
                updateOrbit.Init();
                updateOrbit.UpdateFromUT(Planetarium.GetUniversalTime());

                if (updateVessel.packed)
                {
                    //The OrbitDriver update call will set the vessel position on the next fixed update
                    VesselUtil.CopyOrbit(updateOrbit, updateVessel.orbitDriver.orbit);
                    updateVessel.orbitDriver.pos = updateVessel.orbitDriver.orbit.pos.xzy;
                    updateVessel.orbitDriver.vel = updateVessel.orbitDriver.orbit.vel;
                }
                else
                {
                    //Vessel.SetPosition is full of fun and games. Avoid at all costs.
                    //Also, It's quite difficult to figure out the world velocity due to Krakensbane, and the reference frame.
                    Vector3d posDelta = updateOrbit.getPositionAtUT(Planetarium.GetUniversalTime()) - updateVessel.orbitDriver.orbit.getPositionAtUT(Planetarium.GetUniversalTime());
                    Vector3d velDelta = updateOrbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy - updateVessel.orbitDriver.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy;
                    //Vector3d velDelta = updateOrbit.vel.xzy - updateVessel.orbitDriver.orbit.vel.xzy;
                    updateVessel.Translate(posDelta);
                    updateVessel.ChangeWorldVelocity(velDelta);
                }
            }

            //Rotation
            Quaternion unfudgedRotation = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
            Quaternion updateRotation = normalRotate * unfudgedRotation;
            updateVessel.SetRotation(updateVessel.mainBody.bodyTransform.rotation * updateRotation);
            if (updateVessel.packed)
            {
                updateVessel.srfRelRotation = updateRotation;
                updateVessel.protoVessel.rotation = updateVessel.srfRelRotation;
            }

            //===DEBUG===
            if (rotFudge)
            {
                if (ourRotDebug == null)
                {
                    ourRotDebug = new QuaternionRendererDebug(Color.yellow);
                    theirRotDebug = new QuaternionRendererDebug(Color.green);
                    identityRotDebug = new QuaternionRendererDebug(Color.white);
                    fudgeRotDebug = new QuaternionRendererDebug(Color.grey);
                }
                ourRotDebug.UpdateRotation(startPos, updateBody.bodyTransform.rotation, unfudgedRotation);
                theirRotDebug.UpdateRotation(startPos, updateBody.bodyTransform.rotation, updateRotation);
                identityRotDebug.UpdateRotation(startPos, updateBody.bodyTransform.rotation, Quaternion.identity);
                fudgeRotDebug.UpdateRotation(startPos, updateBody.bodyTransform.rotation, normalRotate);
            }

            if (ourRotDebug != null && (Time.realtimeSinceStartup - lastUpdateTime) > 5f)
            {
                ourRotDebug.Destroy();
                ourRotDebug = null;
                theirRotDebug.Destroy();
                theirRotDebug = null;
                identityRotDebug.Destroy();
                identityRotDebug = null;
                fudgeRotDebug.Destroy();
                fudgeRotDebug = null;
                ourNormalLine.Destroy();
                ourNormalLine = null;
                theirNormalLine.Destroy();
                theirNormalLine = null;
                crossNormalLine.Destroy();
                crossNormalLine = null;
                debugMessage.duration = float.NegativeInfinity;
                debugMessage = null;
            }
            //===END DEBUG==

            //Angular velocity
            //Vector3 angularVelocity = new Vector3(this.angularVelocity[0], this.angularVelocity[1], this.angularVelocity[2]);
            if (updateVessel.parts != null)
            {
                //Vector3 newAng = updateVessel.ReferenceTransform.rotation * angularVelocity;
                foreach (Part vesselPart in updateVessel.parts)
                {
                    if (vesselPart.rb != null && !vesselPart.rb.isKinematic && vesselPart.State == PartStates.ACTIVE)
                    {
                        //The parts can have different rotations - This transforms them into the root part direction which is where the angular velocity is transferred.
                        //vesselPart.rb.angularVelocity = (Quaternion.Inverse(updateVessel.rootPart.rb.rotation) * vesselPart.rb.rotation) * newAng;
                        vesselPart.rb.angularVelocity = Vector3.zero;
                    }
                }
            }

            //Flight state controls (Throttle etc)
            if (!VesselWorker.fetch.isSpectating)
            {
                updateVessel.ctrlState.CopyFrom(flightState);
            }
            else
            {
                FlightInputHandler.state.CopyFrom(flightState);
            }

            //Action group controls
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.Gear, actiongroupControls[0]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.Light, actiongroupControls[1]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, actiongroupControls[2]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, actiongroupControls[3]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, actiongroupControls[4]);
        }
    }
}


