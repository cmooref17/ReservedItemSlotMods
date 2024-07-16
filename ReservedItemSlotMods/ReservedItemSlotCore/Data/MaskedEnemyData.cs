using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Networking;
using UnityEngine;

namespace ReservedItemSlotCore.Data
{
    [HarmonyPatch]
    public class MaskedEnemyData
    {
        public static Dictionary<MaskedPlayerEnemy, MaskedEnemyData> allMaskedEnemyData = new Dictionary<MaskedPlayerEnemy, MaskedEnemyData>();
        
        public MaskedPlayerEnemy maskedEnemy;
        public BoneMap boneMap = new BoneMap();
        public List<GameObject> equippedReservedItems;

        public PlayerControllerB originallyMimickingPlayer = null;
        

        public MaskedEnemyData(MaskedPlayerEnemy maskedEnemy)
        {
            this.maskedEnemy = maskedEnemy;
            boneMap = new BoneMap();
            boneMap.CreateBoneMap(maskedEnemy.transform);
        }


        public Transform GetBone(PlayerBone eBone) => boneMap?.GetBone(eBone);


        public void DestroyEquippedItems()
        {
            if (equippedReservedItems != null)
            {
                foreach (var item in equippedReservedItems)
                    GameObject.Destroy(item.gameObject);
            }
        }
    }
}
