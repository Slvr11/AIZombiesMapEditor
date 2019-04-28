using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using InfinityScript;

namespace AIZombiesMapEditor
{
    public class aizEditor : BaseScript
    {
        private static List<Entity> objects = new List<Entity>();
        private static List<Entity> waypoints = new List<Entity>();
        private static List<Vector3> wpLocs = new List<Vector3>();

        private static string baseMapFileDir = "scripts\\aizombies\\maps\\";

        private static int selectedPerk = 1;
        private static int selectedTool = 0;
        private static string[] tools = new string[] { "Wall", "Invisible Wall", "Death Wall", "Crate", "Ramp", "Teleport Flags", "Teleporter", "Door", "Floor", "Invisible Floor", "Death Floor", "Elevator", "Model", "Fall Limit", "X Limit", "Y Limit", "Map Name", "Is Hell Map", "Max Wave" };
        private static string[] perks = new string[] { "Juggernog", "Stamin-Up", "Speed Cola", "Mule Kick", "Double Tap", "Stalker Soda", "Quick Revive Pro", "Scavenge-Aid" };
        private static bool hellMap = false;
        private static Vector3 yLimit = Vector3.Zero;
        private static Vector3 xLimit = Vector3.Zero;
        private static int? fallLimit = null;
        private static string mapName = "Custom Map";
        private static int maxWave = 30;

        private static Entity selectedObject = null;

        private static int currentMapVariant = 0;
        private static string currentMapFilename;
        private static string currentMapWPFilename;
        private static string mapname;

        private static string botModel;

        private static int noClipAddress = 0x01AC56C0;
        private static byte noClipValue = 0x01;

        private static HudElem host;

        private static FileStream currentMapFile = null;

        public aizEditor()
        {
            //Box - Jugg USP
            //Gambler - Five Seven
            //Bank - P99
            //Ammo - 44Mag Xmags
            //Killstreak - Deagle
            //Pap - as50 Acog
            //power - L118a Acog
            //Perk1-7 - MSR acog (UI Toggle)
            //spawn - alt mk14 hybrid Xmags
            //zombiespawn - mk14 hybrid Xmags
            //toolbox - mp412 jugg

            try
            {
                mapname = GetDvar("mapname");
            }
            catch
            {
                Log.Write(LogLevel.Error, "You must be using InfinityScript v1.5 to run this script!");
                return;
            }

            switch (mapname)
            {
                case "mp_exchange":
                case "mp_hardhat":
                case "mp_underground":
                case "mp_boardwalk":
                case "mp_nola":
                case "mp_overwatch":
                    botModel = "mp_body_russian_military_assault_a_airborne";
                    break;
                case "mp_cement":
                case "mp_crosswalk_ss":
                case "mp_roughneck":
                    botModel = "mp_body_russian_military_smg_a_airborne";
                    break;
                case "mp_seatown":
                case "mp_aground_ss":
                case "mp_burn_ss":
                case "mp_courtyard_ss":
                case "mp_italy":
                case "mp_meteora":
                case "mp_qadeem":
                case "mp_morningwood":
                    botModel = "mp_body_henchmen_assault_a";
                    break;
                case "mp_interchange":
                case "mp_lambeth":
                case "mp_six_ss":
                case "mp_moab":
                case "mp_park":
                    botModel = "mp_body_russian_military_assault_a_woodland";
                    break;
                case "mp_mogadishu":
                case "mp_carbon":
                case "mp_village":
                case "mp_bravo":
                case "mp_shipbreaker":
                    botModel = "mp_body_africa_militia_assault_a";
                    break;
                case "mp_radar":
                    botModel = "mp_body_russian_military_assault_a_arctic";
                    break;
                default:
                    botModel = "mp_body_russian_military_assault_a";
                    break;
            }

            for (int i = 1; i < 11; i++)
            {
                if (File.Exists(baseMapFileDir + mapname + ".map"))
                {
                    currentMapVariant = i;
                    if (File.Exists(baseMapFileDir + mapname + "_" + i.ToString() + ".map"))
                        continue;
                    else break;
                }
                else break;
            }

            if (currentMapVariant != 0)
                currentMapFilename = baseMapFileDir + mapname + "_" + currentMapVariant.ToString() + ".map";
            else
                currentMapFilename = baseMapFileDir + mapname + ".map";
            currentMapWPFilename = currentMapFilename.Replace(".map", ".wyp");
            //Log.Write(LogLevel.All, currentMapFilename);

            PlayerConnected += onPlayerConnect;

            PreCacheShader("hud_iw5_divider");
            PreCacheShader("clanlvl_box");
            //PreCacheMenu("error_popmenu");
            initServerHud();

            SetTeamRadar("allies", true);
            SetTeamRadar("axis", true);
            SetTeamRadar("none", true);
            SetTeamRadar("spectator", true);
        }

        private void onPlayerConnect(Entity player)
        {
            if (player.EntRef != 0) return;
            player.SetField("isMapping", false);
            player.SetField("isInNoClip", false);
            player.SetField("isWaypointing", false);
            player.SetField("hasObject", false);
            //player.SetField("enteringMapname", false);

            player.SetClientDvar("g_hardcore", "1");
            player.SetClientDvar("cg_drawCrosshair", "1");
            //player.SetClientDvar("gameMode", "so");

            initHud(player);

            SetMatchData("host", player.Name);

            player.OnNotify("weapon_fired", (ent, wep) => onWeaponFired(ent, wep.As<string>()));
            player.OnNotify("weapon_switch_started", (ent, wep) => onWeaponChanged(ent, (string)wep, true));
            player.OnNotify("weapon_change", (ent, wep) => onWeaponChanged(ent, (string)wep, false));
            player.OnNotify("reload", updateWeaponAmmo);

            player.OnNotify("noclip", noClip);
            player.OnNotify("map", doMap);
            player.OnNotify("wp", doWaypoint);
            player.OnNotify("display", toggleControls);
            player.OnNotify("nextTool", (p) => cycleTools(p, true));
            player.OnNotify("prevTool", (p) => cycleTools(p, false));

            player.NotifyOnPlayerCommand("noclip", "+actionslot 1");
            player.NotifyOnPlayerCommand("map", "+actionslot 4");
            player.NotifyOnPlayerCommand("wp", "+actionslot 5");
            player.NotifyOnPlayerCommand("display", "+actionslot 7");
            player.NotifyOnPlayerCommand("nextTool", "vote no");
            player.NotifyOnPlayerCommand("prevTool", "vote yes");
        }

        public override void OnPlayerDisconnect(Entity player)
        {
            if (player.EntRef == 0)
            {
                if (player.GetField<bool>("isMapping")) stopCurrentMappingSession(player);
                else if (player.GetField<bool>("isWaypointing")) stopCurrentWaypointingSession(player);

                player.ClearField("isMapping");
                player.ClearField("isWaypointing");
                player.ClearField("isInNoClip");
                host.SetText("Current Mapper: None");
            }
        }

        public override EventEat OnSay2(Entity player, string name, string message)
        {
            if (player.HasField("isAwaitingInput") && player.EntRef == 0)
            {
                Entity target = player.GetField<Entity>("isAwaitingInput");
                target.SetField("value", message);
                player.ClearField("isAwaitingInput");
                player.IPrintLnBold("Added " + target.GetField<string>("type"));
                objects.Add(target);
                player.EnableWeapons();

                if (player.HasField("hud_inputHint"))
                    player.ClearField("hud_inputHint");

                return EventEat.EatGame;
            }
            if (player.HasField("isAwaitingNumberInput") && player.EntRef == 0)
            {
                Entity target = player.GetField<Entity>("isAwaitingNumberInput");
                int value;
                if (int.TryParse(message, out value))
                {
                    target.SetField("value", value);
                }
                else
                {
                    player.IPrintLnBold("Please enter a number.");
                    return EventEat.EatGame;
                }
                player.ClearField("isAwaitingNumberInput");
                player.IPrintLnBold("Added " + target.GetField<string>("type"));
                objects.Add(target);
                player.EnableWeapons();

                if (player.HasField("hud_inputHint"))
                    player.ClearField("hud_inputHint");

                return EventEat.EatGame;
            }
            if (player.HasField("isAwaitingWaveInput") && player.EntRef == 0)
            {
                int value;
                if (int.TryParse(message, out value))
                {
                    maxWave = value;
                    player.ClearField("isAwaitingWaveInput");
                    player.IPrintLnBold("Max Wave set to " + value.ToString());
                }
                else
                {
                    player.IPrintLnBold("Please enter a number.");
                    return EventEat.EatGame;
                }

                player.EnableWeapons();

                if (player.HasField("hud_inputHint"))
                    player.ClearField("hud_inputHint");

                return EventEat.EatGame;
            }
            else if (player.HasField("isAwaitingMapInput") && player.EntRef == 0)
            {
                mapName = message;
                player.ClearField("isAwaitingMapInput");
                IPrintLnBold("Set mapname to " + message);
                player.EnableWeapons();

                if (player.HasField("hud_inputHint"))
                    player.ClearField("hud_inputHint");

                return EventEat.EatGame;
            }
            else if (player.HasField("isAwaitingModelName") && player.EntRef == 0)
            {
                Entity target = player.GetField<Entity>("isAwaitingModelName");
                target.SetField("value", message);
                target.SetModel(message);
                return EventEat.EatGame;
            }
            else if (message == "viewpos")
            {
                Log.Write(LogLevel.Info, "({0}, {1}, {2})", player.Origin.X, player.Origin.Y, player.Origin.Z);
                Vector3 angles = player.GetPlayerAngles();
                Log.Write(LogLevel.Info, "({0}, {1}, {2})", angles.X, angles.Y, angles.Z);
                return EventEat.EatGame;
            }
            else return EventEat.EatNone;
        }

        private static void initPlayerInput(Entity player, string message)
        {
            /*
            player.OpenPopUpMenu("error_popmenu");
            player.SetClientDvar("com_errorMessage", message);
            player.SetClientDvar("com_errorTitle", "Awaiting Input");
            player.SetClientDvar("com_errorResolveCommand", "chatmodepublic");
            */
            HudElem hint = HudElem.CreateFontString(player, HudElem.Fonts.Objective, 1.5f);
            hint.SetPoint("center");
            hint.SetText(message + "\n     (Press [{chatmodepublic}])");
            hint.Color = new Vector3(1, 1, 1);
            player.SetField("hud_inputHint", hint);
            float difference = 0.05f;

            OnInterval(50, () =>
            {
                Vector3 color = hint.Color;
                if (color.Y == 1) difference = -0.05f;
                else if (color.Y == 0) difference = 0.05f;

                hint.Color += new Vector3(0, difference, difference);

                if (player.HasField("hud_inputHint"))
                    return true;
                else
                {
                    hint.Destroy();
                    return false;
                }
            });
        }

        private static void noClip(Entity player)
        {
            player.SetField("isInNoClip", !player.GetField<bool>("isInNoClip"));

            byte set = (byte)(player.GetField<bool>("isInNoClip") ? noClipValue : 0x00);
            unsafe
            {
                *(byte*)noClipAddress = set;
            }

            string value = player.GetField<bool>("isInNoClip") ? "ON" : "OFF";
            player.IPrintLn("noclip " + value);
        }

        private static void doMap(Entity player)
        {
            if (player.GetField<bool>("isMapping")) stopCurrentMappingSession(player);
            else initNewMap(player);
        }
        private static void doWaypoint(Entity player)
        {
            if (player.GetField<bool>("isWaypointing")) stopCurrentWaypointingSession(player);
            else initNewWaypoints(player);
        }

        private static void initNewMap(Entity player)
        {
            if (player.GetField<bool>("isWaypointing"))
            {
                player.IPrintLnBold("You must stop waypointing before you can map!");
                return;
            }
            player.SetField("isMapping", true);
            //player.MaxHealth = 999999999;
            //player.Health =  -1;

            Announcement("Mapping started.");

            player.SetPerk("specialty_fastreload", true, true);
            player.SetClientDvar("perk_weapReloadMultiplier", 0);

            player.TakeAllWeapons();

            player.GiveWeapon("iw5_usp45jugg_mp_xmags");
            player.SetWeaponAmmoStock("iw5_usp45jugg_mp_xmags", 0);
            player.GiveWeapon("iw5_fnfiveseven_mp_xmags");
            player.SetWeaponAmmoStock("iw5_fnfiveseven_mp_xmags", 0);
            player.SetWeaponAmmoClip("iw5_fnfiveseven_mp_xmags", 1);
            player.GiveWeapon("iw5_p99_mp_xmags");
            player.SetWeaponAmmoStock("iw5_p99_mp_xmags", 0);
            player.SetWeaponAmmoClip("iw5_p99_mp_xmags", 1);
            player.GiveWeapon("iw5_44magnum_mp_xmags");
            player.SetWeaponAmmoClip("iw5_44magnum_mp_xmags", 2);
            player.SetWeaponAmmoStock("iw5_44magnum_mp_xmags", 0);
            player.GiveWeapon("iw5_deserteagle_mp_xmags");
            player.SetWeaponAmmoStock("iw5_deserteagle_mp_xmags", 0);
            player.SetWeaponAmmoClip("iw5_deserteagle_mp_xmags", 1);
            player.GiveWeapon("iw5_as50_mp_acog");
            player.SetWeaponAmmoStock("iw5_as50_mp_acog", 0);
            player.SetWeaponAmmoClip("iw5_as50_mp_acog", 1);
            player.GiveWeapon("iw5_l96a1_mp_acog");
            player.SetWeaponAmmoStock("iw5_l96a1_mp_acog", 0);
            player.SetWeaponAmmoClip("iw5_l96a1_mp_acog", 1);
            player.GiveWeapon("iw5_msr_mp_acog_xmags");
            player.SetWeaponAmmoClip("iw5_msr_mp_acog_xmags", 7);
            player.SetWeaponAmmoStock("iw5_msr_mp_acog_xmags", 0);
            player.GiveWeapon("iw5_mk14_mp_reflex_xmags");
            player.SetWeaponAmmoStock("iw5_mk14_mp_reflex_xmags", 0);
            player.SetWeaponAmmoClip("iw5_mk14_mp_reflex_xmags", 20);
            player.GiveWeapon("iw5_mk14_mp_reflex_xmags_camo08");
            player.SetWeaponAmmoStock("iw5_mk14_mp_reflex_xmags_camo08", 0);
            player.SetWeaponAmmoClip("iw5_mk14_mp_reflex_xmags_camo08", 20);
            player.GiveWeapon("iw5_mp412jugg_mp");
            player.GiveMaxAmmo("iw5_mp412jugg_mp");
            player.GiveWeapon("defaultweapon_mp");

            player.SwitchToWeaponImmediate("iw5_usp45jugg_mp_xmags");

            if (!File.Exists(currentMapFilename))
                createMapFile();

            currentMapFile = File.OpenWrite(currentMapFilename);

            restoreObjects();
        }

        private static void initNewWaypoints(Entity player)
        {
            if (player.GetField<bool>("isMapping"))
            {
                player.IPrintLnBold("You must stop mapping before you can waypoint!");
                return;
            }

            player.SetField("isWaypointing", true);
            player.Health = -1;

            Announcement("Waypointing started.");

            player.SetPerk("specialty_fastreload", true, true);
            player.SetClientDvar("perk_weapReloadMultiplier", 0);

            player.TakeAllWeapons();

            player.GiveWeapon("iw5_mk14_mp_reflex");
            player.SetWeaponAmmoStock("iw5_mk14_mp_reflex", 20);
            player.SetWeaponAmmoClip("iw5_mk14_mp_reflex", 20);
            player.SwitchToWeaponImmediate("iw5_mk14_mp_reflex");

            if (!File.Exists(currentMapWPFilename))
            {
                createWaypointFile();
                return;
            }

            foreach (string s in File.ReadAllLines(currentMapWPFilename))
                wpLocs.Add(buildVectorFromString(s));

            displayWaypoints();
        }

        private static void stopCurrentMappingSession(Entity player)
        {
            player.SetField("isMapping", false);
            player.Health = player.MaxHealth;

            player.UnSetPerk("specialty_fastreload");
            player.SetClientDvar("perk_weapReloadMultiplier", 0.5f);

            player.TakeAllWeapons();

            try
            {
                saveMapFile();
                currentMapFile.Flush();
                currentMapFile.Dispose();
                currentMapFile.Close();
                currentMapFile = null;
                Announcement("Mapping stopped and saved.");
                removeObjects();
            }
            catch (Exception e)
            {
                Announcement("There was a problem saving the map file!");
                Log.Write(LogLevel.Error, "There was a problem saving the map file:\n{0} from {1}\nPlease contact Slvr99 on the TeknoGods forums with this message.", e.Message, e.Data);
            }
        }

        private static void stopCurrentWaypointingSession(Entity player)
        {
            player.SetField("isWaypointing", false);
            player.Health = player.MaxHealth;

            player.UnSetPerk("specialty_fastreload");
            player.SetClientDvar("perk_weapReloadMultiplier", 0.5f);

            player.TakeAllWeapons();

            try
            {
                saveWaypointFile();
                Announcement("Waypointing stopped and saved.");
                removeWaypoints();
            }
            catch (Exception e)
            {
                Announcement("There was a problem saving the waypoint file!");
                Log.Write(LogLevel.Error, "There was a problem saving the waypoint file:\n{0} \nPlease contact Slvr99 on the TeknoGods forums with this message.", e.Message);
            }
        }

        private static void updateWeaponAmmo(Entity player)
        {
            if (player.EntRef != 0) return;
            //HudElem ammoStock = player.GetField<HudElem>("hud_ammoStock");
            HudElem ammoClip = player.GetField<HudElem>("hud_ammoClip");

            ammoClip.SetValue(player.GetWeaponAmmoClip(player.CurrentWeapon));
        }
        private static void onWeaponFired(Entity player, string weapon)
        {
            if (player.EntRef != 0) return;
            updateWeaponAmmo(player);

            if (weapon == "iw5_mk14_mp_reflex" && !player.GetField<bool>("isMapping"))//Waypoints
            {
                string pos;
                Vector3 trace = doLineTrace(player);
                pos = buildStringFromVector(trace);
                wpLocs.Add(trace);
                Announcement("Waypoint added at position " + pos);
                displayWaypoints();
            }
            else if (player.GetField<bool>("isMapping"))
            {
                string pos;
                Vector3 trace = doLineTrace(player);
                pos = buildStringFromVector(trace);

                Vector3 angles;
                Entity box;
                string objectName;
                string objectType;
                float playerAnglesY = player.GetPlayerAngles().Y;

                switch (weapon)
                {
                    case "iw5_usp45jugg_mp_xmags"://Weapon Box
                        angles = new Vector3(0, playerAnglesY - 90, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 15), angles);
                        objectType = "weapon";
                        objectName = "Weapon Box";
                        break;
                    case "iw5_fnfiveseven_mp_xmags"://Gambler
                        angles = new Vector3(0, playerAnglesY - 90, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 15), angles);
                        objectType = "gambler";
                        objectName = "Gambler";
                        break;
                    case "iw5_p99_mp_xmags"://Bank
                        angles = new Vector3(90, playerAnglesY - 180, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 30), angles);
                        objectType = "bank";
                        objectName = "Bank";
                        break;
                    case "iw5_44magnum_mp_xmags"://Ammo
                        angles = new Vector3(0, playerAnglesY - 90, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 15), angles);
                        objectType = "ammo";
                        objectName = "Ammo";
                        break;
                    case "iw5_deserteagle_mp_xmags"://Killstreak
                        angles = new Vector3(0, playerAnglesY - 90, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 15), angles);
                        objectType = "ks";
                        objectName = "Killstreaks";
                        break;
                    case "iw5_as50_mp_acog"://Pack-a-Punch
                        angles = new Vector3(0, playerAnglesY - 90, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 15), angles);
                        objectType = "pap";
                        objectName = "Pack-a-Punch";
                        break;
                    case "iw5_l96a1_mp_acog"://Power
                        angles = new Vector3(0, playerAnglesY - 90, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 15), angles);
                        objectType = "power";
                        objectName = "Power";
                        break;
                    case "iw5_msr_mp_acog_xmags"://Perks
                        angles = new Vector3(90, playerAnglesY - 180, 0);
                        box = spawnCrate(trace + new Vector3(0, 0, 30), angles);
                        objectType = "perk" + selectedPerk.ToString();
                        objectName = "Perk " + selectedPerk.ToString();
                        break;
                    case "iw5_mk14_mp_reflex_xmags"://Player Spawn
                        angles = new Vector3(0, playerAnglesY, 0);
                        trace = doLineTrace(player, true);
                        box = spawnModel("mp_fullbody_ally_juggernaut", trace, angles);
                        objectType = "spawn";
                        objectName = "Player Spawn";
                        break;
                    case "iw5_mk14_mp_reflex_xmags_camo08"://Zombie Spawn
                        angles = new Vector3(0, playerAnglesY, 0);
                        box = spawnModel(botModel, trace, angles);
                        objectType = "zspawn";
                        objectName = "Zombie Spawn";
                        break;
                    case "iw5_mp412jugg_mp":
                        initTool(player, playerAnglesY, trace);
                        player.SetWeaponAmmoClip("iw5_mp412jugg_mp", 6);
                        return;
                    case "defaultweapon_mp":
                        pickupObject(player);
                        player.SetWeaponAmmoClip("defaultweapon_mp", 10);
                        return;
                    default:
                        return;
                }

                box.SetField("type", objectType);
                objects.Add(box);
                Announcement("Added " + objectName + " at position " + pos);
            }
        }

        private static void initTool(Entity player, float yAngle, Vector3 origin)
        {
            switch (selectedTool)
            {
                case 0:
                    doWall(player, origin, false, false);
                    break;
                case 1:
                    doWall(player, origin, true, false);
                    break;
                case 2:
                    doWall(player, origin, true, true);
                    break;
                case 3:
                    Entity box = spawnCrate(origin + new Vector3(0, 0, 15), new Vector3(0, yAngle, 0));
                    box.SetField("type", "crate");
                    objects.Add(box);
                    Announcement("Added Crate at position " + origin);
                    break;
                case 4:
                    doRamp(player, origin);
                    break;
                case 5:
                    setTeleportFlags(player, origin);
                    break;
                case 6:
                    setupTeleporter(player, origin);
                    break;
                case 7:
                    setupDoor(player, origin, new Vector3(0, yAngle, 0));
                    break;
                case 8:
                    setupFloor(player, origin, false, false);
                    break;
                case 9:
                    setupFloor(player, origin, true, false);
                    break;
                case 10:
                    setupFloor(player, origin, true, true);
                    break;
                case 11:
                    setupElevator(player, origin, new Vector3(0, yAngle, 0));
                    break;
                case 12:
                    setupModel(player, origin, new Vector3(0, yAngle, 0));
                    break;
                case 13:
                    setupFallLimit(player);
                    break;
                case 14:
                    setupXLimit(player);
                    break;
                case 15:
                    setupYLimit(player);
                    break;
                case 16:
                    setupMapname(player);
                    break;
                case 17:
                    setupHellMap(player);
                    break;
                case 18:
                    setupMaxWave(player);
                    break;
            }
        }

        private static void doWall(Entity player, Vector3 origin, bool invisible, bool death)
        {
            player.IPrintLnBold("Set wall width.");
            Entity start;
            if (invisible && !death) start = spawnModel("com_plasticcase_enemy", origin, player.Angles);
            else if (death) start = spawnModel("com_plasticcase_trap_friendly", origin, player.Angles);
            else start = spawnCrate(origin, player.Angles);
            Entity end;
            if (invisible && !death) end = spawnModel("com_plasticcase_enemy", origin, player.Angles);
            else if (death) end = spawnModel("com_plasticcase_trap_friendly", origin, player.Angles);
            else end = spawnCrate(origin, player.Angles);
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 angles = VectorToAngles(end.Origin - start.Origin);
                    start.RotateTo(new Vector3(0, angles.Y, 0), .1f);
                    end.RotateTo(new Vector3(0, angles.Y, 0), .1f);

                    Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 400));
                    dest = new Vector3(dest.X, dest.Y, start.Origin.Z);

                    end.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        if (end.Origin.DistanceTo2D(start.Origin) > 2048)
                        {
                            player.IPrintLnBold("Wall is too large!");
                            return true;
                        }
                        player.IPrintLnBold("Set wall height.");
                        AfterDelay(700, () => doWallHeight(player, start, end, invisible, death));
                        return false;
                    }
                    else if (player.MeleeButtonPressed())
                    {
                        IPrintLnBold("Wall creation cancelled.");
                        start.Delete();
                        end.Delete();
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void doWallHeight(Entity player, Entity start, Entity end, bool invisible, bool death)
        {
            
            Vector3 baseOrigin = end.Origin;

            OnInterval(50, () =>
            {
                float height = player.GetPlayerAngles().X;
                end.MoveTo(baseOrigin + new Vector3(0, 0, -height*4), .1f);

                if (player.AttackButtonPressed())
                {
                    Entity wall = createWall(start.Origin, end.Origin, invisible, death, start, end);
                    string prefix = "";
                    if (invisible && !death) prefix = "invisible";
                    else if (death) prefix = "death";
                    wall.SetField("type", prefix + "wall");
                    wall.SetField("start", start);
                    wall.SetField("end", end);
                    objects.Add(wall);
                    //start.Delete();
                    //end.Delete();
                    start.SetModel("tag_origin");
                    end.SetModel("tag_origin");
                    AfterDelay(500, () => player.EnableWeapons());
                    if (invisible && !death) prefix = " Invisible";
                    else if (death) prefix = " Death";
                    else prefix = "";
                    Announcement("Added" + prefix + " Wall");
                    return false;
                }

                if (!Players.Contains(player)) return false;
                return true;
            });
        }

        private static void doRamp(Entity player, Vector3 origin)
        {
            player.IPrintLnBold("Set ramp length.");
            Entity start = spawnCrate(origin, player.Angles);
            Entity end = spawnCrate(origin, player.Angles);
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 angles = VectorToAngles(end.Origin - start.Origin);
                    start.RotateTo(new Vector3(0, angles.Y - 90, -angles.X), .1f);
                    end.RotateTo(new Vector3(0, angles.Y - 90, -angles.X), .1f);

                    Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 1000));

                    end.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        if (end.Origin.DistanceTo(start.Origin) > 2048)
                        {
                            player.IPrintLnBold("Ramp is too large!");
                            return true;
                        }
                        Entity ramp = createRamp(start.Origin, end.Origin, start, end);
                        ramp.SetField("type", "ramp");
                        ramp.SetField("start", start);
                        ramp.SetField("end", end);
                        objects.Add(ramp);
                        start.SetModel("tag_origin");
                        end.SetModel("tag_origin");
                        AfterDelay(500, () => player.EnableWeapons());
                        Announcement("Added Ramp");
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setTeleportFlags(Entity player, Vector3 origin)
        {
            player.IPrintLnBold("Set teleport destination.");
            Entity start = spawnModel("prop_flag_neutral", origin, player.Angles);
            Entity end = spawnModel("prop_flag_neutral", origin, player.Angles);
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 800));

                    end.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        start.SetField("dest", end);
                        start.SetField("type", "tp");
                        end.SetField("type", "tp_end");
                        end.SetField("scr_ignore", true);
                        objects.Add(start);
                        objects.Add(end);
                        AfterDelay(500, () => player.EnableWeapons());
                        Announcement("Added Teleport Flags");
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupTeleporter(Entity player, Vector3 origin)
        {
            player.IPrintLnBold("Set teleporter destination.");
            Entity baseTP = createTeleporterBase(origin, new Vector3(0, player.Angles.Y, 0));
            Entity end = spawnModel("mp_fullbody_ally_juggernaut", origin, player.Angles);
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 dest = PlayerPhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 500));
                    Vector3 angles = VectorToAngles(player.GetEye() - end.Origin);
                    end.RotateTo(new Vector3(0, angles.Y, 0), .1f);

                    end.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {

                        Announcement("Place teleporter linker.");
                        AfterDelay(700, () => setupLinker(player, baseTP, end));
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupLinker(Entity player, Entity start, Entity end)
        {
            Entity linker = spawnModel("weapon_radar", end.Origin, player.Angles);
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 25));
                    Vector3 angles = VectorToAngles(player.GetEye() - linker.Origin);
                    linker.RotateTo(new Vector3(90, angles.Y, 0), .1f);

                    linker.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        //Announcement("Enter teleport time.");
                        initPlayerInput(player, "Enter teleport time");

                        start.SetField("type", "teleporterBase");
                        start.SetField("scr_ignore", true);
                        objects.Add(start);
                        linker.SetField("tpBase", start);

                        linker.SetField("dest", end);
                        end.SetField("type", "teleporterDestination");
                        end.SetField("scr_ignore", true);
                        objects.Add(end);

                        linker.SetField("type", "teleporter");
                        AfterDelay(700, () => player.SetField("isAwaitingNumberInput", linker));
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupDoor(Entity player, Vector3 origin, Vector3 angle)
        {
            player.IPrintLnBold("Set door width.");
            angle = angle + new Vector3(90, 0, 0);
            Entity start = spawnModel("com_plasticcase_enemy", origin, angle);
            Entity end = spawnModel("com_plasticcase_enemy", origin, angle);
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 angles = VectorToAngles(end.Origin - start.Origin);
                    start.RotateTo(new Vector3(90, angles.Y - 90, 0), .1f);
                    end.RotateTo(new Vector3(90, angles.Y - 90, 0), .1f);

                    Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 400));
                    dest = new Vector3(dest.X, dest.Y, start.Origin.Z);

                    end.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        if (end.Origin.DistanceTo2D(start.Origin) > 2048)
                        {
                            player.IPrintLnBold("Door is too large!");
                            return true;
                        }
                        player.IPrintLnBold("Set door height.");
                        AfterDelay(700, () => doDoorHeight(player, start, end));
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void doDoorHeight(Entity player, Entity start, Entity end)
        {
            Vector3 baseOrigin = end.Origin;

            OnInterval(50, () =>
            {
                float height = player.GetPlayerAngles().X;
                end.MoveTo(baseOrigin + new Vector3(0, 0, 70 * (int)-(height/10)), .1f);

                if (player.AttackButtonPressed())
                {
                    int width = (int)start.Origin.DistanceTo2D(end.Origin) / 25;
                    Vector3 difference = start.Origin - end.Origin;
                    Entity wall = spawnDoor(new Vector3(start.Origin.X - (difference.X / 2), start.Origin.Y - (difference.Y / 2), start.Origin.Z), start.Angles, width, (int)Math.Abs(end.Origin.Z - start.Origin.Z) / 30, 100);
                    wall.SetField("type", "door");
                    start.Delete();
                    end.Delete();
                    player.IPrintLnBold("Move door to open position.");
                    AfterDelay(700, () => doDoorMovement(player, wall));
                    return false;
                }

                if (!Players.Contains(player)) return false;
                return true;
            });
        }
        private static void doDoorMovement(Entity player, Entity door)
        {
            OnInterval(50, () =>
            {
                Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 250));
                //Vector3 angles = VectorToAngles(player.GetEye() - door.Origin);
                //door.RotateTo(new Vector3(90, angles.Y, 0), .1f);

                door.MoveTo(dest, .1f);

                if (player.AttackButtonPressed())
                {
                    door.SetField("open", door.Origin);
                    //player.IPrintLnBold("Type door cost.");
                    initPlayerInput(player, "Enter door cost");
                    door.MoveTo(door.GetField<Vector3>("close"), 3);
                    AfterDelay(700, () => player.SetField("isAwaitingNumberInput", door));
                    return false;
                }

                if (!Players.Contains(player)) return false;
                return true;
            });
        }

        private static void setupFloor(Entity player, Vector3 origin, bool invisible, bool death)
        {
            player.IPrintLnBold("Set floor size.");
            Entity start;
            if (invisible && !death) start = spawnModel("com_plasticcase_enemy", origin, player.Angles);
            else if (death) start = spawnModel("com_plasticcase_trap_friendly", origin, player.Angles);
            else start = spawnCrate(origin, player.Angles);
            start.Angles = Vector3.Zero;
            Entity end;
            if (invisible && !death) end = spawnModel("com_plasticcase_enemy", origin, player.Angles);
            else if (death) end = spawnModel("com_plasticcase_trap_friendly", origin, player.Angles);
            else end = spawnCrate(origin, player.Angles);
            end.Angles = Vector3.Zero;
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 400));
                    dest = new Vector3(dest.X, dest.Y, start.Origin.Z);

                    end.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        if (end.Origin.DistanceTo2D(start.Origin) > 2048)
                        {
                            player.IPrintLnBold("Floor is too large!");
                            return true;
                        }
                        Entity floor = createFloor(start.Origin, end.Origin, invisible, death, start, end);
                        string prefix = "";
                        if (invisible && !death) prefix = "invisible";
                        else if (death) prefix = "death";
                        floor.SetField("type", prefix + "floor");
                        floor.SetField("start", start);
                        floor.SetField("end", end);
                        objects.Add(floor);
                        start.SetModel("tag_origin");
                        end.SetModel("tag_origin");
                        AfterDelay(500, () => player.EnableWeapons());
                        if (invisible && !death) prefix = " Invisible";
                        else if (death) prefix = " Death";
                        else prefix = "";
                        Announcement("Added" + prefix + " Floor");
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupElevator(Entity player, Vector3 origin, Vector3 angles)
        {
            player.IPrintLnBold("Set elevator destination.");
            Entity start = spawnCrate(origin, new Vector3(0, player.Angles.Y, 0));
            Entity end = spawnModel("com_plasticcase_enemy", origin, start.Angles);
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 500));

                    end.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        player.IPrintLnBold("Set drop off location.");
                        AfterDelay(700, () => doElevatorDropOff(player, start, end));
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void doElevatorDropOff(Entity player, Entity start, Entity end)
        {
            Entity dest = spawnModel("mp_fullbody_opforce_juggernaut", end.Origin, new Vector3(0, end.Angles.Y, 0));
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 destPos = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 500));
                    dest.RotateTo(new Vector3(0, player.GetPlayerAngles().Y, 0), .1f);

                    dest.MoveTo(destPos, .1f);

                    if (player.AttackButtonPressed())
                    {
                        Announcement("Added Elevator");
                        dest.SetField("type", "elevator");

                        dest.SetField("start", start);
                        start.SetField("type", "elevatorStart");
                        start.SetField("scr_ignore", true);
                        objects.Add(start);

                        dest.SetField("end", end);
                        end.SetField("type", "elevatorEnd");
                        end.SetField("scr_ignore", true);
                        objects.Add(end);

                        objects.Add(dest);
                        player.EnableWeapons();
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupModel(Entity player, Vector3 origin, Vector3 angles)
        {
            player.DisableWeapons();
            Entity model = spawnModel("fx", origin, angles);
            player.IPrintLnBold("Type in model name.");
            awaitModelInput(player, model);
        }
        private static void awaitModelInput(Entity player, Entity model)
        {
            player.SetField("isAwaitingModelName", model);
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    Vector3 dest = PlayerPhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 500));
                    Vector3 angles = player.GetPlayerAngles();

                    model.RotateTo(angles, .1f);
                    model.MoveTo(dest, .1f);

                    if (player.AttackButtonPressed())
                    {
                        player.ClearField("isAwaitingModelName");
                        Announcement("Added model: " + model.GetField<string>("value"));
                        model.SetField("type", "model");
                        objects.Add(model);
                        player.EnableWeapons();
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupFallLimit(Entity player)
        {
            player.IPrintLnBold("Fly to height limit and click.");
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    if (player.AttackButtonPressed())
                    {
                        fallLimit = (int)player.Origin.Z;
                        Announcement("Set Fall Limit to " + fallLimit);
                        player.EnableWeapons();
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });

        }
        private static void setupXLimit(Entity player)
        {
            player.IPrintLnBold("Move to the most southern X limit bound and click.");
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    if (player.AttackButtonPressed())
                    {
                        xLimit = new Vector3((int)player.Origin.X, 0, 0);
                        setupXLimitTop(player);
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupXLimitTop(Entity player)
        {
            player.IPrintLnBold("Move to the most northern X limit bound and click.");
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    if (player.AttackButtonPressed())
                    {
                        xLimit = new Vector3(xLimit.X, (int)player.Origin.X, 0);
                        Announcement("Set X Limit to (" + xLimit.X + ", " + xLimit.Y + ")");
                        player.EnableWeapons();
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupYLimit(Entity player)
        {
            player.IPrintLnBold("Move to most eastern Y limit bound and click.");
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    if (player.AttackButtonPressed())
                    {
                        yLimit = new Vector3((int)player.Origin.Y, 0, 0);
                        setupYLimitTop(player);
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupYLimitTop(Entity player)
        {
            player.IPrintLnBold("Move to most western Y limit bound and click.");
            player.DisableWeapons();
            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    if (player.AttackButtonPressed())
                    {
                        yLimit = new Vector3(yLimit.X, (int)player.Origin.Y, 0);
                        Announcement("Added Y Limit at (" + (int)yLimit.X + ", " + (int)yLimit.Y + ")");
                        player.EnableWeapons();
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void setupMapname(Entity player)
        {
            initPlayerInput(player, "Enter map name");
            player.SetField("isAwaitingMapInput", Entity.Level);
            player.DisableWeapons();
        }
        private static void setupHellMap(Entity player)
        {
            hellMap = !hellMap;
            Announcement("Hell Map setting set to " + hellMap.ToString());
        }
        private static void setupMaxWave(Entity player)
        {
            initPlayerInput(player, "Enter max wave");
            player.SetField("isAwaitingWaveInput", "");
            player.DisableWeapons();
        }

        private static void pickupObject(Entity player)
        {
            if (selectedObject == null) return;
            player.IPrintLnBold("Picked up " + selectedObject.GetField<string>("type"));
            player.DisableWeapons();
            player.SetField("hasObject", true);
            HudElem del = HudElem.CreateFontString(player, HudElem.Fonts.Objective, 2);
            del.SetPoint("center", "center", -200, -25);
            del.GlowColor = new Vector3(1, 0, 0);
            del.GlowAlpha = 1;
            del.HideWhenInMenu = true;
            del.SetText("^3[{+melee_zoom}] ^2Delete Object\n\n^3[{+frag}] ^3Rotate Object");
            del.Alpha = 1;

            int rotationOffset = 0;

            AfterDelay(500, () =>
            {
                OnInterval(50, () =>
                {
                    //if (selectedObject == null) return false;
                    Vector3 dest;
                    if (selectedObject.GetField<string>("type") == "spawn") dest = PlayerPhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 250));
                    else dest = PhysicsTrace(player.GetEye(), player.GetEye() + (AnglesToForward(player.GetPlayerAngles()) * 250));
                    Vector3 angles = VectorToAngles(player.GetEye() - selectedObject.Origin);
                    int orientation = 0;
                    //int face = 0;
                    switch (selectedObject.GetField<string>("type"))
                    {
                        case "perk1":
                        case "perk2":
                        case "perk3":
                        case "perk4":
                        case "perk5":
                        case "perk6":
                        case "perk7":
                        case "perk8":
                        case "bank":
                        case "door":
                            orientation = 90;
                            //face = 0;
                            break;
                        case "spawn":
                        case "zspawn":
                        case "wall":
                        case "invisiblewall":
                        case "deathwall":
                            //face = 0;
                            break;
                    }
                    //face += rotationOffset;

                    selectedObject.RotateTo(new Vector3(orientation, angles.Y + rotationOffset, 0), .1f);

                    int heightOffset = 15;
                    switch (selectedObject.GetField<string>("type"))
                    {
                        case "perk1":
                        case "perk2":
                        case "perk3":
                        case "perk4":
                        case "perk5":
                        case "perk6":
                        case "perk7":
                        case "perk8":
                        case "bank":
                        case "door":
                            heightOffset = 30;
                            break;
                        case "model":
                        case "spawn":
                        case "zspawn":
                            heightOffset = 0;
                            break;
                    }

                    selectedObject.MoveTo(dest + new Vector3(0, 0, heightOffset), .1f);

                    if (player.AttackButtonPressed())
                    {
                        del.Destroy();
                        player.EnableWeapons();
                        player.SetField("hasObject", false);
                        return false;
                    }
                    else if (player.FragButtonPressed())
                    {
                        rotationOffset += 1;
                        if (rotationOffset >= 360)
                            rotationOffset = 0;
                    }
                    else if (player.MeleeButtonPressed())
                    {
                        del.Destroy();
                        Announcement(selectedObject.GetField<string>("type") + " has been deleted!");
                        objects.Remove(selectedObject);

                        switch (selectedObject.GetField<string>("type"))
                        {
                            case "weapon":
                                player.SetWeaponAmmoClip("iw5_usp45jugg_mp_xmags", player.GetWeaponAmmoClip("iw5_usp45jugg_mp_xmags") + 1);
                                break;
                            case "gambler":
                                player.SetWeaponAmmoClip("iw5_fnfiveseven_mp_xmags", player.GetWeaponAmmoClip("iw5_fnfiveseven_mp_xmags") + 1);
                                break;
                            case "bank":
                                player.SetWeaponAmmoClip("iw5_p99_mp_xmags", player.GetWeaponAmmoClip("iw5_p99_mp_xmags") + 1);
                                break;
                            case "ammo":
                                player.SetWeaponAmmoClip("iw5_44magnum_mp_xmags", player.GetWeaponAmmoClip("iw5_44magnum_mp_xmags") + 1);
                                break;
                            case "ks":
                                player.SetWeaponAmmoClip("iw5_deserteagle_mp_xmags", player.GetWeaponAmmoClip("iw5_deserteagle_mp_xmags") + 1);
                                break;
                            case "pap":
                                player.SetWeaponAmmoClip("iw5_as50_mp_acog", player.GetWeaponAmmoClip("iw5_as50_mp_acog") + 1);
                                break;
                            case "power":
                                player.SetWeaponAmmoClip("iw5_l96a1_mp_acog", player.GetWeaponAmmoClip("iw5_l96a1_mp_acog") + 1);
                                break;
                            case "perk1":
                            case "perk2":
                            case "perk3":
                            case "perk4":
                            case "perk5":
                            case "perk6":
                            case "perk7":
                            case "perk8":
                                player.SetWeaponAmmoClip("iw5_msr_mp_acog", player.GetWeaponAmmoClip("iw5_msr_mp_acog") + 1);
                                break;
                            case "spawn":
                                player.SetWeaponAmmoClip("iw5_mk14_mp_reflex_xmags", player.GetWeaponAmmoClip("iw5_mk14_mp_reflex_xmags") + 1);
                                break;
                            case "zspawn":
                                player.SetWeaponAmmoClip("iw5_mk14_mp_reflex_xmags_camo08", player.GetWeaponAmmoClip("iw5_mk14_mp_reflex_xmags_camo08") + 1);
                                break;
                        }

                        if (selectedObject.HasField("pieces"))
                        {
                            List<Entity> pieces = selectedObject.GetField<List<Entity>>("pieces");
                            foreach (Entity p in pieces)
                                p.Delete();
                        }
                        if (selectedObject.HasField("dest"))
                        {
                            Entity d = selectedObject.GetField<Entity>("dest");
                            d.Delete();
                        }
                        if (selectedObject.HasField("start"))
                        {
                            Entity s = selectedObject.GetField<Entity>("start");
                            s.Delete();
                        }
                        if (selectedObject.HasField("end"))
                        {
                            Entity e = selectedObject.GetField<Entity>("end");
                            e.Delete();
                        }
                        if (selectedObject.HasField("tpBase"))
                        {
                            Entity tp = selectedObject.GetField<Entity>("tpBase");
                            tp.Delete();
                        }
                        selectedObject.Delete();
                        selectedObject = null;
                        player.EnableWeapons();
                        player.SetField("hasObject", false);
                        return false;
                    }

                    if (!Players.Contains(player)) return false;
                    return true;
                });
            });
        }
        private static void watchPickup(Entity player)
        {
            Entity selector = spawnModel("com_plasticcase_trap_bombsquad", player.Origin, Vector3.Zero);
            selector.Hide();
            OnInterval(50, () =>
                {
                    if (player.GetField<bool>("hasObject")) return true;
                    foreach (Entity obj in objects)
                    {
                        if (obj.Origin.DistanceTo(player.Origin) > 512) continue;

                        if (player.SightConeTrace(obj.Origin) > 0.5f && player.WorldPointInReticle_Circle(obj.Origin, 65, 65))
                        {
                            selector.Show();
                            selector.Origin = obj.Origin;
                            selector.Angles = obj.Angles;
                            selectedObject = obj;
                            break;
                        }
                        else
                        {
                            selectedObject = null;
                            selector.Hide();
                        }
                    }

                    if (player.CurrentWeapon == "defaultweapon_mp") return true;
                    selector.Delete();
                    selectedObject = null;
                    return false;
                });
        }

        private static void onWeaponChanged(Entity player, string weapon, bool updateName)
        {
            if (player.EntRef != 0) return;
            //HudElem ammoStock = player.GetField<HudElem>("hud_ammoStock");
            HudElem ammoClip = player.GetField<HudElem>("hud_ammoClip");
            //HudElem ammoSlash = player.GetField<HudElem>("hud_ammoSlash");

            ammoClip.Alpha = 1;

            //ammoStock.SetValue(player.GetWeaponAmmoStock(weapon));
            ammoClip.SetValue(player.GetWeaponAmmoClip(weapon));
            //ammoSlash.SetText("/");
            updateWeaponName(player, weapon, updateName);

            if (weapon == "iw5_msr_mp_acog_xmags")
            {
                HudElem tool = player.GetField<HudElem>("hud_tools");
                tool.FadeOverTime(.25f);
                tool.Alpha = 1;
                tool.SetText("Vote Yes(^3[{vote yes}]^7)^2<^7 Current Perk: " + perks[selectedPerk-1] + " ^2>^7Vote No(^3[{vote no}]^7)");
            }
            else if (weapon == "iw5_mp412jugg_mp")
            {
                HudElem tool = player.GetField<HudElem>("hud_tools");
                tool.FadeOverTime(.25f);
                tool.Alpha = 1;
                tool.SetText("Vote Yes(^3[{vote yes}]^7)^2<^7 Current Tool: " + tools[selectedTool] + " ^2>^7Vote No(^3[{vote no}]^7)");
            }
            else
            {
                HudElem tool = player.GetField<HudElem>("hud_tools");
                if (tool.Alpha != 0)
                {
                    tool.FadeOverTime(.25f);
                    tool.Alpha = 0;
                }
            }

            if (weapon == "defaultweapon_mp")
            {
                ammoClip.Alpha = 0;
                watchPickup(player);
            }
        }
        private static void updateWeaponName(Entity player, string weapon, bool animate)
        {
            HudElem weaponName = player.GetField<HudElem>("hud_weaponName");
            HudElem weaponSlider = player.GetField<HudElem>("hud_weaponSlider");
            weaponName.Alpha = 1;

            if (animate)
            {
                weaponSlider.ScaleOverTime(.25f, 200, 24);
                weaponSlider.FadeOverTime(.2f);
                weaponSlider.Alpha = 1;
                weaponSlider.Foreground = true;
            }

            AfterDelay(300, () =>
            {
                string name = getWeaponName(weapon);
                weaponName.SetText(name);
                weaponSlider.FadeOverTime(.25f);
                weaponSlider.Alpha = .4f;
                int width = name.Length * 10;
                weaponSlider.ScaleOverTime(.25f, width, 24);
                AfterDelay(250, () => weaponSlider.Foreground = false);
            });
        }
        private static string getWeaponName(string weapon)
        {
            switch (weapon)
            {
                case "iw5_usp45jugg_mp_xmags":
                    return "^5Weapon Box";
                case "iw5_fnfiveseven_mp_xmags":
                    return "^5Gambler";
                case "iw5_p99_mp_xmags":
                    return "^5Bank";
                case "iw5_44magnum_mp_xmags":
                    return "^5Ammo";
                case "iw5_deserteagle_mp_xmags":
                    return "^5Killstreak";
                case "iw5_as50_mp_acog":
                    return "^5Pack-a-Punch";
                case "iw5_l96a1_mp_acog":
                    return "^5Power";
                case "iw5_msr_mp_acog_xmags":
                    return "^5Perk Box";
                case "iw5_mk14_mp_reflex_xmags":
                    return "^5Player Spawn";
                case "iw5_mk14_mp_reflex_xmags_camo08":
                    return "^5Zombie Spawn";
                case "iw5_mp412jugg_mp":
                    return "^5Toolbox";
                case "defaultweapon_mp":
                    return "^5Object Selector";
                case "iw5_mk14_mp_reflex":
                    return "^5Waypointer";
                default:
                    return "";
            }
        }

        private static void initServerHud()
        {
            host = NewHudElem();
            host.X = 0;
            host.Y = 0;
            host.HorzAlign = HudElem.HorzAlignments.Center;
            host.VertAlign = HudElem.VertAlignments.SubTop;
            host.AlignX = HudElem.XAlignments.Center;
            host.AlignY = HudElem.YAlignments.Top;
            host.Archived = true;
            host.Alpha = 1;
            host.Font = HudElem.Fonts.HudBig;
            host.FontScale = 1;
            host.Foreground = true;
            host.GlowColor = new Vector3(0, 1, 0);
            host.GlowAlpha = .3f;
            host.HideWhenInDemo = true;
            host.HideWhenInMenu = true;
            host.Sort = 0;
            host.SetText("Current Mapper: None");

            HudElem version = NewHudElem();
            version.X = 0;
            version.Y = 0;
            version.HorzAlign = HudElem.HorzAlignments.Right_Adjustable;
            //version.VertAlign = HudElem.VertAlignments.Top;
            version.AlignX = HudElem.XAlignments.Right;
            version.AlignY = HudElem.YAlignments.Top;
            version.Archived = true;
            version.Alpha = 1;
            version.Font = HudElem.Fonts.Default;
            version.FontScale = .85f;
            version.Color = new Vector3(0, 1, 0);
            version.SetText("AIZombies Supreme Map Editor version 1.0");
        }
        private static void initHud(Entity player)
        {
            HudElem controls = NewClientHudElem(player);
            controls.X = -5;
            controls.Y = -50;
            controls.HorzAlign = HudElem.HorzAlignments.Right_Adjustable;
            controls.VertAlign = HudElem.VertAlignments.Middle;
            controls.AlignX = HudElem.XAlignments.Right;
            controls.AlignY = HudElem.YAlignments.Middle;
            controls.Archived = true;
            controls.Font = HudElem.Fonts.HudBig;
            controls.FontScale = .75f;
            controls.Foreground = true;
            controls.HideIn3rdPerson = false;
            controls.HideWhenDead = true;
            controls.HideWhenInDemo = true;
            controls.HideWhenInMenu = true;
            controls.Sort = -1;
            controls.Alpha = 1;
            controls.SetText(@"^2Controls:^7

No Clip: [{+actionslot 1}]
Start/Stop Mapping: [{+actionslot 4}]
Start/Stop Waypointing: [{+actionslot 5}]

Toggle Controls Menu: [{+actionslot 7}]");

            player.SetField("hud_controls", controls);

            //Ammo counters
            /*
            HudElem ammoSlash = HudElem.CreateFontString(player, HudElem.Fonts.HudSmall, 1f);
            ammoSlash.SetPoint("bottom right", "bottom right", -150, -28);
            ammoSlash.HideWhenInMenu = true;
            ammoSlash.Archived = true;
            ammoSlash.AlignX = HudElem.XAlignments.Left;
            ammoSlash.SetText("/");
            ammoSlash.Sort = 0;

            HudElem ammoStock = HudElem.CreateFontString(player, HudElem.Fonts.HudSmall, 1f);
            ammoStock.Parent = ammoSlash;
            ammoStock.SetPoint("bottom left", "bottom left", 8, 0);
            ammoStock.HideWhenInMenu = true;
            ammoStock.Archived = true;
            ammoStock.SetValue(48);
            ammoStock.Sort = 0;
            */

            HudElem ammoClip = HudElem.CreateFontString(player, HudElem.Fonts.HudBig, 1f);
            //ammoClip.Parent = ammoSlash;
            ammoClip.SetPoint("bottom right", "bottom right", -100, -28);
            ammoClip.HideWhenInMenu = true;
            ammoClip.Archived = true;
            ammoClip.SetValue(12);
            ammoClip.Sort = 0;

            HudElem weaponName = HudElem.CreateFontString(player, HudElem.Fonts.HudSmall, 1f);
            weaponName.SetPoint("bottom right", "bottom right", -125, -8);
            weaponName.HideWhenInMenu = true;
            weaponName.Archived = true;
            weaponName.Alpha = 1;
            weaponName.SetText("Elephant Gun");
            weaponName.Sort = 0;

            HudElem weaponSlider = HudElem.CreateIcon(player, "clanlvl_box", 0, 24);
            weaponSlider.Parent = weaponName;
            weaponSlider.SetPoint("right", "right", 10, -2);
            weaponSlider.Alpha = 1;
            weaponSlider.Foreground = true;
            weaponSlider.HideWhenInMenu = true;
            weaponSlider.Archived = true;
            weaponSlider.Color = new Vector3(0, 1, 0);
            weaponSlider.Sort = 1;

            //Item divider
            HudElem divider = HudElem.CreateIcon(player, "hud_iw5_divider", 200, 24);
            divider.SetPoint("BOTTOMRIGHT", "BOTTOMRIGHT", -67, -20);
            divider.HideWhenInMenu = true;
            divider.Alpha = 1;
            divider.Archived = true;
            divider.Sort = 1;

            //Set up player fields for ammo hud
            //player.SetField("hud_ammoSlash", ammoSlash);
            //player.SetField("hud_ammoStock", ammoStock);
            player.SetField("hud_ammoClip", ammoClip);
            player.SetField("hud_weaponName", weaponName);
            player.SetField("hud_weaponSlider", weaponSlider);
            player.SetField("hud_divider", divider);

            HudElem tools = HudElem.CreateFontString(player, HudElem.Fonts.HudSmall, 1f);
            tools.SetPoint("bottom", "bottom", 0, -50);
            tools.HideWhenInMenu = true;
            tools.Archived = true;
            tools.Alpha = 1;
            tools.SetText("Current Tool: ");
            tools.Sort = 0;

            player.SetField("hud_tools", tools);

            host.SetText("Current Mapper: " + player.Name);
        }
        private static void toggleControls(Entity player)
        {
            if (!player.HasField("hud_controls")) return;
            HudElem controls = player.GetField<HudElem>("hud_controls");

            controls.FadeOverTime(.25f);
            if (controls.Alpha == 1) controls.Alpha = 0;
            else if (controls.Alpha == 0) controls.Alpha = 1;
        }

        private static void cycleTools(Entity player, bool forward)
        {
            string weapon = player.CurrentWeapon;
            if (weapon != "iw5_msr_mp_acog_xmags" && weapon != "iw5_mp412jugg_mp") return;

            HudElem tool = player.GetField<HudElem>("hud_tools");

            if (weapon == "iw5_msr_mp_acog_xmags")
            {
                if (forward)
                {
                    selectedPerk++;
                    if (selectedPerk > 7) selectedPerk = 1;
                }
                else
                {
                    selectedPerk--;
                    if (selectedPerk < 1) selectedPerk = 7;
                }
                tool.SetText("Vote Yes(^3[{vote yes}]^7)^2<^7 Current Perk: " + perks[selectedPerk-1] + " ^2>^7Vote No(^3[{vote no}]^7)");
            }
            else if (weapon == "iw5_mp412jugg_mp")
            {
                if (forward)
                {
                    selectedTool++;
                    if (selectedTool > tools.Length-1) selectedTool = 0;
                }
                else
                {
                    selectedTool--;
                    if (selectedTool < 0) selectedTool = tools.Length - 1;
                }
                tool.SetText("Vote Yes(^3[{vote yes}]^7)^2<^7 Current Tool: " + tools[selectedTool] + " ^2>^7Vote No(^3[{vote no}]^7)");
            }
        }

        private static Vector3 doLineTrace(Entity player, bool playerSize = false)
        {
            Vector3 eye = player.GetEye();
            Vector3 angles = player.GetPlayerAngles();
            Vector3 forward = AnglesToForward(angles);
            Vector3 endPos = eye + forward * 50000;
            Vector3 trace;
            if (playerSize) trace = PlayerPhysicsTrace(eye, endPos);
            else trace = PhysicsTrace(eye, endPos);
            return trace;
        }

        private static void displayWaypoints()
        {
            foreach (Vector3 v in wpLocs)
            {
                foreach (Entity e in waypoints)
                {
                    if (e.Origin.Equals(v)) continue;
                }
                Entity wp = Spawn("script_model", v);
                wp.SetModel("prop_flag_neutral");
                waypoints.Add(wp);
            }
        }
        private static void removeWaypoints()
        {
            foreach (Entity e in waypoints)
            {
                e.Delete();
            }
            waypoints.Clear();
            wpLocs.Clear();
        }
        private static void removeObjects()
        {

            foreach (Entity ent in objects)
            {
                if (ent.HasField("pieces"))
                {
                    List<Entity> pieces = ent.GetField<List<Entity>>("pieces");
                    foreach (Entity p in pieces)
                        p.Hide();
                }
                if (ent.HasField("dest"))
                {
                    Entity dest = ent.GetField<Entity>("dest");
                    dest.Hide();
                }
                if (ent.HasField("tpBase"))
                {
                    Entity tp = ent.GetField<Entity>("tpBase");
                    tp.Hide();
                }
                ent.Hide();
            }

            //objects.Clear();
        }
        private static void restoreObjects()
        {

            foreach (Entity ent in objects)
            {
                if (ent.HasField("pieces"))
                {
                    List<Entity> pieces = ent.GetField<List<Entity>>("pieces");
                    foreach (Entity p in pieces)
                        p.Show();
                }
                if (ent.HasField("dest"))
                {
                    Entity dest = ent.GetField<Entity>("dest");
                    dest.Show();
                }
                if (ent.HasField("tpBase"))
                {
                    Entity tp = ent.GetField<Entity>("tpBase");
                    tp.Show();
                }
                ent.Show();
            }

            //objects.Clear();
        }

        private static void createMapFile()
        {
            Directory.CreateDirectory(baseMapFileDir);
            StreamWriter bankCreate = File.CreateText(currentMapFilename);
            bankCreate.Flush();
            bankCreate.Close();
        }

        private static void createWaypointFile()
        {
            Directory.CreateDirectory(baseMapFileDir);
            StreamWriter wpCreate = File.CreateText(currentMapWPFilename);
            wpCreate.Flush();
            wpCreate.Close();
        }

        private static void saveMapFile()
        {
            if (currentMapFile == null)
                return;

            List<Entity> weaponBoxContainer = new List<Entity>();
            foreach (Entity box in objects)
            {
                if (box.HasField("scr_ignore")) continue;

                switch (box.GetField<string>("type"))
                {
                    case "weapon":
                        weaponBoxContainer.Add(box);
                        break;
                    case "gambler":
                        byte[] gambler = Encoding.UTF8.GetBytes("gambler: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(gambler, 0, gambler.Length);
                        break;
                    case "bank":
                        byte[] bank = Encoding.UTF8.GetBytes("bank: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(bank, 0, bank.Length);
                        break;
                    case "ammo":
                        byte[] ammo = Encoding.UTF8.GetBytes("ammo: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(ammo, 0, ammo.Length);
                        break;
                    case "ks":
                        byte[] ks = Encoding.UTF8.GetBytes("killstreak: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(ks, 0, ks.Length);
                        break;
                    case "pap":
                        byte[] pap = Encoding.UTF8.GetBytes("pap: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(pap, 0, pap.Length);
                        break;
                    case "power":
                        byte[] power = Encoding.UTF8.GetBytes("power: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(power, 0, power.Length);
                        break;
                    case "perk1":
                        byte[] perk1 = Encoding.UTF8.GetBytes("perk1: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(perk1, 0, perk1.Length);
                        break;
                    case "perk2":
                        byte[] perk2 = Encoding.UTF8.GetBytes("perk2: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(perk2, 0, perk2.Length);
                        break;
                    case "perk3":
                        byte[] perk3 = Encoding.UTF8.GetBytes("perk3: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(perk3, 0, perk3.Length);
                        break;
                    case "perk4":
                        byte[] perk4 = Encoding.UTF8.GetBytes("perk4: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(perk4, 0, perk4.Length);
                        break;
                    case "perk5":
                        byte[] perk5 = Encoding.UTF8.GetBytes("perk5: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(perk5, 0, perk5.Length);
                        break;
                    case "perk6":
                        byte[] perk6 = Encoding.UTF8.GetBytes("perk6: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(perk6, 0, perk6.Length);
                        break;
                    case "perk7":
                        byte[] perk7 = Encoding.UTF8.GetBytes("perk7: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(perk7, 0, perk7.Length);
                        break;
                    case "spawn":
                        byte[] spawn = Encoding.UTF8.GetBytes("spawn: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(spawn, 0, spawn.Length);
                        break;
                    case "zspawn":
                        byte[] zspawn = Encoding.UTF8.GetBytes("zombiespawn: " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(zspawn, 0, zspawn.Length);
                        break;
                    case "wall":
                        byte[] wall = Encoding.UTF8.GetBytes("wall: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("end").Origin + '\r' + '\n');
                        currentMapFile.Write(wall, 0, wall.Length);
                        break;
                    case "invisiblewall":
                        byte[] iwall = Encoding.UTF8.GetBytes("invisiblewall: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("end").Origin + '\r' + '\n');
                        currentMapFile.Write(iwall, 0, iwall.Length);
                        break;
                    case "deathwall":
                        byte[] dwall = Encoding.UTF8.GetBytes("deathwall: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("end").Origin + '\r' + '\n');
                        currentMapFile.Write(dwall, 0, dwall.Length);
                        break;
                    case "ramp":
                        byte[] ramp = Encoding.UTF8.GetBytes("ramp: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("end").Origin + '\r' + '\n');
                        currentMapFile.Write(ramp, 0, ramp.Length);
                        break;
                    case "tp":
                        byte[] tp = Encoding.UTF8.GetBytes("teleporter: " + box.Origin + " ; " + box.GetField<Entity>("dest").Origin + '\r' + '\n');
                        currentMapFile.Write(tp, 0, tp.Length);
                        break;
                    case "teleporter":
                        byte[] tele = Encoding.UTF8.GetBytes("timedTeleporter: " + (box.GetField<Entity>("tpBase").Origin - new Vector3(0, 0, 45)) + " ; " + box.GetField<Entity>("tpBase").Angles + " ; " + box.GetField<Entity>("dest").Origin + " ; " + box.GetField<Entity>("dest").Angles + " ; " + (box.Origin - new Vector3(0, 0, 45)) + " ; " + box.Angles + " ; " + box.GetField("value") +'\r' + '\n');
                        currentMapFile.Write(tele, 0, tele.Length);
                        break;
                    case "door":
                        byte[] door = Encoding.UTF8.GetBytes("door: " + box.GetField<Vector3>("open") + " ; " + box.Origin + " ; " + box.Angles + " ; " + box.GetField("size") + " ; " + box.GetField("height") + " ; " + box.GetField("range") + " ; " + box.GetField("value") +'\r' + '\n');
                        currentMapFile.Write(door, 0, door.Length);
                        break;
                    case "floor":
                        byte[] floor = Encoding.UTF8.GetBytes("floor: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("end").Origin + '\r' + '\n');
                        currentMapFile.Write(floor, 0, floor.Length);
                        break;
                    case "invisiblefloor":
                        byte[] ifloor = Encoding.UTF8.GetBytes("invisiblefloor: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("end").Origin + '\r' + '\n');
                        currentMapFile.Write(ifloor, 0, ifloor.Length);
                        break;
                    case "deathfloor":
                        byte[] dfloor = Encoding.UTF8.GetBytes("deathfloor: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("end").Origin + '\r' + '\n');
                        currentMapFile.Write(dfloor, 0, dfloor.Length);
                        break;
                    case "elevator":
                        byte[] elev = Encoding.UTF8.GetBytes("elevator: " + box.GetField<Entity>("start").Origin + " ; " + box.GetField<Entity>("start").Angles + " ; " + box.GetField<Entity>("end").Origin + " ; " + box.Origin +'\r' + '\n');
                        currentMapFile.Write(elev, 0, elev.Length);
                        break;
                    case "model":
                        byte[] model = Encoding.UTF8.GetBytes("model:" + box.GetField<string>("value") + "; " + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(model, 0, model.Length);
                        break;
                    case "crate":
                        byte[] crate = Encoding.UTF8.GetBytes("crate:" + box.Origin + " ; " + box.Angles + '\r' + '\n');
                        currentMapFile.Write(crate, 0, crate.Length);
                        break;
                }
            }
            if (fallLimit != null)
            {
                byte[] fall = Encoding.UTF8.GetBytes("fallLimit:" + fallLimit + '\r' + '\n');
                currentMapFile.Write(fall, 0, fall.Length);
            }
            if (!xLimit.Equals(Vector3.Zero))
            {
                byte[] xlim = Encoding.UTF8.GetBytes("xLimit:" + xLimit.X + ";" + xLimit.Y + '\r' + '\n');
                currentMapFile.Write(xlim, 0, xlim.Length);
            }
            if (!yLimit.Equals(Vector3.Zero))
            {
                byte[] ylim = Encoding.UTF8.GetBytes("yLimit:" + yLimit.X + ";" + yLimit.Y + '\r' + '\n');
                currentMapFile.Write(ylim, 0, ylim.Length);
            }
            if (mapName != "")
            {
                byte[] map = Encoding.UTF8.GetBytes("mapname:" + mapName + '\r' + '\n');
                currentMapFile.Write(map, 0, map.Length);
            }
            if (maxWave  != 30)
            {
                byte[] wave = Encoding.UTF8.GetBytes("maxWave:" + maxWave + '\r' + '\n');
                currentMapFile.Write(wave, 0, wave.Length);
            }
            byte[] hmap = Encoding.UTF8.GetBytes("hellMap:" + hellMap + '\r' + '\n');
            currentMapFile.Write(hmap, 0, hmap.Length);//Always write hell map

            if (weaponBoxContainer.Count != 0)
            {
                byte[] boxEntry = Encoding.UTF8.GetBytes("randombox: ");
                currentMapFile.Write(boxEntry, 0, boxEntry.Length);
                foreach (Entity weaponbox in weaponBoxContainer)
                {
                    byte[] thisBox = Encoding.UTF8.GetBytes(weaponbox.Origin + " ; " + weaponbox.Angles + " ; ");
                    currentMapFile.Write(thisBox, 0, thisBox.Length);
                }
                currentMapFile.Write(new byte[2] { Convert.ToByte('\r'), Convert.ToByte('\n') }, 0, 1);
            }
        }

        private static void saveWaypointFile()
        {
            StreamWriter wpCreate = File.CreateText(currentMapWPFilename);
            foreach (Vector3 s in wpLocs)
                wpCreate.WriteLine(buildStringFromVector(s));
            wpCreate.Flush();
            wpCreate.Close();
        }

        private static Entity spawnCrate(Vector3 origin, Vector3 angles)
        {
            Entity crate = Spawn("script_model", origin);
            crate.Angles = angles;
            crate.SetModel("com_plasticcase_friendly");
            return crate;
        }

        private static Entity spawnModel(string model, Vector3 origin, Vector3 angles)
        {
            Entity crate = Spawn("script_model", origin);
            crate.Angles = angles;
            crate.SetModel(model);
            return crate;
        }

        private static Entity createWall(Vector3 start, Vector3 end, bool invisible, bool death, Entity startEnt, Entity endEnt)
        {
            float D = new Vector3(start.X, start.Y, 0).DistanceTo(new Vector3(end.X, end.Y, 0));
            float H = new Vector3(0, 0, start.Z).DistanceTo(new Vector3(0, 0, end.Z));
            int blocks = (int)Math.Round(D / 55, 0);
            int height = (int)Math.Round(H / 30, 0);

            Vector3 C = end - start;
            Vector3 A = new Vector3(C.X / blocks, C.Y / blocks, C.Z / height);
            float TXA = A.X / 4;
            float TYA = A.Y / 4;
            Vector3 angle = VectorToAngles(C);
            angle = new Vector3(0, angle.Y, 90);
            Entity center = Spawn("script_origin", new Vector3(
                (start.X + end.X) / 2, (start.Y + end.Y) / 2, (start.Z + end.Z) / 2));
            List<Entity> pieces = new List<Entity>();
            for (int h = 0; h < height; h++)
            {
                Entity crate = spawnCrate((start + new Vector3(TXA, TYA, 10) + (new Vector3(0, 0, A.Z) * h)), angle);
                if (invisible && !death) crate.SetModel("com_plasticcase_enemy");
                else if (death) crate.SetModel("com_plasticcase_trap_friendly");
                crate.EnableLinkTo();
                crate.LinkTo(center);
                pieces.Add(crate);
                for (int i = 0; i < blocks; i++)
                {
                    crate = spawnCrate(start + (new Vector3(A.X, A.Y, 0) * i) + new Vector3(0, 0, 10) + (new Vector3(0, 0, A.Z) * h), angle);
                    if (invisible && !death) crate.SetModel("com_plasticcase_enemy");
                    else if (death) crate.SetModel("com_plasticcase_trap_friendly");
                    crate.EnableLinkTo();
                    crate.LinkTo(center);
                    pieces.Add(crate);
                }
                crate = spawnCrate(new Vector3(end.X, end.Y, start.Z) + new Vector3(TXA * -1, TYA * -1, 10) + (new Vector3(0, 0, A.Z) * h), angle);
                if (invisible && !death) crate.SetModel("com_plasticcase_enemy");
                else if (death) crate.SetModel("com_plasticcase_trap_friendly");
                crate.EnableLinkTo();
                crate.LinkTo(center);
                pieces.Add(crate);
            }
            center.SetField("pieces", new Parameter(pieces));
            startEnt.LinkTo(center);
            endEnt.LinkTo(center);
            return center;
        }

        private static Entity createFloor(Vector3 corner1, Vector3 corner2, bool invisible, bool death, Entity start, Entity end)
        {
            float width = corner1.X - corner2.X;
            if (width < 0) width = width * -1;
            float length = corner1.Y - corner2.Y;
            if (length < 0) length = length * -1;

            int bwide = (int)Math.Round(width / 50, 0);
            int blength = (int)Math.Round(length / 30, 0);
            Vector3 C = corner2 - corner1;
            Vector3 A = new Vector3(C.X / bwide, C.Y / blength, 0);
            Entity center = Spawn("script_origin", new Vector3(
                (corner1.X + corner2.X) / 2, (corner1.Y + corner2.Y) / 2, corner1.Z));
            List<Entity> pieces = new List<Entity>();
            for (int i = 0; i < bwide; i++)
            {
                for (int j = 0; j < blength; j++)
                {
                    Entity crate = spawnCrate(corner1 + (new Vector3(A.X, 0, 0) * i) + (new Vector3(0, A.Y, 0) * j), new Vector3(0, 0, 0));
                    if (invisible && !death) crate.SetModel("com_plasticcase_enemy");
                    else if (death) crate.SetModel("com_plasticcase_trap_friendly");
                    crate.EnableLinkTo();
                    crate.LinkTo(center);
                    pieces.Add(crate);
                }
            }
            center.SetField("pieces", new Parameter(pieces));
            start.LinkTo(center);
            end.LinkTo(center);
            return center;
        }

        private static Entity createRamp(Vector3 top, Vector3 bottom, Entity start, Entity end)
        {
            float distance = top.DistanceTo(bottom);
            int blocks = (int)Math.Ceiling(distance / 30);
            Vector3 A = new Vector3((top.X - bottom.X) / blocks, (top.Y - bottom.Y) / blocks, (top.Z - bottom.Z) / blocks);
            Vector3 temp = VectorToAngles(top - bottom);
            Vector3 BA = new Vector3(temp.Z, temp.Y + 90, temp.X);
            List<Entity> pieces = new List<Entity>();
            for (int b = 0; b <= blocks; b++)
            {
                Entity piece = spawnCrate(bottom + (A * b), BA);
                pieces.Add(piece);
            }
            Entity ramp = Spawn("script_model", pieces[0].Origin);
            ramp.EnableLinkTo();
            foreach (Entity e in pieces)
                e.LinkTo(ramp);
            ramp.SetField("pieces", new Parameter(pieces));
            start.LinkTo(ramp);
            end.LinkTo(ramp);
            return ramp;
        }

        private static Entity spawnDoor(Vector3 close, Vector3 angle, int size, int height, int range)
        {
            double offset = (((size / 2) - 0.5) * -1);
            Entity center = Spawn("script_model", close);
            List<Entity> pieces = new List<Entity>();
            for (int j = 0; j < size; j++)
            {
                Entity door = spawnCrate(close + (new Vector3(0, 30, 0) * (float)offset), new Vector3(0, 0, 0));
                door.SetModel("com_plasticcase_enemy");
                door.EnableLinkTo();
                door.LinkTo(center);
                pieces.Add(door);
                for (int h = 1; h < height; h++)
                {
                    Entity door2 = spawnCrate(close + (new Vector3(0, 30, 0) * (float)offset) - (new Vector3(70, 0, 0) * h), new Vector3(0, 0, 0));
                    door2.SetModel("com_plasticcase_enemy");
                    door2.EnableLinkTo();
                    door2.LinkTo(center);
                    pieces.Add(door2);
                }
                offset += 1;
            }
            center.Angles = angle;
            center.SetField("close", close);
            center.SetField("size", size);
            center.SetField("height", height);
            center.SetField("range", range);
            center.SetField("pieces", new Parameter(pieces));
            return center;
        }

        private static Entity createTeleporterBase(Vector3 startPos, Vector3 startAngles)
        {
            Entity teleporter = Spawn("script_model", startPos + new Vector3(0, 0, 45));
            teleporter.Angles = startAngles;
            teleporter.SetModel("tag_origin");
            teleporter.Hide();
            teleporter.EnableLinkTo();
            Entity[] floorsActive = new Entity[6];
            for (int i = 0; i < 6; i++)
            {
                Entity floor = Spawn("script_model", startPos);
                floor.SetModel("com_plasticcase_enemy");
                Vector3 offset = new Vector3(0, 0, 0);
                switch (i)
                {
                    case 0:
                        offset = new Vector3(28, 30, -45);
                        break;
                    case 1:
                        offset = new Vector3(-28, 30, -45);
                        break;
                    case 2:
                        offset = new Vector3(28, -30, -45);
                        break;
                    case 3:
                        offset = new Vector3(-28, -30, -45);
                        break;
                    case 4:
                        offset = new Vector3(28, 0, -45);
                        break;
                    case 5:
                        offset = new Vector3(-28, 0, -45);
                        break;
                }
                floor.LinkTo(teleporter, "tag_origin", offset);
                floorsActive[i] = floor;
            }
            List<Entity> pieces = new List<Entity>();
            foreach (Entity e in floorsActive)
            {
                pieces.Add(e);
            }
            teleporter.SetField("pieces", new Parameter(pieces));
            return teleporter;
        }

        private static Vector3 buildVectorFromString(string vec3)
        {
            vec3 = vec3.Replace(" ", string.Empty);
            if (!vec3.StartsWith("(") && !vec3.EndsWith(")"))
            {
                Log.Write(LogLevel.Error, "Vector was not formatted properly: {0}", vec3);
                return Vector3.Zero;
            }
            vec3 = vec3.Replace("(", string.Empty);
            vec3 = vec3.Replace(")", string.Empty);
            String[] split = vec3.Split(',');
            if (split.Length < 3)
            {
                Log.Write(LogLevel.Error, "Vector was not formatted properly: {0}", vec3);
                return Vector3.Zero;
            }
            return new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
        }

        private static string buildStringFromVector(Vector3 vector)
        {
            string ret = "";
            ret = "(" + vector.X.ToString() + "," + vector.Y.ToString() + "," + vector.Z.ToString() + ")";
            return ret;
        }
    }
}
