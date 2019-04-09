using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        protected extern void orig_GenerateStageRNG();

        private static extern void orig_PopulateValidStages();

        private bool allowNewParticipants;

        public static event Action<Run> onRunStartGlobal;

        private static readonly StringConVar cvRunSceneOverride;

        private void GenerateStageRNG() {
            orig_GenerateStageRNG();

        }

        private static void PopulateValidStages() {
            orig_PopulateValidStages();
        }

        [Server]
        private void SetupUserCharacterMaster(NetworkUser user) {
            orig_SetupUserCharacterMaster(user);

            int averageItemCountT1 = 0;
            int averageItemCountT2 = 0;
            int averageItemCountT3 = 0;
            ReadOnlyCollection<NetworkUser> readOnlyInstancesList = NetworkUser.readOnlyInstancesList;

            int playerCount = readOnlyInstancesList.Count;

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
            if (NetworkServer.active) {
                this.OverrideSeed();
                this.runRNG = new Xoroshiro128Plus(this.seed);
                this.nextStageRng = new Xoroshiro128Plus(this.runRNG.nextUlong);
                this.stageRngGenerator = new Xoroshiro128Plus(this.runRNG.nextUlong);
                GenerateStageRNG();
                orig_PopulateValidStages();
            }
            allowNewParticipants = true;
            UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
            ReadOnlyCollection<NetworkUser> readOnlyInstancesList = NetworkUser.readOnlyInstancesList;
            for (int i = 0; i < readOnlyInstancesList.Count; i++) {
                this.OnUserAdded(readOnlyInstancesList[i]);
            }
            if (NetworkServer.active) {
                SceneField[] choices = this.startingScenes;
                string @string = cvRunSceneOverride.GetString();
                if (@string != "") {
                    choices = new SceneField[]
                    {
                        new SceneField(@string)
                    };
                }
                this.PickNextStageScene(choices);
                NetworkManager.singleton.ServerChangeScene(this.nextStageScene);
            }
            this.BuildUnlockAvailability();
            this.BuildDropTable();
            Action<Run> action = patch_Run.onRunStartGlobal;
            if (action == null) {
                return;
            }
            action(this);

        }


        [ConCommand(commandName = "spawnas", flags = ConVarFlags.ExecuteOnServer, helpText = "Spawn as a new character. Type body_list for a full list of characters")]
        private static void CCSpawnAs(ConCommandArgs args) {
            string newValue = args[0];
            CharacterMaster master = args.sender.master;
            GameObject newBody = BodyCatalog.FindBodyPrefab(newValue);

            if (newBody == null) {
                Debug.Log(args.sender.name + " could not spawn as " + newValue);
                return;
            }
            master.bodyPrefab = newBody;
            Debug.Log(args.sender.name + " is respawning as " + newValue);

            master.Respawn(master.GetBody().transform.position, master.GetBody().transform.rotation);
        }


    }
}
