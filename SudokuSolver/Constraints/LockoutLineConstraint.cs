using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SudokuSolver.SolverUtility;

namespace SudokuSolver.Constraints
{
    [Constraint(DisplayName = "Lockout Line", ConsoleName = "lockoutline")]
    public class LockoutLineConstraint : Constraint
    {
        public readonly List<(int, int)> Cells;
        private List<(int, int)> diamonds = new List<(int, int)>();
        private List<(int, int)> line;
        private int minimumDifference;
        private int maximumDifference;
        private int minimumUniqueDigits;
        private int degreesOfFreedom;

        public override string SpecificName => $"Lockout Line {CellName(diamonds[0])}-{CellName(diamonds[1])}";

        public LockoutLineConstraint(Solver sudokuSolver, string options) : base(sudokuSolver) 
        {
            var cellGroups = ParseCells(options);

            if (cellGroups.Count != 1)
            {
                throw new ArgumentException($"Lockout Lines constraint expects 1 cell group, got {cellGroups.Count}.");
            }

            Cells = cellGroups[0];
            diamonds.Add(Cells[0]);
            diamonds.Add(Cells[^1]);

            line = Cells.Where(cell => !IsDiamond(cell)).ToList();

            this.minimumDifference = MAX_VALUE / 2;
        }

        public override LogicResult InitCandidates(Solver sudokuSolver)
        {
            CalculateMaximumDifference(sudokuSolver);

            return RemoveImpossibleDigitsFromLine(sudokuSolver);
        }

        private LogicResult RemoveImpossibleDigitsFromLine(Solver solver)
        {
            // In odd sized grids the middle digit is impossible
            // [1 2 3 4 [5] 6 7 8 9]
            // Even grids have 2 impossible
            // [1 2 3 [4 5] 6 7 8]

            uint impossibleMask = 0;
            
            if(MAX_VALUE % 2 == 1)
            {
                impossibleMask = ValueMask(this.minimumDifference + 1);
            }
            else
            {
                impossibleMask = ValuesMask(this.minimumDifference, this.minimumDifference + 1);
            }
            
            bool changed = false;


            for (int cellIndex = 1; cellIndex < Cells.Count - 1; cellIndex++)
            {
                var cell = Cells[cellIndex];

                var result = solver.ClearMask(cell.Item1, cell.Item2, impossibleMask);

                if(result == LogicResult.Invalid)
                {
                    return result;
                }

                changed = changed ? true : result == LogicResult.Changed;
            }

            return changed ? LogicResult.Changed : LogicResult.None;
        }

        private void CalculateMaximumDifference(Solver sudokuSolver)
        {
            // The max difference between diamonds is constrained by groups which force distinct digits on the line.
            
            var groups = sudokuSolver.SplitIntoGroups(line).OrderByDescending(group => group.Count).ToList();

            this.minimumUniqueDigits = groups[0].Count;
            this.degreesOfFreedom = MAX_VALUE - (this.minimumDifference + 2) - (this.minimumUniqueDigits - 1);
            this.maximumDifference = this.minimumDifference + this.degreesOfFreedom;
        }

        public override bool EnforceConstraint(Solver sudokuSolver, int i, int j, int val)
        {
            //var result = LogicResult.Changed;

            //while (result == LogicResult.Changed)
            //{
            //    result = StepLogic(sudokuSolver, (List<LogicalStepDesc>)null, false);
            //}

            //return result != LogicResult.Invalid;
            return true;
        }

        public override LogicResult StepLogic(Solver sudokuSolver, List<LogicalStepDesc> logicalStepDescription, bool isBruteForcing)
        {
            return RunLogic(sudokuSolver, logicalStepDescription,
                EnforceDiamondDifference,
                LineNotBetweenDiamonds,
                DiamondsMustNotInvalidateCell
            );
        }

        private LogicResult RunLogic(Solver solver, List<LogicalStepDesc> logicalStepDescs, params Func<Solver, List<LogicalStepDesc>, LogicResult>[] steps)
        {
            foreach(var step in steps)
            {
                var res = step(solver, logicalStepDescs);

                if(res != LogicResult.None)
                {
                    // Something interesting has happened, return here.
                    return res;
                }
            }

            // If we've got here then nothing happened.
            return LogicResult.None;
        }

        private LogicResult EnforceDiamondDifference(Solver sudokuSolver, List<LogicalStepDesc> logicalStepDescription)
        {
            var qualification = String.Empty;

            if(degreesOfFreedom == 0)
            {
                qualification = $"exactly {minimumDifference}";
            }
            else if(maximumDifference < MAX_VALUE - 2)
            {
                qualification = $"between {minimumDifference} and {minimumDifference + degreesOfFreedom}";
            }
            else
            {
                qualification = $"at least {minimumDifference}";
            }

            var message = String.Empty;

            if(this.minimumUniqueDigits == 1)
            {
                message = $"Diamonds must be {qualification} apart.";
            }
            else if(degreesOfFreedom == 0)
            {
                message = $"Line contains the maximum {this.minimumUniqueDigits} unique digits so diamonds must be {qualification} apart.";
            }
            else if(degreesOfFreedom < minimumDifference)
            {
                message = $"Line contains at least {this.minimumUniqueDigits} unique digits so diamonds must be {qualification} apart.";
            }

            return ConstrainDiamondsToDifference(this.minimumDifference, maximumDifference, message, logicalStepDescription, sudokuSolver);
        }

        private LogicResult LineNotBetweenDiamonds(Solver sudokuSolver, List<LogicalStepDesc> logicalStepDescs)
        {
            var board = sudokuSolver.Board;

            uint[] diamondMasks = 
            {
                board[diamonds[0].Item1,diamonds[0].Item2],
                board[diamonds[1].Item1,diamonds[1].Item2]
            };

            uint invalidLineMask = ~valueSetMask;

            foreach (var pair in ValidDiamondPair(diamondMasks[0], diamondMasks[1], this.minimumDifference, this.maximumDifference))
            {
                uint lineMask = 0;

                // For this pair we couldn't have anything between min and max
                foreach(var between in Between(pair.Item1, pair.Item2))
                {
                    lineMask |= ValueMask(between);
                }

                invalidLineMask &= lineMask;
            }
            
            return RemoveCandidates(sudokuSolver, CollectCandidates(sudokuSolver, invalidLineMask, Cells.Where(cell => !IsDiamond(cell))), "Removing candidates from line which fall between all possible pairs", logicalStepDescs);
        }

        private LogicResult DiamondsMustNotInvalidateCell(Solver sudokuSolver, List<LogicalStepDesc> logicalStepDescs)
        {
            var board = sudokuSolver.Board;

            uint minMask = 0;
            uint maxMask = 0;

            // If line contains digit, diamonds should be adjusted.
            // If line is limited in candidates for any cell, that should also affect diamonds
            foreach(var cell in Cells.Where(cell => ! IsDiamond(cell)))
            {
                // This should probably be using MinValue / MaxValue in SolverUtility.

                // Are we basically saying that the diamonds must not rule out all the values for this cell?
                var candidates = Candidates(board[cell.Item1, cell.Item2]).ToList();

                var (min, max) = (candidates.Min(), candidates.Max());

                if(max - min > MAX_VALUE / 2)
                {
                    // No min or max values for diamonds are guarenteed to invalidate this cell
                    continue;
                }

                // We can't accept any min values between maxCell - (MAX / 2) and minCell
                for(var invalidMin = max - (MAX_VALUE / 2); invalidMin <= min; invalidMin++)
                {
                    if(invalidMin < 1) { continue; }

                    // If the smaller diamond was this value, this cell would break.
                    minMask |= ValueMask(invalidMin);
                }

                for(var invalidMax = min + (MAX_VALUE / 2); invalidMax >= max; invalidMax--)
                {
                    if(invalidMax > MAX_VALUE) { continue; }

                    maxMask |= ValueMask(invalidMax);
                }
            }

            // minMask and maxMask are now not allowed as diamond values.
            // let's collect the valid pairs that do not break this rule.
            uint[] keepMasks = new uint[2];

            foreach(var pair in ValidDiamondPair(board[diamonds[0].Item1, diamonds[0].Item2], board[diamonds[1].Item1, diamonds[1].Item2], this.minimumDifference, this.maximumDifference))
            {
                if(HasValue(minMask, Math.Min(pair.Item1, pair.Item2)) || HasValue(maxMask, Math.Max(pair.Item1, pair.Item2)))
                {
                    // This pair violates either the min or max constraints calculated from the line.
                    continue;
                }

                keepMasks[0] |= ValueMask(pair.Item1);
                keepMasks[1] |= ValueMask(pair.Item2);
            }

            List<int> toRemove = new List<int>();

            for(int i = 0; i < 2; i++)
            {
                // We should not be calling this twice as it will result in multiple steps being taken... Should seperate the removal from the collection.
                toRemove.AddRange(CollectCandidates(sudokuSolver, ~keepMasks[i], diamonds[i].ToEnumerable()));
            }

            return RemoveCandidates(sudokuSolver, toRemove, "Removing candidates from diamond which would break a cell in the line", logicalStepDescs);
        }

        private LogicResult ConstrainDiamondsToDifference(int minDiff, int maxDiff, string message, List<LogicalStepDesc> logicalStepDescription, Solver sudokuSolver)
        {
            bool foundValid = false;

            var board = sudokuSolver.Board;

            uint[] diamondMasks =
            {
                board[diamonds[0].Item1, diamonds[0].Item2],
                board[diamonds[1].Item1, diamonds[1].Item2]
            };

            var keepMasks = new uint[2];

            foreach (var pair in ValidDiamondPair(diamondMasks[0], diamondMasks[1], minDiff, maxDiff))
            {
                keepMasks[0] |= ValueMask(pair.Item1);
                keepMasks[1] |= ValueMask(pair.Item2);

                foundValid = true;
            }

            if (!foundValid)
            {
                logicalStepDescription?.Add(new(desc: $"No valid options are left for the diamonds", diamonds));
                return LogicResult.Invalid;
            }

            var elims = new List<int>();

            for (var index = 0; index < diamonds.Count; index++)
            {
                if (IsValueSet(diamondMasks[index]))
                {
                    continue;
                }

                var toRemoveMask = diamondMasks[index] & ~keepMasks[index];

                elims.AddRange(sudokuSolver.CandidateIndexes(toRemoveMask, diamonds[index].ToEnumerable()));
            }

            if (elims.Count > 0)
            {
                bool invalid = !sudokuSolver.ClearCandidates(elims);
                logicalStepDescription?.Add(new(
                    desc: $"{message} => {sudokuSolver.DescribeElims(elims)}",
                    sourceCandidates: Enumerable.Empty<int>(),
                    elimCandidates: elims
                ));

                return invalid ? LogicResult.Invalid : LogicResult.Changed;
            }

            return LogicResult.None;
        }

        private List<int> CollectCandidates(Solver sudokuSolver, uint candidates, IEnumerable<(int,int)> cells)
        {
            var elims = new List<int>();

            foreach (var cell in cells)
            {
                if (IsValueSet(sudokuSolver.Board[cell.Item1, cell.Item2]))
                {
                    continue;
                }

                elims.AddRange(sudokuSolver.CandidateIndexes(candidates, cell.ToEnumerable()));
            }

            return elims;
        }

        private LogicResult RemoveCandidates(Solver sudokuSolver, List<int> toRemove, string message, List<LogicalStepDesc> logicalStepDescription)
        {
            
            if (toRemove.Count > 0)
            {
                bool invalid = !sudokuSolver.ClearCandidates(toRemove);
                logicalStepDescription?.Add(new(
                    desc: $"{message} => {sudokuSolver.DescribeElims(toRemove)}",
                    sourceCandidates: Enumerable.Empty<int>(),
                    elimCandidates: toRemove
                ));

                return invalid ? LogicResult.Invalid : LogicResult.Changed;
            }

            return LogicResult.None;
        }

        private IEnumerable<(int,int)> ValidDiamondPair(uint diamond1, uint diamond2,int minDiff, int maxDiff)
        {
            foreach (var first in Candidates(diamond1))
            {
                foreach (var second in Candidates(diamond2).Where(val => Math.Abs(val - first) >= minDiff && Math.Abs(val - first) <= maxDiff))
                {
                    yield return (first, second);
                }
            }
        }

        private IEnumerable<int> Candidates(uint mask)
        {
            if(IsValueSet(mask))
            {
                yield return GetValue(mask);
                yield break;
            }
            for (var i = 1; i <= MAX_VALUE; i++)
            {
                if((mask & ValueMask(i)) != 0)
                {
                    yield return i;
                }
            }    
        }

        private IEnumerable<int> Between(int first, int second)
        {
            if((first == second) || Math.Abs(first - second) < 2)
            {
                yield break;
            }

            var start = Math.Min(first, second);
            var end = Math.Max(first, second);

            for(int i = start; i <= end; i += 1)
            {
                yield return i;
            }
        }

        private uint Mask(IEnumerable<int> candidates)
        {
            uint mask = 0;

            foreach(var candidate in candidates)
            {
                mask |= ValueMask(candidate);
            }

            return mask;
        }

        public override IEnumerable<(int, int)> SeenCells((int, int) cell)
        {
            if(! IsDiamond(cell))
            {
                yield break;
            }

            foreach(var member in Cells)
            {
                if(cell != member)
                {
                    yield return member;
                }
            }
        }

        private bool IsDiamond((int, int) cell)
        {
            return cell == Cells[0] || cell == Cells[^1];
        }
    }
}
