using StardewValley.GameData.Buildings;
using StardewValley.GameData.Locations;
using Microsoft.Xna.Framework;


namespace FAUNA
{
    public static class DenDataBuilder
    {
        public static BuildingData BuildData()
        {
            return new BuildingData
            {
                Name = "Familiar Den",
                Description = "A magical den where your familiars live.",
                Texture = "Buildings/FAUNA.FamiliarDen",
                DrawShadow = true,
                Size = new Point(7, 3),
                SourceRect = new Rectangle(0, 0, 112, 128),
                DrawOffset = new Vector2(0, 17),
                FadeWhenBehind = true,
                CollisionMap = "XXXXXXX\nXXXXXXX\nXXXXXXX",
                Builder = "Robin",
                MagicalConstruction = true,
                BuildDays = 0,
                BuildCost = 5000,
                BuildMaterials = new List<BuildingMaterial>
                {
                    new() { ItemId = "(O)709", Amount = 50 },
                    new() { ItemId = "(O)420", Amount = 20  }
                },
                HumanDoor = new Point(1, 2),
                AnimalDoor = new Rectangle(3, 1, 2, 2),
                AnimalDoorOpenDuration  = 0.3125f,
                AnimalDoorOpenSound     = "doorCreak",
                AnimalDoorCloseDuration = 0.3125f,
                AnimalDoorCloseSound    = "doorCreakReverse",
                IndoorMap     = "FAUNA.FamiliarDen",
                IndoorMapType = "StardewValley.GameLocation",
                MaxOccupants  = -1,
                AllowAnimalPregnancy  = false,
                AllowsFlooringUnderneath = true
            };
        }

        public static LocationData BuildLocationData()
        {
            return new LocationData
            {
                DisplayName = "Familiar Den",
                DefaultArrivalTile = new Point(4, 19),
                CreateOnLoad = new CreateLocationData
                {
                    MapPath = "Maps/FAUNA.FamiliarDen"
                }
            };
        }
    }
}