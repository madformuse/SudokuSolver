using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SudokuSolver;
using SudokuSolver.Constraints;
using static SudokuSolver.SolverUtility;

namespace SudokuTests.UnitTests
{
    [TestClass]
    public class LockoutLineTests : ConstraintTests<LockoutLineConstraint>
    {
        [TestMethod]
        public void NamedAfterDiamonds()
        {
            var lockout = CreateConstraint("r1c1r1c2r1c3");

            Assert.AreEqual("Lockout Line r1c1-r1c3", lockout.SpecificName);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void OnlyAcceptsSingleGroup()
        {
            CreateConstraint("r1c1r1c2;r1c3r1c4");
        }

        public void CellsContainsAllCellsInLine()
        {
            var lockout = CreateConstraint("r1c1r1c2r1c3r1c4");

            Assert.AreEqual(4, lockout.Cells.Count);
        }

        [TestMethod]
        public void DiamondsCantAppearAnywhereOnLine()
        {
            var lockout = CreateConstraint("r1c1r1c2r2c3r2c4");

            Assert.AreEqual(3, lockout.SeenCells((0, 0)).Count());
            Assert.AreEqual(3, lockout.SeenCells((1, 3)).Count());
        }

        [TestMethod]
        public void InitRemovesImpossibleCandidatesFromLine()
        {
            var lockout = CreateConstraint("r1c1r1c2r1c3");

            lockout.InitCandidates(SudokuSolver);

            Assert.IsFalse(HasValue(SudokuSolver.Board[0, 1], 5));
        }

        [TestMethod]
        public void WorksForOddSizedGrid()
        {
            var lockout = CreateConstraint("r1c1r1c2r1c3", 17);

            lockout.InitCandidates(SudokuSolver);

            // This time it's 9 that can't appear
            Assert.IsFalse(HasValue(SudokuSolver.Board[0, 1], 9));
        }

        [TestMethod]
        public void InEvenGridMiddle2DigitsAreImpossible()
        {
            var lockout = CreateConstraint("r1c1-4", 16);

            lockout.InitCandidates(SudokuSolver);

            var lineCellMask = SudokuSolver.Board[0, 2];

            Assert.IsFalse(HasValue(lineCellMask, 8));
            Assert.IsFalse(HasValue(lineCellMask, 9));
        }

        [TestMethod]
        public void DetectsInvalidDiamondValues()
        {
            TestLogic(
                options: "r1c1-3",
                gridSize: 9,
                expectedResult: LogicResult.Invalid,
                messageContains: "No valid options are left for the diamonds",
                setup: (solver) =>
                {
                    // Set r1c1 to only have candidates of 1 & 2
                    solver.Board[0, 0] &= ValuesMask(1, 2);
                    solver.Board[0, 2] &= ValuesMask(3, 4);
                }
            );
        }

        [TestMethod]
        public void LimitsDiamondsToPossibleCandidates()
        {
            

            var elims = new List<int>();

            TestLogic(
                options: "r1c1-3",
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Diamonds must be at least 4 apart",
                setup: (solver) =>
                {
                    // Set r1c1 to only have candidates of 1 & 2
                    solver.Board[0, 0] &= ValuesMask(1, 2);
                },
                after: (solver) =>
                {
                    var secondDiamondMask = solver.Board[0, 2];

                    Assert.AreEqual(
                        0, 
                        (int)(secondDiamondMask & MaskStrictlyLower(5)), 
                        $"Should have removed all options lower than 5 but found { MaskToString(secondDiamondMask) }"
                    );
                }
            );
        }

        [TestMethod]
        public void LineNotBetweenDiamonds()
        {
            TestLogic(
                options: "r1c1-3",
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Removing candidates from line which fall between all possible pairs",
                setup: (solver) =>
                {
                    // Set r1c1 to only have candidates of 1 & 2
                    solver.SetValue(0, 0, 1);
                    solver.SetValue(0, 2, 5);
                },
                after: (solver) =>
                {
                    var lineMask = solver.Board[0, 1];

                    Assert.AreEqual(
                        0,
                        (int)(lineMask & MaskStrictlyLower(6)),
                        $"Should have removed all options lower than 6 but found { MaskToString(lineMask) }"
                    );
                }
            );
        }

        [TestMethod]
        public void DigitOnTheLineLimitsDiamonds()
        {
            TestLogic(
                options: "r1c1-3",
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Removing candidates from diamond which would break a cell in the line",
                setup: (solver) =>
                {
                    // Setting the middle to 4 should rule out 1 - 4 from the diamonds
                    solver.SetValue(0, 1, 4);
                },
                after: (solver) =>
                {
                    var diamondMask = solver.Board[0, 2];

                    Assert.AreEqual(
                        0,
                        (int)(diamondMask & MaskStrictlyLower(5)),
                        $"Should have removed all options lower than 5 but found { MaskToString(diamondMask) }"
                    );
                }
            );
        }
        
        [TestMethod]
        public void UniqueConstraintsLimitDiamondDiff()
        {
            TestLogic(
                options: "r1c1r2c1-3r3c3r3c4",   // Line is 4 long (excluding diamonds) so diff is max 4.
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Line contains the maximum 4 unique digits so diamonds must be exactly 4 apart",
                setup: (solver) =>
                {
                    // If we set the first diamond to 1, the second has to be 5 (or there aren't enough spare digits)
                    solver.SetValue(0, 0, 1);

                    // We have to reduce the options for the second diamond to 5 or higher, otherwise the earlier logic
                    // would make that change first.
                    solver.SetMask(2, 3, ValuesMask(5, 6, 7, 8, 9));

                    // Similarly, we need to remove the normally constrained options 2, 3 and 4 from the line (1 removed because of seen cells)
                    var toRemove = ValuesMask(2, 3, 4);
                    var from = new (int, int)[] { (1, 0), (1, 1), (1, 2), (2, 2) };

                    foreach(var cell in from)
                    {
                        solver.ClearMask(cell.Item1, cell.Item2, toRemove);
                    }

                    // We also need to remove 9 as an option from second diamond or it realises that it would break the individual cells.
                    solver.ClearMask(2, 3, ValueMask(9));
                },
                after: (solver) =>
                {
                    // Now only 5 should remain
                    var diamondMask = solver.Board[2, 3];

                    Assert.AreEqual(
                        0,
                        (int)(diamondMask & ValuesMask(6,7,8,9)),
                        $"Should have removed all options higher than 5 but found { MaskToString(diamondMask) }"
                    );
                }
            );
        }

        protected override LockoutLineConstraint CreateConstraint(Solver solver, string options)
        {
            return new LockoutLineConstraint(solver, options);
        }
    }
}
