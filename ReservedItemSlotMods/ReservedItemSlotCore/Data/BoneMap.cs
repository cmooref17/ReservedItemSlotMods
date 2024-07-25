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
        internal Transform spine0 { get { return boneArray != null ? boneArray[0] : null; } }
        internal Transform spine1 { get { return boneArray != null ? boneArray[1] : null; } }
        internal Transform spine2 { get { return boneArray != null ? boneArray[2] : null; } }
        internal Transform spine3 { get { return boneArray != null ? boneArray[3] : null; } }
        
        internal Transform neck { get { return boneArray != null ? boneArray[4] : null; } }
        internal Transform head { get { return boneArray != null ? boneArray[5] : null; } }

        internal Transform shoulderL { get { return boneArray != null ? boneArray[6] : null; } }
        internal Transform armUpperL { get { return boneArray != null ? boneArray[7] : null; } }
        internal Transform armLowerL { get { return boneArray != null ? boneArray[8] : null; } }
        internal Transform handL { get { return boneArray != null ? boneArray[9] : null; } }

        internal Transform shoulderR { get { return boneArray != null ? boneArray[10] : null; } }
        internal Transform armUpperR { get { return boneArray != null ? boneArray[11] : null; } }
        internal Transform armLowerR { get { return boneArray != null ? boneArray[12] : null; } }
        internal Transform handR { get { return boneArray != null ? boneArray[13] : null; } }

        internal Transform thighL { get { return boneArray != null ? boneArray[14] : null; } }
        internal Transform calfL { get { return boneArray != null ? boneArray[15] : null; } }
        internal Transform footL { get { return boneArray != null ? boneArray[16] : null; } }
        internal Transform heelL { get { return boneArray != null ? boneArray[17] : null; } }
        internal Transform toeL { get { return boneArray != null ? boneArray[18] : null; } }
        
        internal Transform thighR { get { return boneArray != null ? boneArray[19] : null; } }
        internal Transform calfR { get { return boneArray != null ? boneArray[20] : null; } }
        internal Transform footR { get { return boneArray != null ? boneArray[21] : null; } }
        internal Transform heelR { get { return boneArray != null ? boneArray[22] : null; } }
        internal Transform toeR { get { return boneArray != null ? boneArray[23] : null; } }

        Transform[] boneArray;


        private static List<string> defaultBoneNames = new List<string>
        {
            "spine", "spine.001", "spine.002", "spine.003",
            "spine.004", "spine.004", // neck, head
            "shoulder.L", "arm.L_upper", "arm.L_lower", "hand.L",
            "shoulder.R", "arm.R_upper", "arm.R_lower", "hand.R",
            "thigh.L", "shin.L", "foot.L", "heel.02.L", "toe.L",
            "thigh.R", "shin.R", "foot.R", "heel.02.R", "toe.R"
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

            boneArray = new Transform[] { spine0, spine1, spine2, spine3, neck, head, shoulderL, armUpperL, armLowerL, handL, shoulderR, armUpperR, armLowerR, handR, thighL, calfL, footL, heelL, toeL, thighR, calfR, footR, heelR, toeR };
            Debug.Assert(boneArray.Length == boneNames.Count);
            MapBoneRecursive(rootBone);
        }

        private void MapBoneRecursive(Transform bone)
        {
            IEnumerable<int> indices = boneNames
                .Select((name, index) => bone.name == name ? index : -1);

            foreach ( int boneIndex in indices )
            {
            if (boneIndex >= 0 && boneIndex < boneArray.Length && boneArray[boneIndex] == null)
                boneArray[boneIndex] = bone;
			}

            for (int i = 0; i < bone.childCount; i++)
                MapBoneRecursive(bone.GetChild(i));
        }

        public Transform GetBone(PlayerBone eBone)
        {
            if (boneArray == null)
                return null;

            int index = ((int)eBone) - 1;
            return index >= 0 && index < boneArray.Length ? boneArray[index] : null;
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
