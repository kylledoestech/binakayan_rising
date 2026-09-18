using System.Collections.Generic;
using BinakayanRising.Core.Grid;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>One building of the encampment: where it stands and where you stand to use it.</summary>
    public sealed class CampSite
    {
        /// <summary>The <see cref="Places"/> id. Objectives point at sites by this id.</summary>
        public readonly string Place;

        /// <summary>Sprite name under <c>Art/Encampment/</c>.</summary>
        public readonly string Art;

        /// <summary>The footprint's lowest cell, and its size in cells.</summary>
        public readonly GridCoord Origin;
        public readonly int Width;
        public readonly int Height;

        /// <summary>The walkable cell the player walks to before the building opens.</summary>
        public readonly GridCoord Door;

        public readonly LocString Name;

        /// <summary>One line on what the building is for, shown on its name plate on hover.</summary>
        public readonly LocString Purpose;

        /// <summary>The <see cref="Characters"/> id of whoever runs it, or null.</summary>
        public readonly string Keeper;

        public CampSite(string place, string art, GridCoord origin, int width, int height, GridCoord door,
            LocString name, LocString purpose, string keeper)
        {
            Place = place;
            Art = art;
            Origin = origin;
            Width = width;
            Height = height;
            Door = door;
            Name = name;
            Purpose = purpose;
            Keeper = keeper;
        }

        public bool Covers(GridCoord cell)
        {
            return cell.X >= Origin.X && cell.Y >= Origin.Y
                && cell.X < Origin.X + Width && cell.Y < Origin.Y + Height;
        }

        /// <summary>The footprint's middle, in fractional cells. Where the sprite's pivot goes.</summary>
        public float CentreX
        {
            get { return Origin.X + ((Width - 1) * 0.5f); }
        }

        public float CentreY
        {
            get { return Origin.Y + ((Height - 1) * 0.5f); }
        }
    }

    /// <summary>A person standing in the camp.</summary>
    public sealed class CampFigure
    {
        /// <summary>The <see cref="Characters"/> id, which is also the sprite folder.</summary>
        public readonly string Character;

        public readonly GridCoord Cell;

        /// <summary>Where the player stands to talk to them.</summary>
        public readonly GridCoord Talk;

        /// <summary>Figures are drawn facing right; this mirrors one to face left.</summary>
        public readonly bool FacesLeft;

        public CampFigure(string character, GridCoord cell, GridCoord talk, bool facesLeft)
        {
            Character = character;
            Cell = cell;
            Talk = talk;
            FacesLeft = facesLeft;
        }
    }

    /// <summary>Scenery that takes up a cell: a flag, a palm, a stack of crates.</summary>
    public sealed class CampProp
    {
        /// <summary>Sprite name under <c>Art/Encampment/</c>.</summary>
        public readonly string Art;

        public readonly GridCoord Cell;

        public CampProp(string art, GridCoord cell)
        {
            Art = art;
            Cell = cell;
        }
    }

    /// <summary>
    /// The map of the encampment hub: a square grid of cells with the eight facilities along its
    /// edges and an open plaza in the middle where the aide waits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cells use the battle board's convention: X runs up and to the right on screen, Y up and to
    /// the left, so (0, 0) is the corner nearest the viewer. The five tall buildings stand along
    /// the two far edges, where nothing can walk behind them; the Farm, the Training Grounds and
    /// the Exchange stall are low and stand along the near edges.
    /// </para>
    /// <para>
    /// The layout is data so its promises can be tested with the editor closed: nothing overlaps,
    /// every door and every keeper can be reached from where the player appears, and every place
    /// an objective can name exists here.
    /// </para>
    /// </remarks>
    public static class Encampment
    {
        public const int Width = 12;
        public const int Height = 12;

        /// <summary>
        /// Where the player appears on entering the camp: beside the aide rather than in front of
        /// him, so the figure the first objective points at is not hidden behind the player's.
        /// </summary>
        public static readonly GridCoord Spawn = new GridCoord(6, 4);

        private static readonly List<CampSite> sites = new List<CampSite>
        {
            new CampSite(Places.MissionTent, "MissionTent", new GridCoord(9, 9), 3, 3, new GridCoord(8, 8),
                new LocString("Mission Tent", "Tolda ng Misyon"),
                new LocString("Choose your next mission on the map of Cavite", "Piliin ang susunod na misyon sa mapa ng Kabite"),
                null),
            new CampSite(Places.Library, "Library", new GridCoord(9, 4), 3, 3, new GridCoord(8, 5),
                new LocString("Library", "Aklatan"),
                new LocString("Read lessons and take each level's assessment", "Magbasa ng aralin at kumuha ng pagsusulit ng bawat antas"),
                null),
            new CampSite(Places.Mine, "Mine", new GridCoord(9, 0), 3, 3, new GridCoord(8, 1),
                new LocString("Mine", "Minahan"),
                new LocString("Dig Scrap for drills and weapon synthesis", "Humukay ng Bakal para sa pagsasanay at pagpapanday"),
                Characters.Miner),
            new CampSite(Places.Armory, "Armory", new GridCoord(4, 9), 3, 3, new GridCoord(5, 8),
                new LocString("Armory", "Taguan ng Armas"),
                new LocString("Your inventory: equip and reforge weapons", "Iyong imbentaryo: magbigay at magpanday ng sandata"),
                null),
            new CampSite(Places.Recruitment, "RecruitmentHall", new GridCoord(0, 9), 3, 3, new GridCoord(1, 8),
                new LocString("Recruitment Hall", "Bulwagan ng Pangangalap"),
                new LocString("Recruit new units with Reales", "Mangalap ng bagong yunit gamit ang Reales"),
                null),
            new CampSite(Places.Farm, "Farm", new GridCoord(4, 0), 3, 3, new GridCoord(5, 3),
                new LocString("Farm", "Bukid"),
                new LocString("Grow Rations to feed your army on missions", "Magtanim ng Rasyon para sa hukbo sa mga misyon"),
                Characters.Farmer),
            new CampSite(Places.Training, "TrainingGrounds", new GridCoord(0, 4), 3, 3, new GridCoord(3, 5),
                new LocString("Training Grounds", "Sanayan"),
                new LocString("Drill units to raise their level", "Sanayin ang mga yunit upang tumaas ang antas"),
                Characters.Sergeant),
            new CampSite(Places.Exchange, "Exchange", new GridCoord(0, 0), 2, 2, new GridCoord(2, 2),
                new LocString("Exchange", "Palitan"),
                new LocString("Sell Rations and Scrap for Reales", "Ipagbili ang Rasyon at Bakal kapalit ng Reales"),
                Characters.Trader)
        };

        private static readonly List<CampFigure> figures = new List<CampFigure>
        {
            new CampFigure(Characters.Tomas, new GridCoord(6, 6), new GridCoord(5, 6), true),
            new CampFigure(Characters.Farmer, new GridCoord(4, 3), new GridCoord(5, 3), false),
            new CampFigure(Characters.Miner, new GridCoord(8, 2), new GridCoord(8, 1), true),
            new CampFigure(Characters.Trader, new GridCoord(1, 2), new GridCoord(2, 2), false),
            new CampFigure(Characters.Sergeant, new GridCoord(3, 6), new GridCoord(3, 5), false)
        };

        private static readonly List<CampProp> props = new List<CampProp>
        {
            new CampProp("Flag", new GridCoord(7, 7)),
            new CampProp("Palm", new GridCoord(7, 11)),
            new CampProp("Palm", new GridCoord(11, 7)),
            new CampProp("Crates", new GridCoord(3, 11)),
            new CampProp("Cart", new GridCoord(11, 3)),
            new CampProp("Sacks", new GridCoord(7, 0)),
            new CampProp("Rack", new GridCoord(0, 7))
        };

        public static IReadOnlyList<CampSite> Sites
        {
            get { return sites; }
        }

        public static IReadOnlyList<CampFigure> Figures
        {
            get { return figures; }
        }

        public static IReadOnlyList<CampProp> Props
        {
            get { return props; }
        }

        /// <summary>The site for a <see cref="Places"/> id, or null.</summary>
        public static CampSite Site(string place)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                if (sites[i].Place == place)
                {
                    return sites[i];
                }
            }

            return null;
        }

        /// <summary>The building covering <paramref name="cell"/>, or null.</summary>
        public static CampSite SiteAt(GridCoord cell)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                if (sites[i].Covers(cell))
                {
                    return sites[i];
                }
            }

            return null;
        }

        /// <summary>The figure for a <see cref="Characters"/> id, or null.</summary>
        public static CampFigure Figure(string character)
        {
            for (int i = 0; i < figures.Count; i++)
            {
                if (figures[i].Character == character)
                {
                    return figures[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Where the player stands to use <paramref name="place"/>: a building's door, or the cell
        /// beside the aide. Null for an unknown place.
        /// </summary>
        public static GridCoord? StandCell(string place)
        {
            if (place == Places.Aide)
            {
                return Figure(Characters.Tomas).Talk;
            }

            CampSite site = Site(place);
            return site != null ? site.Door : (GridCoord?)null;
        }

        public static bool Inside(GridCoord cell)
        {
            return cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;
        }

        /// <summary>True when the player can stand on <paramref name="cell"/>.</summary>
        public static bool IsWalkable(GridCoord cell)
        {
            if (!Inside(cell) || SiteAt(cell) != null)
            {
                return false;
            }

            for (int i = 0; i < figures.Count; i++)
            {
                if (figures[i].Cell == cell)
                {
                    return false;
                }
            }

            for (int i = 0; i < props.Count; i++)
            {
                if (props[i].Cell == cell)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The player's walk from one cell to another, or null when there is no way.</summary>
        public static List<GridCoord> Path(GridCoord from, GridCoord to)
        {
            return GridPathfinder.FindPath(Width, Height, IsWalkable, from, to);
        }
    }
}
