using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SudokuSolver.SolverUtility;
using SudokuSolver;
using SudokuSolver.Constraints;

namespace SudokuTests.UnitTests
{
    [TestClass]
    public class ArrowTests : ConstraintTests<ArrowSumConstraint>
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MustSupplyTwoGroups()
        {
            // Only providing one group where two are required
            CreateConstraint("r1c1");
        }

        [TestMethod]
        public void PopulatesCellGroups()
        {
            var arrow = CreateConstraint("r1c1; r1c2r1c3");

            Assert.AreEqual(1, arrow.circleCells.Count);
            Assert.AreEqual(2, arrow.arrowCells.Count);

            Assert.AreEqual((0, 0), arrow.circleCells[0]);
            Assert.AreEqual((0, 1), arrow.arrowCells[0]);
            Assert.AreEqual((0, 2), arrow.arrowCells[1]);
        }

        [TestMethod]
        public void NamedAfterCircleCell()
        {
            Assert.AreEqual("Arrow at r1c1", CreateConstraint("r1c1; r1c2r1c3").SpecificName);
        }

        [TestMethod]
        public void CantHaveMoreThan3CircleCells()
        {
            // Multi-cell circles must (arrowCells.Count * MAX_VALUE) > 99. So we
            // need at least 7 arrow cells in 16x16.
            var broken_arrow = CreateConstraint("r1c1r1c2r1c3r1c4; r1c5r1c6r1c7r1c8r1c9r1c10r1c11", 16);
            var working_arrow = CreateConstraint("r2c1r2c2r2c3; r1c6r1c7r1c8r1c9r1c10r1c11r1c12", 16);

            Assert.AreEqual(LogicResult.Invalid, broken_arrow.InitCandidates(SudokuSolver));
            Assert.AreEqual(LogicResult.Changed, working_arrow.InitCandidates(SudokuSolver));
        }

        [TestMethod]
        public void ArrowCellsConstainedMax()
        {
            var arrow = CreateConstraint("r1c1; r1c2r1c3r1c4");

            arrow.InitCandidates(SudokuSolver);

            // 3 arrow cells, max any one cell can be is 7 (if max is 9)
            Assert.AreEqual(7, MaxValue(SudokuSolver.Board[0, 1]));
        }

        [TestMethod]
        public void CircleConstrainedMin()
        {
            var arrow = CreateConstraint("r1c1; r1c2r1c3r1c4");

            arrow.InitCandidates(SudokuSolver);

            // 3 arrow cells, min circle can be is 3
            Assert.AreEqual(3, MinValue(SudokuSolver.Board[0,0]));
        }

        [TestMethod]
        public void MaxFirstDigitWorksWhen16by16()
        {
            // Arrow 7 cells long
            var arrow = CreateConstraint("r1c1r1c2; r1c3r1c4r1c5r1c6r1c7r1c8r1c9", 16);

            arrow.InitCandidates(SudokuSolver);

            Assert.AreEqual(11, MaxValue(SudokuSolver.Board[0, 0]), "7 * 16 = 112 so first digit should be <= 11");
        }

        [TestMethod]
        public void ArrowSumMustMatchCircle()
        {
            TestLogic(
                options: "r1c1;r1c2",
                gridSize: 9,
                expectedResult: LogicResult.Invalid,
                messageContains: "Sum of circle 4 and arrow 3 do not match.",
                setup: (solver) =>
                {
                    solver.SetValue(0, 0, 4);
                    solver.SetValue(0, 1, 3);
                }
            );
        }

        [TestMethod]
        public void CantFitSumInCircle()
        {
            // "Sum of arrow ({arrowSum}) is impossible to fill into circle."
            TestLogic(
                options: "r1c1; r1c2r1c3",
                gridSize: 9,
                expectedResult: LogicResult.Invalid,
                messageContains: "Sum of arrow (11) is impossible to fill into circle.",
                setup: (solver) =>
                {
                    solver.SetValue(0, 1, 5);
                    solver.SetValue(0, 2, 6);
                }
            );
        }

        [TestMethod]
        public void SumMustBeCandidate()
        {
            TestLogic(
                options: "r1c1; r1c2r1c3",
                gridSize: 9,
                expectedResult: LogicResult.Invalid,
                messageContains: "Sum of arrow (5) is impossible to fill into circle.",
                setup: (solver) =>
                {
                    // Arrow sums to 5
                    solver.SetValue(0, 1, 2);
                    solver.SetValue(0, 2, 3);

                    // But circle cannot accept that value
                    solver.ClearValue(0,0,5);
                }
             );
        }

        [TestMethod]
        public void CantHave0InSum()
        {
            TestLogic(
                options: "r1c1r1c2; r2c1r2c2",
                gridSize: 9,
                expectedResult: LogicResult.Invalid,
                messageContains: "Sum of arrow (10) is impossible to fill into pill.",
                setup: (solver) =>
                {
                    // Arrow sums to 5
                    solver.SetValue(1, 0, 9);
                    solver.SetValue(1, 1, 1);
                }
             );
        }

        [TestMethod]
        public void CircleSetToSum()
        {
            TestLogic(
                options: "r1c1; r1c2r1c3",
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Circle Sum",
                setup: (solver) =>
                {
                    solver.SetValue(0, 1, 2);
                    solver.SetValue(0, 2, 3);
                },
                after: (solver) =>
                {
                    Assert.AreEqual(5, solver.GetValue((0, 0)));
                }
            );
        }

        [TestMethod]
        public void CanEnterSumWhenLessThan99()
        {
            TestLogic(
                options: "r1c1r1c2; r1c3r1c4",
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Circle Sum",
                setup: (solver) =>
                {
                    solver.SetValue(0, 2, 9);
                    solver.SetValue(0, 3, 8);
                },
                after: (solver) =>
                {
                    Assert.AreEqual(1, solver.GetValue((0, 0)));
                    Assert.AreEqual(7, solver.GetValue((0, 1)));
                }
            );
        }

        [TestMethod]
        public void ReportsWhenNoValidSumRemains()
        {
            TestLogic(
                options: "r1c1; r1c2",
                gridSize: 9,
                expectedResult: LogicResult.Invalid,
                messageContains: "There are no value sums for the arrow",
                setup: (solver) =>
                {
                    solver.SetMask(0, 0, 3, 4);
                    solver.SetMask(0, 1, 1, 2);
                }
            );
        }

        [TestMethod]
        public void OnlyValidSumsAllowedWhenGrouped()
        {
            TestLogic(
                options: "r1c1; r1c2r1c3",
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Impossible sums",
                setup: (solver) =>
                {
                    solver.SetMask(0, 1, 5, 2);
                    solver.SetMask(0, 2, 4, 2);
                },
                after: (solver) =>
                {
                    Assert.AreEqual(ValuesMask(9, 7, 6), solver.Board[0, 0], "4 should not be allowed due to double 2");
                }
            );
        }

        [TestMethod]
        public void MultiCircleCanHandleSecret()
        {
            TestLogic(
                options: "r1c1r1c2; r2c1r2c2r2c3r2c4r2c5r2c6r2c7r2c8r2c9",
                gridSize: 9,
                expectedResult: LogicResult.Changed,
                messageContains: "Circle Sum",
                setup: (solver) =>
                {
                    // Set arrow to digits 1 - 9
                    for(var col = 0; col < 9; col++)
                    {
                        solver.SetValue(1, col, col + 1);
                    }
                },
                after: (solver) =>
                {
                    // Don't tell anyone
                    Assert.AreEqual(4, solver.GetValue((0, 0)));
                    Assert.AreEqual(5, solver.GetValue((0, 1)));
                }
            );
        }

        [TestMethod]
        public void Bug_CodeAssumesCellLessThan10()
        {
            var arrow = CreateConstraint("r1c1;r1c2r1c3", 16);

            arrow.InitCandidates(SudokuSolver);

            // Was throwing OutOfRangeException
            arrow.StepLogic(SudokuSolver, new List<LogicalStepDesc>(), false);
        }

        [TestMethod]
        public void Bug_SumLessThanMaxCanFit()
        {
            var arrow = CreateConstraint("r1c1r1c2; r1c3", 16);

            Assert.AreEqual(LogicResult.Changed, arrow.InitCandidates(SudokuSolver));
        }

        [TestMethod]
        public void Bug_CodeFailsToHandleValidSum()
        {
            TestLogic(
                "r1c1r1c2; r5c3r5c4r5c5r5c6r5c7r5c8r5c9r5c10r5c11r5c12",
                gridSize: 16,
                expectedResult: LogicResult.Changed,
                messageContains: "Impossible sums",
                setup: (solver) =>
                {
                    foreach(var col in Enumerable.Range(3,10))
                    {
                        solver.SetValue(4, col - 1, 19 - col);
                    }
                },
                after: (solver) =>
                {
                    Assert.AreEqual(ValuesMask(1, 11), solver.Board[0, 0], "First digit");
                    Assert.AreEqual(ValuesMask(15, 5), solver.Board[0, 1], "Second digit");
                }
            );
        }

        #region PossibleCircleArrangements

        [TestMethod]
        public void WhenNothingValidReturnsEmptyList()
        {
            Assert.AreEqual(0, ArrowSumConstraint.PossibleCircleArrangements(10, 1, 9).ToList().Count);
        }

        [TestMethod]
        public void ForOneCellOnlyDependsOnMax()
        {
            var possible = ArrowSumConstraint.PossibleCircleArrangements(9, 1, 9).ToList();

            Assert.AreEqual(1, possible.Count);

            Assert.AreEqual(9, possible[0][0]);

            Assert.AreEqual(0, ArrowSumConstraint.PossibleCircleArrangements(9, 1, 8).ToList().Count);
        }

        [TestMethod]
        public void AssumingNonZeroValues()
        {
            Assert.AreEqual(0, ArrowSumConstraint.PossibleCircleArrangements(0, 1, 1).ToList().Count);
        }

        [TestMethod]
        public void MultiDigitBase10()
        {
            var possible = ArrowSumConstraint.PossibleCircleArrangements(111, 2, 16).ToList();

            Assert.AreEqual(2, possible.Count);

            Assert.AreEqual(1, possible[0][0]);
            Assert.AreEqual(11, possible[0][1]);

            Assert.AreEqual(11, possible[1][0]);
            Assert.AreEqual(1, possible[1][1]);
        }

        [TestMethod]
        public void WhenDigitsEqualsCirclesOnly1Possibility()
        {
            Assert.AreEqual(1, ArrowSumConstraint.PossibleCircleArrangements(123, 3, 9).ToList().Count);
        }

        [TestMethod]
        public void MaxEffectsPossibilities()
        {
            // If max is 9 then we can only take 1 digit, even though there are enough digits to go around
            Assert.AreEqual(0, ArrowSumConstraint.PossibleCircleArrangements(111, 2, 9).ToList().Count);
        }

        #endregion


        protected override ArrowSumConstraint CreateConstraint(Solver solver, string options)
        {
            return new ArrowSumConstraint(solver, options);
        }
    }
}
