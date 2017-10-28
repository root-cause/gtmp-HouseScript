using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Constant;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Server.Managers;
using GrandTheftMultiplayer.Shared;
using GrandTheftMultiplayer.Shared.Math;
using Newtonsoft.Json;

namespace HouseScript
{
    #region House Class
    public class House
    {
        public Guid ID { get; }
        public string Owner { get; private set; }
        public int Type { get; private set; }
        public Vector3 Position { get; }
        public int Price { get; private set; }
        public bool Locked { get; private set; }
        [JsonIgnore] public int Dimension { get; set; }

        // customization
        public string Name { get; private set; }
        public List<HouseFurniture> Furnitures { get; }

        // storage
        public long Money { get; private set; }
        public List<HouseWeapon> Weapons { get; }

        // entities
        [JsonIgnore]
        private Blip Blip;

        [JsonIgnore]
        private Marker Marker;

        [JsonIgnore]
        private ColShape ColShape;

        [JsonIgnore]
        private TextLabel Label;

        // misc
        [JsonIgnore]
        private List<NetHandle> PlayersInside = new List<NetHandle>();

        [JsonIgnore]
        private Client OwnerHandle;

        [JsonIgnore]
        private DateTime LastSave;

        public House(Guid id, string owner, int type, Vector3 position, int price, bool locked, string name = "", List<HouseFurniture> furnitures = null, long money = 0, List<HouseWeapon> weapons = null)
        {
            ID = id;
            Owner = owner;
            Type = type;
            Position = position;
            Price = price;
            Locked = locked;

            Name = name;
            Furnitures = (furnitures == null) ? new List<HouseFurniture>() : furnitures;

            Money = money;
            Weapons = (weapons == null) ? new List<HouseWeapon>() : weapons;

            // create blip
            Blip = API.shared.createBlip(position);

            if (string.IsNullOrEmpty(owner))
            {
                Blip.sprite = 350;
                Blip.name = "House For Sale";
            }
            else
            {
                Blip.sprite = 40;
                Blip.name = "Player House";
            }

            Blip.color = 0;
            Blip.scale = 1f;
            Blip.shortRange = true;
            Blip.setSyncedData("PlayersInside", 0);

            // create marker
            Marker = API.shared.createMarker(1, position - new Vector3(0.0, 0.0, 1.0), new Vector3(), new Vector3(), new Vector3(1.0, 1.0, 0.5), 150, 64, 196, 255);

            // create colshape
            ColShape = API.shared.createCylinderColShape(position, 0.85f, 0.85f);
            ColShape.onEntityEnterColShape += (s, ent) =>
            {
                Client player;

                if ((player = API.shared.getPlayerFromHandle(ent)) != null)
                {
                    player.setData("HouseMarker_ID", ID);
                    player.triggerEvent("ShowHouseText", 1);
                }
            };

            ColShape.onEntityExitColShape += (s, ent) =>
            {
                Client player;

                if ((player = API.shared.getPlayerFromHandle(ent)) != null)
                {
                    player.resetData("HouseMarker_ID");
                    player.triggerEvent("ShowHouseText", 0);
                }
            };

            // create text label
            Label = API.shared.createTextLabel("House", position, 15f, 0.65f);
            UpdateLabel();

            OwnerHandle = null;
        }

        private void UpdateLabel()
        {
            if (string.IsNullOrEmpty(Owner))
            {
                Label.text = string.Format("~b~House For Sale~n~~n~~w~Interior: ~b~{0}~n~~w~Price: ~g~${1:n0}", HouseTypes.HouseTypeList[ Type ].Name, Price);
            }
            else
            {
                Label.text = string.Format("~b~House~n~~n~~w~{0}~n~~w~Owner: ~b~{1}~n~{2}", Name, Owner, ((Locked) ? "~r~Locked" : "~g~Not Locked"));
            }
        }

        private void UpdateBlip()
        {
            int count = PlayersInside.Count;
            Blip.setSyncedData("PlayersInside", count);

            if (count < 1)
            {
                if (Blip.sprite != 40)
                {
                    Blip.sprite = 40;

                    if (OwnerHandle != null) UpdateBlipForOwner();
                }

                API.shared.sendNativeToAllPlayers(Hash.HIDE_NUMBER_ON_BLIP, Blip.handle);
            }
            else
            {
                if (Blip.sprite != 417)
                {
                    Blip.sprite = 417;

                    if (OwnerHandle != null) UpdateBlipForOwner();
                }

                API.shared.sendNativeToAllPlayers(Hash.SHOW_NUMBER_ON_BLIP, Blip.handle, count);
            }
        }

        public void SetOwner(Client player)
        {
            if (OwnerHandle != null) OwnerHandle.triggerEvent("ResetHouseBlip", Blip.handle); 
            Owner = (player == null) ? string.Empty : player.socialClubName;

            Blip.sprite = (player == null) ? 350 : 40;
            Blip.color = 0;
            Blip.shortRange = true;

            SetOwnerHandle(player);
            UpdateLabel();
            Save();
        }

        public void SetOwnerHandle(Client player)
        {
            OwnerHandle = player;
            if (player != null) player.triggerEvent("UpdateHouseBlip", Blip.handle);
        }

        public void UpdateBlipForOwner()
        {
            if (OwnerHandle == null) return;
            OwnerHandle.triggerEvent("UpdateHouseBlip", Blip.handle);
        }

        public void SetName(string new_name)
        {
            Name = new_name;

            UpdateLabel();
            Save();
        }

        public void SetLock(bool locked)
        {
            Locked = locked;

            UpdateLabel();
            Save();
        }

        public void SetType(int new_type)
        {
            Type = new_type;

            UpdateLabel();
            Save();
        }

        public void SetPrice(int new_price)
        {
            Price = new_price;

            UpdateLabel();
            Save();
        }

        public void ChangeMoney(int amount)
        {
            Money += amount;

            Save();
        }

        public void SendPlayer(Client player)
        {
            player.position = HouseTypes.HouseTypeList[ Type ].Position;
            player.dimension = Dimension;
            player.setData("InsideHouse_ID", ID);

            if (!PlayersInside.Contains(player.handle)) PlayersInside.Add(player.handle);
            UpdateBlip();
        }

        public void RemovePlayer(Client player, bool set_pos = true)
        {
            if (set_pos)
            {
                player.position = Position;
                player.dimension = 0;
            }

            player.resetData("InsideHouse_ID");

            if (PlayersInside.Contains(player.handle)) PlayersInside.Remove(player.handle);
            UpdateBlip();
        }

        public void RemoveAllPlayers(bool exit = false)
        {
            for (int i = PlayersInside.Count - 1; i >= 0; i--)
            {
                Client player = API.shared.getEntityFromHandle<Client>(PlayersInside[i]);
                
                if (player != null)
                {
                    player.position = Position;
                    player.dimension = 0;

                    player.resetData("InsideHouse_ID");
                }

                PlayersInside.RemoveAt(i);
            }

            if (!exit) UpdateBlip();
        }

        public void Save(bool force = false)
        {
            if (!force && DateTime.Now.Subtract(LastSave).TotalSeconds < Main.SAVE_INTERVAL) return;

            File.WriteAllText(Main.HOUSE_SAVE_DIR + Path.DirectorySeparatorChar + ID + ".json", JsonConvert.SerializeObject(this, Formatting.Indented));
            LastSave = DateTime.Now;
        }

        public void Destroy(bool exit = false)
        {
            foreach (HouseFurniture furniture in Furnitures) furniture.Destroy();
            RemoveAllPlayers(exit);

            Blip.delete();
            Marker.delete();
            API.shared.deleteColShape(ColShape);
            Label.delete();
        }
    }
    #endregion

    public class Main : Script
    {
        // settings
        public static string HOUSE_SAVE_DIR = "HouseData";
        public static int PLAYER_HOUSE_LIMIT = 3;
        public static int HOUSE_MONEY_LIMIT = 5000000;
        public static int HOUSE_WEAPON_LIMIT = 10;
        public static int HOUSE_FURNITURE_LIMIT = 25;
        public static bool RESET_DIMENSION_ON_DEATH = true;
        public static int SAVE_INTERVAL = 120;

        public static List<House> Houses = new List<House>();       
        public static int DimensionID = 1;

        public Main()
        {
            API.onResourceStart += House_Init;

            API.onPlayerFinishedDownload += House_PlayerJoin;
            API.onClientEventTrigger += House_ClientEvent;
            API.onPlayerDeath += House_PlayerDeath;
            API.onPlayerDisconnected += House_PlayerLeave;

            API.onResourceStop += House_Exit;
        }

        #region Methods
        public static Guid GetGuid()
        {
            Guid new_guid;

            do
            {
                new_guid = Guid.NewGuid();
            } while (Houses.Count(h => h.ID == new_guid) > 0);

            return new_guid;
        }

        public void RemovePlayerFromHouseList(Client player)
        {
            if (player.hasData("InsideHouse_ID"))
            {
                House house = Houses.FirstOrDefault(h => h.ID == player.getData("InsideHouse_ID"));
                if (house == null) return;

                house.RemovePlayer(player, false);
            }
        }
        #endregion

        #region Events
        public void House_Init()
        {
            DimensionID = 1;

            // load settings
            if (API.hasSetting("houseDirName")) HOUSE_SAVE_DIR = API.getSetting<string>("houseDirName");

            HOUSE_SAVE_DIR = API.getResourceFolder() + Path.DirectorySeparatorChar + HOUSE_SAVE_DIR;
            if (!Directory.Exists(HOUSE_SAVE_DIR)) Directory.CreateDirectory(HOUSE_SAVE_DIR);

            if (API.hasSetting("playerHouseLimit")) PLAYER_HOUSE_LIMIT = API.getSetting<int>("playerHouseLimit");
            if (API.hasSetting("houseMoneyLimit")) HOUSE_MONEY_LIMIT = API.getSetting<int>("houseMoneyLimit");
            if (API.hasSetting("houseWeaponLimit")) HOUSE_WEAPON_LIMIT = API.getSetting<int>("houseWeaponLimit");
            if (API.hasSetting("houseFurnitureLimit")) HOUSE_FURNITURE_LIMIT = API.getSetting<int>("houseFurnitureLimit");
            if (API.hasSetting("resetDimensionOnDeath")) RESET_DIMENSION_ON_DEATH = API.getSetting<bool>("resetDimensionOnDeath");
            if (API.hasSetting("saveInterval")) SAVE_INTERVAL = API.getSetting<int>("saveInterval");

            API.consoleOutput("-> Player House Limit: {0}", ((PLAYER_HOUSE_LIMIT == 0) ? "Disabled" : PLAYER_HOUSE_LIMIT.ToString()));
            API.consoleOutput("-> House Safe Limit: ${0:n0}", HOUSE_MONEY_LIMIT);
            API.consoleOutput("-> House Weapon Limit: {0}", ((HOUSE_WEAPON_LIMIT == 0) ? "Disabled" : HOUSE_WEAPON_LIMIT.ToString()));
            API.consoleOutput("-> House Furniture Limit: {0}", ((HOUSE_FURNITURE_LIMIT == 0) ? "Disabled" : HOUSE_FURNITURE_LIMIT.ToString()));
            API.consoleOutput("-> Dimension Reset On Death: {0}", ((RESET_DIMENSION_ON_DEATH) ? "Enabled" : "Disabled"));
            API.consoleOutput("-> Save Interval: {0}", TimeSpan.FromSeconds(SAVE_INTERVAL).ToString(@"hh\:mm\:ss"));

            // load houses
            foreach (string file in Directory.EnumerateFiles(HOUSE_SAVE_DIR, "*.json"))
            {
                House house = JsonConvert.DeserializeObject<House>(File.ReadAllText(file));
                house.Dimension = DimensionID;
                foreach (HouseFurniture furniture in house.Furnitures) furniture.Create(DimensionID);

                Houses.Add(house);
                DimensionID++;
            }

            API.consoleOutput("Loaded {0} houses.", Houses.Count);
        }

        public void House_PlayerJoin(Client player)
        {
            API.delay(2000, true, () => { foreach (House house in Houses.Where(h => h.Owner == player.socialClubName)) house.SetOwnerHandle(player); });
        }

        public void House_ClientEvent(Client player, string event_name, params object[] args)
        {
            switch (event_name)
            {
                case "HouseInteract":
                {
                    if (!player.hasData("HouseMarker_ID")) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("HouseMarker_ID"));
                    if (house == null) return;

                    if (string.IsNullOrEmpty(house.Owner))
                    {
                        // not owned house
                        player.triggerEvent("House_PurchaseMenu", API.toJson(new { Interior = HouseTypes.HouseTypeList[ house.Type ].Name, Price = house.Price }));
                    }
                    else
                    {
                        // owned house
                        if (house.Locked)
                        {
                            if (house.Owner == player.socialClubName)
                            {
                                house.SendPlayer(player);
                            }
                            else
                            {
                                player.sendNotification("Error", "~r~Only the owner can access this house.");
                            }
                        }
                        else
                        {
                            house.SendPlayer(player);
                        }
                    }

                    break;
                }

                case "HousePurchase":
                {
                    if (!player.hasData("HouseMarker_ID")) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("HouseMarker_ID"));
                    if (house == null) return;

                    if (!string.IsNullOrEmpty(house.Owner))
                    {
                        player.sendNotification("Error", "~r~This house is owned.");
                        return;
                    }

                    if (house.Price > API.exported.MoneyAPI.GetMoney(player))
                    {
                        player.sendNotification("Error", "~r~You can't afford this house.");
                        return;
                    }

                    if (PLAYER_HOUSE_LIMIT > 0 && Houses.Count(h => h.Owner == player.socialClubName) >= PLAYER_HOUSE_LIMIT)
                    {
                        player.sendNotification("Error", "~r~You can't own any more houses.");
                        return;
                    }

                    player.sendNotification("House Purchased", "~g~Congratulations, you purchased this house!");

                    house.SetLock(true);
                    house.SetOwner(player);
                    house.SendPlayer(player);

                    API.exported.MoneyAPI.ChangeMoney(player, -house.Price);
                    break;
                }

                case "HouseMenu":
                {
                    if (!player.hasData("InsideHouse_ID")) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("InsideHouse_ID"));
                    if (house == null) return;

                    if (house.Owner != player.socialClubName)
                    {
                        player.sendNotification("Error", "~r~Only the owner can access house menu.");
                        return;
                    }

                    player.triggerEvent("HouseMenu", API.toJson(house));
                    break;
                }

                case "HouseSetName":
                {
                    if (!player.hasData("InsideHouse_ID") || args.Length < 1) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("InsideHouse_ID"));
                    if (house == null) return;

                    if (house.Owner != player.socialClubName)
                    {
                        player.sendNotification("Error", "~r~Only the owner can do this.");
                        return;
                    }

                    string new_name = args[0].ToString();
                    if (new_name.Length > 32)
                    {
                        player.sendNotification("Error", "~r~Name can't be more than 32 characters.");
                        return;
                    }

                    house.SetName(new_name);
                    player.sendNotification("Success", string.Format("~g~House name changed to: ~w~\"{0}\"", new_name));
                    break;
                }

                case "HouseSetLock":
                {
                    if (!player.hasData("InsideHouse_ID") || args.Length < 1) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("InsideHouse_ID"));
                    if (house == null) return;

                    if (house.Owner != player.socialClubName)
                    {
                        player.sendNotification("Error", "~r~Only the owner can do this.");
                        return;
                    }

                    bool new_state = Convert.ToBoolean(args[0]);
                    house.SetLock(new_state);

                    player.sendNotification("Success", ((new_state) ? "~g~The house is now locked." : "~g~The house is now unlocked."));
                    break;
                }

                case "HouseSafe":
                {
                    if (!player.hasData("InsideHouse_ID") || args.Length < 2) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("InsideHouse_ID"));
                    if (house == null) return;

                    if (house.Owner != player.socialClubName)
                    {
                        player.sendNotification("Error", "~r~Only the owner can do this.");
                        return;
                    }

                    int type = Convert.ToInt32(args[0]);
                    int amount = 0;

                    if (!int.TryParse(args[1].ToString(), out amount))
                    {
                        player.sendNotification("Error", "~r~Invalid amount.");
                        return;
                    }

                    if (amount < 1) return;
                    if (type == 0)
                    {
                        if (API.exported.MoneyAPI.GetMoney(player) < amount)
                        {
                            player.sendNotification("Error", "~r~You don't have that much money.");
                            return;
                        }

                        if (house.Money + amount > HOUSE_MONEY_LIMIT)
                        {
                            player.sendNotification("Error", "~r~House money limit reached.");
                            return;
                        }

                        API.exported.MoneyAPI.ChangeMoney(player, -amount);

                        house.ChangeMoney(amount);
                        player.sendNotification("Success", string.Format("~g~Put ${0:n0} to the house safe.", amount));
                        player.triggerEvent("HouseUpdateSafe", API.toJson(new { Money = house.Money }));
                    }
                    else
                    {
                        if (house.Money < amount)
                        {
                            player.sendNotification("Error", "~r~The house safe doesn't have that much money.");
                            return;
                        }

                        API.exported.MoneyAPI.ChangeMoney(player, amount);

                        house.ChangeMoney(-amount);
                        player.sendNotification("Success", string.Format("~g~Took ${0:n0} from the house safe.", amount));
                        player.triggerEvent("HouseUpdateSafe", API.toJson(new { Money = house.Money }));
                    }

                    break;
                }

                case "HouseSell":
                {
                    if (!player.hasData("InsideHouse_ID")) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("InsideHouse_ID"));
                    if (house == null) return;

                    if (house.Owner != player.socialClubName)
                    {
                        player.sendNotification("Error", "~r~Only the owner can do this.");
                        return;
                    }

                    if (house.Money > 0)
                    {
                        player.sendNotification("Error", "~r~Empty the house safe before selling the house.");
                        return;
                    }

                    if (house.Weapons.Count > 0)
                    {
                        player.sendNotification("Error", "~r~Empty the house gun locker before selling the house.");
                        return;
                    }

                    if (house.Furnitures.Count > 0)
                    {
                        player.sendNotification("Error", "~r~Sell the furnitures before selling the house.");
                        return;
                    }

                    int price = (int)Math.Round(house.Price * 0.8);
                    API.exported.MoneyAPI.ChangeMoney(player, price);
                    
                    house.RemoveAllPlayers();
                    house.SetOwner(null);

                    player.sendNotification("Success", string.Format("~g~Sold your house for ${0:n0}.", price));
                    break;
                }

                case "HouseLeave":
                {
                    if (!player.hasData("InsideHouse_ID")) return;

                    House house = Houses.FirstOrDefault(h => h.ID == player.getData("InsideHouse_ID"));
                    if (house == null) return;

                    house.RemovePlayer(player);
                    break;
                }
            }
        }

        public void House_PlayerDeath(Client player, NetHandle killer, int weapon)
        {
            if (RESET_DIMENSION_ON_DEATH) player.dimension = 0;
            RemovePlayerFromHouseList(player);
        }

        public void House_PlayerLeave(Client player, string reason)
        {
            RemovePlayerFromHouseList(player);
            foreach (House house in Houses.Where(h => h.Owner == player.socialClubName)) house.SetOwnerHandle(null);
        }

        public void House_Exit()
        {
            foreach (House house in Houses)
            {
                house.Save(true);
                house.Destroy(true);
            }

            Houses.Clear();
        }
        #endregion
    }
}