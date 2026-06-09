namespace RoguelikeServerMVP
{
    public class GameConfig
    {
        /// <summary>
        /// Ширина видимой области вокруг игрока (по X).
        /// Должно быть нечётное число (чёткий центр).
        /// </summary>
        public int ViewWidth { get; set; }

        /// <summary>
        /// Высота видимой области вокруг игрока (по Y).
        /// Должно быть нечётное число (чёткий центр).
        /// </summary>
        public int ViewHeight { get; set; }

        public int RoomWidth { get; set; }
        public int RoomHeight { get; set; }

        public int PlayerDefaultMaxHp { get; set; }
        public int PlayerAttackDamage { get; set; }

        public int MobAggroRange { get; set; }

        public int AggressiveMobAggroRange { get; set; }

        /// <summary>
        /// Whether the room layout is procedurally generated. If false, the room
        /// is an empty rectangle with a solid border wall.
        /// </summary>
        public bool UseProceduralGeneration { get; set; } = true;

        /// <summary>
        /// Seed for procedural generation. The same seed always produces the same room.
        /// </summary>
        public int Seed { get; set; } = 12345;

        // --- Dungeon (multi-room floor) settings ---

        /// <summary>Number of rooms on the first floor.</summary>
        public int BaseRooms { get; set; } = 5;

        /// <summary>Extra rooms added per floor (difficulty scaling).</summary>
        public int RoomsPerFloor { get; set; } = 2;

        /// <summary>Base size of a mob pack in a (non-start) room on floor 1.</summary>
        public int MobPackSize { get; set; } = 3;
    }
}