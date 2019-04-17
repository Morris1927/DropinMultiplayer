using System;
using BepInEx;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RoR2;
using UnityEngine;
using MonoMod.Cil;
using System.Linq;
using System.Collections;
using System.Reflection;
using Mono.Cecil.Cil;

namespace DropInMultiplayer
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("dev.morris1927.ror2.DropInMultiplayer", "DropInMultiplayer", "2.0.0")]
    public class DropInMultiplayer : BaseUnityPlugin {

        public void Awake() {

            On.RoR2.Console.Awake += (orig, self) => {
                CommandHelper.RegisterCommands(self);
                orig(self);
            };

            IL.RoR2.Run.Start += il => {
                var c = new ILCursor(il);
                c.GotoNext(x => x.MatchStfld("RoR2.Run", "allowNewParticipants"));
                c.GotoNext(x => x.MatchStfld("RoR2.Run", "allowNewParticipants"));
                c.EmitDelegate<Func<bool, bool>>((b) => { return true; });
            };

            On.RoR2.Run.SetupUserCharacterMaster += SetupUserCharacterMaster;
            On.RoR2.Chat.UserChatMessage.ConstructChatString += UserChatMessage_ConstructChatString;
        }

        private string UserChatMessage_ConstructChatString(On.RoR2.Chat.UserChatMessage.orig_ConstructChatString orig, Chat.UserChatMessage self) {
            List<string> split = new List<string>(self.text.Split(Char.Parse(" ")));
            string commandName = ArgsHelper.GetValue(split, 0);
            split.RemoveAt(0);
            if (commandName.Equals("spawn_as", StringComparison.CurrentCultureIgnoreCase)) {


                string bodyString = ArgsHelper.GetValue(split, 1);
                string userString = ArgsHelper.GetValue(split, 2);

                switch (split.Count) {
                    case 2:
                        bodyString = split[0];
                        userString = split[1];
                        break;
                    case 1:
                        bodyString = split[0];
                        break;
                    default:
                        Debug.Log("Incorrect arguments");
                        return orig(self);
                }
                SpawnAs(self.sender.GetComponent<NetworkUser>().master, bodyString, userString);
            }
            return orig(self);
        }

        private static void SpawnAs(CharacterMaster sender, string bodyString, string userString) {
            bodyString = bodyString.Replace("Master", "");
            bodyString = bodyString.Replace("Body", "");
            bodyString = bodyString + "Body";

            NetworkUser player = GetNetUserFromString(userString);
            CharacterMaster master = player != null ? player.master : sender;

            if (!master.alive) {
                Debug.Log("Player is dead and cannot respawn.");
                return;
            }

            GameObject bodyPrefab = BodyCatalog.FindBodyPrefab(bodyString);

            if (bodyPrefab == null) {
                List<string> array = new List<string>();
                foreach (var item in BodyCatalog.allBodyPrefabs) {
                    array.Add(item.name);
                }
                string list = string.Join("\n", array);
                Debug.LogFormat("Could not spawn as {0}, Try: spawn_as GolemBody   --- \n{1}", bodyString, list);
                return;
            }
            master.bodyPrefab = bodyPrefab;
            Debug.Log(master.GetBody().GetUserName() + " is spawning as " + bodyString);

            master.Respawn(master.GetBody().transform.position, master.GetBody().transform.rotation);
        }

        private static NetworkUser GetNetUserFromString(string playerString) {
            int result = 0;
            if (playerString != "") {
                if (int.TryParse(playerString, out result)) {
                    if (result < NetworkUser.readOnlyInstancesList.Count && result >= 0) {

                        return NetworkUser.readOnlyInstancesList[result];
                    }
                    Debug.Log("Specified player index does not exist");
                    return null;
                } else {
                    foreach (NetworkUser n in NetworkUser.readOnlyInstancesList) {
                        if (n.userName.Equals(playerString, StringComparison.CurrentCultureIgnoreCase)) {
                            return n;
                        }
                    }
                    Debug.Log("Specified player does not exist");
                    return null;
                }
            }

            return null;
        }


        private void SetupUserCharacterMaster(On.RoR2.Run.orig_SetupUserCharacterMaster orig, Run self, NetworkUser user) {
            orig(self, user);

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
                CharacterMaster cm = readOnlyInstancesList[i].master;
                averageItemCountT1 += cm.inventory.GetTotalItemCountOfTier(ItemTier.Tier1);
                averageItemCountT2 += cm.inventory.GetTotalItemCountOfTier(ItemTier.Tier2);
                averageItemCountT3 += cm.inventory.GetTotalItemCountOfTier(ItemTier.Tier3);
            }

            averageItemCountT1 /= playerCount;
            averageItemCountT2 /= playerCount;
            averageItemCountT3 /= playerCount;

            CharacterMaster characterMaster = user.master;
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




        [ConCommand(commandName = "spawn_as", flags = ConVarFlags.ExecuteOnServer, helpText = "Spawn as a new character. Type body_list for a full list of characters")]
        private static void CCSpawnAs(ConCommandArgs args) {
            if (args.Count == 0) {
                return;
            }

            string bodyString = ArgsHelper.GetValue(args.userArgs, 0);
            string playerString = ArgsHelper.GetValue(args.userArgs, 1);

            SpawnAs(args.sender.master, bodyString, playerString);
            
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


    public class ArgsHelper {

        public static string GetValue(List<string> args, int index) {
            if (index < args.Count && index >= 0) {
                return args[index];
            }

            return "";
        }
    }


    //CommandHelper written by Wildbook
    public class CommandHelper {
        public static void RegisterCommands(RoR2.Console self) {
            var types = typeof(CommandHelper).Assembly.GetTypes();
            var catalog = self.GetFieldValue<IDictionary>("concommandCatalog");

            foreach (var methodInfo in types.SelectMany(x => x.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))) {
                var customAttributes = methodInfo.GetCustomAttributes(false);
                foreach (var attribute in customAttributes.OfType<ConCommandAttribute>()) {
                    var conCommand = Reflection.GetNestedType<RoR2.Console>("ConCommand").Instantiate();

                    conCommand.SetFieldValue("flags", attribute.flags);
                    conCommand.SetFieldValue("helpText", attribute.helpText);
                    conCommand.SetFieldValue("action", (RoR2.Console.ConCommandDelegate)Delegate.CreateDelegate(typeof(RoR2.Console.ConCommandDelegate), methodInfo));

                    catalog[attribute.commandName.ToLower()] = conCommand;
                }
            }
        }
    }

}
