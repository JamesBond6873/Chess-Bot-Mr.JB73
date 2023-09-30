using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        int[] pieceVal = { 0, 100, 310, 330, 500, 1000, 10000 };
        int[] piecePhase = { 0, 0, 1, 1, 2, 4, 0 };
        ulong[] psts = { 657614902731556116, 420894446315227099, 384592972471695068, 312245244820264086, 364876803783607569, 366006824779723922, 366006826859316500, 786039115310605588, 421220596516513823, 366011295806342421, 366006826859316436, 366006896669578452, 162218943720801556, 440575073001255824, 657087419459913430, 402634039558223453, 347425219986941203, 365698755348489557, 311382605788951956, 147850316371514514, 329107007234708689, 402598430990222677, 402611905376114006, 329415149680141460, 257053881053295759, 291134268204721362, 492947507967247313, 367159395376767958, 384021229732455700, 384307098409076181, 402035762391246293, 328847661003244824, 365712019230110867, 366002427738801364, 384307168185238804, 347996828560606484, 329692156834174227, 365439338182165780, 386018218798040211, 456959123538409047, 347157285952386452, 365711880701965780, 365997890021704981, 221896035722130452, 384289231362147538, 384307167128540502, 366006826859320596, 366006826876093716, 366002360093332756, 366006824694793492, 347992428333053139, 457508666683233428, 329723156783776785, 329401687190893908, 366002356855326100, 366288301819245844, 329978030930875600, 420621693221156179, 422042614449657239, 384602117564867863, 419505151144195476, 366274972473194070, 329406075454444949, 275354286769374224, 366855645423297932, 329991151972070674, 311105941360174354, 256772197720318995, 365993560693875923, 258219435335676691, 383730812414424149, 384601907111998612, 401758895947998613, 420612834953622999, 402607438610388375, 329978099633296596, 67159620133902 };

        struct Transposition
        {
            public ulong zobristHash;
            public Move move;
            public int evaluation;
            public sbyte depth;
            public byte flag;
        };

        private Transposition[] m_TPTable = new Transposition[0x800000];

        public int getPstVal(int psq)
        {
            return (int)(((psts[psq / 10] >> (6 * (psq % 10))) & 63) - 20) * 8;
        }

        public Move Think(Board board, Timer timer)
        {

            int movesPruned; // #DEBUG
            int nodesSearched; // #DEBUG
            int TTuses; // #DEBUG
            movesPruned = 0; // #DEBUG
            nodesSearched = 0; // #DEBUG
            TTuses = 0; // #DEBUG


            Move bestMove = Move.NullMove, chosenMove = bestMove;

            int intThinkTime = timer.MillisecondsRemaining / 30;

            int intPlyDepth = 2;

            while (intPlyDepth < 90)
            {

                int rootValue = NegaMax(intPlyDepth, 0, -999_999, 999_999);

                if (timer.MillisecondsElapsedThisTurn > intThinkTime)
                {
                    Console.WriteLine("out of time");// #DEBUG
                    break;
                }

                bestMove = chosenMove;

                if (rootValue > 99000)
                {
                    Console.WriteLine("root value: " + rootValue.ToString());// #DEBUG
                    break;// Mate found, no point in searching deeper
                }

                intPlyDepth += 1;

            }

            int intThinkDuration = timer.MillisecondsElapsedThisTurn;// #DEBUG

            Console.WriteLine($"| Depth: {intPlyDepth} | Nodes Searched: {nodesSearched} | Moves Prunned: {movesPruned} | TT Uses: {TTuses} | Time Spent: {intThinkDuration} ms | KNPS: {nodesSearched / (intThinkDuration + 1)} |"); // #DEBUG
            return bestMove.IsNull ? board.GetLegalMoves()[0] : chosenMove;


            int Eval()
            {
                int mg = 0, eg = 0, phase = 0;

                foreach (bool stm in new[] { true, false })
                {
                    for (var p = PieceType.Pawn; p <= PieceType.King; p++)
                    {
                        int piece = (int)p, ind;
                        ulong mask = board.GetPieceBitboard(p, stm);
                        while (mask != 0)
                        {
                            phase += piecePhase[piece];
                            ind = 128 * (piece - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (stm ? 56 : 0);
                            mg += getPstVal(ind) + pieceVal[piece];
                            eg += getPstVal(ind + 64) + pieceVal[piece];
                        }
                    }

                    mg = -mg;
                    eg = -eg;
                }

                return (mg * phase + eg * (24 - phase)) / 24 * (board.IsWhiteToMove ? 1 : -1);
            }

            int NegaMax(int intDepth, int ply, int alpha, int beta)
            {

                bool notRoot = ply > 0;
                bool qsearch = intDepth <= 0;
                int bestValue = -99_999_999;

                // Check for Repetition
                if (notRoot && board.IsRepeatedPosition())
                    return 0;

                // Check Extensions
                if (board.IsInCheck()) intDepth++;

                int startingAlpha = alpha;


                ref Transposition transposition = ref m_TPTable[board.ZobristKey & 0x7FFFFF];

                if (notRoot && transposition.zobristHash == board.ZobristKey && transposition.depth >= intDepth && (
                    transposition.flag == 1 // exact score
                        || transposition.flag == 2 && transposition.evaluation >= beta // lower bound, fail high
                        || transposition.flag == 3 && transposition.evaluation <= alpha // upper bound, fail low
                ))
                {
                    TTuses++; // #DEBUG
                    return transposition.evaluation;
                }


                int intStandPat = Eval();
                nodesSearched++; // #DEBUG

                // Qsearch
                if (qsearch)
                {
                    bestValue = intStandPat;
                    if (bestValue >= beta)
                        return bestValue;
                    alpha = Math.Max(alpha, bestValue);
                }

                // Move Gen
                Move[] moves = board.GetLegalMoves(qsearch);
                int[] moveScores = new int[moves.Length];

                // Score Moves
                for (int i = 0; i < moves.Length; i++)
                {

                    if (moves[i] == transposition.move) moveScores[i] -= 1_000_000;

                    if (moves[i].IsCapture) moveScores[i] -= 100 * (int)moves[i].CapturePieceType - (int)moves[i].MovePieceType;

                }

                // Sort Moves based on their score
                Array.Sort(moveScores, moves);

                Move bestLoopMove = Move.NullMove;

                for (int i = 0; i < moves.Length; i++)
                {

                    if (intDepth > 2 && timer.MillisecondsElapsedThisTurn > intThinkTime) return 99999;

                    board.MakeMove(moves[i]);

                    int Value = -NegaMax(intDepth - 1, ply + 1, -beta, -alpha);

                    board.UndoMove(moves[i]);

                    if (Value > bestValue)
                    {

                        bestValue = Value;
                        bestLoopMove = moves[i];

                        alpha = Math.Max(alpha, bestValue);

                        if (!notRoot)
                            chosenMove = moves[i];

                        if (alpha >= beta)
                        {
                            movesPruned++; // #DEBUG
                            break;
                        }

                    }

                }

                // Gamestate, checkmate and draws
                if (!qsearch && moves.Length == 0)
                    return board.IsInCheck() ? ply - 99999 : 0;

                transposition.evaluation = bestValue;
                transposition.zobristHash = board.ZobristKey;
                transposition.move = bestLoopMove;
                transposition.depth = (sbyte)intDepth;
                transposition.flag = (byte)(bestValue < startingAlpha ? 3 : bestValue >= beta ? 2 : 1);

                return bestValue;

            }
        }
    }
}