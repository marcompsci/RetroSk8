using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class CustomParkTests
    {
        [Test]
        public void Add_PlacesPiece_OnTheGrid_AndOffTheStartPad()
        {
            var park = CustomPark.Create("custom_1", "TEST");
            int i = park.Add(PieceKind.Ledge, CustomPark.SpawnX + 1, 0);
            Assert.AreEqual(0, i);
            var p = park.pieces[0];
            var (w, d) = CustomPark.Footprint(p);
            Assert.IsTrue(CustomPark.InsideGrid(p.x, p.z, w, d));
            Assert.IsFalse(p.x < CustomPark.SpawnX + CustomPark.SpawnW && CustomPark.SpawnX < p.x + w && p.z < CustomPark.SpawnZ + CustomPark.SpawnD,
                "pieces must never cover the start pad");
        }

        [Test]
        public void Add_FindsTheNearestFreeSpot_WhenTheCellIsTaken()
        {
            var park = CustomPark.Create("custom_1", "TEST");
            park.Add(PieceKind.Funbox, 20, 20);
            int second = park.Add(PieceKind.Funbox, 20, 20);
            Assert.AreEqual(1, second);
            Assert.AreNotEqual(park.pieces[0].x * 100 + park.pieces[0].z, park.pieces[1].x * 100 + park.pieces[1].z);
        }

        [Test]
        public void ArrowMoves_StepOneCell_AndStopAtEdgesAndPieces()
        {
            var park = CustomPark.Create("custom_1", "TEST");
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Bench, x = 10, z = 10 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Bench, x = 12, z = 10 });

            Assert.IsTrue(park.TryMove(0, 0, 1), "up");
            Assert.AreEqual(11, park.pieces[0].z);
            Assert.IsTrue(park.TryMove(0, 0, -1), "down");
            Assert.IsTrue(park.TryMove(0, -1, 0), "left");
            Assert.AreEqual(9, park.pieces[0].x);
            Assert.IsTrue(park.TryMove(0, 1, 0), "right");
            Assert.IsTrue(park.TryMove(0, 1, 0), "right again, next to the other bench");
            Assert.IsFalse(park.TryMove(0, 1, 0), "blocked by the other bench");
            Assert.AreEqual(11, park.pieces[0].x);

            park.pieces[1].x = CustomPark.GridCells - 1;
            Assert.IsFalse(park.TryMove(1, 1, 0), "blocked by the grid edge");
        }

        [Test]
        public void Rotate_SwapsFootprint_AndKeepsTheCentre()
        {
            var park = CustomPark.Create("custom_1", "TEST");
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.QuarterPipe, x = 20, z = 20 });
            var before = CustomPark.WorldCenter(park.pieces[0]);
            Assert.IsTrue(park.TryRotate(0));
            Assert.AreEqual(1, park.pieces[0].rot);
            Assert.AreEqual((2, 4), CustomPark.Footprint(park.pieces[0]));
            var after = CustomPark.WorldCenter(park.pieces[0]);
            Assert.Less(System.Math.Abs(before.x - after.x), 2.1f);
            Assert.Less(System.Math.Abs(before.z - after.z), 2.1f);
        }

        [Test]
        public void PieceAt_FindsTappedPiece()
        {
            var park = CustomPark.Create("custom_1", "TEST");
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Bowl, x = 5, z = 20 });
            Assert.AreEqual(0, park.PieceAt(7, 23));
            Assert.AreEqual(-1, park.PieceAt(13, 23));
            var (wx, wz) = CustomPark.WorldCenter(park.pieces[0]);
            var cell = CustomPark.CellAt(wx, wz);
            Assert.AreEqual(0, park.PieceAt(cell.x, cell.z));
        }

        [Test]
        public void Sanitize_DropsOverlapsAndJunk_AndLimitsTheName()
        {
            var park = CustomPark.Create("custom_1", "   A REALLY REALLY LONG PARK NAME   ");
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Funbox, x = 10, z = 10, rot = 7, size = 9 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Funbox, x = 11, z = 11 });   // overlaps the first
            park.pieces.Add(new ParkPiece { kind = 99, x = 30, z = 30 });                        // unknown kind
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Ledge, x = 39, z = 39 });      // off the grid
            park.pieces.Add(null);
            park.Sanitize();
            Assert.AreEqual(1, park.pieces.Count);
            Assert.AreEqual(3, park.pieces[0].rot);
            Assert.AreEqual(2, park.pieces[0].size);
            Assert.LessOrEqual(park.name.Length, CustomPark.MaxNameLength);
        }

        [Test]
        public void Starter_IsValid_AndFull_IsCapped()
        {
            var park = CustomPark.Starter("custom_1", "STARTER");
            Assert.GreaterOrEqual(park.pieces.Count, 4);
            int count = park.pieces.Count;
            park.Sanitize();
            Assert.AreEqual(count, park.pieces.Count, "starter pieces never overlap");

            var full = CustomPark.Create("custom_2", "FULL");
            for (int i = 0; i < CustomPark.MaxPieces + 10; i++) full.Add(PieceKind.Bench, 20, 20);
            Assert.AreEqual(CustomPark.MaxPieces, full.pieces.Count);
        }

        [Test]
        public void Duplicate_PlacesACopyNearby()
        {
            var park = CustomPark.Create("custom_1", "TEST");
            park.Add(PieceKind.Ledge, 10, 20, 1, 2);
            int copy = park.Duplicate(0);
            Assert.AreEqual(1, copy);
            Assert.AreEqual(park.pieces[0].kind, park.pieces[1].kind);
            Assert.AreEqual(1, park.pieces[1].rot);
            Assert.AreEqual(2, park.pieces[1].size);
        }
    }
}
