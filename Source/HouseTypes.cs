using System.Collections.Generic;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Server.Managers;
using GrandTheftMultiplayer.Shared.Math;

namespace HouseScript
{
    #region HouseType Class
    public class HouseType
    {
        public string Name { get; }
        public Vector3 Position { get; }

        private Marker Marker;
        private ColShape ColShape;
        private TextLabel Label;

        public HouseType(string name, Vector3 position)
        {
            Name = name;
            Position = position;
        }

        public void Create()
        {
            Marker = API.shared.createMarker(1, Position - new Vector3(0.0, 0.0, 1.0), new Vector3(), new Vector3(), new Vector3(1.0, 1.0, 0.5), 150, 64, 196, 255);

            ColShape = API.shared.createCylinderColShape(Position, 0.85f, 0.85f);
            ColShape.onEntityEnterColShape += (s, ent) =>
            {
                Client player;

                if ((player = API.shared.getPlayerFromHandle(ent)) != null)
                {
                    player.triggerEvent("ShowHouseText", 2);
                }
            };

            ColShape.onEntityExitColShape += (s, ent) =>
            {
                Client player;

                if ((player = API.shared.getPlayerFromHandle(ent)) != null)
                {
                    player.triggerEvent("ShowHouseText", 0);
                }
            };

            Label = API.shared.createTextLabel("Leave House", Position, 10f, 0.65f);
        }

        public void Destroy()
        {
            Marker.delete();
            API.shared.deleteColShape(ColShape);
            Label.delete();
        }
    }
    #endregion

    public class HouseTypes : Script
    {
        public static List<HouseType> HouseTypeList = new List<HouseType>
        {
            // name, position
            new HouseType("Low End", new Vector3(265.0858, -1000.888, -99.00855)),
            new HouseType("Medium End", new Vector3(346.453, -1002.456, -99.19622))
        };

        public HouseTypes()
        {
            API.onResourceStart += HouseTypes_Init;
            API.onResourceStop += HouseTypes_Exit;
        }

        #region Events
        public void HouseTypes_Init()
        {
            foreach (HouseType house_type in HouseTypeList) house_type.Create();
        }

        public void HouseTypes_Exit()
        {
            foreach (HouseType house_type in HouseTypeList) house_type.Destroy();
        }
        #endregion
    }
}