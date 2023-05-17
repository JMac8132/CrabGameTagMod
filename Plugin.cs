using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using UnityEngine;
using SteamworksNative;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GameLoop = MonoBehaviourPublicObInLi1GagasmLi1GaUnique;
using PlayerMovement = MonoBehaviourPublicGaplfoGaTrorplTrRiBoUnique;
using SteamManager = MonoBehaviourPublicObInUIgaStCSBoStcuCSUnique;
using GameManager = MonoBehaviourPublicDi2UIObacspDi2UIObUnique;
using LobbyManager = MonoBehaviourPublicCSDi2UIInstObUIloDiUnique;
using ServerSend = MonoBehaviourPublicInInUnique;
using PlayerManager = MonoBehaviourPublicCSstReshTrheObplBojuUnique;
using GameModeTag = GameModePublicLi1UIUnique;
using GameServer = MonoBehaviourPublicObInCoIE85SiAwVoFoCoUnique;
using GameUI = MonoBehaviourPublicGaroloGaObInCacachGaUnique;
using LobbySettings = MonoBehaviourPublicObjomaOblogaTMObseprUnique;
using Chatbox = MonoBehaviourPublicRaovTMinTemeColoonCoUnique;
using ModesAndMaps = MonoBehaviourPublicGamomaGaTrmoTrmaUnique;
using GameModeManager = MonoBehaviourPublicGadealGaLi1pralObInUnique;
using MapManager = MonoBehaviourPublicObInMamaLi1plMadeMaUnique;
using ServerConfig = ObjectPublicInSiInInInInInInInInUnique;
using Client = ObjectPublicBoInBoCSItBoInSiBySiUnique;
using ServerHandle = MonoBehaviourPublicPlVoUI9GaVoUI9UsPlUnique;
using KillPlayerOutOfBounds = MonoBehaviourPublicSikiUnique;

namespace TagMod
{
    public struct MapInfo
    {
        public string name;
        public int id;
        public int minPlayers;
        public int maxPlayers;
        public int roundTime;
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, "1.4.0")]
    public class Plugin : BasePlugin
    {

        public static float gameTimer;
        public static int gameState;
        public static int prevGameState;
        public static bool canStartGame = true;
        public static bool canEndGame = true;
        public static bool canAfkCheck = true;
        public static bool canRespawnPlayers = true;
        public static bool toggleAutoStart = true;
        public static bool toggleAfk = false;
        public static bool toggleSnowballs = false;
        public static int prevMapID = 0;
        public static List<ulong> taggedPlayers = new();
        public static List<ulong> nonTaggedPlayers = new();
        public static List<ulong> alivePlayers = new();
        public static string lastServerMessage;
        public static float afkTimer = 0;
        public static List<ulong> afkPlayers = new();
        public static Dictionary<ulong, Vector3> playerRotations = new();
        public static Dictionary<int, MapInfo> mapDictionary = new();
        public static int randomMapID = 0;

        public override void Load()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Log.LogInfo("Mod created by JMac");
        }

        public static void InitializeMapData()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "BepInEx", "plugins", "TagModConfig.txt");

            if (!File.Exists(path))
            {
                Debug.Log("Trying To Create File");

                Dictionary<int, (int min, int max)> defaultMapSettings = new()
                {
                    { 3, (0, 5) },
                    { 28, (0, 5) },
                    { 35, (0, 5) },
                    { 36, (0, 5) },
                    { 37, (0, 5) },
                    { 38, (0, 5) },
                    { 39, (0, 5) },
                    { 40, (0, 5) },
                };

                using (StreamWriter writer = new(path))
                {
                    Map[] mapArray = MapManager.Instance.maps;
                    foreach (var map in mapArray)
                    {
                        int id = map.id;
                        (int min, int max) = defaultMapSettings.GetValueOrDefault(id, (5, 40));

                        MapInfo mapInfo = new()
                        {
                            name = map.mapName,
                            id = id,
                            minPlayers = min,
                            maxPlayers = max,
                            roundTime = 50,
                        };
                        mapDictionary.Add(id, mapInfo);

                        writer.WriteLine($"Map Name = {map.mapName}; Map ID = {map.id}; Min Players = {mapInfo.minPlayers}; Max Players = {mapInfo.maxPlayers}; Round Time = {mapInfo.roundTime};");
                        writer.WriteLine();
                    }
                }
                Debug.Log("File Created");
            }
            else
            {
                Debug.Log("Trying To Read File");
                StreamReader reader = new(path);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = Regex.Match(line, @"Map Name = (?<name>[^;]+); Map ID = (?<id>\d+); Min Players = (?<min>\d+); Max Players = (?<max>\d+); Round Time = (?<round>\d+);");
                    if (match.Success)
                    {
                        string mapName = match.Groups["name"].Value;
                        int id = int.Parse(match.Groups["id"].Value);
                        int minPlayers = int.Parse(match.Groups["min"].Value);
                        int maxPlayers = int.Parse(match.Groups["max"].Value);
                        int roundTime = int.Parse(match.Groups["round"].Value);

                        MapInfo mapInfo = new()
                        {
                            name = mapName,
                            id = id,
                            minPlayers = minPlayers,
                            maxPlayers = maxPlayers,
                            roundTime = roundTime,
                        };
                        mapDictionary.Add(id, mapInfo);
                    }
                }
                Debug.Log("File Read");
            }
        }

        public static void CheckGameState()
        {
            int lobbyManagerState = 0;
            int gameManagerState = 0;

            if (LobbyManager.Instance) lobbyManagerState = (int)LobbyManager.Instance.state;
            if (GameManager.Instance) gameManagerState = (int)GameManager.Instance.gameMode.modeState;

            if (lobbyManagerState == 0 && gameState != 0) //MainMenu
            {
                gameState = 0;
                prevGameState = 0;
                lastServerMessage = "";
            }
            else if (lobbyManagerState == 2 && gameManagerState == 0 && GetModeID() == 0 && gameState != 1) //Lobby
            {
                gameState = 1;
                prevGameState = 1;
            }
            else if (lobbyManagerState == 1 && alivePlayers.Count == 0 && gameState != 2) //Loading
            {
                gameState = 2;
                prevGameState = 2;
            }
            else if (lobbyManagerState == 2 && gameManagerState == 0 && GetModeID() != 0 && prevGameState != 1 && gameState != 3) //Frozen
            {
                gameState = 3;
                prevGameState = 3;
            }
            else if ((lobbyManagerState == 4 || lobbyManagerState == 2) && gameManagerState == 1 && gameState != 4) //Playing
            {
                gameState = 4;
                prevGameState = 4;
            }
            else if (lobbyManagerState == 2 && gameManagerState == 2 && gameState != 5) //Ended
            {
                gameState = 5;
                prevGameState = 5;
            }
            else if (lobbyManagerState == 4 && (gameManagerState == 2 || gameManagerState == 3) && gameState != 6) //GameOver
            {
                gameState = 6;
                prevGameState = 6;
            }

            //Debug.Log(gameStateInt.ToString());
        }

        public static int GetMapID()
        {
            return LobbyManager.Instance.map.id;
        }

        public static int GetModeID()
        {
            return LobbyManager.Instance.gameMode.id;
        }

        public static int GetPlayerCount()
        {
            return GameManager.Instance.activePlayers.Count + GameManager.Instance.spectators.Count;
        }

        public static ulong GetMyID()
        {
            return SteamManager.Instance.field_Private_CSteamID_0.m_SteamID;
        }

        public static ulong GetHostID()
        {
            return SteamManager.Instance.field_Private_CSteamID_1.m_SteamID;
        }

        public static Rigidbody GetPlayerRigidBody(ulong player)
        {
            if (player == GetMyID()) return PlayerMovement.prop_MonoBehaviourPublicGaplfoGaTrorplTrRiBoUnique_0.GetRb();
            else return GameManager.Instance.activePlayers[player].prop_MonoBehaviourPublicObVeSiVeRiSiAnVeanTrUnique_0.field_Private_Rigidbody_0;
        }

        public static Vector3 GetPlayerRotation(ulong id)
        {
            if (id == GetMyID()) return PlayerInput.Instance.cameraRot;
            else return new Vector3(GameManager.Instance.activePlayers[id].field_Private_MonoBehaviourPublicObVeSiVeRiSiAnVeanTrUnique_0.xRot, GameManager.Instance.activePlayers[id].field_Private_MonoBehaviourPublicObVeSiVeRiSiAnVeanTrUnique_0.yRot, 0f);
        }

        public static List<ulong> GetAlivePlayers()
        {
            List<ulong> list = new();
            foreach (var player in GameManager.Instance.activePlayers)
            {
                if (player == null || player.Value.dead) continue;
                list.Add(player.Key);
            }
            return list;
        }

        public static bool IsHost()
        {
            return SteamManager.Instance.IsLobbyOwner() && !LobbyManager.Instance.Method_Public_Boolean_0();
        }

        public static void BoundsCheck(ulong id)
        {
            float maxDistance = 0f;

            switch (prevMapID)
            {
                case 0:
                    maxDistance = 100;
                    break;
                case 1:
                    maxDistance = 110;
                    break;
                case 2:
                    maxDistance = 90;
                    break;
                case 3:
                    maxDistance = 100;
                    break;
                case 5:
                    maxDistance = 200;
                    break;
                case 6:
                    maxDistance = 100;
                    break;
                case 7:
                    maxDistance = 100;
                    break;
                case 8:
                    maxDistance = 160;
                    break;
                case 9:
                    maxDistance = 100;
                    break;
                case 10:
                    maxDistance = 80;
                    break;
                case 11:
                    maxDistance = 170;
                    break;
                case 13:
                    maxDistance = 170;
                    break;
                case 14:
                    maxDistance = 80;
                    break;
                case 15:
                    maxDistance = 90;
                    break;
                case 17:
                    maxDistance = 210;
                    break;
                case 18:
                    maxDistance = 70;
                    break;
                case 19:
                    maxDistance = 70;
                    break;
                case 20:
                    maxDistance = 100;
                    break;
                case 21:
                    maxDistance = 80;
                    break;
                case 22:
                    maxDistance = 200;
                    break;
                case 23:
                    maxDistance = 70;
                    break;
                case 24:
                    maxDistance = 80;
                    break;
                case 25:
                    maxDistance = 70;
                    break;
                case 26:
                    maxDistance = 125;
                    break;
                case 27:
                    maxDistance = 125;
                    break;
                case 28:
                    maxDistance = 50;
                    break;
                case 29:
                    maxDistance = 150;
                    break;
                case 30:
                    maxDistance = 70;
                    break;
                case 31:
                    maxDistance = 70;
                    break;
                case 32:
                    maxDistance = 70;
                    break;
                case 33:
                    maxDistance = 150;
                    break;
                case 34:
                    maxDistance = 80;
                    break;
                case 35:
                    maxDistance = 80;
                    break;
                case 36:
                    maxDistance = 100;
                    break;
                case 37:
                    maxDistance = 60;
                    break;
                case 38:
                    maxDistance = 60;
                    break;
                case 39:
                    maxDistance = 60;
                    break;
                case 40:
                    maxDistance = 60;
                    break;
                case 41:
                    maxDistance = 80;
                    break;
                case 42:
                    maxDistance = 80;
                    break;
                case 43:
                    maxDistance = 80;
                    break;
                case 44:
                    maxDistance = 80;
                    break;
                case 45:
                    maxDistance = 170;
                    break;
                case 46:
                    maxDistance = 130;
                    break;
                case 47:
                    maxDistance = 100;
                    break;
                case 48:
                    maxDistance = 100;
                    break;
                case 49:
                    maxDistance = 100;
                    break;
                case 50:
                    maxDistance = 100;
                    break;
                case 51:
                    maxDistance = 100;
                    break;
                case 52:
                    maxDistance = 120;
                    break;
                case 53:
                    maxDistance = 90;
                    break;
                case 54:
                    maxDistance = 220;
                    break;
                case 55:
                    maxDistance = 190;
                    break;
                case 56:
                    maxDistance = 210;
                    break;
                case 57:
                    maxDistance = 700;
                    break;
                case 59:
                    maxDistance = 80;
                    break;
                case 60:
                    maxDistance = 80;
                    break;
                case 61:
                    maxDistance = 80;
                    break;
            }

            if (Vector3.Distance(GetPlayerRigidBody(id).position, Vector3.zero) > maxDistance)
            {
                GameServer.PlayerDied(id, 1, Vector3.zero);
                //ServerSend.RespawnPlayer(id, Vector3.zero);
            }

            /*if (GetPlayerRigidBody(id).position.y <= -100)
            {
                ServerSend.RespawnPlayer(id, Vector3.zero);
            }*/
        }

        public static void GlitchingCheck(ulong id)
        {
            if (prevMapID == 3)// Big Color Climb
            {
                if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(9.4f, -25.1f, -9.4f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(13.0f, -25.2f, -7.4f));
                }
                else if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(-9.4f, -28.1f, 9.4f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(-10.6f, -22.1f, 10.6f));
                }
                else if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(-9.4f, -28.1f, -9.4f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(-9f, -28.1f, -13f));
                }
            }
            else if (prevMapID == 29)// Snow Top
            {
                if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(10.4f, 69.9f, -6.4f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(14.8f, 69.9f, -5.1f));
                }
                else if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(-40.6f, 59.9f, 21.5f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(-44.8f, 59.9f, 21.4f));
                }
                else if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(54.5f, 79.0f, 14.6f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(56.0f, 68.7f, 15.0f));
                }
            }
            else if (prevMapID == 36)// Small Beach
            {
                if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(20.8f, -1.1f, -15.8f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(19.2f, -1.1f, -17.3f));
                }
                else if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(-10.6f, -4.1f, 14.4f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(-14.4f, -4.1f, 14.4f));
                }
            }
            else if (prevMapID == 0)// Bitter Beach
            {
                if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(25.3f, -1.1f, -20.8f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(23.2f, -1.1f, -22.2f));
                }
                else if (Vector3.Distance(GetPlayerRigidBody(id).position, new Vector3(-6.6f, -4.1f, 9.4f)) < 1f)
                {
                    ServerSend.RespawnPlayer(id, new Vector3(-11.3f, -4.1f, 9.4f));
                }
            }
        }

        public static void ChangeMap()
        {
            taggedPlayers.Clear();
            nonTaggedPlayers.Clear();
            afkPlayers.Clear();
            playerRotations.Clear();
            GameLoop.Instance.ResetAllInventories();

            List<int> tempMapIDs = new();
            randomMapID = 0;

            int nextRoundPlayerCount;

            if (alivePlayers.Count <= 1)
            {
                canRespawnPlayers = true;
                nextRoundPlayerCount = GetPlayerCount();
                if (toggleAfk) nextRoundPlayerCount--;
            }
            else
            {
                nextRoundPlayerCount = alivePlayers.Count;
            }

            alivePlayers.Clear();

            if (MapManager.Instance.playableMaps.Count > 0)
            {
                foreach (var map in MapManager.Instance.playableMaps)
                {
                    if (nextRoundPlayerCount >= mapDictionary[map.id].minPlayers && nextRoundPlayerCount <= mapDictionary[map.id].maxPlayers)
                    {
                        tempMapIDs.Add(map.id);
                    }
                }

                randomMapID = tempMapIDs[new System.Random().Next(0, tempMapIDs.Count)];
                while (randomMapID == prevMapID && tempMapIDs.Count > 1)
                {
                    randomMapID = tempMapIDs[new System.Random().Next(0, tempMapIDs.Count)];
                }
                prevMapID = randomMapID;
            }

            prevMapID = randomMapID;
            ServerSend.LoadMap(randomMapID, 4);
        }

        public static void CheckGameOver()
        {
            if (!IsHost()) return;

            if (taggedPlayers.Count == 0 || nonTaggedPlayers.Count == 0 || alivePlayers.Count <= 1 || GameManager.Instance.activePlayers.Count <= 1)
            {
                ServerSend.GameOver(0);
                Debug.Log("Game Over");
            }
        }

        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Start))]
        [HarmonyPostfix]
        public static void SteamManagerStart()
        {
            InitializeMapData();

            MapManager.Instance.playableMaps.Clear();
            var defaultMaps = new int[] { 35, 36, 37, 38, 39, 40 };
            foreach (var mapIndex in defaultMaps) MapManager.Instance.playableMaps.Add(MapManager.Instance.maps[mapIndex]);

            GameModeManager.Instance.allPlayableGameModes.Clear();
            GameModeManager.Instance.allPlayableGameModes.Add(GameModeManager.Instance.allGameModes[4]);

            ServerConfig.field_Public_Static_Int32_5 = 4; // round start freeze
            ServerConfig.field_Public_Static_Int32_6 = 4; // round stop cinematic
            ServerConfig.field_Public_Static_Int32_7 = 3; // round end timeout
            ServerConfig.field_Public_Static_Int32_8 = 3; // game over timeout
            //ServerConfig.field_Public_Static_Int32_9 = 5; // load time before kicked
            //ServerConfig.field_Public_Static_Single_0 // speak after death time
        }

        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Update))]
        [HarmonyPostfix]
        public static void SteamManagerUpdate()
        {
            CheckGameState();

            if (!IsHost()) return;

            // Start Game
            if (canStartGame && toggleAutoStart && gameState == 1 && (GetPlayerCount() >= 2 && !toggleAfk || GetPlayerCount() >= 3 && toggleAfk))
            {
                ChangeMap();
                canStartGame = false;
                Debug.Log("Game Started");
            }

            /*if (Input.GetKeyDown(KeyCode.R))
            {
                ChangeMap();
                //GameLoop.Instance.StartGames();
                //ServerSend.LoadMap(39, 4);
            }*/
        }

        [HarmonyPatch(typeof(GameMode), nameof(GameMode.Update))]
        [HarmonyPostfix]
        public static void GameModeUpdate(GameMode __instance)
        {
            if (!IsHost() || GetModeID() != 4) return;

            gameTimer = __instance.freezeTimer.field_Private_Single_0;

            if (gameState == 4)
            {
                CheckGameOver();

                // Check Player Positions
                foreach (ulong id in alivePlayers)
                {
                    BoundsCheck(id);
                    GlitchingCheck(id);
                }

                // Afk Check
                afkTimer -= Time.deltaTime;
                if (afkTimer <= 0 && canAfkCheck && alivePlayers.Count > 1)
                {
                    List<ulong> playersToKill = new();

                    foreach (ulong id in alivePlayers)
                    {
                        if (GetPlayerRotation(id) == playerRotations[id])
                        {
                            afkPlayers.Add(id);
                            playersToKill.Add(id);
                        }
                    }

                    foreach (ulong id in playersToKill)
                    {
                        GameServer.PlayerDied(id, 1, Vector3.zero);
                    }
                    canAfkCheck = false;
                }
            }
        }

        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.GameRequestToSpawn))]
        [HarmonyPrefix]
        public static void ServerHandleGameRequestToSpawn(ulong param_0)
        {
            if (!IsHost()) return;

            if (canRespawnPlayers) LobbyManager.Instance.GetClient(param_0).field_Public_Boolean_0 = true; // active player
            if (toggleAfk && param_0 == GetMyID()) LobbyManager.Instance.GetClient(param_0).field_Public_Boolean_0 = false; // active player
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SpawnPlayer))]
        [HarmonyPostfix]
        public static void ServerSendGameSpawnPlayer(ulong param_1)
        {
            if (!IsHost()) return;

            if (!alivePlayers.Contains(param_1)) alivePlayers.Add(param_1);
        }

        [HarmonyPatch(typeof(GameMode), nameof(GameMode.Init))]
        [HarmonyPostfix]
        public static void GameModeInit()
        {
            if (!IsHost() || GetModeID() != 4 || mapDictionary[randomMapID].roundTime < 1) return;

            LobbyManager.Instance.gameMode.shortModeTime = mapDictionary[randomMapID].roundTime;
            LobbyManager.Instance.gameMode.longModeTime = mapDictionary[randomMapID].roundTime;
            LobbyManager.Instance.gameMode.mediumModeTime = mapDictionary[randomMapID].roundTime;
        }

        [HarmonyPatch(typeof(GameLoop), nameof(GameLoop.CheckGameOver))]
        [HarmonyPrefix]
        public static bool GameLoopCheckGameOver()
        {
            if (!IsHost() || GetModeID() != 4) return true;
            return false;
        }

        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.CheckGameOver))]
        [HarmonyPrefix]
        public static bool GameModeTagCheckGameOver()
        {
            if (!IsHost() || GetModeID() != 4) return true;
            return false;
        }

        /*[HarmonyPatch(typeof(KillPlayerOutOfBounds), nameof(KillPlayerOutOfBounds.Awake))]
        [HarmonyPrefix]
        public static bool KillPlayerOutOfBoundsAwake()
        {
            if (!IsHost() || GetModeID() != 4) return true;
            return false;
        }*/

        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.OnFreezeOver))]
        [HarmonyPrefix]
        public static bool GameModeTagOnFreezeOver()
        {
            if (!IsHost() || GetModeID() != 4) return true;

            if (alivePlayers.Count <= 1)
            {
                //CheckGameOver();
                return false;
            }

            if (toggleAfk && alivePlayers.Contains(GetMyID()) && alivePlayers.Count <= 2)
            {
                GameLoop.Instance.RestartLobby();
                return false;
            }

            foreach (ulong id in alivePlayers)
            {
                nonTaggedPlayers.Add(id);
            }

            int numOfTaggers;
            if (alivePlayers.Count <= 3)
            {
                numOfTaggers = 1;
            }
            else if (alivePlayers.Count >= 4 && alivePlayers.Count <= 5)
            {
                numOfTaggers = 2;
            }
            else if (alivePlayers.Count >= 6 && alivePlayers.Count <= 8)
            {
                numOfTaggers = 3;
            }
            else if (alivePlayers.Count >= 9 && alivePlayers.Count <= 10)
            {
                numOfTaggers = 4;
            }
            else
            {
                numOfTaggers = 5;
            }

            List<ulong> tempAlivePlayers = alivePlayers;
            for (int i = 0; i < numOfTaggers; i++)
            {
                ulong randomPlayer = tempAlivePlayers[new System.Random().Next(tempAlivePlayers.Count)];
                ServerSend.TagPlayer(0, randomPlayer);
                GameServer.ForceGiveWeapon(randomPlayer, 10, 0);
                tempAlivePlayers.Remove(randomPlayer);
            }

            // Afk Check
            afkTimer = 10f;
            canAfkCheck = true;
            foreach (ulong id in alivePlayers)
            {
                playerRotations.Add(id, GetPlayerRotation(id));
            }

            canRespawnPlayers = false;

            return false;
        }

        [HarmonyPatch(typeof(GameModeTag), nameof(GameModeTag.OnRoundOver))]
        [HarmonyPrefix]
        public static bool GameModeTagOnRoundOver()
        {
            if (!IsHost() || GetModeID() != 4) return true;

            List<ulong> playersToKill = new();

            if (nonTaggedPlayers.Count > 0 && taggedPlayers.Count > 0)
            {
                foreach (ulong id in taggedPlayers)
                {
                    playersToKill.Add(id);
                }
            }

            foreach (ulong id in playersToKill)
            {
                GameServer.PlayerDied(id, 1, Vector3.zero);
            }

            // Win Message
            if (alivePlayers.Count == 1)
            {
                ServerSend.SendChatMessage(1, $"{GameManager.Instance.activePlayers[alivePlayers[0]].username} wins!");
            }

            GameServer.ForceRemoveAllWeapons();

            return false;
        }

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.TagPlayer))]
        [HarmonyPostfix]
        public static void ServerSendTagPlayer(ulong param_0, ulong param_1)
        {
            if (!IsHost() || GetModeID() != 4) return;

            nonTaggedPlayers.Remove(param_1);
            taggedPlayers.Add(param_1);
            if (param_0 == 0) return;
            nonTaggedPlayers.Add(param_0);
            taggedPlayers.Remove(param_0);

            int distance = (int)Vector3.Distance(GetPlayerRigidBody(param_0).position, GetPlayerRigidBody(param_1).position);
            if (distance > 8)
            {
                ServerSend.SendChatMessage(1, $"{GameManager.Instance.activePlayers[param_0].username} tagged {GameManager.Instance.activePlayers[param_1].username} from a distance of {distance}");
            }
        }

        [HarmonyPatch(typeof(GameLoop), nameof(GameLoop.StartGames))]
        [HarmonyPrefix]
        public static bool GameLoopStartGames()
        {
            if (!IsHost()) return true;
            canRespawnPlayers = true;
            ChangeMap();
            return false;
        }

        [HarmonyPatch(typeof(GameLoop), nameof(GameLoop.NextGame))]
        [HarmonyPrefix]
        public static bool GameLoopNextGame()
        {
            if (!IsHost() || GetModeID() != 4) return true;

            if (alivePlayers.Count >= 2)
            {
                ChangeMap();
            }
            else
            {
                GameLoop.Instance.RestartLobby();
            }
            
            return false;
        }

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.SendWinner))]
        [HarmonyPrefix]
        public static bool ServerSendSendWinner()
        {
            if (!IsHost() || GetModeID() != 4) return true;

            if (toggleAutoStart && ((toggleAfk && GetPlayerCount() > 2) || (!toggleAfk && GetPlayerCount() > 1)))
            {
                ChangeMap();
                Debug.Log("Started New Game");
            }
            else
            {
                GameLoop.Instance.RestartLobby();
                Debug.Log("Skipped Win Screen");
            }
            return false;
        }

        [HarmonyPatch(typeof(GameLoop), nameof(GameLoop.RestartLobby))]
        [HarmonyPrefix]
        public static bool GameLoopRestartLobby()
        {
            if (!IsHost() || GetModeID() != 4) return true;

            if (toggleAutoStart && ((toggleAfk && GetPlayerCount() > 2) || (!toggleAfk && GetPlayerCount() > 1)))
            {
                ChangeMap();
                Debug.Log("Started New Game");
                return false;
            }
            canStartGame = true;
            return true;
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDied))]
        [HarmonyPostfix]
        public static void GameManagerPlayerDied(ulong param_1)
        {
            if (!IsHost() || GetModeID() != 4) return;

            if (afkPlayers.Contains(param_1)) ServerSend.SendChatMessage(1, $"{GameManager.Instance.activePlayers[param_1].username} was killed for being afk");
            else ServerSend.SendChatMessage(1, $"{GameManager.Instance.activePlayers[param_1].username} died");

            if (taggedPlayers.Contains(param_1)) taggedPlayers.Remove(param_1);
            if (nonTaggedPlayers.Contains(param_1)) nonTaggedPlayers.Remove(param_1);
            if (alivePlayers.Contains(param_1)) alivePlayers.Remove(param_1);
        }

        [HarmonyPatch(typeof(GameServer), nameof(GameServer.ForceGiveWeapon))]
        [HarmonyPrefix]
        public static bool GameServerForceGiveWeapon(int param_1)
        {
            if (IsHost() && param_1 == 9 && !toggleSnowballs) return false;
            else return true;
        }

        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.OnPlayerJoinLeaveUpdate))]
        [HarmonyPostfix]
        public static void LobbyManagerOnPlayerJoinLeaveUpdate(CSteamID param_1, bool param_2)
        {
            if (!IsHost() || param_2) return;

            if (taggedPlayers.Contains(param_1.m_SteamID)) taggedPlayers.Remove(param_1.m_SteamID);
            if (nonTaggedPlayers.Contains(param_1.m_SteamID)) nonTaggedPlayers.Remove(param_1.m_SteamID);
            if (alivePlayers.Contains(param_1.m_SteamID)) alivePlayers.Remove(param_1.m_SteamID);
        }

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.SendChatMessage))]
        [HarmonyPrefix]
        public static bool ServerSendSendChatMessagePre(ulong param_0, string param_1)
        {
            if (!IsHost() || param_1 == "!creator") return true;
            string msg = param_1.ToLower();
            if (param_0 == GetMyID() && msg.StartsWith("!"))
            {
                switch (msg)
                {
                    case "!start":
                        toggleAutoStart = !toggleAutoStart;
                        if (toggleAutoStart) Chatbox.Instance.AppendMessage(1, "Auto Start ON", "");
                        else Chatbox.Instance.AppendMessage(1, "Auto Start OFF", "");
                        break;
                    case "!afk":
                        toggleAfk = !toggleAfk;
                        if (toggleAfk) Chatbox.Instance.AppendMessage(1, "Afk ON", "");
                        else Chatbox.Instance.AppendMessage(1, "Afk OFF", "");
                        break;
                    case "!snowball":
                        toggleSnowballs = !toggleSnowballs;
                        if (toggleSnowballs) Chatbox.Instance.AppendMessage(1, "Snowballs ON", "");
                        else Chatbox.Instance.AppendMessage(1, "Snowballs OFF", "");
                        break;
                    case "!help":
                        Chatbox.Instance.AppendMessage(1, "!start", "");
                        Chatbox.Instance.AppendMessage(1, "!afk", "");
                        Chatbox.Instance.AppendMessage(1, "!snowball", "");
                        break;
                    default:
                        Chatbox.Instance.AppendMessage(1, "Invalid Command", "");
                        break;
                }
                return false;
            }
            else return true;
        }

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.SendChatMessage))]
        [HarmonyPostfix]
        public static void ServerSendSendChatMessagePost(string param_1)
        {
            if (!IsHost()) return;
            string msg = param_1.ToLower();
            switch (msg)
            {
                case "!creator":
                    ServerSend.SendChatMessage(1, "Mod created by JMac");
                    break;
            }
        }

        [HarmonyPatch(typeof(MonoBehaviourPublicGataInefObInUnique), "Method_Private_Void_GameObject_Boolean_Vector3_Quaternion_0")]
        [HarmonyPatch(typeof(MonoBehaviourPublicCSDi2UIInstObUIloDiUnique), "Method_Private_Void_0")]
        [HarmonyPatch(typeof(MonoBehaviourPublicVesnUnique), "Method_Private_Void_0")]
        [HarmonyPatch(typeof(MonoBehaviourPublicObjomaOblogaTMObseprUnique), "Method_Public_Void_PDM_2")]
        [HarmonyPatch(typeof(MonoBehaviourPublicTeplUnique), "Method_Private_Void_PDM_32")]
        [HarmonyPrefix]
        public static bool Prefix(System.Reflection.MethodBase __originalMethod)
        {
            return false;
        }
    }
}