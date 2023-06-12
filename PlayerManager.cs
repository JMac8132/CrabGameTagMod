using HarmonyLib;
using SteamworksNative;
using System.Collections.Generic;
using UnityEngine;
using static TagMod.Plugin;

namespace TagMod
{
    public static class PlayerManager
    {
        public static Dictionary<ulong, Player> players = new();
        public static bool canRespawnPlayers = true;

        public class Player
        {
            public string Name { get; set; } = "";
            public ulong SteamID { get; set; } = 0;
            public Client Client { get; set; } = null;
            public int PlayerNumber { get; set; } = 0;
            public Vector3 AfkRotation { get; set; } = Vector3.zero;
            public bool Playing { get; set; } = false;
            public bool Dead { get; set; } = true;
            public bool Tagged { get; set; } = false;
            public bool Afk { get; set; } = false;
            public int RoundsAfk { get; set; } = 0;

            public void Reset()
            {
                AfkRotation = Vector3.zero;
                Playing = false;
                Dead = true;
                Tagged = false;
                Afk = false;
            }

            public Vector3 GetRotation()
            {
                if (SteamID == GetMyID()) return PlayerInput.Instance.cameraRot;
                else return new Vector3(GameManager.Instance.activePlayers[SteamID].field_Private_MonoBehaviourPublicObVeSiVeRiSiAnVeanTrUnique_0.xRot, GameManager.Instance.activePlayers[SteamID].field_Private_MonoBehaviourPublicObVeSiVeRiSiAnVeanTrUnique_0.yRot, 0f);
            }

            public Rigidbody GetRigidBody()
            {
                if (SteamID == GetMyID()) return PlayerMovement.prop_MonoBehaviourPublicGaplfoGaTrorplTrRiBoUnique_0.GetRb();
                else return GameManager.Instance.activePlayers[SteamID].prop_MonoBehaviourPublicObVeSiVeRiSiAnVeanTrUnique_0.field_Private_Rigidbody_0;
            }
        }

        public static void ResetPlayers()
        {
            foreach (var player in players.Values)
            {
                player.Reset();
            }
        }

        public static int GetPlayerCount()
        {
            return players.Count;
        }

        public static Dictionary<ulong, Player> GetAlivePlayers()
        {
            Dictionary<ulong, Player> dict = new();

            foreach (var player in players.Values)
            {
                if (player.Playing == true && player.Dead == false)
                {
                    dict.Add(player.SteamID, player);
                }
            }
            return dict;
        }

        public static Dictionary<ulong, Player> GetTaggedPlayers()
        {
            Dictionary<ulong, Player> dict = new();

            foreach (var player in players.Values)
            {
                if (player.Playing == true && player.Dead == false && player.Tagged == true)
                {
                    dict.Add(player.SteamID, player);
                }
            }
            return dict;
        }

        public static Dictionary<ulong, Player> GetNonTaggedPlayers()
        {
            Dictionary<ulong, Player> dict = new();

            foreach (var player in players.Values)
            {
                if (player.Playing == true && player.Dead == false && player.Tagged == false)
                {
                    dict.Add(player.SteamID, player);
                }
            }
            return dict;
        }

        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.AddPlayerToLobby))]
        [HarmonyPostfix]
        public static void LobbyManagerAddPlayerToLobby(CSteamID param_1)
        {
            if (players.ContainsKey(param_1.m_SteamID) == false)
            {
                Player player = new()
                {
                    Name = SteamFriends.GetFriendPersonaName(param_1),
                    SteamID = param_1.m_SteamID,
                    Client = LobbyManager.Instance.GetClient(param_1.m_SteamID),
                    PlayerNumber = LobbyManager.steamIdToUID[param_1.m_SteamID] + 1,
                };
                players.Add(param_1.m_SteamID, player);
                Debug.Log($"{SteamFriends.GetFriendPersonaName(param_1)} joined the server");
            } 
        }

        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.RemovePlayerFromLobby))]
        [HarmonyPostfix]
        public static void LobbyManagerRemovePlayerFromLobby(CSteamID param_1)
        {
            if (players.ContainsKey(param_1.m_SteamID) == true)
            {
                players.Remove(param_1.m_SteamID);
                Debug.Log($"{SteamFriends.GetFriendPersonaName(param_1)} left the server");
                CheckGameOver();
            }
        }

        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.CloseLobby))]
        [HarmonyPostfix]
        public static void LobbyManagerCloseLobby()
        {
            players.Clear();
        }

        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.GameRequestToSpawn))]
        [HarmonyPrefix]
        public static void ServerHandleGameRequestToSpawn(ulong param_0)
        {
            if (!IsHost()) return;

            if (canRespawnPlayers) players[param_0].Client.field_Public_Boolean_0 = true; // active player
            if (toggleAfk && param_0 == GetMyID()) players[param_0].Client.field_Public_Boolean_0 = false; // active player
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SpawnPlayer))]
        [HarmonyPostfix]
        public static void GameManagerSpawnPlayer(ulong param_1)
        {
            if (!IsHost()) return;

            players[param_1].Playing = true;
            players[param_1].Dead = false;
        }
    }
}
