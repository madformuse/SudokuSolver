using System;
using System.Collections.Generic;
using SudokuSolver;
using SudokuSolver.Constraints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SudokuTests.UnitTests
{
    public abstract class ConstraintTests<T> where T : Constraint
    {
        protected Solver SudokuSolver { get; set; }

        protected void TestLogic(String options, int gridSize, LogicResult expectedResult, String messageContains, Action<Solver> setup, Action<Solver> after = null)
        {
            SudokuSolver = SolverFactory.CreateBlank(gridSize);
            var constraint = CreateConstraint(SudokuSolver, options);

            constraint.InitCandidates(SudokuSolver);

            setup(SudokuSolver);

            var step = new List<LogicalStepDesc>();

            Assert.AreEqual(expectedResult, constraint.StepLogic(SudokuSolver, step, false));

            if (!String.IsNullOrEmpty(messageContains))
            {
                Assert.IsTrue(step[0].desc.Contains(messageContains), $"Message was actually {step[0].desc}");
            }
            if (after != null)
            {
                after(SudokuSolver);
            }
        }

        protected T CreateConstraint(String options, int gridSize = 9)
        {
            SudokuSolver = SolverFactory.CreateBlank(gridSize);

            return CreateConstraint(SudokuSolver, options);
        }

        protected abstract T CreateConstraint(Solver solver, String options);
    }
}
