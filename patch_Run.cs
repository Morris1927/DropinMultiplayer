using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RoR2;
using RoR2.ConVar;
using UnityEngine;
using UnityEngine.Networking;

#pragma warning disable CS0626

namespace RoR2
{
    internal class patch_Run : Run {

        protected extern void orig_Start();

        [Server]
        private extern void orig_SetupUserCharacterMaster(NetworkUser user);

        private bool allowNewParticipants;

        [Server]
        private void SetupUserCharacterMaster(NetworkUser user) {
            orig_SetupUserCharacterMaster(user);

            int averageItemCountT1 = 0;
            int averageItemCountT2 = 0;
            int averageItemCountT3 = 0;
            ReadOnlyCollection<NetworkUser> readOnlyInstancesList = NetworkUser.readOnlyInstancesList;

            int playerCount = PlayerCharacterMasterController.instances.Count;

            if (playerCount <= 1)
                return;
            else
                playerCount--;

            for (int i = 0; i < readOnlyInstancesList.Count; i++) {
                if (readOnlyInstancesList[i].id.Equals(user.id))
                    continue;
                CharacterMaster cm = GetUserMaster(readOnlyInstancesList[i].id);
                averageItemCountT1 += cm.inventory.GetTotalItemCountOfTier(ItemTier.Tier1);
                averageItemCountT2 += cm.inventory.GetTotalItemCountOfTier(ItemTier.Tier2);
                averageItemCountT3 += cm.inventory.GetTotalItemCountOfTier(ItemTier.Tier3);
            }

            averageItemCountT1 /= playerCount;
            averageItemCountT2 /= playerCount;
            averageItemCountT3 /= playerCount;

            CharacterMaster characterMaster = this.GetUserMaster(user.id);
            int itemCountT1 = averageItemCountT1 - characterMaster.inventory.GetTotalItemCountOfTier(ItemTier.Tier1);
            int itemCountT2 = averageItemCountT2 - characterMaster.inventory.GetTotalItemCountOfTier(ItemTier.Tier2);
            int itemCountT3 = averageItemCountT3 - characterMaster.inventory.GetTotalItemCountOfTier(ItemTier.Tier3);


            itemCountT1 = itemCountT1 < 0 ? 0 : itemCountT1;
            itemCountT2 = itemCountT2 < 0 ? 0 : itemCountT2;
            itemCountT3 = itemCountT3 < 0 ? 0 : itemCountT3;
            Debug.Log(itemCountT1 + " " + itemCountT2 + " " + itemCountT3 + " itemcount to add");
            Debug.Log(averageItemCountT1 + " " + averageItemCountT2 + " " + averageItemCountT3 + " average");
            for (int i = 0; i < itemCountT1; i++) {
                characterMaster.inventory.GiveItem(GetRandomItem(ItemCatalog.tier1ItemList), 1);
            }
            for (int i = 0; i < itemCountT2; i++) {
                characterMaster.inventory.GiveItem(GetRandomItem(ItemCatalog.tier2ItemList), 1);
            }
            for (int i = 0; i < itemCountT3; i++) {
                characterMaster.inventory.GiveItem(GetRandomItem(ItemCatalog.tier3ItemList), 1);
            }
        }

        private ItemIndex GetRandomItem(List<ItemIndex> items) {
            int itemID = UnityEngine.Random.Range(0, items.Count);

            return items[itemID];
        }

        protected void Start() {

            orig_Start();
            allowNewParticipants = true;
        }


        [ConCommand(commandName = "spawnas", flags = ConVarFlags.ExecuteOnServer, helpText = "Spawn as a new character. Type body_list for a full list of characters")]
        private static void CCSpawnAs(ConCommandArgs args) {
            string newValue = args[0];
            string playerID = "";
            int result = 0;
            NetworkUser player = null;

            if (args.Count == 2) {
                playerID = args[1];
            }

            if(playerID != "") {
                if (int.TryParse(playerID, out result)) {
                    player = NetworkUser.readOnlyInstancesList[result];
                } else {
                    foreach (NetworkUser n in NetworkUser.readOnlyInstancesList) {
                        if (n.userName.Equals(playerID, StringComparison.CurrentCultureIgnoreCase)) {
                            player = n;
                        }
                    }
                }
            }

            CharacterMaster master;// = player != null ? player.master : args.sender.master;
            if (player != null) {
                master = player.master;
            } else {
                master = args.sender.master;
            }
            GameObject newBody = BodyCatalog.FindBodyPrefab(newValue);

            if (newBody == null) {
                Debug.Log(args.sender.userName + " could not spawn as " + newValue);
                return;
            }
            master.bodyPrefab = newBody;
            Debug.Log(args.sender.userName + " is respawning as " + newValue);

            master.Respawn(master.GetBody().transform.position, master.GetBody().transform.rotation);
        }

        [ConCommand(commandName = "player_list", flags = ConVarFlags.ExecuteOnServer, helpText = "Shows list of players with their ID")]
        private static void CCPlayerList(ConCommandArgs args) {
            NetworkUser n;
            for (int i = 0; i < NetworkUser.readOnlyInstancesList.Count; i++) {
                n = NetworkUser.readOnlyInstancesList[i];
                Debug.Log(i + ": " + n.userName);
            }
        }
    }
}
