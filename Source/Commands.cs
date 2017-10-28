using System.IO;
using System.Linq;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Server.Managers;

namespace HouseScript
{
    public class Commands : Script
    {
        [Command("createhouse")]
        public void CMD_CreateHouse(Client player, int type, int price)
        {
            if (API.getPlayerAclGroup(player) != "Admin")
            {
                player.sendChatMessage("~r~ERROR: ~w~Only admins can use this command.");
                return;
            }

            if (type < 0 || type >= HouseTypes.HouseTypeList.Count)
            {
                player.sendChatMessage("~r~ERROR: ~w~Invalid type ID.");
                return;
            }

            House new_house = new House(Main.GetGuid(), string.Empty, type, player.position, price, false);
            new_house.Dimension = Main.DimensionID;
            new_house.Save();

            Main.Houses.Add(new_house);
        }

        [Command("sethousename", GreedyArg = true)]
        public void CMD_HouseName(Client player, string new_name)
        {
            if (API.getPlayerAclGroup(player) != "Admin")
            {
                player.sendChatMessage("~r~ERROR: ~w~Only admins can use this command.");
                return;
            }

            if (!player.hasData("HouseMarker_ID"))
            {
                player.sendChatMessage("~r~ERROR: ~w~Stand in the entrance marker of the house you want to edit.");
                return;
            }

            House house = Main.Houses.FirstOrDefault(h => h.ID == player.getData("HouseMarker_ID"));
            if (house == null) return;

            house.SetName(new_name);
            player.sendChatMessage(string.Format("~b~HOUSE SCRIPT: ~w~House name set to ~y~\"{0}\".", new_name));
        }

        [Command("sethousetype")]
        public void CMD_HouseType(Client player, int new_type)
        {
            if (API.getPlayerAclGroup(player) != "Admin")
            {
                player.sendChatMessage("~r~ERROR: ~w~Only admins can use this command.");
                return;
            }

            if (!player.hasData("HouseMarker_ID"))
            {
                player.sendChatMessage("~r~ERROR: ~w~Stand in the entrance marker of the house you want to edit.");
                return;
            }

            if (new_type < 0 || new_type >= HouseTypes.HouseTypeList.Count)
            {
                player.sendChatMessage("~r~ERROR: ~w~Invalid type ID.");
                return;
            }

            House house = Main.Houses.FirstOrDefault(h => h.ID == player.getData("HouseMarker_ID"));
            if (house == null) return;

            house.SetType(new_type);
            player.sendChatMessage(string.Format("~b~HOUSE SCRIPT: ~w~House type set to ~y~{0}.", HouseTypes.HouseTypeList[new_type].Name));
        }

        [Command("sethouseprice")]
        public void CMD_HousePrice(Client player, int new_price)
        {
            if (API.getPlayerAclGroup(player) != "Admin")
            {
                player.sendChatMessage("~r~ERROR: ~w~Only admins can use this command.");
                return;
            }

            if (!player.hasData("HouseMarker_ID"))
            {
                player.sendChatMessage("~r~ERROR: ~w~Stand in the entrance marker of the house you want to edit.");
                return;
            }

            House house = Main.Houses.FirstOrDefault(h => h.ID == player.getData("HouseMarker_ID"));
            if (house == null) return;

            house.SetPrice(new_price);
            player.sendChatMessage(string.Format("~b~HOUSE SCRIPT: ~w~House price set to ~g~${0:n0}.", new_price));
        }

        [Command("removehouse")]
        public void CMD_RemoveHouse(Client player)
        {
            if (API.getPlayerAclGroup(player) != "Admin")
            {
                player.sendChatMessage("~r~ERROR: ~w~Only admins can use this command.");
                return;
            }

            if (!player.hasData("HouseMarker_ID"))
            {
                player.sendChatMessage("~r~ERROR: ~w~Stand in the entrance marker of the house you want to remove.");
                return;
            }

            House house = Main.Houses.FirstOrDefault(h => h.ID == player.getData("HouseMarker_ID"));
            if (house == null) return;

            house.Destroy();
            Main.Houses.Remove(house);

            string house_file = Main.HOUSE_SAVE_DIR + Path.DirectorySeparatorChar + house.ID + ".json";
            if (File.Exists(house_file)) File.Delete(house_file);
        }
    }
}