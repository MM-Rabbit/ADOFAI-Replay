﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ADOFAI;
using DG.Tweening;
using HarmonyLib;
using Replay.Clasz;
using Replay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using TinyJson;
using Replay.Lib;

namespace Replay.Patch
{
    internal static class PlayHistory
    {
        public static int index = 0;
        public static long ms = 0;
        public static bool alive = false, isSave = false, isMenuOpening = false;
        public static ReplayData data = new ReplayData();
        internal static ReplayMenu Menu;
        

        [AdofaiPatch(
            "Common.Hit",
            "scrController",
            "Hit"
            )]
        public static class Hit
        {
            public static void Prefix()
            {
                if (!Main.IsEnabled) return;
                scrController __instance = scrController.instance;
                if (!__instance.gameworld) return;
                if (!__instance.goShown) return;
                if (__instance.currFloor.midSpin) return;

                if (!WorldReplay.isReplayStart) data.angles.Add(new TileInfo(__instance.currentSeqID, __instance.chosenplanet.angle, ms.ToString()));
                else WorldReplay.Hit(__instance);
            }
        }

        [AdofaiPatch(
            "PlayHistory.Update",
            "scnCLS",
            "Update"
            )]
        public static class Update
        {
            public static void Prefix(string ___levelToSelect, Dictionary<string, LevelData> ___loadedLevels)
            {
                if (!Main.IsEnabled) return;
                scnCLS __instance = scnCLS.instance;
                if (__instance.cls)
                {
                    data.id = ___levelToSelect;
                    data.name = ___loadedLevels[___levelToSelect].artist + " - " + ___loadedLevels[___levelToSelect].song;
                    data.path = ___loadedLevels[___levelToSelect].pathData;
                }
            }
        }



        [AdofaiPatch(
            "PlayHistory.CountValidKeysPressed",
            "scrController",
            "CountValidKeysPressed"
            )]
        public static class CountValidKeysPressed
        {
            public static bool Prefix()
            {
                if (Main.IsEnabled)
                {
                    scrController __instance = scrController.instance;
                    if (!__instance.CLSMode && !__instance.isEditingLevel&&!__instance.gameworld)
                    {
                        if (Input.GetKeyDown(KeyCode.Z))
                        {
                            return false;
                        }
                        if (Menu != null) return false;
                    }
                }
                return true;
            }
        }


        [AdofaiPatch(
            "PlayHistory.MoveToNextFloor",
            "scrPlanet",
            "MoveToNextFloor"
            )]
        public static class MoveToNextFloor
        {
            public static void Prefix(scrFloor floor)
            {
                if (!Main.IsEnabled) return;
                if (WorldReplay.isReplayStart) return;
                scrController __instance = scrController.instance;
                if (!__instance.controller.gameworld) return;
                double time = scrMisc.GetTimeBetweenAngles(floor.entryangle, floor.exitangle, floor.speed, __instance.conductor.bpm, !floor.isCCW);
                ms += (long)(time * 1000);

            }
        }

        [AdofaiPatch(
              "PlayHistory.Awake",
              "scrController",
              "Awake"
             )]
        public static class Awake
        {
            public static void Prefix()
            {
                Main.mainSong = scrConductor.instance.song.clip;
            }
        }


        [AdofaiPatch(
            "PlayHistory.CheckForSpecialInputKeysOrPause",
            "scrController",
            "CheckForSpecialInputKeysOrPause"
            )]
        public static class CheckForSpecialInputKeysOrPause
        {
            public static void Prefix()
            {
                if (!Main.IsEnabled) return;
                if (!WorldReplay.isReplayStart)
                {
                    scrController __instance = scrController.instance;
                    //메뉴 닫기
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        if (Menu == null) return;
                        if (isMenuOpening) return;
                        isMenuOpening = true;
                        try
                        {
                            Menu.closeWindow();
                        }
                        catch (Exception e)
                        {
                            LogError.Show(ErrorCodes.OverlapGUI, e.Message);
                        }
                    }

                    // 메뉴 열기
                    if (Input.GetKeyDown(KeyCode.Z))
                    {
                        if (__instance.CLSMode) return;
                        if (__instance.gameworld) return;
                        if (isMenuOpening) return;
                        try
                        {
                            isMenuOpening = true;

                            if (!Directory.Exists("./Replay/"))
                            {
                                Directory.CreateDirectory("./Replay/");
                            }

                            if (Menu == null)
                            {

                                List<ReplayData> replays = new List<ReplayData>();
                                DirectoryInfo di = new DirectoryInfo("./Replay/");

                                foreach (FileInfo item in di.GetFiles())
                                {
                                    try
                                    {
                                        string read = File.ReadAllText($"./Replay/{item.Name}");
                                        ReplayData json = CustomJson.parse(read);
                                        json.filepath = $"./Replay/{item.Name}";
                                        if (Misc.IsNull(json.id)) continue;
                                        replays.Add(json);
                                    }
                                    catch
                                    {
                                        LogError.Show(ErrorCodes.FailedParsing, item.Name);
                                    }
                                }
                                Menu = new GameObject().AddComponent<ReplayMenu>();
                                Menu.replays = replays;
                                UnityEngine.Object.DontDestroyOnLoad(Menu);
                                __instance.camy.SetYOffset(0f);
                                __instance.chosenplanet.transform.position = new Vector3(0f, 0f, __instance.chosenplanet.transform.position.z);
                                __instance.menuPhase = 0;
                                __instance.camy.ViewObjectInstant(__instance.chosenplanet.transform, false);
                                __instance.camy.positionState = 1;
                            }
                            else
                            {
                                Menu.closeWindow();
                            }

                        }
                        catch (Exception e)
                        {
                            LogError.Show(ErrorCodes.OverlapGUI, e.Message);

                        }
                    }


                    //저장
                    if (Input.GetKeyDown(KeyCode.F1))
                    {
                        if (alive) return;
                        if (!__instance.gameworld) return;
                        if (__instance.isEditingLevel) return;
                        try
                        {
                            if (isSave) return;
                            isSave = true;

                            data.end = __instance.currentSeqID;
                            data.name = $"{__instance.customLevel.levelData.artist} - {__instance.customLevel.levelData.song}";
                            data.time = ms.ToString();
                            data.speed = GCS.currentSpeedRun;
                            data.path = __instance.customLevel.levelData.pathData;

                            if (!Directory.Exists("./Replay/"))
                            {
                                Directory.CreateDirectory("./Replay/");
                            }

                            string pattern = @"<(.|\n)*?>";
                            string delete = Regex.Replace(data.name, pattern, string.Empty);

                            File.WriteAllText($"./Replay/{Regex.Replace(delete, @"[^0-9a-zA-Z가-힣]", "")} {DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day} {DateTime.Now.Hour}.{DateTime.Now.Minute}.{DateTime.Now.Second}.rpl", CustomJson.stringify(data));
                            index = 0;
                            Main.gui.ShowSaveText(true);
                        }
                        catch (Exception e)
                        {
                            LogError.Show(ErrorCodes.CantSaved, e.Message);
                            Main.gui.ShowSaveText(false);
                        }
                    }
                }
                
            }
        }

 

        [AdofaiPatch(
            "Common.ShowText",
            "scrPressToStart",
            "ShowText"
            )]
        public static class Countdown
        {
            public static void Postfix(scrPressToStart __instance)
            {
                if (!Main.IsEnabled) return;
                if (!WorldReplay.isReplayStart)
                {
                    data.angles.Clear();
                    data.start = __instance.controller.currFloor.seqID;
                } else
                {
                    WorldReplay.ShowText(__instance);
                }
            }
        }


        [AdofaiPatch(
            "PlayHistory.FailAction",
            "scrController",
            "FailAction"
            )]
        public static class FailAction
        {
            public static void Prefix()
            {
                if (!Main.IsEnabled) return;
                scrController __instance = scrController.instance;
                if (!__instance.gameworld) return;
                alive = false;
                
            }
        }


        [AdofaiPatch(
            "PlayHistory.Play",
            "CustomLevel",
            "Play"
            )]
        public static class Play
        {
            public static void Prefix()
            {
                if (!Main.IsEnabled) return;
                if (WorldReplay.isReplayStart) return;

               isSave = false;
               alive = true;
               ms = 0;
            }
        }

    }
}