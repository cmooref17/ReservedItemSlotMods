using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReservedItemSlotCore.Data
{
    public class BoneMap
    {
        public Transform spine0 { get { return boneMap != null ? boneMap[0] : null; } }
        public Transform spine1 { get { return boneMap != null ? boneMap[1] : null; } }
        public Transform spine2 { get { return boneMap != null ? boneMap[2] : null; } }
        public Transform spine3 { get { return boneMap != null ? boneMap[3] : null; } }

        public Transform neck { get { return boneMap != null ? boneMap[4] : null; } }
        public Transform head { get { return boneMap != null ? boneMap[5] : null; } }

        public Transform shoulderL { get { return boneMap != null ? boneMap[6] : null; } }
        public Transform armUpperL { get { return boneMap != null ? boneMap[7] : null; } }
        public Transform armLowerL { get { return boneMap != null ? boneMap[8] : null; } }
        public Transform handL { get { return boneMap != null ? boneMap[9] : null; } }

        public Transform shoulderR { get { return boneMap != null ? boneMap[10] : null; } }
        public Transform armUpperR { get { return boneMap != null ? boneMap[11] : null; } }
        public Transform armLowerR { get { return boneMap != null ? boneMap[12] : null; } }
        public Transform handR { get { return boneMap != null ? boneMap[13] : null; } }

        public Transform thighL { get { return boneMap != null ? boneMap[14] : null; } }
        public Transform calfL { get { return boneMap != null ? boneMap[15] : null; } }
        public Transform footL { get { return boneMap != null ? boneMap[16] : null; } }
        public Transform heelL { get { return boneMap != null ? boneMap[17] : null; } }
        public Transform toeL { get { return boneMap != null ? boneMap[18] : null; } }

        public Transform thighR { get { return boneMap != null ? boneMap[19] : null; } }
        public Transform calfR { get { return boneMap != null ? boneMap[20] : null; } }
        public Transform footR { get { return boneMap != null ? boneMap[21] : null; } }
        public Transform heelR { get { return boneMap != null ? boneMap[22] : null; } }
        public Transform toeR { get { return boneMap != null ? boneMap[23] : null; } }

        Transform[] boneMap;


        static List<string> defaultBoneNames = new List<string>
        {
            "spine", "spine.001", "spine.002", "spine.003",
            "spine.004", "spine.004", // neck, head
            "shoulder.L", "armUpper.L", "armLower.L", "hand.L",
            "shoulder.R", "armUpper.R", "armLower.R", "hand.R",
            "thigh.L", "calf.L", "foot.L", "heel.02.L", "toe.L",
            "thigh.R", "calf.R", "foot.R", "heel.02.R", "toe.R"
        };

        public List<string> boneNames;


        public void CreateBoneMap(Transform rootBone, List<string> overrideBoneNames = null)
        {
            if (rootBone == null)
                return;

            boneNames = overrideBoneNames != null ? overrideBoneNames : defaultBoneNames;
            if (boneNames.Count != defaultBoneNames.Count)
            {
                Plugin.LogWarning("Failed to create bonemap. Passed in custom bone names list with unsupported size. Size must be: " + defaultBoneNames.Count + ". Custom name list size: " + boneNames.Count);
                return;
            }

            boneMap = new Transform[] { spine0, spine1, spine2, spine3, neck, head, shoulderL, armUpperL, armLowerL, handL, shoulderR, armUpperR, armLowerR, handR, thighL, calfL, footL, heelL, toeL, thighR, calfR, footR, heelR, toeR };
            Debug.Assert(boneMap.Length == boneNames.Count);
            MapBoneRecursive(rootBone);
        }

        public void MapBoneRecursive(Transform bone)
        {
            int boneIndex = boneNames.IndexOf(bone.name);
            if (boneIndex >= 0 && boneIndex < boneMap.Length && boneMap[boneIndex] == null)
                boneMap[boneIndex] = bone;

            for (int i = 0; i < bone.childCount; i++)
                MapBoneRecursive(bone.GetChild(i));
        }

        public Transform GetBone(PlayerBone eBone)
        {
            if (boneMap == null)
                return null;

            int index = ((int)eBone) - 1;
            return index >= 0 && index < boneMap.Length ? boneMap[index] : null;
        }
    }

    public enum PlayerBone
    {
        None,
        Hips,
        Spine1,
        Spine2,
        Spine3,
        Neck,
        Head,
        LeftShoulder,
        LeftArmUpper,
        LeftArmLower,
        LeftHand,
        RightShoulder,
        RightArmUpper,
        RightArmLower,
        RightHand,
        LeftThigh,
        LeftCalf,
        LeftFoot,
        LeftHeel,
        LeftToe,
        RightThigh,
        RightCalf,
        RightFoot,
        RightHeel,
        RightToe
    }
}
